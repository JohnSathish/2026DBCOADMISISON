import {
  Component,
  OnInit,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CountUpModule } from 'ngx-countup';
import {
  AdmissionsAdminApiService,
  AdminDashboardDto,
  OnlineApplicationDto,
} from '@client/shared/data';
import { AuthService } from '../../auth/auth.service';
import { ToastService } from '../../shared/toast.service';
import { firstValueFrom } from 'rxjs';
import { ADMIN_APPLICANT_STATUSES } from '../admin.constants';
import {
  APPLICATION_LIFECYCLE_FILTER_OPTIONS,
  getApplicationLifecycleLabel,
  getApplicationLifecycleBadgeClass,
} from '../admissions/application-lifecycle.util';
import { createAngularTable, getCoreRowModel, type ColumnDef } from '@tanstack/angular-table';
import type { Row } from '@tanstack/angular-table';

type SortKey = 'createdOnUtc' | 'fullName' | 'uniqueId' | 'status';

const APPLICATION_TABLE_COLUMNS: ColumnDef<OnlineApplicationDto>[] = [
  { id: 'uniqueId', accessorKey: 'uniqueId', header: 'App #' },
  { id: 'fullName', accessorKey: 'fullName', header: 'Applicant' },
  {
    id: 'majorSubject',
    accessorFn: (r) => r.majorSubject ?? '',
    header: 'Course',
  },
  { id: 'shift', accessorFn: (r) => r.shift ?? '', header: 'Shift' },
  {
    id: 'lifecycle',
    accessorFn: (r) => getApplicationLifecycleLabel(r),
    header: 'Application stage',
  },
  { id: 'status', accessorKey: 'status', header: 'Pipeline' },
  {
    id: 'payment',
    accessorFn: (r) => (r.isPaymentCompleted ? 'Paid' : 'Not paid'),
    header: 'Payment',
  },
  { id: 'createdOnUtc', accessorKey: 'createdOnUtc', header: 'Created' },
  {
    id: 'erp',
    accessorFn: (r) => r.erpStudentId ?? '',
    header: 'ERP student',
  },
  { id: 'actions', header: 'Actions', accessorFn: () => '' },
];

