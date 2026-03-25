import { Component, computed, CUSTOM_ELEMENTS_SCHEMA, inject, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApplicantPortalStore } from './applicant-portal.store';
import { PaymentComponent } from '../payment/payment.component';
import { API_BASE_URL } from '@client/shared/util';
import { ApplicantElectiveSubjectDto } from '@client/shared/data';

const SHIFT_LABELS: Record<string, string> = {
  ShiftI: 'Shift - I (6:30 am – 9:30 am)',
  ShiftII: 'Shift - II (9:45 am – 3:30 pm)',
  ShiftIII: 'Shift - III (no longer offered)',
  Morning: 'Shift - I (Morning)',
  Day: 'Shift - II (Day)',
  Evening: 'Evening (legacy; no longer offered)',
  'SHIFT - I (TIMING : 7.30 AM - 1.15 PM)': 'Shift - I (Legacy)',
  'SHIFT - II (TIMING : 9.45 AM - 3.30 PM)': 'Shift - II (Legacy)',
  'SHIFT - III (TIMING : 1.30 PM - 6.15 PM)': 'Shift - III (Legacy; no longer offered)',
};

/** Matches server/PDF format "CODE \u2014 NAME" (em dash). */
function splitElectiveCatalogLine(s: string): { code: string; name: string; full: string } | null {
  const emSep = ' \u2014 ';
  let idx = s.indexOf(emSep);
  let sepLen = emSep.length;
  if (idx < 0) {
    idx = s.indexOf(' - ');
    sepLen = 3;
  }
  if (idx < 0) {
    return null;
  }
  const code = s.slice(0, idx).trim();
  const name = s
    .slice(idx + sepLen)
    .trim()
    .replace(/\s*\(Auto Assigned\)\s*$/i, '')
    .trim();
  return { code, name: name || code, full: s };
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, PaymentComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class DashboardComponent {
  private readonly store = inject(ApplicantPortalStore);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  readonly paymentComponent = viewChild(PaymentComponent);

  readonly profile = computed(() => this.store.dashboard()?.profile ?? null);

  /** Application fee (online) vs minimum post-selection admission fee from server config. */
  readonly paymentFeePolicy = computed(() => {
    const p = this.store.dashboard()?.payment;
    if (
      p?.applicationFeeAmount == null ||
      p?.postSelectionAdmissionFeeAmount == null
    ) {
      return null;
    }
    return {
      applicationFee: p.applicationFeeAmount,
      admissionMin: p.postSelectionAdmissionFeeAmount,
    };
  });
  readonly profilePhotoUrl = computed(() => {
    const photo = this.profile()?.photoUrl?.trim();
    if (!photo) return null;
    if (/^https?:\/\//i.test(photo)) return photo;
    const root = this.apiBaseUrl.replace(/\/api\/?$/, '').replace(/\/$/, '');
    const path = photo.startsWith('/') ? photo : `/${photo}`;
    return `${root}${path}`;
  });
  readonly profileCompletionPercent = computed(() => {
    const s = this.store.summary();
    const total = s.application.steps.length;
    if (!total) return null;
    const done = s.application.steps.filter((st) => st.isComplete).length;
    return Math.round((done / total) * 100);
  });
  readonly documents = computed(
    () => this.store.dashboard()?.documents ?? []
  );
  readonly notifications = computed(
    () => this.store.dashboard()?.notifications ?? []
  );
  readonly summary = computed(() => this.store.summary());
  /** Shown only after application submission + application fee payment (API sends null otherwise). */
  readonly courseSelection = computed(
    () => this.store.dashboard()?.courseSelection ?? null
  );

  /** Elective rows for template @for (Tailwind layout). */
  readonly electiveRows = computed((): {
    key: string;
    label: string;
    raw: unknown;
    tooltip: string | null;
  }[] => {
    const cs = this.courseSelection();
    if (!cs) {
      return [];
    }
    return [
      { key: 'mdc', label: 'MDC', raw: cs.mdc, tooltip: this.electiveTooltip(cs.mdc) },
      { key: 'aec', label: 'AEC', raw: cs.aec, tooltip: this.electiveTooltip(cs.aec) },
      { key: 'sec', label: 'SEC', raw: cs.sec, tooltip: this.electiveTooltip(cs.sec) },
      { key: 'vac', label: 'VAC', raw: cs.vac, tooltip: this.electiveTooltip(cs.vac) },
    ];
  });
  readonly incompleteSteps = computed(
    () => this.summary().application.incomplete
  );
  readonly paymentPending = computed(() => this.summary().fees.canPay);
  readonly selectionStatus = computed(() => this.summary().selection.status);
  readonly selectionNote = computed(() => this.summary().selection.note);
  readonly shiftLabel = computed(() => {
    const shift = this.profile()?.shift?.trim();
    if (!shift) {
      return 'Pending selection';
    }
    return SHIFT_LABELS[shift] ?? shift;
  });

  readonly documentsUploadedCount = computed(() =>
    this.documents().filter((d) => d.isComplete).length
  );

  /** Core form sections before uploads (matches server step keys). */
  private readonly formCoreKeys = ['personal', 'address', 'family', 'academics', 'courses'] as const;

  readonly applicationStepsFromSummary = computed(
    () => this.store.summary().application.steps
  );

  readonly isFormCoreComplete = computed(() => {
    const steps = this.applicationStepsFromSummary();
    return this.formCoreKeys.every((k) => steps.find((s) => s.key === k)?.isComplete);
  });

  readonly isUploadsStepComplete = computed(() => {
    return this.applicationStepsFromSummary().find((s) => s.key === 'uploads')?.isComplete ?? false;
  });

  readonly isFeePaid = computed(() => {
    const f = this.summary().fees;
    return f.status === 'Completed' || f.remaining <= 0;
  });

  readonly isApplicationSubmitted = computed(
    () => this.store.dashboard()?.application?.isSubmitted === true
  );

  readonly feeStatusLower = computed(() => this.summary().fees.status?.toLowerCase() ?? '');

  readonly feeLooksFailed = computed(() => {
    const s = this.feeStatusLower();
    return s.includes('fail') || s === 'failed' || s === 'cancelled';
  });

  readonly documentsNeedAttention = computed(() => {
    const docs = this.documents();
    if (!docs.length) return false;
    return docs.some((d) => !d.isComplete);
  });

  /**
   * Four-step admission journey: form → uploads → payment → submitted.
   */
  readonly admissionJourney = computed(() => {
    const s1 = this.isFormCoreComplete();
    const s2 = this.isUploadsStepComplete();
    const s3 = this.isFeePaid();
    const s4 = this.isApplicationSubmitted();

    let currentIndex = 0;
    if (!s1) {
      currentIndex = 0;
    } else if (!s2) {
      currentIndex = 1;
    } else if (!s3) {
      currentIndex = 2;
    } else if (!s4) {
      currentIndex = 3;
    } else {
      currentIndex = -1;
    }

    const base = [
      {
        id: 'form',
        title: 'Application information',
        hint: 'Personal details through course preferences',
        done: s1,
      },
      {
        id: 'documents',
        title: 'Documents & uploads',
        hint: 'Mark sheets and declaration on the form',
        done: s2,
      },
      {
        id: 'payment',
        title: 'Application fee',
        hint: 'Secure online payment',
        done: s3,
      },
      {
        id: 'submit',
        title: 'Submit to admissions',
        hint: 'Application sent for review',
        done: s4,
      },
    ];

    const steps = base.map((step, i) => {
      let state: 'complete' | 'current' | 'pending';
      if (step.done) {
        state = 'complete';
      } else if (i === currentIndex) {
        state = 'current';
      } else {
        state = 'pending';
      }
      return { ...step, state };
    });

    return { steps, allComplete: s1 && s2 && s3 && s4, currentIndex };
  });

  /** Primary CTA for the hero and next-step card. */
  readonly nextGuidance = computed((): {
    title: string;
    body: string;
    ctaLabel: string;
    ctaRouterLink: string | null;
    ctaAction: 'pay' | 'application' | 'documents' | null;
    urgency: 'critical' | 'high' | 'normal';
  } => {
    const sum = this.summary();
    const remaining = sum.fees.remaining;

    if (this.feeLooksFailed() && remaining > 0 && sum.fees.canPay) {
      return {
        title: 'Payment needs attention',
        body: 'Your last payment attempt did not complete. Try again using the Pay now button.',
        ctaLabel: 'Retry payment',
        ctaRouterLink: null,
        ctaAction: 'pay',
        urgency: 'critical',
      };
    }
    if (sum.fees.canPay && remaining > 0) {
      return {
        title: 'Pay your application fee',
        body: `₹${remaining.toFixed(0)} is due. Unpaid applications may not be considered for admission. Complete payment as soon as possible.`,
        ctaLabel: 'Pay now',
        ctaRouterLink: null,
        ctaAction: 'pay',
        urgency: 'critical',
      };
    }
    if (this.incompleteSteps().length > 0) {
      const first = this.incompleteSteps()[0];
      return {
        title: 'Complete your application',
        body: first
          ? `${first.title} still needs to be finished. Finish all sections before your application can be reviewed.`
          : 'Continue your application form to move forward.',
        ctaLabel: 'Continue application',
        ctaRouterLink: '/app/profile/application',
        ctaAction: 'application',
        urgency: 'high',
      };
    }
    if (this.documentsNeedAttention()) {
      return {
        title: 'Upload required documents',
        body: 'Some required documents are still pending. Upload them so admissions can verify your file.',
        ctaLabel: 'Go to documents',
        ctaRouterLink: '/app/documents',
        ctaAction: 'documents',
        urgency: 'high',
      };
    }
    const status = this.selectionStatus();
    if (status === 'Under Review' || status === 'Submitted') {
      return {
        title: 'Application under review',
        body: 'No action needed right now. We will email you when there is an update.',
        ctaLabel: '',
        ctaRouterLink: null,
        ctaAction: null,
        urgency: 'normal',
      };
    }
    if (status === 'Approved') {
      return {
        title: 'Congratulations',
        body: 'Follow the instructions in your email for the next steps.',
        ctaLabel: 'View admission offer',
        ctaRouterLink: '/app/offer',
        ctaAction: null,
        urgency: 'normal',
      };
    }
    return {
      title: 'You are up to date',
      body: this.store.summary().checklist.nextAction,
      ctaLabel: 'Open application',
      ctaRouterLink: '/app/profile/application',
      ctaAction: 'application',
      urgency: 'normal',
    };
  });

  /** Ordered quick actions: most urgent first, max practical items. */
  readonly quickActionItems = computed(() => {
    const items: {
      id: string;
      label: string;
      icon: string;
      routerLink?: string;
      action?: 'pay';
      priority: number;
    }[] = [];

    if (this.paymentPending()) {
      items.push({
        id: 'pay',
        label: 'Pay application fee',
        icon: 'solar:card-bold',
        action: 'pay',
        priority: 1,
      });
    }
    if (this.incompleteSteps().length > 0) {
      items.push({
        id: 'profile',
        label: 'Continue application form',
        icon: 'solar:clipboard-list-bold',
        routerLink: '/app/profile/application',
        priority: 2,
      });
    }
    if (this.documentsNeedAttention()) {
      items.push({
        id: 'docs',
        label: 'Upload documents',
        icon: 'solar:document-add-bold',
        routerLink: '/app/documents',
        priority: 3,
      });
    }
    items.sort((a, b) => a.priority - b.priority);
    return items.slice(0, 4);
  });

  runGuidanceAction(): void {
    const g = this.nextGuidance();
    if (g.ctaAction === 'pay') {
      this.startPayment();
    }
  }

  statusStepDone(step: string): boolean {
    const status = this.selectionStatus();
    if (step === 'Submitted') return true;
    if (step === 'Under Review') return ['Under Review', 'Approved', 'Rejected', 'WaitingList', 'EntranceExam'].includes(status);
    if (step === 'Approved') return status === 'Approved';
    return false;
  }

  startPayment(): void {
    const amount = this.summary().fees.amountDue;
    this.paymentComponent()?.open(amount);
  }

  /**
   * Normalize API payload: nested DTO, PascalCase JSON, or legacy plain string (same as pre-refactor).
   */
  private coerceElective(raw: unknown): ApplicantElectiveSubjectDto {
    if (raw == null) {
      return { code: '', name: '—', description: null };
    }
    if (typeof raw === 'string') {
      const s = raw.trim();
      if (!s) {
        return { code: '', name: '—', description: null };
      }
      const split = splitElectiveCatalogLine(s);
      if (split) {
        return {
          code: split.code,
          name: split.name,
          description: split.full,
        };
      }
      return { code: s, name: s, description: null };
    }
    if (typeof raw === 'object') {
      const o = raw as Record<string, unknown>;
      const code = String(o['code'] ?? o['Code'] ?? '').trim();
      const name = String(o['name'] ?? o['Name'] ?? '').trim();
      const description = (o['description'] ?? o['Description'] ?? null) as string | null;
      if (!code && !name) {
        return { code: '', name: '—', description };
      }
      return {
        code,
        name: name || code || '—',
        description: description ?? null,
      };
    }
    return { code: '', name: '—', description: null };
  }

  /** Elective row: bold name + muted code; tooltip uses full catalog line from API. */
  electiveDisplay(raw: unknown): {
    name: string;
    code: string;
    showCode: boolean;
  } {
    const e = this.coerceElective(raw);
    const name = (e.name ?? '').trim() || '—';
    const code = (e.code ?? '').trim();
    const showCode = Boolean(code && code !== name);
    return { name, code, showCode };
  }

  private formatElectivePlainText(raw: unknown): string {
    const e = this.coerceElective(raw);
    const name = (e.name ?? '').trim();
    const code = (e.code ?? '').trim();
    if (!name && !code) {
      return '—';
    }
    if (!code || code === name) {
      return name || code;
    }
    return `${name} (${code})`;
  }

  /** Native tooltip: full catalog line when available. */
  electiveTooltip(raw: unknown): string | null {
    const e = this.coerceElective(raw);
    const d = e.description?.trim();
    if (d) {
      return d;
    }
    const name = (e.name ?? '').trim();
    const code = (e.code ?? '').trim();
    if (!name || name === '—') {
      return null;
    }
    if (code && code !== name) {
      return `${code} \u2014 ${name}`;
    }
    return name;
  }

  downloadCourseSummary(): void {
    const d = this.store.dashboard();
    const cs = d?.courseSelection;
    const p = d?.profile;
    if (!cs || !p) {
      return;
    }
    const lines = [
      'Don Bosco College, Tura — Application summary',
      '========================================',
      `Application reference: ${p.uniqueId}`,
      `Name: ${p.fullName}`,
      '',
      'YOUR SELECTED COURSE DETAILS',
      '-----------------------------',
      `Shift: ${cs.preferredShiftLabel}`,
      `Major: ${cs.majorSubject}`,
      `Minor: ${cs.minorSubject}`,
      '',
      'Electives',
      `MDC: ${this.formatElectivePlainText(cs.mdc)}`,
      `AEC: ${this.formatElectivePlainText(cs.aec)}`,
      `SEC: ${this.formatElectivePlainText(cs.sec)}`,
      `VAC: ${this.formatElectivePlainText(cs.vac)}`,
      '',
      cs.applicationFeePaidOnUtc
        ? `Application fee paid on: ${new Date(cs.applicationFeePaidOnUtc).toLocaleString()}`
        : '',
      cs.draftLastUpdatedOnUtc
        ? `Application data last saved: ${new Date(cs.draftLastUpdatedOnUtc).toLocaleString()}`
        : '',
    ].filter((line) => line !== '');
    const blob = new Blob([lines.join('\n')], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `application-summary-${p.uniqueId}.txt`;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
