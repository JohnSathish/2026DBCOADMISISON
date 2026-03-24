import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
  computed,
  CUSTOM_ELEMENTS_SCHEMA,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  AdmissionsAdminApiService,
  AdminDashboardDto,
  ApplicationDocumentsDto,
  DocumentDto,
  OnlineApplicationDto,
  OnlineApplicationDetailDto,
} from '@client/shared/data';
import { ToastService } from '../../../shared/toast.service';
import { AuthService } from '../../../auth/auth.service';
import { LoggingService } from '../../../shared/logging.service';
import { firstValueFrom } from 'rxjs';

type StatusFilterValue = 'all' | 'submitted' | 'approved' | 'rejected' | 'paid';
type DocVerificationKind = 'pending' | 'approved' | 'rejected' | 'partial' | 'none' | 'loading' | 'unknown';

interface DocVerificationSummary {
  kind: DocVerificationKind;
  verifier: string | null;
}

@Component({
  selector: 'app-admin-document-verification',
  standalone: true,
  imports: [CommonModule, RouterModule, DatePipe, ReactiveFormsModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './admin-document-verification.component.html',
  styleUrls: ['./admin-document-verification.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDocumentVerificationComponent implements OnInit {
  private readonly api = inject(AdmissionsAdminApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly logger = inject(LoggingService);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly applications = signal<OnlineApplicationDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly tableSkeleton = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedApplication = signal<OnlineApplicationDetailDto | null>(null);
  protected readonly documents = signal<DocumentDto[]>([]);
  protected readonly loadingDocuments = signal(false);
  protected readonly viewMode = signal<'list' | 'detail'>('list');
  protected readonly verifyingDocument = signal<string | null>(null);

  protected readonly dashboardStats = signal<AdminDashboardDto | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly currentPage = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly sortBy = signal<'createdOnUtc' | 'fullName' | 'status'>('createdOnUtc');
  protected readonly sortDescending = signal(true);
  protected readonly exporting = signal(false);
  protected readonly enriching = signal(false);
  protected readonly verificationByApp = signal<Map<string, DocVerificationSummary>>(new Map());
  protected readonly selectedIds = signal<string[]>([]);
  protected readonly bulkWorking = signal(false);

  protected readonly previewModalOpen = signal(false);
  protected readonly previewObjectUrl = signal<string | null>(null);
  protected readonly previewDoc = signal<DocumentDto | null>(null);

  protected readonly filterForm = this.fb.group({
    search: [''],
    status: ['all' as StatusFilterValue],
    dateFrom: [''],
    dateTo: [''],
  });
  
  protected readonly verificationForm = this.fb.group({
    isVerified: [true, Validators.required],
    remarks: [''],
  });

  protected readonly documentTypeLabels: { [key: string]: string } = {
    StdXMarksheet: 'Class X Marksheet',
    StdXIIMarksheet: 'Class XII Marksheet',
    CuetMarksheet: 'CUET Marksheet',
    DifferentlyAbledProof: 'Differently Abled Proof',
    EconomicallyWeakerProof: 'Economically Weaker Section Proof',
  };

  protected readonly statusFilterOptions: { value: StatusFilterValue; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'submitted', label: 'Pending' },
    { value: 'approved', label: 'Approved' },
    { value: 'rejected', label: 'Rejected' },
    { value: 'paid', label: 'Paid' },
  ];

  protected readonly pageSizeOptions = [10, 25, 50] as const;

  protected readonly totalPages = computed(() => {
    const total = this.totalCount();
    const size = this.pageSize();
    return Math.max(1, Math.ceil(total / size) || 1);
  });

  protected readonly isAllPageSelected = computed(() => {
    const apps = this.applications();
    const sel = new Set(this.selectedIds());
    if (!apps.length) {
      return false;
    }
    return apps.every((a) => sel.has(a.id));
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadDashboardStats(), this.loadApplications()]);
  }

  private async loadDashboardStats(): Promise<void> {
    try {
      const d = await firstValueFrom(this.api.getAdminDashboard());
      this.dashboardStats.set(d);
    } catch (e) {
      this.logger.error('Failed to load dashboard stats', e);
    }
  }

  private listParams(): Parameters<AdmissionsAdminApiService['listOnlineApplications']>[0] {
    const raw = this.filterForm.getRawValue();
    const status = raw.status as StatusFilterValue;
    const params: Parameters<AdmissionsAdminApiService['listOnlineApplications']>[0] = {
      page: this.currentPage(),
      pageSize: this.pageSize(),
      isApplicationSubmitted: true,
      searchTerm: raw.search?.trim() || undefined,
      sortBy: this.sortBy(),
      sortDescending: this.sortDescending(),
    };

    if (raw.dateFrom) {
      const d = new Date(raw.dateFrom + 'T00:00:00.000Z');
      if (!Number.isNaN(d.getTime())) {
        params.createdFromUtc = d.toISOString();
      }
    }
    if (raw.dateTo) {
      const d = new Date(raw.dateTo + 'T23:59:59.999Z');
      if (!Number.isNaN(d.getTime())) {
        params.createdToUtc = d.toISOString();
      }
    }

    if (status === 'paid') {
      params.isPaymentCompleted = true;
    } else if (status === 'submitted') {
      params.status = 'Submitted';
    } else if (status === 'approved') {
      params.status = 'Approved';
    } else if (status === 'rejected') {
      params.status = 'Rejected';
    }

    return params;
  }

  async loadApplications(): Promise<void> {
    this.loading.set(true);
    this.tableSkeleton.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(this.api.listOnlineApplications(this.listParams()));
      this.applications.set(response.applications);
      this.totalCount.set(response.totalCount);
      this.currentPage.set(response.page);
      this.verificationByApp.set(new Map());
      void this.enrichVerificationForApps(response.applications);
    } catch (error: unknown) {
      this.logger.error('Failed to load applications', error);
      this.error.set('Failed to load applications. Please try again.');
      this.toast.error('Failed to load applications.');
    } finally {
      this.loading.set(false);
      this.tableSkeleton.set(false);
    }
  }

  private async enrichVerificationForApps(apps: OnlineApplicationDto[]): Promise<void> {
    if (!apps.length) {
      return;
    }
    this.enriching.set(true);
    const map = new Map(this.verificationByApp());
    for (const app of apps) {
      map.set(app.id, { kind: 'loading', verifier: null });
    }
    this.verificationByApp.set(new Map(map));

    const batchSize = 5;
    for (let i = 0; i < apps.length; i += batchSize) {
      const batch = apps.slice(i, i + batchSize);
      await Promise.all(
        batch.map(async (app) => {
          try {
            const docs = await firstValueFrom(this.api.getApplicationDocuments(app.id));
            const summary = this.aggregateDocuments(docs);
            map.set(app.id, summary);
          } catch (e) {
            this.logger.error('Failed to load documents for list row', e);
            map.set(app.id, { kind: 'unknown', verifier: null });
          }
          this.verificationByApp.set(new Map(map));
        })
      );
    }
    this.enriching.set(false);
  }

  private aggregateDocuments(docs: ApplicationDocumentsDto): DocVerificationSummary {
    const uploaded = docs.documents.filter((d) => d.isUploaded);
    if (uploaded.length === 0) {
      return { kind: 'none', verifier: null };
    }
    const rejected = uploaded.filter((d) => d.verificationStatus && !d.verificationStatus.isVerified);
    if (rejected.length > 0) {
      const verifier = this.lastVerifier(rejected);
      return { kind: 'rejected', verifier };
    }
    const pending = uploaded.filter((d) => !d.verificationStatus);
    if (pending.length > 0) {
      return { kind: 'pending', verifier: null };
    }
    const allOk = uploaded.every((d) => d.verificationStatus?.isVerified === true);
    if (allOk) {
      const verifier = this.lastVerifier(uploaded);
      return { kind: 'approved', verifier };
    }
    return { kind: 'partial', verifier: this.lastVerifier(uploaded) };
  }

  private lastVerifier(docs: DocumentDto[]): string | null {
    let last: string | null = null;
    for (const d of docs) {
      const v = d.verificationStatus?.verifiedBy;
      if (v) {
        last = v;
      }
    }
    return last;
  }

  async applyFilters(): Promise<void> {
    this.currentPage.set(1);
    await this.loadApplications();
  }

  async resetFilters(): Promise<void> {
    this.filterForm.reset({
      search: '',
      status: 'all',
      dateFrom: '',
      dateTo: '',
    });
    this.currentPage.set(1);
    this.sortBy.set('createdOnUtc');
    this.sortDescending.set(true);
    await this.loadApplications();
  }

  async refresh(): Promise<void> {
    await Promise.all([this.loadDashboardStats(), this.loadApplications()]);
  }

  setSort(column: 'fullName' | 'createdOnUtc' | 'status'): void {
    if (this.sortBy() === column) {
      this.sortDescending.update((d) => !d);
    } else {
      this.sortBy.set(column);
      this.sortDescending.set(column === 'createdOnUtc');
    }
    void this.loadApplications();
  }

  sortIcon(column: 'fullName' | 'createdOnUtc' | 'status'): string {
    if (this.sortBy() !== column) {
      return 'solar:sort-vertical-bold';
    }
    return this.sortDescending() ? 'solar:sort-from-bottom-to-top-bold' : 'solar:sort-from-top-to-bottom-bold';
  }

  async goPage(delta: number): Promise<void> {
    const next = this.currentPage() + delta;
    if (next < 1 || next > this.totalPages()) {
      return;
    }
    this.currentPage.set(next);
    this.selectedIds.set([]);
    await this.loadApplications();
  }

  async setPageSize(n: number): Promise<void> {
    this.pageSize.set(n);
    this.currentPage.set(1);
    this.selectedIds.set([]);
    await this.loadApplications();
  }

  toggleRow(id: string): void {
    this.selectedIds.update((arr) => {
      const s = new Set(arr);
      if (s.has(id)) {
        s.delete(id);
      } else {
        s.add(id);
      }
      return [...s];
    });
  }

  toggleSelectAll(checked: boolean): void {
    const ids = this.applications().map((a) => a.id);
    if (checked) {
      this.selectedIds.update((arr) => [...new Set([...arr, ...ids])]);
    } else {
      const drop = new Set(ids);
      this.selectedIds.update((arr) => arr.filter((id) => !drop.has(id)));
    }
  }

  rowSelected(id: string): boolean {
    return this.selectedIds().includes(id);
  }

  async exportExcel(): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await firstValueFrom(this.api.exportSubmittedApplicationsExcel());
      const url = window.URL.createObjectURL(blob);
      const a = window.document.createElement('a');
      a.href = url;
      a.download = `submitted-applications-${new Date().toISOString().slice(0, 10)}.xlsx`;
      window.document.body.appendChild(a);
      a.click();
      window.document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
      this.toast.success('Excel export downloaded.');
    } catch (e) {
      this.logger.error('Excel export failed', e);
      this.toast.error('Export failed.');
    } finally {
      this.exporting.set(false);
    }
  }

  async exportPdfSample(): Promise<void> {
    const ids = this.selectedIds();
    if (ids.length !== 1) {
      this.toast.info('Select exactly one application to export its PDF, or open View Application.');
      return;
    }
    this.exporting.set(true);
    try {
      const blob = await firstValueFrom(this.api.downloadOnlineApplicationPdf(ids[0]));
      const url = window.URL.createObjectURL(blob);
      const a = window.document.createElement('a');
      a.href = url;
      a.download = `application-${ids[0].slice(0, 8)}.pdf`;
      window.document.body.appendChild(a);
      a.click();
      window.document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
      this.toast.success('PDF downloaded.');
    } catch (e) {
      this.logger.error('PDF export failed', e);
      this.toast.error('PDF export failed.');
    } finally {
      this.exporting.set(false);
    }
  }

  canApprove(app: OnlineApplicationDto): boolean {
    return app.status !== 'Approved' && app.status !== 'Enrolled';
  }

  canReject(app: OnlineApplicationDto): boolean {
    return app.status !== 'Rejected' && app.status !== 'Enrolled';
  }

  async approveApplication(app: OnlineApplicationDto): Promise<void> {
    if (!this.canApprove(app)) {
      return;
    }
    if (!window.confirm(`Approve application ${app.uniqueId} (${app.fullName})?`)) {
      return;
    }
    try {
      await firstValueFrom(
        this.api.approveOnlineApplication(app.id, { notifyApplicant: true, remarks: null })
      );
      this.toast.success('Application approved.');
      await this.refresh();
    } catch (e: unknown) {
      this.logger.error('Approve failed', e);
      this.toast.error('Failed to approve application.');
    }
  }

  async rejectApplication(app: OnlineApplicationDto): Promise<void> {
    if (!this.canReject(app)) {
      return;
    }
    if (!window.confirm(`Reject application ${app.uniqueId} (${app.fullName})?`)) {
      return;
    }
    try {
      await firstValueFrom(
        this.api.rejectOnlineApplication(app.id, { notifyApplicant: true, remarks: null })
      );
      this.toast.success('Application rejected.');
      await this.refresh();
    } catch (e: unknown) {
      this.logger.error('Reject failed', e);
      this.toast.error('Failed to reject application.');
    }
  }

  async bulkApprove(): Promise<void> {
    const ids = this.selectedIds();
    if (!ids.length) {
      this.toast.error('Select at least one application.');
      return;
    }
    if (!window.confirm(`Approve ${ids.length} selected application(s)?`)) {
      return;
    }
    this.bulkWorking.set(true);
    try {
      let ok = 0;
      for (const id of ids) {
        const app = this.applications().find((a) => a.id === id);
        if (!app || !this.canApprove(app)) {
          continue;
        }
        try {
          await firstValueFrom(
            this.api.approveOnlineApplication(id, { notifyApplicant: true, remarks: null })
          );
          ok++;
        } catch (e) {
          this.logger.error('Bulk approve item failed', e);
        }
      }
      this.toast.success(`Approved ${ok} application(s).`);
      this.selectedIds.set([]);
      await this.refresh();
    } finally {
      this.bulkWorking.set(false);
    }
  }

  async bulkReject(): Promise<void> {
    const ids = this.selectedIds();
    if (!ids.length) {
      this.toast.error('Select at least one application.');
      return;
    }
    if (!window.confirm(`Reject ${ids.length} selected application(s)?`)) {
      return;
    }
    this.bulkWorking.set(true);
    try {
      let ok = 0;
      for (const id of ids) {
        const app = this.applications().find((a) => a.id === id);
        if (!app || !this.canReject(app)) {
          continue;
        }
        try {
          await firstValueFrom(
            this.api.rejectOnlineApplication(id, { notifyApplicant: true, remarks: null })
          );
          ok++;
        } catch (e) {
          this.logger.error('Bulk reject item failed', e);
        }
      }
      this.toast.success(`Rejected ${ok} application(s).`);
      this.selectedIds.set([]);
      await this.refresh();
    } finally {
      this.bulkWorking.set(false);
    }
  }

  async viewDocuments(application: OnlineApplicationDto): Promise<void> {
    this.selectedApplication.set(application as OnlineApplicationDetailDto);
    this.viewMode.set('detail');
    this.loadingDocuments.set(true);
    this.documents.set([]);

    try {
      const docs = await firstValueFrom(this.api.getApplicationDocuments(application.id));
      this.documents.set(docs.documents);
    } catch (error: unknown) {
      this.logger.error('Failed to load documents', error);
      this.toast.error('Failed to load documents.');
    } finally {
      this.loadingDocuments.set(false);
    }
  }

  async downloadDocument(document: DocumentDto): Promise<void> {
    const app = this.selectedApplication();
    if (!app || !document.isUploaded) {
      return;
    }

    try {
      const blob = await firstValueFrom(
        this.api.downloadDocument(app.id, document.documentType)
      );

      const url = window.URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.download = document.fileName || `${document.documentType}.pdf`;
      window.document.body.appendChild(link);
      link.click();
      window.document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      this.toast.success('Document downloaded successfully!');
    } catch (error: unknown) {
      this.logger.error('Failed to download document', error);
      this.toast.error('Failed to download document.');
    }
  }

  async openPreview(document: DocumentDto): Promise<void> {
    const app = this.selectedApplication();
    if (!app || !document.isUploaded) {
      return;
    }
    this.closePreview();
    try {
      const blob = await firstValueFrom(
        this.api.downloadDocument(app.id, document.documentType)
      );
      const url = window.URL.createObjectURL(blob);
      this.previewObjectUrl.set(url);
      this.previewDoc.set(document);
      this.previewModalOpen.set(true);
    } catch (e) {
      this.logger.error('Preview failed', e);
      this.toast.error('Could not open preview.');
    }
  }

  closePreview(): void {
    const u = this.previewObjectUrl();
    if (u) {
      window.URL.revokeObjectURL(u);
    }
    this.previewObjectUrl.set(null);
    this.previewDoc.set(null);
    this.previewModalOpen.set(false);
  }

  isImagePreview(doc: DocumentDto | null): boolean {
    if (!doc?.contentType) {
      return false;
    }
    return doc.contentType.startsWith('image/');
  }

  safeBlobUrl(url: string | null): SafeResourceUrl | null {
    if (!url) {
      return null;
    }
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  goBack(): void {
    this.closePreview();
    this.viewMode.set('list');
    this.selectedApplication.set(null);
    this.documents.set([]);
  }

  getDocumentIcon(documentType: string): string {
    const iconMap: { [key: string]: string } = {
      StdXMarksheet: 'solar:document-text-bold',
      StdXIIMarksheet: 'solar:document-text-bold',
      CuetMarksheet: 'solar:document-text-bold',
      DifferentlyAbledProof: 'solar:file-check-bold',
      EconomicallyWeakerProof: 'solar:file-check-bold',
    };
    return iconMap[documentType] || 'solar:document-bold';
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) {
      return '0 B';
    }
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  openVerificationModal(documentType: string): void {
    this.verifyingDocument.set(documentType);
    const doc = this.documents().find((d) => d.documentType === documentType);
    if (doc?.verificationStatus) {
      this.verificationForm.patchValue({
        isVerified: doc.verificationStatus.isVerified,
        remarks: doc.verificationStatus.remarks || '',
      });
    } else {
      this.verificationForm.reset({
        isVerified: false,
        remarks: '',
      });
    }
  }

  closeVerificationModal(): void {
    this.verifyingDocument.set(null);
    this.verificationForm.reset();
  }

  async verifyDocument(): Promise<void> {
    const app = this.selectedApplication();
    const docType = this.verifyingDocument();
    if (!app || !docType) {
      return;
    }

    const formValue = this.verificationForm.getRawValue();
    try {
      await firstValueFrom(
        this.api.verifyDocument(
          app.id,
          docType,
          formValue.isVerified!,
          formValue.remarks || undefined,
          this.getCurrentUser()
        )
      );

      this.toast.success('Document verification status updated successfully!');
      this.closeVerificationModal();
      await this.viewDocuments(app);
    } catch (error: unknown) {
      this.logger.error('Failed to verify document', error);
      this.toast.error('Failed to update verification status.');
    }
  }

  getVerificationStatusBadgeClass(doc: DocumentDto): string {
    if (!doc.verificationStatus) {
      return 'badge-secondary';
    }
    return doc.verificationStatus.isVerified ? 'badge-success' : 'badge-danger';
  }

  getVerificationStatusText(doc: DocumentDto): string {
    if (!doc.verificationStatus) {
      return 'Not Verified';
    }
    return doc.verificationStatus.isVerified ? 'Verified' : 'Rejected';
  }

  verificationSummaryText(summary: DocVerificationSummary | undefined): string {
    if (!summary || summary.kind === 'loading') {
      return '…';
    }
    switch (summary.kind) {
      case 'approved':
        return 'Approved';
      case 'rejected':
        return 'Rejected';
      case 'pending':
      case 'partial':
        return 'Pending';
      case 'none':
        return 'No uploads';
      case 'unknown':
        return '—';
      default:
        return '—';
    }
  }

  verificationBadgeClass(summary: DocVerificationSummary | undefined): string {
    if (!summary || summary.kind === 'loading') {
      return 'badge-pill badge-pill--muted';
    }
    switch (summary.kind) {
      case 'approved':
        return 'badge-pill badge-pill--success';
      case 'rejected':
        return 'badge-pill badge-pill--danger';
      case 'pending':
      case 'partial':
        return 'badge-pill badge-pill--warning';
      case 'none':
        return 'badge-pill badge-pill--muted';
      default:
        return 'badge-pill badge-pill--muted';
    }
  }

  private getCurrentUser(): string {
    const profile = this.authService.profile;
    return profile?.fullName || profile?.email || profile?.uniqueId || 'System';
  }
}
