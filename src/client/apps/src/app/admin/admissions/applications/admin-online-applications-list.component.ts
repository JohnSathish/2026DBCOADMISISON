import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
  CUSTOM_ELEMENTS_SCHEMA,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { OnlineApplicationDto, ApplicantStatus } from '@client/shared/data';
import { firstValueFrom } from 'rxjs';
import { AdmissionsAdminApiService } from '@client/shared/data';
import { ToastService } from '../../../shared/toast.service';
import { ADMIN_APPLICANT_STATUSES } from '../../admin.constants';
import {
  applicationMatchesWorkflowStage,
  getWorkflowPipelineLabel,
  type WorkflowStageFilter,
} from '../admission-workflow';
import {
  APPLICATION_LIFECYCLE_FILTER_OPTIONS,
  getApplicationLifecycleBadgeClass,
  getApplicationLifecycleLabel,
} from '../application-lifecycle.util';

@Component({
  selector: 'app-admin-online-applications-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule, DatePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-online-applications-list.component.html',
  styleUrls: ['./admin-online-applications-list.component.scss'],
})
export class AdminOnlineApplicationsListComponent implements OnInit {
  private readonly api = inject(AdmissionsAdminApiService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);

  protected readonly applications = signal<OnlineApplicationDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly currentPage = signal(1);
  protected readonly pageSize = signal(50);
  protected readonly enrollingAccountId = signal<string | null>(null);
  protected readonly enrollmentRemarks = signal<string>('');
  protected readonly showEnrollModal = signal(false);

  protected readonly directAdmissionCutoff = signal(75);
  protected readonly sortBy = signal<string>('createdOnUtc');
  protected readonly sortDescending = signal(true);
  protected readonly showDirectModal = signal(false);
  protected readonly directTargetId = signal<string | null>(null);
  protected readonly erpWorkingId = signal<string | null>(null);

  protected readonly searchControl = this.fb.control<string>('');
  protected readonly submittedFilterControl = this.fb.control<boolean | null>(null);
  protected readonly paymentFilterControl = this.fb.control<boolean | null>(null);
  protected readonly statusFilterControl = this.fb.control<string>('');
  protected readonly shiftFilterControl = this.fb.control<string>('');
  protected readonly admissionPathControl = this.fb.control<'all' | 'direct' | 'normal'>('all');
  protected readonly percentageRangeControl = this.fb.control<
    'all' | '60-70' | '70-80' | '80plus'
  >('all');
  protected readonly lifecycleStageControl = this.fb.control<string>('');
  protected readonly createdFromControl = this.fb.control<string>('');
  protected readonly createdToControl = this.fb.control<string>('');

  protected readonly workflowStageFilter = signal<WorkflowStageFilter | ''>('');
  protected readonly categoryContains = signal('');
  protected readonly majorContains = signal('');

  protected readonly statusOptions = ADMIN_APPLICANT_STATUSES;
  protected readonly shiftOptions = ['Day', 'Morning', 'Evening', 'Night'];
  protected readonly lifecycleFilterOptions = APPLICATION_LIFECYCLE_FILTER_OPTIONS;

  protected readonly workflowStageOptions: { value: WorkflowStageFilter | ''; label: string }[] = [
    { value: '', label: 'All stages' },
    { value: 'intake', label: '1 · Intake (draft / fee)' },
    { value: 'documents', label: '2 · Document verification' },
    { value: 'merit', label: '3 · Merit / entrance / waitlist' },
    { value: 'decision', label: '4 · Decision (approved / rejected / waitlist)' },
    { value: 'fee', label: '5 · Selected (admission fee path)' },
    { value: 'enrollment', label: '6 · Enrolled' },
  ];

  protected readonly filteredApplications = computed(() => {
    let list = this.applications();
    const stage = this.workflowStageFilter();
    if (stage) {
      list = list.filter((a) => applicationMatchesWorkflowStage(a, stage));
    }
    const cat = this.categoryContains().trim().toLowerCase();
    if (cat) {
      list = list.filter((a) => (a.category ?? '').toLowerCase().includes(cat));
    }
    const maj = this.majorContains().trim().toLowerCase();
    if (maj) {
      list = list.filter((a) => (a.majorSubject ?? '').toLowerCase().includes(maj));
    }
    return list;
  });

