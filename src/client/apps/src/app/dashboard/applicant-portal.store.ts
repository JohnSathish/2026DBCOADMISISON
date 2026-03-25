import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplicantDashboardDto,
  ApplicantPortalApiService,
} from '@client/shared/data';

export interface FeeSummary {
  amountDue: number;
  amountPaid: number;
  remaining: number;
  status: string;
  canPay: boolean;
  transactionId?: string | null;
}

export interface ChecklistSummary {
  completedSteps: number;
  totalSteps: number;
  nextAction: string;
}

export interface ApplicationProgressStep {
  key: string;
  title: string;
  description: string;
  isComplete: boolean;
}

export interface ApplicationStatusSummary {
  steps: ApplicationProgressStep[];
  currentStep: string;
  overallStatus: 'Not Started' | 'In Progress' | 'Completed';
  incomplete: ApplicationProgressStep[];
}

export interface SelectionSummary {
  shift: string | null;
  status: string;
  note: string;
}

export interface DashboardSummary {
  fees: FeeSummary;
  checklist: ChecklistSummary;
  application: ApplicationStatusSummary;
  selection: SelectionSummary;
}

const PLACEHOLDER_SUMMARY: DashboardSummary = {
  fees: {
    amountDue: 700,
    amountPaid: 0,
    remaining: 700,
    status: 'Pending',
    canPay: false,
  },
  checklist: {
    completedSteps: 0,
    totalSteps: 7,
    nextAction: 'Continue filling out your application to see progress here.',
  },
  application: {
    steps: [
      {
        key: 'registration',
        title: 'Registration Created',
        description: 'Your applicant account has been created.',
        isComplete: true,
      },
      {
        key: 'personal',
        title: 'Personal Information',
        description: 'Complete your personal details.',
        isComplete: false,
      },
      {
        key: 'address',
        title: 'Addresses & Identity',
        description: 'Provide your address and identity information.',
        isComplete: false,
      },
      {
        key: 'family',
        title: 'Family & Guardian',
        description: 'Add contact details for parents or guardians.',
        isComplete: false,
      },
      {
        key: 'academics',
        title: 'Academic Records',
        description: 'Enter your Class XII marks and board information.',
        isComplete: false,
      },
      {
        key: 'courses',
        title: 'Course Preferences',
        description: 'Choose your shift, major, minor and electives.',
        isComplete: false,
      },
      {
        key: 'uploads',
        title: 'Uploads & Declaration',
        description: 'Upload required documents and accept the declaration.',
        isComplete: false,
      },
    ],
    currentStep: 'Personal Information',
    overallStatus: 'Not Started',
    incomplete: [],
  },
  selection: {
    shift: null,
    status: 'Under Review',
    note: 'Your application is currently under review.',
  },
};

@Injectable()
export class ApplicantPortalStore {
  private readonly api = inject(ApplicantPortalApiService);

  readonly dashboard = signal<ApplicantDashboardDto | null>(null);
  readonly summary = signal<DashboardSummary>(PLACEHOLDER_SUMMARY);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  async loadDashboard(): Promise<void> {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      const data: ApplicantDashboardDto = await firstValueFrom(
        this.api.getDashboard()
      );
      this.dashboard.set(data);
      this.summary.set(deriveSummary(data));
    } catch (error) {
      const message =
        (error as { error?: { message?: string } })?.error?.message ??
        'Unable to load dashboard information. Please try again shortly.';
      this.error.set(message);
      this.summary.set(PLACEHOLDER_SUMMARY);
    } finally {
      this.loading.set(false);
    }
  }

  reset(): void {
    this.dashboard.set(null);
    this.summary.set(PLACEHOLDER_SUMMARY);
    this.error.set(null);
    this.loading.set(false);
  }
}

function deriveSummary(dashboard: ApplicantDashboardDto): DashboardSummary {
  const serverSteps = dashboard.application?.steps ?? [];
  const steps: ApplicationProgressStep[] = serverSteps.map((step) => ({
    key: step.key,
    title: step.title,
    description: step.description ?? '',
    isComplete: step.isComplete,
  }));

  const totalSteps = steps.length || PLACEHOLDER_SUMMARY.application.steps.length;
  const completedSteps = steps.filter((step) => step.isComplete).length;
  const overallStatus: 'Not Started' | 'In Progress' | 'Completed' =
    completedSteps === 0
      ? 'Not Started'
      : completedSteps === totalSteps
      ? 'Completed'
      : 'In Progress';

  const firstIncomplete =
    steps.find((step) => !step.isComplete) ?? PLACEHOLDER_SUMMARY.application.steps.find((step) => !step.isComplete) ?? PLACEHOLDER_SUMMARY.application.steps[0];

  const checklist: ChecklistSummary = {
    completedSteps,
    totalSteps,
    nextAction: firstIncomplete
      ? `${firstIncomplete.title} needs attention.`
      : 'All steps are complete. Await further updates from admissions.',
  };

  const amountDue = dashboard.payment?.amountDue ?? 700;
  const amountPaid = dashboard.payment?.amountPaid ?? 0;
  const fees: FeeSummary = {
    amountDue,
    amountPaid,
    remaining: Math.max(amountDue - amountPaid, 0),
    status: dashboard.payment?.status ?? 'Pending',
    canPay: dashboard.payment?.canPay ?? false,
    transactionId: dashboard.payment?.transactionId ?? null,
  };

  const application: ApplicationStatusSummary = {
    steps: steps.length ? steps : PLACEHOLDER_SUMMARY.application.steps,
    currentStep: firstIncomplete?.title ?? 'All steps complete',
    overallStatus,
    incomplete: (steps.length ? steps : PLACEHOLDER_SUMMARY.application.steps).filter(
      (step) => !step.isComplete
    ),
  };

  const selection: SelectionSummary = {
    shift: dashboard.profile.shift || null,
    status: dashboard.application?.status ?? 'Under Review',
    note: getSelectionNote(dashboard.application?.status ?? 'Under Review'),
  };

  return {
    fees,
    checklist,
    application,
    selection,
  };
}

function getSelectionNote(status: string): string {
  switch (status.toLowerCase()) {
    case 'approved':
    case 'accepted':
      return 'Congratulations! Your application has been approved. Please check your email for further instructions.';
    case 'rejected':
      return "We're sorry to inform you that your application was not successful. Please check your email for details.";
    case 'waitinglist':
    case 'waitlisted':
      return 'Your application is on the waiting list. We will notify you via email if a seat becomes available.';
    case 'entranceexam':
    case 'entrance exam scheduled':
      return 'An entrance exam has been scheduled. Please check your email and dashboard for exam details.';
    case 'submitted':
      return 'Your application has been submitted successfully. It is currently under review by the admissions committee.';
    default:
      return 'Your application is currently under review by the admissions committee. You will be notified via email when there is an update.';
  }
}

