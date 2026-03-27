import {
  Component,
  OnInit,
  OnDestroy,
  computed,
  inject,
  signal,
  CUSTOM_ELEMENTS_SCHEMA,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { AuthService } from '../auth/auth.service';
import { ChangePasswordComponent } from '../auth/change-password.component';
import { ApplicantPortalStore } from './applicant-portal.store';
import {
  ApplicantApplicationNavigationService,
  ApplicantApplicationStep,
} from '../profile/applicant-application-navigation.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { API_BASE_URL } from '@client/shared/util';
import { ApplicantNotificationReadService } from './applicant-notification-read.service';

@Component({
  selector: 'app-dashboard-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ChangePasswordComponent,
    CdkMenu,
    CdkMenuItem,
    CdkMenuTrigger,
  ],
  templateUrl: './dashboard-shell.component.html',
  styleUrls: ['./dashboard-shell.component.scss'],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  providers: [ApplicantPortalStore],
})
export class DashboardShellComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(ApplicantPortalStore);
  private readonly applicationNav = inject(ApplicantApplicationNavigationService);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly notificationRead = inject(ApplicantNotificationReadService);
  
  private dashboardRefreshInterval: ReturnType<typeof setInterval> | null = null;
  private focusHandler: (() => void) | null = null;
  private visibilityHandler: (() => void) | null = null;

  readonly dashboard = computed(() => this.store.dashboard());
  readonly loading = computed(() => this.store.loading());
  readonly error = computed(() => this.store.error());
  readonly profile = computed(() => this.dashboard()?.profile ?? null);
  /** Unread count: notifications newer than last visit to /app/notifications (see ApplicantNotificationReadService). */
  readonly notificationCount = computed(() => {
    this.notificationRead.readEpoch();
    const dash = this.dashboard();
    return this.notificationRead.unreadCount(
      dash?.notifications,
      dash?.profile?.uniqueId
    );
  });

  readonly profilePhotoUrl = computed(() => {
    const photo = this.profile()?.photoUrl?.trim();
    if (!photo) {
      return null;
    }
    if (/^https?:\/\//i.test(photo)) {
      return photo;
    }
    const root = this.apiBaseUrl.replace(/\/api\/?$/, '').replace(/\/$/, '');
    const normalizedPhoto = photo.startsWith('/') ? photo : `/${photo}`;
    return `${root}${normalizedPhoto}`;
  });
  readonly applicationSteps = this.applicationNav.steps;
  readonly activeApplicationStep = this.applicationNav.currentIndex;
  /** True when the application has been submitted (draft locked); drives sidebar “done” for the Declaration step. */
  readonly applicationFormSubmitted = computed(
    () =>
      this.store.dashboard()?.application?.isSubmitted === true ||
      this.store.dashboard()?.application?.coursesLocked === true
  );
  readonly sidebarOpen = signal(false);
  changePasswordOpen = false;
  passwordUpdated = false;

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  closeMobileSidebar(): void {
    this.sidebarOpen.set(false);
  }

  constructor() {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => {
        if (params.get('forcePassword')) {
          this.passwordUpdated = false;
          this.changePasswordOpen = true;
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { forcePassword: null },
            queryParamsHandling: 'merge',
          });
        }
      });

    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.closeMobileSidebar());
  }

  ngOnInit(): void {
    void this.store.loadDashboard();
    
    // Auto-refresh dashboard every 2 minutes to get status updates
    // CRITICAL: Only refresh when on the dashboard route, NOT when filling out the application form
    // This prevents form data from being lost due to dashboard refreshes
    this.dashboardRefreshInterval = setInterval(() => {
      // Check if we're on the dashboard route (not on application form or other pages)
      const currentUrl = this.router.url;
      const isOnDashboard = currentUrl === '/app' || currentUrl === '/app/dashboard' || currentUrl.startsWith('/app/dashboard/');
      
      // Only refresh if:
      // 1. Tab is visible (not in background)
      // 2. Not already loading
      // 3. Actually on the dashboard route (not on application form)
      if (!document.hidden && !this.loading() && isOnDashboard) {
        void this.store.loadDashboard();
      }
    }, 120000); // 2 minutes
    
    // Refresh when window regains focus - but only if on dashboard
    this.focusHandler = () => {
      const currentUrl = this.router.url;
      const isOnDashboard = currentUrl === '/app' || currentUrl === '/app/dashboard' || currentUrl.startsWith('/app/dashboard/');
      if (!this.loading() && isOnDashboard) {
        void this.store.loadDashboard();
      }
    };
    window.addEventListener('focus', this.focusHandler);
    
    // Refresh when tab becomes visible again - but only if on dashboard
    this.visibilityHandler = () => {
      if (!document.hidden) {
        const currentUrl = this.router.url;
        const isOnDashboard = currentUrl === '/app' || currentUrl === '/app/dashboard' || currentUrl.startsWith('/app/dashboard/');
        if (!this.loading() && isOnDashboard) {
          void this.store.loadDashboard();
        }
      }
    };
    document.addEventListener('visibilitychange', this.visibilityHandler);
  }

  ngOnDestroy(): void {
    // Clear the interval to prevent memory leaks and multiple intervals
    if (this.dashboardRefreshInterval !== null) {
      clearInterval(this.dashboardRefreshInterval);
      this.dashboardRefreshInterval = null;
    }
    
    // Remove the focus event listener
    if (this.focusHandler !== null) {
      window.removeEventListener('focus', this.focusHandler);
      this.focusHandler = null;
    }
    
    // Remove the visibility change listener
    if (this.visibilityHandler !== null) {
      document.removeEventListener('visibilitychange', this.visibilityHandler);
      this.visibilityHandler = null;
    }
  }

  openChangePassword(): void {
    this.passwordUpdated = false;
    this.changePasswordOpen = true;
  }

  handlePasswordClosed(): void {
    this.changePasswordOpen = false;
  }

  handlePasswordUpdated(): void {
    this.passwordUpdated = true;
  }

  logout(): void {
    this.passwordUpdated = false;
    this.store.reset();
    this.auth.logout();
  }

  navigateToApplicationStep(step: ApplicantApplicationStep): void {
    void this.router.navigate(['/app/profile/application']);
    this.applicationNav.setCurrentIndex(step.index);
  }

  /**
   * Sidebar previously used only `index < active`, so the current step never looked “completed”.
   * After submit, the last step (Declaration) should show as completed (green), not stuck as active (blue).
   */
  isSidebarStepCompleted(step: ApplicantApplicationStep): boolean {
    const steps = this.applicationSteps();
    if (steps.length === 0) {
      return false;
    }
    const lastIndex = steps.length - 1;
    const active = this.activeApplicationStep();
    const submitted = this.applicationFormSubmitted();
    if (step.index < active) {
      return true;
    }
    return submitted && step.index === lastIndex;
  }

  isSidebarStepActive(step: ApplicantApplicationStep): boolean {
    const steps = this.applicationSteps();
    if (steps.length === 0) {
      return false;
    }
    const lastIndex = steps.length - 1;
    const active = this.activeApplicationStep();
    const submitted = this.applicationFormSubmitted();
    if (step.index !== active) {
      return false;
    }
    if (submitted && step.index === lastIndex) {
      return false;
    }
    return true;
  }
}