  protected readonly hasMore = computed(() => {
    const total = this.totalCount();
    const current = this.applications().length;
    return current < total;
  });

  async ngOnInit(): Promise<void> {
    try {
      const s = await firstValueFrom(this.api.getAdmissionWorkflowSettings());
      this.directAdmissionCutoff.set(s.directAdmissionCutoffPercentage);
    } catch {
      /* use default */
    }
    await this.loadApplications();
  }

  private percentageBounds(): { min: number | null; max: number | null } {
    switch (this.percentageRangeControl.value) {
      case '60-70':
        return { min: 60, max: 70 };
      case '70-80':
        return { min: 70, max: 80 };
      case '80plus':
        return { min: 80, max: null };
      default:
        return { min: null, max: null };
    }
  }

  async loadApplications(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const b = this.percentageBounds();
    const lc = (this.lifecycleStageControl.value ?? '').trim();
    const useLifecycle = !!lc;
    const from = (this.createdFromControl.value ?? '').trim();
    const to = (this.createdToControl.value ?? '').trim();
    const createdFromUtc = from
      ? new Date(from + 'T00:00:00.000Z').toISOString()
      : undefined;
    const createdToUtc = to
      ? new Date(to + 'T23:59:59.999Z').toISOString()
      : undefined;
    try {
      const response = await firstValueFrom(
        this.api.listOnlineApplications({
          page: this.currentPage(),
          pageSize: this.pageSize(),
          searchTerm: this.searchControl.value || undefined,
          isApplicationSubmitted: useLifecycle
            ? undefined
            : (this.submittedFilterControl.value ?? undefined),
          isPaymentCompleted: useLifecycle
            ? undefined
            : (this.paymentFilterControl.value ?? undefined),
          applicationLifecycleStage: lc || undefined,
          status: this.statusFilterControl.value || undefined,
          shift: this.shiftFilterControl.value || undefined,
          createdFromUtc,
          createdToUtc,
          sortBy: this.sortBy(),
          sortDescending: this.sortDescending(),
          minClassXiiPercentage: b.min,
          maxClassXiiPercentage: b.max ?? undefined,
          admissionPath:
            this.admissionPathControl.value === 'all' ? undefined : this.admissionPathControl.value,
        })
      );
      this.applications.set(response.applications);
      this.totalCount.set(response.totalCount);
      this.currentPage.set(response.page);
    } catch (error) {
      console.error('Failed to load applications', error);
      this.error.set('Unable to load applications. Please retry.');
    } finally {
      this.loading.set(false);
    }
  }

  toggleSort(column: string): void {
    if (this.sortBy() === column) {
      this.sortDescending.update((d) => !d);
    } else {
      this.sortBy.set(column);
      this.sortDescending.set(column === 'createdOnUtc');
    }
    void this.loadApplications();
  }

  sortIcon(column: string): string {
    if (this.sortBy() !== column) {
      return 'solar:sort-vertical-bold';
    }
    return this.sortDescending() ? 'solar:sort-from-bottom-to-top-bold' : 'solar:sort-from-top-to-bottom-bold';
  }

  async onSearch(): Promise<void> {
    this.currentPage.set(1);
    await this.loadApplications();
  }

  async onFilterChange(): Promise<void> {
    this.currentPage.set(1);
    await this.loadApplications();
  }

  clearLocalRefiners(): void {
    this.workflowStageFilter.set('');
    this.categoryContains.set('');
    this.majorContains.set('');
  }

  applicationLifecycleLabel(app: OnlineApplicationDto): string {
    return getApplicationLifecycleLabel(app);
  }

  applicationLifecycleBadgeClass(app: OnlineApplicationDto): string {
    return getApplicationLifecycleBadgeClass(getApplicationLifecycleLabel(app));
  }

