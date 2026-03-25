import { Component, OnInit } from '@angular/core';
import {
  OfflineAdmissionApiService,
  AdminDashboardDto,
  readHttpErrorMessage,
} from '@client/shared/data';

const TOKEN_KEY = 'erpAdminJwt';

@Component({
  selector: 'app-offline-admission',
  templateUrl: './offline-admission.component.html',
  styleUrls: ['./offline-admission.component.scss'],
  standalone: false,
})
export class OfflineAdmissionComponent implements OnInit {
  adminToken = '';
  dashboard: AdminDashboardDto | null = null;
  error: string | null = null;
  busy = false;

  issue = {
    formNumber: '',
    studentName: '',
    mobileNumber: '',
    applicationFeeAmount: 700,
  };

  receiveFormNumber = '';
  receiveMajorSubject = '';

  publishRound: 'First' | 'Second' | 'Third' = 'First';
  publishSendSms = true;

  assignApplicationId = '';
  assignRound: 'First' | 'Second' | 'Third' = 'First';

  constructor(private readonly api: OfflineAdmissionApiService) {}

  ngOnInit(): void {
    const saved = sessionStorage.getItem(TOKEN_KEY);
    if (saved) {
      this.adminToken = saved;
      this.refreshDashboard();
    }
  }

  saveToken(): void {
    sessionStorage.setItem(TOKEN_KEY, this.adminToken.trim());
    this.refreshDashboard();
  }

  refreshDashboard(): void {
    const t = this.adminToken.trim();
    if (!t) {
      this.dashboard = null;
      return;
    }
    this.error = null;
    this.busy = true;
    this.api.getAdminDashboard(t).subscribe({
      next: (d) => {
        this.dashboard = d;
        this.busy = false;
      },
      error: (e) => {
        void this.showRequestError(e, 'Failed to load dashboard');
      },
    });
  }

  issueForm(): void {
    const t = this.adminToken.trim();
    if (!t) {
      this.error = 'Enter admin JWT first.';
      return;
    }
    this.error = null;
    this.busy = true;
    this.api
      .issueOfflineForm(t, {
        formNumber: this.issue.formNumber.trim(),
        studentName: this.issue.studentName.trim(),
        mobileNumber: this.issue.mobileNumber.trim(),
        applicationFeeAmount: this.issue.applicationFeeAmount,
      })
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `offline-receipt-${this.issue.formNumber.trim()}.pdf`;
          a.click();
          URL.revokeObjectURL(url);
          this.busy = false;
          this.refreshDashboard();
        },
        error: (e) => {
          void this.showRequestError(e, 'Could not issue form');
        },
      });
  }

  receiveForm(): void {
    const t = this.adminToken.trim();
    if (!t) {
      this.error = 'Enter admin JWT first.';
      return;
    }
    this.error = null;
    this.busy = true;
    const major = this.receiveMajorSubject.trim();
    if (!major) {
      this.error = 'Enter final major / course.';
      return;
    }
    this.api
      .receiveOfflineForm(t, { formNumber: this.receiveFormNumber.trim(), majorSubject: major })
      .subscribe({
      next: () => {
        this.busy = false;
        this.receiveFormNumber = '';
        this.receiveMajorSubject = '';
        this.refreshDashboard();
      },
      error: (e) => {
        void this.showRequestError(e, 'Could not mark form as received');
      },
    });
  }

  publishList(): void {
    const t = this.adminToken.trim();
    if (!t) {
      this.error = 'Enter admin JWT first.';
      return;
    }
    this.error = null;
    this.busy = true;
    this.api.publishSelectionList(t, this.publishRound, this.publishSendSms).subscribe({
      next: () => {
        this.busy = false;
      },
      error: (e) => {
        void this.showRequestError(e, 'Could not publish selection list');
      },
    });
  }

  assignRoundAction(): void {
    const t = this.adminToken.trim();
    if (!t || !this.assignApplicationId.trim()) {
      this.error = 'Enter admin JWT and application GUID.';
      return;
    }
    this.error = null;
    this.busy = true;
    this.api.assignSelectionListRound(t, this.assignApplicationId.trim(), this.assignRound).subscribe({
      next: () => {
        this.busy = false;
      },
      error: (e) => {
        void this.showRequestError(e, 'Could not assign selection list round');
      },
    });
  }

  receiptLink(): string {
    const n = this.issue.formNumber.trim();
    if (!n) {
      return '';
    }
    return this.api.receiptPdfUrl(n);
  }

  /** Shows API or network message in the page and in a blocking alert so it is never missed. */
  private async showRequestError(e: unknown, fallback: string): Promise<void> {
    const msg = (await readHttpErrorMessage(e)) || fallback;
    this.error = msg;
    window.alert(msg);
    this.busy = false;
  }
}
