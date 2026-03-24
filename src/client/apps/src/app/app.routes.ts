import { Route } from '@angular/router';
import { authGuard } from './auth/auth.guard';

export const appRoutes: Route[] = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'register',
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./registration/registration.component').then(
        (m) => m.RegistrationComponent
      ),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'AdminLogin',
    loadComponent: () =>
      import('./auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    path: 'admin',
    loadChildren: () =>
      import('./admin/admin.routes').then((m) => m.adminRoutes),
  },
  /** Legacy Preskool URLs → canonical admin dashboard (bookmark-safe). */
  {
    path: 'preskool',
    redirectTo: '/admin/dashboard',
    pathMatch: 'prefix',
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./dashboard/dashboard-shell.component').then(
        (m) => m.DashboardShellComponent
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/dashboard.component').then(
            (m) => m.DashboardComponent
          ),
      },
      {
        path: 'profile',
        children: [
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'application',
          },
          {
            path: 'application',
            loadComponent: () =>
              import('./profile/applicant-application.component').then(
                (m) => m.ApplicantApplicationComponent
              ),
          },
          {
            path: 'summary',
            loadComponent: () =>
              import('./profile/profile-overview.component').then(
                (m) => m.ProfileOverviewComponent
              ),
          },
        ],
      },
      {
        path: 'documents',
        loadComponent: () =>
          import('./documents/documents.component').then(
            (m) => m.DocumentsComponent
          ),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./notifications/notifications.component').then(
            (m) => m.NotificationsComponent
          ),
      },
      {
        path: 'offer',
        loadComponent: () =>
          import('./offer/offer.component').then((m) => m.OfferComponent),
      },
    ],
  },
  {
    path: '**',
    redirectTo: 'register',
  },
];