  protected hasServerFilters(): boolean {
    return !!(
      (this.searchControl.value ?? '').trim() ||
      this.submittedFilterControl.value !== null ||
      this.paymentFilterControl.value !== null ||
      (this.statusFilterControl.value ?? '').trim() ||
      (this.shiftFilterControl.value ?? '').trim() ||
      this.percentageRangeControl.value !== 'all' ||
      this.admissionPathControl.value !== 'all' ||
      (this.lifecycleStageControl.value ?? '').trim() ||
      (this.createdFromControl.value ?? '').trim() ||
      (this.createdToControl.value ?? '').trim()
    );
  }

  emptyServerMessage(): string {
    if (this.hasServerFilters()) {
      return 'No students match the current filters. Adjust application stage, dates, or other filters, or clear them to see all registered accounts.';
    }
    return 'No registered students yet.';
  }

  workflowLabel(app: OnlineApplicationDto): string {
    return getWorkflowPipelineLabel(app);
  }

  async viewApplication(applicationId: string): Promise<void> {
    await this.router.navigate(['/admin/admissions/applications', applicationId]);
  }

  async refresh(): Promise<void> {
    await this.loadApplications();
  }

  trackByApplicationId(index: number, item: OnlineApplicationDto): string {
    return item.id;
  }

  isStatusEnrolled(status: ApplicantStatus): boolean {
    return status === 'Enrolled';
  }

  formatPct(app: OnlineApplicationDto): string {
    const p = app.classXIIPercentage;
    if (p === null || p === undefined) {
      return '—';
    }
    return `${Number(p).toFixed(2)}%`;
  }

  rowEligibleHighlight(app: OnlineApplicationDto): boolean {
    const p = app.classXIIPercentage;
    if (p === null || p === undefined) {
      return false;
    }
    return p >= this.directAdmissionCutoff();
  }

  isDirectAdmissionEligible(app: OnlineApplicationDto): boolean {
    const p = app.classXIIPercentage;
    if (p === null || p === undefined || p < this.directAdmissionCutoff()) {
      return false;
    }
    if (
      app.status === 'Approved' ||
      app.status === 'Enrolled' ||
      app.status === 'DirectAdmissionGranted' ||
      app.status === 'AdmissionFeePaid'
    ) {
      return false;
    }
    return true;
  }

  directTooltip(app: OnlineApplicationDto): string {
    if (this.isDirectAdmissionEligible(app)) {
      return 'Eligible for Direct Admission';
    }
    const p = app.classXIIPercentage;
    if (p === null || p === undefined) {
      return 'Class XII % not available';
    }
    if (p < this.directAdmissionCutoff()) {
      return `Minimum ${this.directAdmissionCutoff()}% required`;
    }
    return 'Not eligible';
  }

  openDirectModal(app: OnlineApplicationDto): void {
    this.directTargetId.set(app.id);
    this.showDirectModal.set(true);
  }

  closeDirectModal(): void {
    this.showDirectModal.set(false);
    this.directTargetId.set(null);
  }

  async confirmAdmissionFeePaid(app: OnlineApplicationDto): Promise<void> {
    if (!window.confirm(`Record admission fee payment for ${app.uniqueId}?`)) {
      return;
    }
    this.loading.set(true);
    try {
      await firstValueFrom(this.api.confirmAdmissionFeePayment(app.id, { notifyApplicant: true }));
      this.toast.success('Admission fee payment recorded.');
      await this.loadApplications();
    } catch (e: unknown) {
      this.toast.error(
        (e as { error?: string })?.error ?? (e as { message?: string })?.message ?? 'Failed'
      );
    } finally {
      this.loading.set(false);
    }
  }

