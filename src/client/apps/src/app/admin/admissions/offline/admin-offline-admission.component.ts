import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdmissionsAdminApiService,
  AdminDashboardDto,
  CourseDto,
  OfflineFormIssuancePreviewDto,
  SettingsApiService,
} from '@client/shared/data';
import { finalize } from 'rxjs/operators';
import { ToastService } from '../../../shared/toast.service';

@Component({
  selector: 'app-admin-offline-admission',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-offline-admission.component.html',
  styleUrls: ['./admin-offline-admission.component.scss'],
})
export class AdminOfflineAdmissionComponent implements OnInit {
  private readonly api = inject(AdmissionsAdminApiService);
  private readonly settingsApi = inject(SettingsApiService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(false);
  readonly dashboard = signal<AdminDashboardDto | null>(null);
  /** Active courses from settings (alphabetical by name). */
  readonly activeCourses = signal<CourseDto[]>([]);
  readonly coursesLoadError = signal(false);
  /** Filter for searchable course list */
  courseSearch = '';
  readonly confirmReceiveOpen = signal(false);

  issue = {
    formNumber: '',
    studentName: '',
    shift: '' as '' | 'ShiftI' | 'ShiftII' | 'ShiftIII',
    mobileNumber: '',
    cuet: '' as '' | 'yes' | 'no',
    applicationFeeAmount: 0,
  };

  receiveFormNumber = '';
  /** Selected course name (matches master data; sent as majorSubject). */
  receiveMajorSubject = '';
  readonly receivePreview = signal<OfflineFormIssuancePreviewDto | null | undefined>(undefined);

  /** Filtered list for the dropdown (recomputed each change detection when search changes). */
  getFilteredCourses(): CourseDto[] {
    const q = this.courseSearch.trim().toLowerCase();
    const list = this.activeCourses();
    let result = !q
      ? [...list]
      : list.filter(
          (c) =>
            c.name.toLowerCase().includes(q) ||
            c.code.toLowerCase().includes(q) ||
            (c.programName?.toLowerCase().includes(q) ?? false)
        );
    const sel = this.receiveMajorSubject.trim();
    if (sel && !result.some((c) => c.name === sel)) {
      const picked = list.find((c) => c.name === sel);
      if (picked) {
        result = [picked, ...result.filter((c) => c.name !== sel)];
      }
    }
    return result;
  }

  /** Enable major/course UI only after a successful lookup with no existing account. */
  readonly canSelectFinalMajor = computed(() => {
    const p = this.receivePreview();
    return p != null && !p.applicantAccountCreated;
  });

  assignApplicationId = '';
  assignRound: 'First' | 'Second' | 'Third' = 'First';
  publishRound: 'First' | 'Second' | 'Third' = 'First';
  publishSendSms = true;
  reprintFormNumber = '';

  ngOnInit(): void {
    this.loadDashboard();
    this.loadActiveCourses();
  }

  private loadActiveCourses(): void {
    const acc: CourseDto[] = [];
    const loadPage = (page: number): void => {
      this.settingsApi.listCourses({ page, pageSize: 200, isActive: true }).subscribe({
        next: (res) => {
          acc.push(...res.courses);
          const totalPages = Math.max(1, Math.ceil(res.totalCount / res.pageSize));
          if (page < totalPages) {
            loadPage(page + 1);
          } else {
            acc.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
            this.activeCourses.set(acc);
            this.coursesLoadError.set(false);
          }
        },
        error: () => {
          this.coursesLoadError.set(true);
          this.toast.error('Could not load courses from settings. Check Settings → Courses.');
        },
      });
    };
    loadPage(1);
  }

  loadDashboard(): void {
    this.loading.set(true);
    this.api
      .getAdminDashboard()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (d) => this.dashboard.set(d),
        error: () => this.toast.error('Could not load admission dashboard.'),
      });
  }

  issueForm(): void {
    const v = this.issue;
    if (!v.formNumber.trim() || v.formNumber.trim().length !== 6) {
      this.toast.error('Enter a 6-digit form number.');
      return;
    }
    if (!v.shift) {
      this.toast.error('Select a shift.');
      return;
    }
    if (!v.cuet) {
      this.toast.error('Select whether the student has applied for CUET.');
      return;
    }
    this.loading.set(true);
    this.api
      .issueOfflineAdmissionForm({
        formNumber: v.formNumber.trim(),
        studentName: v.studentName.trim(),
        mobileNumber: v.mobileNumber.trim(),
        shift: v.shift,
        cuetApplied: v.cuet === 'yes',
        applicationFeeAmount: v.applicationFeeAmount,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `offline-receipt-${v.formNumber.trim()}.pdf`;
          a.click();
          URL.revokeObjectURL(url);
          this.toast.success('Form issued. Receipt PDF downloaded (no portal account yet).');
          this.loadDashboard();
        },
        error: (err) => {
          this.toast.error(this.errMsg(err) ?? 'Issue failed.');
        },
      });
  }

  lookupReceivePreview(): void {
    const n = this.receiveFormNumber.trim();
    this.receivePreview.set(undefined);
    this.receiveMajorSubject = '';
    this.courseSearch = '';
    if (n.length !== 6) {
      return;
    }
    this.loading.set(true);
    this.api
      .getOfflineFormIssuancePreview(n)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (p) => this.receivePreview.set(p),
        error: () => {
          this.receivePreview.set(null);
          this.toast.error('Could not look up form.');
        },
      });
  }

  openConfirmReceive(): void {
    const n = this.receiveFormNumber.trim();
    const major = this.receiveMajorSubject.trim();
    if (n.length !== 6) {
      this.toast.error('Enter a 6-digit form number and click Look up.');
      return;
    }
    const p = this.receivePreview();
    if (p == null) {
      this.toast.error('Look up the form first.');
      return;
    }
    if (p.applicantAccountCreated) {
      this.toast.error('This form was already received — applicant account exists.');
      return;
    }
    if (!major) {
      this.toast.error('Select the final major / course from the list.');
      return;
    }
    if (this.coursesLoadError()) {
      this.toast.error('Courses could not be loaded. Refresh the page or check Settings → Courses.');
      return;
    }
    if (!this.activeCourses().length) {
      this.toast.error('No active courses found. Add courses under Settings → Courses.');
      return;
    }
    this.confirmReceiveOpen.set(true);
  }

  cancelConfirmReceive(): void {
    this.confirmReceiveOpen.set(false);
  }

  receiveForm(): void {
    const n = this.receiveFormNumber.trim();
    const major = this.receiveMajorSubject.trim();
    this.confirmReceiveOpen.set(false);
    if (n.length !== 6) {
      this.toast.error('Enter a 6-digit form number.');
      return;
    }
    if (!major) {
      this.toast.error('Select the final major / course.');
      return;
    }
    const p = this.receivePreview();
    if (p?.applicantAccountCreated) {
      this.toast.error('An applicant account already exists for this form.');
      return;
    }
    this.loading.set(true);
    this.api
      .receiveOfflineAdmissionForm({ formNumber: n, majorSubject: major })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (r) => {
          this.toast.success(
            `Account created: ${r.studentName} — ${r.majorSubject}. Form marked received.`
          );
          this.receiveFormNumber = '';
          this.receiveMajorSubject = '';
          this.courseSearch = '';
          this.receivePreview.set(undefined);
          this.loadDashboard();
        },
        error: (err) => this.toast.error(this.errMsg(err) ?? 'Receive failed.'),
      });
  }

  reprintReceipt(): void {
    const n = this.reprintFormNumber.trim();
    if (n.length !== 6) {
      this.toast.error('Enter a 6-digit form number.');
      return;
    }
    this.loading.set(true);
    this.api
      .getOfflineFormReceiptPdf(n)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `offline-receipt-${n}.pdf`;
          a.click();
          URL.revokeObjectURL(url);
        },
        error: (err) => this.toast.error(this.errMsg(err) ?? 'Could not load receipt.'),
      });
  }

  assignRoundSave(): void {
    const id = this.assignApplicationId.trim();
    if (!id) {
      this.toast.error('Enter application (account) ID GUID.');
      return;
    }
    this.loading.set(true);
    this.api
      .assignSelectionListRound(id, this.assignRound)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.toast.success('Selection list round saved.'),
        error: (err) => this.toast.error(this.errMsg(err) ?? 'Save failed.'),
      });
  }

  publishList(): void {
    this.loading.set(true);
    this.api
      .publishSelectionList({
        round: this.publishRound,
        sendSmsNotifications: this.publishSendSms,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (r) =>
          this.toast.success(
            `Published: ${r.publishedCount}. SMS sent: ${r.smsSent}, failed: ${r.smsFailed}.`
          ),
        error: (err) => this.toast.error(this.errMsg(err) ?? 'Publish failed.'),
      });
  }

  private errMsg(err: unknown): string | null {
    const e = err as { error?: unknown; message?: string };
    if (typeof e?.error === 'string' && e.error.length) {
      return e.error;
    }
    if (e?.error && typeof e.error === 'object' && 'message' in (e.error as object)) {
      const m = (e.error as { message?: unknown }).message;
      if (m != null && String(m).length) {
        return String(m);
      }
    }
    if (typeof e?.message === 'string' && !e.message.startsWith('Http failure')) {
      return e.message;
    }
    return null;
  }
}
