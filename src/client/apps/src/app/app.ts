import { Component, computed, inject, signal, Renderer2 } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { ToastService } from './shared/toast.service';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly toastsService = inject(ToastService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly renderer = inject(Renderer2);

  protected readonly currentYear = new Date().getFullYear();
  protected readonly profile = computed(() => this.auth.profile);
  protected readonly toasts = this.toastsService.toasts;
  protected readonly isAdminRoute = signal(false);
  /** Footer “Powered By” contact popup */
  protected readonly creditsOpen = signal(false);

  constructor() {
    const syncAdminBody = (url: string) => {
      const admin = url.startsWith('/admin');
      this.isAdminRoute.set(admin);
      const body = this.document.body;
      if (admin) {
        this.renderer.addClass(body, 'erp-admin-body');
      } else {
        this.renderer.removeClass(body, 'erp-admin-body');
      }
    };

    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        syncAdminBody(event.urlAfterRedirects || event.url);
      });

    syncAdminBody(this.router.url);
  }

  logout(): void {
    this.auth.logout();
  }

  dismissToast(id: number): void {
    this.toastsService.dismiss(id);
  }

  openCreditsModal(): void {
    this.creditsOpen.set(true);
  }

  closeCreditsModal(): void {
    this.creditsOpen.set(false);
  }
}