  async confirmDirectAdmission(): Promise<void> {
    const id = this.directTargetId();
    if (!id) {
      return;
    }
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.api.grantDirectAdmission(id, { notifyApplicant: true }));
      this.toast.success(`Direct admission granted. Payment link: ${res.paymentUrl}`);
      this.closeDirectModal();
      await this.loadApplications();
    } catch (e: unknown) {
      const msg =
        (e as { error?: string })?.error ??
        (e as { message?: string })?.message ??
        'Failed to grant direct admission';
      this.toast.error(String(msg));
    } finally {
      this.loading.set(false);
    }
  }

  async downloadPdf(app: OnlineApplicationDto): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.downloadOnlineApplicationPdf(app.id));
      const url = window.URL.createObjectURL(blob);
      const a = window.document.createElement('a');
      a.href = url;
      a.download = `${app.uniqueId}.pdf`;
      window.document.body.appendChild(a);
      a.click();
      window.document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
      this.toast.success('PDF downloaded');
    } catch {
      this.toast.error('PDF download failed');
    }
  }

  async approveApp(app: OnlineApplicationDto): Promise<void> {
    if (!window.confirm(`Approve application ${app.uniqueId}?`)) {
      return;
    }
    this.loading.set(true);
    try {
      await firstValueFrom(this.api.approveOnlineApplication(app.id, { notifyApplicant: true }));
      this.toast.success('Approved');
      await this.loadApplications();
    } catch (e: unknown) {
      this.toast.error((e as { error?: { message?: string } })?.error?.message ?? 'Approve failed');
    } finally {
      this.loading.set(false);
    }
  }

  async rejectApp(app: OnlineApplicationDto): Promise<void> {
    if (!window.confirm(`Reject application ${app.uniqueId}?`)) {
      return;
    }
    this.loading.set(true);
    try {
      await firstValueFrom(this.api.rejectOnlineApplication(app.id, { notifyApplicant: true }));
      this.toast.success('Rejected');
      await this.loadApplications();
    } catch (e: unknown) {
      this.toast.error((e as { error?: { message?: string } })?.error?.message ?? 'Reject failed');
    } finally {
      this.loading.set(false);
    }
  }

  async syncErp(app: OnlineApplicationDto): Promise<void> {
    this.erpWorkingId.set(app.id);
    try {
      const r = await firstValueFrom(this.api.syncApplicationToErp(app.id));
      this.toast.success(r.message || 'ERP sync completed');
      await this.loadApplications();
    } catch (e: unknown) {
      this.toast.error((e as { error?: { message?: string } })?.error?.message ?? 'ERP sync failed');
    } finally {
      this.erpWorkingId.set(null);
    }
  }

  statusBadgeClass(status: ApplicantStatus): string {
    switch (status) {
      case 'Approved':
      case 'Enrolled':
        return 'status-approved';
      case 'Rejected':
        return 'status-rejected';
      case 'DirectAdmissionGranted':
        return 'status-direct';
      case 'AdmissionFeePaid':
        return 'status-fee-paid';
      case 'Submitted':
      default:
        return 'status-submitted';
    }
  }

  canEnroll(app: OnlineApplicationDto): boolean {
    return app.status === 'Approved' && app.isPaymentCompleted && app.isApplicationSubmitted;
  }

  openEnrollModal(app: OnlineApplicationDto): void {
    this.enrollingAccountId.set(app.id);
    this.enrollmentRemarks.set('');
    this.showEnrollModal.set(true);
  }

  closeEnrollModal(): void {
    this.showEnrollModal.set(false);
    this.enrollingAccountId.set(null);
    this.enrollmentRemarks.set('');
  }

  canShowApprove(app: OnlineApplicationDto): boolean {
    if (app.status === 'DirectAdmissionGranted') {
      return false;
    }
    return (
      app.status !== 'Approved' && app.status !== 'Enrolled' && app.status !== 'Rejected'
    );
  }

  canShowReject(app: OnlineApplicationDto): boolean {
    return app.status !== 'Rejected' && app.status !== 'Enrolled';
  }

  async enrollApplication(): Promise<void> {
    const accountId = this.enrollingAccountId();
    if (!accountId) {
      return;
    }

    this.loading.set(true);
    try {
      await firstValueFrom(
        this.api.enrollApplication(accountId, {
          remarks: this.enrollmentRemarks() || null,
          notifyApplicant: true,
        })
      );
      this.toast.success('Application enrolled successfully!');
      this.closeEnrollModal();
      await this.loadApplications();
    } catch (error: unknown) {
      console.error('Failed to enroll application', error);
      const errorMessage =
        (error as { error?: { message?: string } })?.error?.message ||
        (error as Error)?.message ||
        'Failed to enroll application';
      this.toast.error(errorMessage);
    } finally {
      this.loading.set(false);
    }
  }
}