@Component({
  selector: 'app-admin-dashboard-home',
  standalone: true,
  imports: [RouterLink, CommonModule, CountUpModule, FormsModule],
  templateUrl: './admin-dashboard-home.component.html',
  styleUrl: './admin-dashboard-home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
export class AdminDashboardHomeComponent implements OnInit {
  private readonly admissionsApi = inject(AdmissionsAdminApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly dashboard = signal<AdminDashboardDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly tableLoading = signal(false);
  protected readonly exporting = signal(false);
  protected readonly syncingId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly applications = signal<OnlineApplicationDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly currentPage = signal(1);
  protected readonly pageSize = signal(15);

  protected readonly sortBy = signal<SortKey>('createdOnUtc');
  protected readonly sortDescending = signal(true);

  protected readonly searchTerm = signal('');
  protected readonly statusFilter = signal<string>('');
  /** Server funnel filter (registered → paid). When set, submission/payment booleans are not sent. */
  protected readonly lifecycleStageFilter = signal<string>('');
  protected readonly paymentFilter = signal<'all' | 'paid' | 'unpaid'>('all');
  protected readonly shiftFilter = signal('');
  protected readonly dateFrom = signal('');
  protected readonly dateTo = signal('');
  /** Refines the current page by course / major (client-side; API has no draft filter). */
  protected readonly courseContains = signal('');

  protected readonly statusOptions = ADMIN_APPLICANT_STATUSES;
  protected readonly lifecycleFilterOptions = APPLICATION_LIFECYCLE_FILTER_OPTIONS;
  protected readonly shiftOptions = ['', 'Day', 'Morning', 'Evening', 'Night'];
  protected readonly pageSizeOptions = [10, 15, 25, 50];

  protected readonly displayApplications = computed(() => {
    const q = this.courseContains().trim().toLowerCase();
    let list = this.applications();
    if (q) {
      list = list.filter((a) =>
        (a.majorSubject ?? '').toLowerCase().includes(q)
      );
    }
    return list;
  });

  /** TanStack Table: headless row model (sorting remains server-driven). */
  protected readonly applicationsTable = createAngularTable<OnlineApplicationDto>(() => ({
    data: this.displayApplications(),
    columns: APPLICATION_TABLE_COLUMNS,
    getCoreRowModel: getCoreRowModel(),
  }));

  protected readonly recentActivity = signal<
    { icon: string; text: string; time: string }[]
  >([]);

  readonly quickActions = [
    {
      icon: '📋',
      label: 'All applications',
      route: '/admin/admissions/applications',
      gradient: 'violet',
    },
    {
      icon: '📊',
      label: 'Analytics',
      route: '/admin/admissions/analytics',
      gradient: 'sky',
    },
    {
      icon: '✅',
      label: 'Document check',
      route: '/admin/admissions/verification',
      gradient: 'cyan',
    },
    {
      icon: '💳',
      label: 'Payments',
      route: '/admin/admissions/payment-verification',
      gradient: 'indigo',
    },
  ];

  ngOnInit(): void {
    void this.bootstrap();
  }

  private async bootstrap(): Promise<void> {
    await Promise.all([this.loadDashboard(), this.loadApplications()]);
  }

  async loadDashboard(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const data = await firstValueFrom(this.admissionsApi.getAdminDashboard());
      this.dashboard.set(data);
      this.patchActivity(data);
    } catch (err) {
      console.error(err);
      this.error.set('Unable to load admission summary. Please retry.');
    } finally {
      this.loading.set(false);
    }
  }

  private patchActivity(data: AdminDashboardDto): void {
    this.recentActivity.set([
      {
        icon: '📌',
        text: `${data.submittedApplications} submitted application(s)`,
        time: 'All time',
      },
      {
        icon: '💰',
        text: `${data.paidApplications} payment(s) completed`,
        time: 'Recorded',
      },
      {
        icon: '⏳',
        text: `${data.pendingPipelineCount ?? 0} pending review`,
        time: 'Pipeline',
      },
      {
        icon: '✅',
        text: `${data.approvedApplications} approved`,
        time: 'Decisions',
      },
    ]);
  }

  async loadApplications(): Promise<void> {
    this.tableLoading.set(true);
    try {
      const lc = this.lifecycleStageFilter().trim();
      const useLifecycle = !!lc;
      const pay = this.paymentFilter();
      const isPaymentCompleted = useLifecycle
        ? undefined
        : pay === 'all'
          ? undefined
          : pay === 'paid'
            ? true
            : false;

      const from = this.dateFrom();
      const to = this.dateTo();
      const createdFromUtc = from
        ? new Date(from + 'T00:00:00.000Z').toISOString()
        : undefined;
      const createdToUtc = to
        ? new Date(to + 'T23:59:59.999Z').toISOString()
        : undefined;

      const response = await firstValueFrom(
        this.admissionsApi.listOnlineApplications({
          page: this.currentPage(),
          pageSize: this.pageSize(),
          isPaymentCompleted,
          applicationLifecycleStage: lc || undefined,
          status: this.statusFilter() || undefined,
          searchTerm: this.searchTerm().trim() || undefined,
          shift: this.shiftFilter() || undefined,
          createdFromUtc,
          createdToUtc,
          sortBy: this.sortBy(),
          sortDescending: this.sortDescending(),
        })
      );
      this.applications.set(response.applications);
      this.totalCount.set(response.totalCount);
      this.currentPage.set(response.page);
    } catch (err) {
      console.error(err);
      this.toast.error('Unable to load applications.');
    } finally {
      this.tableLoading.set(false);
    }
  }

  async applyFilters(): Promise<void> {
    this.currentPage.set(1);
    await this.loadApplications();
  }

  async resetFilters(): Promise<void> {
    this.searchTerm.set('');
    this.statusFilter.set('');
    this.lifecycleStageFilter.set('');
    this.paymentFilter.set('all');
    this.shiftFilter.set('');
    this.dateFrom.set('');
    this.dateTo.set('');
    this.courseContains.set('');
    this.currentPage.set(1);
    await this.loadApplications();
  }

  async onPageChange(page: number): Promise<void> {
    this.currentPage.set(page);
    await this.loadApplications();
  }

  async onPageSizeChange(size: number): Promise<void> {
    this.pageSize.set(size);
    this.currentPage.set(1);
    await this.loadApplications();
  }

  toggleSort(column: SortKey): void {
    if (this.sortBy() === column) {
      this.sortDescending.update((v) => !v);
    } else {
      this.sortBy.set(column);
      this.sortDescending.set(column === 'createdOnUtc');
    }
    void this.loadApplications();
  }

  sortIndicator(column: SortKey): string {
    if (this.sortBy() !== column) {
      return '↕';
    }
    return this.sortDescending() ? '↓' : '↑';
  }

  async exportExcel(): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await firstValueFrom(
        this.admissionsApi.exportSubmittedApplicationsExcel()
      );
      this.downloadBlob(
        blob,
        `admissions-export-${new Date().toISOString().slice(0, 10)}.xlsx`
      );
      this.toast.success('Excel export started.');
    } catch (err) {
      console.error(err);
      this.toast.error('Excel export failed.');
    } finally {
      this.exporting.set(false);
    }
  }

  async exportExcelAndCloseMenu(ev: Event): Promise<void> {
    await this.exportExcel();
    (ev.currentTarget as HTMLElement | null)
      ?.closest('details')
      ?.removeAttribute('open');
  }

  async syncErp(app: OnlineApplicationDto): Promise<void> {
    this.syncingId.set(app.id);
    try {
      const r = await firstValueFrom(this.admissionsApi.syncApplicationToErp(app.id));
      if (r.success && r.erpStudentId) {
        this.toast.success(r.message || 'Linked to ERP student.');
      } else {
        this.toast.info(r.message || 'Sync finished.');
      }
      await Promise.all([this.loadDashboard(), this.loadApplications()]);
    } catch (err: unknown) {
      console.error(err);
      this.toast.error(this.httpErrorMessage(err, 'ERP sync failed.'));
    } finally {
      this.syncingId.set(null);
    }
  }

  showErpSync(app: OnlineApplicationDto): boolean {
    return app.status === 'Approved' && !app.erpStudentId;
  }

  erpCellText(app: OnlineApplicationDto): string {
    if (app.erpStudentId) {
      return app.erpStudentId.slice(0, 8) + '…';
    }
    if (app.erpSyncLastError) {
      return 'Error';
    }
    return '—';
  }

  erpTitle(app: OnlineApplicationDto): string {
    if (app.erpStudentId) {
      return `ERP student ID: ${app.erpStudentId}`;
    }
    if (app.erpSyncLastError) {
      return app.erpSyncLastError;
    }
    return '';
  }

  async downloadPdf(app: OnlineApplicationDto): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.admissionsApi.downloadOnlineApplicationPdf(app.id)
      );
      const safeName = app.uniqueId.replace(/[^\w.-]+/g, '_');
      this.downloadBlob(blob, `application-${safeName}.pdf`);
    } catch (err) {
      console.error(err);
      this.toast.error('Could not download PDF.');
    }
  }

  private downloadBlob(blob: Blob, fallbackName: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fallbackName;
    a.click();
    URL.revokeObjectURL(url);
  }

  async viewDetail(app: OnlineApplicationDto): Promise<void> {
    await this.router.navigate(['/admin/admissions/applications', app.id]);
  }

  async approve(app: OnlineApplicationDto): Promise<void> {
    const remarks = window.prompt(`Optional remarks for approving ${app.uniqueId}:`);
    if (remarks === null) {
      return;
    }
    this.tableLoading.set(true);
    try {
      await firstValueFrom(
        this.admissionsApi.approveOnlineApplication(app.id, {
          remarks: remarks.trim() || undefined,
          notifyApplicant: true,
        })
      );
      this.toast.success('Application approved.');
      await Promise.all([this.loadDashboard(), this.loadApplications()]);
    } catch (err: unknown) {
      console.error(err);
      this.toast.error(this.httpErrorMessage(err, 'Approve failed.'));
    } finally {
      this.tableLoading.set(false);
    }
  }

  async reject(app: OnlineApplicationDto): Promise<void> {
    const remarks = window.prompt(`Remarks for rejecting ${app.uniqueId} (optional):`);
    if (remarks === null) {
      return;
    }
    this.tableLoading.set(true);
    try {
      await firstValueFrom(
        this.admissionsApi.rejectOnlineApplication(app.id, {
          remarks: remarks.trim() || undefined,
          notifyApplicant: true,
        })
      );
      this.toast.success('Application rejected.');
      await Promise.all([this.loadDashboard(), this.loadApplications()]);
    } catch (err: unknown) {
      console.error(err);
      this.toast.error(this.httpErrorMessage(err, 'Reject failed.'));
    } finally {
      this.tableLoading.set(false);
    }
  }

  private httpErrorMessage(err: unknown, fallback: string): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const e = err as { error?: unknown };
      if (typeof e.error === 'string' && e.error.trim()) {
        return e.error;
      }
    }
    return fallback;
  }

  canApprove(app: OnlineApplicationDto): boolean {
    return app.status !== 'Approved' && app.status !== 'Enrolled';
  }

  canReject(app: OnlineApplicationDto): boolean {
    return app.status !== 'Rejected' && app.status !== 'Enrolled';
  }

  paymentLabel(app: OnlineApplicationDto): string {
    return app.isPaymentCompleted ? 'Paid' : 'Not paid';
  }

  lifecycleBadgeClass(app: OnlineApplicationDto): string {
    return getApplicationLifecycleBadgeClass(getApplicationLifecycleLabel(app));
  }

  protected hasActiveServerFilters(): boolean {
    return !!(
      this.searchTerm().trim() ||
      this.statusFilter().trim() ||
      this.lifecycleStageFilter().trim() ||
      this.paymentFilter() !== 'all' ||
      this.shiftFilter().trim() ||
      this.dateFrom() ||
      this.dateTo()
    );
  }

  protected emptyTableMessage(): string {
    if (this.hasActiveServerFilters()) {
      return 'No students match the current filters. Try Reset or change application stage, dates, or search.';
    }
    return 'No registered students yet.';
  }

  customFormattingFn(value: number): string {
    return value.toString();
  }

  getUserDisplayName(): string {
    return this.auth.profile?.fullName?.trim() || 'Admin';
  }

  getCurrentDate(): string {
    return new Date().toLocaleDateString('en-IN', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
  }

  trackByRow(_i: number, row: Row<OnlineApplicationDto>): string {
    return row.original.id;
  }

  /** Visual variant for status column badges */
  statusBadgeClass(status: unknown): string {
    const s = String(status ?? '').toLowerCase();
    if (s.includes('approved') || s.includes('enrolled')) {
      return 'admin-dash__status admin-dash__status--success';
    }
    if (s.includes('reject')) {
      return 'admin-dash__status admin-dash__status--danger';
    }
    if (s.includes('waiting') || s.includes('entrance')) {
      return 'admin-dash__status admin-dash__status--info';
    }
    if (s.includes('submit') || s.includes('pending')) {
      return 'admin-dash__status admin-dash__status--warning';
    }
    return 'admin-dash__status admin-dash__status--neutral';
  }

  paymentBadgeClass(paid: boolean): string {
    return paid ? 'admin-dash__status admin-dash__status--success' : 'admin-dash__status admin-dash__status--danger';
  }
}
