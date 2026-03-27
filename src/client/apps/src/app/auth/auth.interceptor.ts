import { inject, Injectable } from '@angular/core';
import {
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private readonly auth = inject(AuthService);

  intercept(
    req: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    const profile = this.auth.profile;
    /** Only login + refresh are called without Bearer; change-password and other /auth/applicants/* routes need JWT. */
    const isApplicantAuthWithoutBearer =
      req.url.includes('/auth/applicants/login') ||
      req.url.includes('/auth/applicants/refresh');
    /** Do not send JWT — endpoint must be anonymous; a stale token causes 401 from JwtBearer before the controller runs. */
    const isPublicAnonymous =
      req.url.includes('/registration/student') ||
      req.url.includes('/admissions/class-xii-subjects');

    const authReq =
      profile && !isApplicantAuthWithoutBearer && !isPublicAnonymous && profile.token
        ? req.clone({
            setHeaders: { Authorization: `Bearer ${profile.token}` },
          })
        : req;

    return next.handle(authReq).pipe(
      catchError((error) => {
        const url = req.url;
        const isLoginAttempt =
          url.includes('/auth/applicants/login') || url.includes('/auth/admin/login');
        const isRefreshCall = url.includes('/auth/applicants/refresh');
        const isPublicAnonymousUrl =
          url.includes('/registration/student') ||
          url.includes('/admissions/class-xii-subjects');
        const isApplicantChangePassword =
          url.includes('/auth/applicants/change-password');

        // Wrong password / invalid login returns 401 — do not run refresh flow (stale session would mask errors).
        // change-password: 401 means wrong current password or bad JWT — do not refresh-loop; logout skipped below.
        if (
          error.status === 401 &&
          profile?.refreshToken &&
          !isLoginAttempt &&
          !isRefreshCall &&
          !isPublicAnonymousUrl &&
          !isApplicantChangePassword
        ) {
          return this.auth.refreshToken(profile.refreshToken).pipe(
            switchMap(() => {
              const updatedToken = this.auth.profile?.token;
              if (!updatedToken) {
                this.auth.logout();
                return throwError(() => error);
              }
              const retryRequest = req.clone({
                setHeaders: { Authorization: `Bearer ${updatedToken}` },
              });
              return next.handle(retryRequest);
            })
          );
        }

        // Expired session on protected APIs — clear stored auth. Skip for failed login / public anonymous / wrong current password on change-password.
        if (
          (error.status === 401 || error.status === 403) &&
          !isLoginAttempt &&
          !isPublicAnonymousUrl &&
          !isApplicantChangePassword
        ) {
          this.auth.logout();
        }

        return throwError(() => error);
      })
    );
  }
}
