import { Injectable, signal, computed } from '@angular/core';

export interface NavigationItem {
  label: string;
  icon: string;
  route?: string;
  children?: NavigationItem[];
  expanded?: boolean;
}

@Injectable({ providedIn: 'root' })
export class AdminNavigationService {
  private readonly navigationItems = signal<NavigationItem[]>([
    {
      label: 'Home',
      icon: '🏠',
      route: '/admin',
    },
    {
      label: 'Dashboard',
      icon: '📊',
      route: '/admin/dashboard',
    },
    {
      label: 'Online Admission',
      icon: '📝',
      expanded: false,
      children: [
        {
          label: 'Admission workflow',
          icon: '📋',
          route: '/admin/admissions/workflow',
        },
        {
          label: 'Entrance Exam Management',
          icon: '✏️',
          route: '/admin/admissions/exams',
        },
        {
          label: 'Application Form',
          icon: '📋',
          route: '/admin/admissions/applications',
        },
        {
          label: 'Admitted Students',
          icon: '🎓',
          route: '/admin/admissions/admitted-students',
        },
        {
          label: 'Document Verification System',
          icon: '✅',
          route: '/admin/admissions/verification',
        },
        {
          label: 'Merit List & Selection',
          icon: '🏆',
          route: '/admin/admissions/merit-list',
        },
            {
              label: 'Direct Admission',
              icon: '🎓',
              route: '/admin/admissions/direct-admission',
            },
            {
              label: 'Send Individual Offer',
              icon: '✉️',
              route: '/admin/admissions/send-offer',
            },
        {
          label: 'Admission Approval',
          icon: '✓',
          route: '/admin/admissions/approval',
        },
        {
          label: 'Payment Verification',
          icon: '💰',
          route: '/admin/admissions/payment-verification',
        },
      ],
    },
    // Other ERP modules (Offline Admission, Students, Staff, Academics, etc.) are hidden
    // while focusing on Online Admission. Restore by merging from git history when needed.
  ]);

  readonly items = this.navigationItems.asReadonly();

  toggleExpanded(index: number): void {
    const items = [...this.navigationItems()];
    if (items[index].children) {
      items[index].expanded = !items[index].expanded;
      this.navigationItems.set(items);
    }
  }
}
