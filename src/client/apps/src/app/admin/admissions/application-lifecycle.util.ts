import type { OnlineApplicationDto } from '@client/shared/data';

export type ApplicationLifecycleLabel =
  | 'Registered'
  | 'Form In Progress'
  | 'Payment Pending'
  | 'Paid';

export function getApplicationLifecycleLabel(app: OnlineApplicationDto): ApplicationLifecycleLabel {
  if (app.isApplicationSubmitted && app.isPaymentCompleted) {
    return 'Paid';
  }
  if (app.isApplicationSubmitted && !app.isPaymentCompleted) {
    return 'Payment Pending';
  }
  if (app.hasApplicationDraft) {
    return 'Form In Progress';
  }
  return 'Registered';
}

export function getApplicationLifecycleBadgeClass(label: ApplicationLifecycleLabel): string {
  switch (label) {
    case 'Registered':
      return 'app-lifecycle app-lifecycle--registered';
    case 'Form In Progress':
      return 'app-lifecycle app-lifecycle--in-progress';
    case 'Payment Pending':
      return 'app-lifecycle app-lifecycle--payment-pending';
    case 'Paid':
      return 'app-lifecycle app-lifecycle--paid';
    default:
      return 'app-lifecycle app-lifecycle--registered';
  }
}

export const APPLICATION_LIFECYCLE_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'All stages' },
  { value: 'registered', label: 'Registered' },
  { value: 'formInProgress', label: 'Form In Progress' },
  { value: 'paymentPending', label: 'Payment pending' },
  { value: 'paid', label: 'Paid' },
  { value: 'submitted', label: 'Submitted (any payment status)' },
];
