import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, of, throwError, from } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { API_BASE_URL } from '@client/shared/util';
import {
  AdminApplicantDto,
  CreateApplicantPayload,
  UpdateApplicantStatusPayload,
  OnlineApplicationDto,
  OnlineApplicationsListResponse,
  OnlineApplicationDetailDto,
  UpdateOnlineApplicationStatusPayload,
  AdminDashboardDto,
  EntranceExamDto,
  EntranceExamsListResponse,
  CreateEntranceExamPayload,
  UpdateEntranceExamPayload,
  ExamRegistrationDto,
  ExamRegistrationsListResponse,
  PaymentDto,
  PaymentsListResponse,
  ApplicationDocumentsDto,
  DocumentDto,
  PaymentReportDto,
  AdmissionsAnalyticsDto,
  AdmissionErpSyncResult,
  AdmissionWorkflowSettingsDto,
  GrantDirectAdmissionResultDto,
  AdmittedStudentsListResponse,
} from '../dtos/admin-applicant.dto';

/** Parse error body when `responseType: 'blob'` — 4xx/5xx bodies arrive as Blob, not JSON. */
function parseApiErrorBodyText(text: string): string {
  const t = text.trim();
  if (!t) {
    return 'Request failed.';
  }
  try {
    const j = JSON.parse(t) as { title?: string; detail?: string; message?: string };
    if (typeof j === 'string') {
      return j;
    }
    if (j.detail) {
      return String(j.detail);
    }
    if (j.title && j.title !== 'Bad Request' && j.title !== 'One or more validation errors occurred.') {
      return String(j.title);
    }
    if (j.message) {
      return String(j.message);
    }
  } catch {
    /* plain text or quoted JSON string */
  }
  if (t.startsWith('"') && t.endsWith('"')) {
    try {
      return JSON.parse(t) as string;
    } catch {
      return t.slice(1, -1);
    }
  }
  return t;
}

function catchBlobHttpError(err: HttpErrorResponse): Observable<never> {
  if (err.error instanceof Blob) {
    return from(err.error.text()).pipe(
      switchMap((text) => {
        const msg = parseApiErrorBodyText(text);
        return throwError(
          () =>
            new HttpErrorResponse({
              error: { message: msg },
              headers: err.headers,
              status: err.status,
              statusText: err.statusText,
              url: err.url ?? undefined,
            })
        );
      })
    );
  }
  return throwError(() => err);
}

@Injectable({ providedIn: 'root' })
export class AdmissionsAdminApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  listApplicants(): Observable<AdminApplicantDto[]> {
    return this.http.get<AdminApplicantDto[]>(
      `${this.apiBaseUrl}/admissions/applicants`
    );
  }

  getApplicant(applicantId: string): Observable<AdminApplicantDto> {
    return this.http.get<AdminApplicantDto>(
      `${this.apiBaseUrl}/admissions/applicants/${applicantId}`
    );
  }

  createApplicant(payload: CreateApplicantPayload): Observable<string> {
    return this.http.post<string>(
      `${this.apiBaseUrl}/admissions/applicants`,
      payload
    );
  }

  updateApplicantStatus(
    applicantId: string,
    payload: UpdateApplicantStatusPayload
  ): Observable<AdminApplicantDto> {
    return this.http.post<AdminApplicantDto>(
      `${this.apiBaseUrl}/admissions/applicants/${applicantId}/status`,
      payload
    );
  }

  listOnlineApplications(params?: {
    page?: number;
    pageSize?: number;
    status?: string;
    searchTerm?: string;
    isApplicationSubmitted?: boolean;
    isPaymentCompleted?: boolean;
    shift?: string | null;
    createdFromUtc?: string | null;
    createdToUtc?: string | null;
    sortBy?: string | null;
    sortDescending?: boolean | null;
    minClassXiiPercentage?: number | null;
    maxClassXiiPercentage?: number | null;
    admissionPath?: string | null;
    admissionChannel?: string | null;
    /** Server-side funnel filter: registered | formInProgress | paymentPending | paid | submitted */
    applicationLifecycleStage?: string | null;
  }): Observable<OnlineApplicationsListResponse> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.page !== undefined) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.status) {
        httpParams = httpParams.set('status', params.status);
      }
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
      if (params.isApplicationSubmitted !== undefined) {
        httpParams = httpParams.set(
          'isApplicationSubmitted',
          params.isApplicationSubmitted.toString()
        );
      }
      if (params.isPaymentCompleted !== undefined) {
        httpParams = httpParams.set(
          'isPaymentCompleted',
          params.isPaymentCompleted.toString()
        );
      }
      if (params.shift) {
        httpParams = httpParams.set('shift', params.shift);
      }
      if (params.createdFromUtc) {
        httpParams = httpParams.set('createdFromUtc', params.createdFromUtc);
      }
      if (params.createdToUtc) {
        httpParams = httpParams.set('createdToUtc', params.createdToUtc);
      }
      if (params.sortBy) {
        httpParams = httpParams.set('sortBy', params.sortBy);
      }
      if (params.sortDescending !== undefined && params.sortDescending !== null) {
        httpParams = httpParams.set('sortDescending', params.sortDescending.toString());
      }
      if (params.minClassXiiPercentage !== undefined && params.minClassXiiPercentage !== null) {
        httpParams = httpParams.set('minClassXiiPercentage', params.minClassXiiPercentage.toString());
      }
      if (params.maxClassXiiPercentage !== undefined && params.maxClassXiiPercentage !== null) {
        httpParams = httpParams.set('maxClassXiiPercentage', params.maxClassXiiPercentage.toString());
      }
      if (params.admissionPath) {
        httpParams = httpParams.set('admissionPath', params.admissionPath);
      }
      if (params.admissionChannel) {
        httpParams = httpParams.set('admissionChannel', params.admissionChannel);
      }
      if (params.applicationLifecycleStage) {
        httpParams = httpParams.set(
          'applicationLifecycleStage',
          params.applicationLifecycleStage
        );
      }
    }
    return this.http.get<OnlineApplicationsListResponse>(
      `${this.apiBaseUrl}/admissions/applications`,
      { params: httpParams }
    );
  }

  getAdmissionWorkflowSettings(): Observable<AdmissionWorkflowSettingsDto> {
    return this.http.get<AdmissionWorkflowSettingsDto>(
      `${this.apiBaseUrl}/admissions/admission-workflow-settings`
    );
  }

  updateAdmissionWorkflowSettings(body: {
    directAdmissionCutoffPercentage: number;
  }): Observable<AdmissionWorkflowSettingsDto> {
    return this.http.put<AdmissionWorkflowSettingsDto>(
      `${this.apiBaseUrl}/admissions/admission-workflow-settings`,
      body
    );
  }

  grantDirectAdmission(
    accountId: string,
    body?: { notifyApplicant?: boolean }
  ): Observable<GrantDirectAdmissionResultDto> {
    return this.http.post<GrantDirectAdmissionResultDto>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/direct-admission`,
      body ?? {}
    );
  }

  confirmAdmissionFeePayment(
    accountId: string,
    body?: { notifyApplicant?: boolean }
  ): Observable<{ accountId: string; status: string }> {
    return this.http.post<{ accountId: string; status: string }>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/confirm-admission-fee`,
      body ?? {}
    );
  }

  listAdmittedStudents(params?: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
  }): Observable<AdmittedStudentsListResponse> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.page !== undefined) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
    }
    return this.http.get<AdmittedStudentsListResponse>(
      `${this.apiBaseUrl}/admissions/admitted-students`,
      { params: httpParams }
    );
  }

  getOnlineApplication(accountId: string): Observable<OnlineApplicationDetailDto> {
    return this.http.get<OnlineApplicationDetailDto>(
      `${this.apiBaseUrl}/admissions/online-applications/${accountId}`
    );
  }

  updateOnlineApplicationStatus(
    accountId: string,
    payload: UpdateOnlineApplicationStatusPayload
  ): Observable<OnlineApplicationDetailDto> {
    return this.http.post<OnlineApplicationDetailDto>(
      `${this.apiBaseUrl}/admissions/online-applications/${accountId}/status`,
      payload
    );
  }

  enrollApplication(
    accountId: string,
    payload: {
      remarks?: string | null;
      notifyApplicant?: boolean;
    }
  ): Observable<{
    accountId: string;
    applicationNumber: string;
    fullName: string;
    enrolledOnUtc: string;
  }> {
    return this.http.post<{
      accountId: string;
      applicationNumber: string;
      fullName: string;
      enrolledOnUtc: string;
    }>(
      `${this.apiBaseUrl}/admissions/online-applications/${accountId}/enroll`,
      payload
    );
  }

  getAdminDashboard(): Observable<AdminDashboardDto> {
    return this.http.get<AdminDashboardDto>(
      `${this.apiBaseUrl}/admissions/admin/dashboard`
    );
  }

  // Entrance Exam Management

  listEntranceExams(params?: {
    page?: number;
    pageSize?: number;
    isActive?: boolean;
    examDateFrom?: string;
    examDateTo?: string;
    searchTerm?: string;
  }): Observable<EntranceExamsListResponse> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.page !== undefined) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.isActive !== undefined) {
        httpParams = httpParams.set('isActive', params.isActive.toString());
      }
      if (params.examDateFrom) {
        httpParams = httpParams.set('examDateFrom', params.examDateFrom);
      }
      if (params.examDateTo) {
        httpParams = httpParams.set('examDateTo', params.examDateTo);
      }
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
    }
    return this.http.get<EntranceExamsListResponse>(
      `${this.apiBaseUrl}/admissions/entrance-exams`,
      { params: httpParams }
    );
  }

  getEntranceExam(examId: string): Observable<EntranceExamDto> {
    return this.http.get<EntranceExamDto>(
      `${this.apiBaseUrl}/admissions/entrance-exams/${examId}`
    );
  }

  createEntranceExam(payload: CreateEntranceExamPayload): Observable<string> {
    return this.http.post<string>(
      `${this.apiBaseUrl}/admissions/entrance-exams`,
      payload
    );
  }

  updateEntranceExam(
    examId: string,
    payload: UpdateEntranceExamPayload
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiBaseUrl}/admissions/entrance-exams/${examId}`,
      payload
    );
  }

  registerApplicantForExam(
    examId: string,
    applicantId: string
  ): Observable<string> {
    return this.http.post<string>(
      `${this.apiBaseUrl}/admissions/entrance-exams/${examId}/register/${applicantId}`,
      {}
    );
  }

  listExamRegistrations(
    examId: string,
    params?: {
      page?: number;
      pageSize?: number;
      isPresent?: boolean;
      searchTerm?: string;
    }
  ): Observable<ExamRegistrationsListResponse> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.page !== undefined) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.isPresent !== undefined) {
        httpParams = httpParams.set('isPresent', params.isPresent.toString());
      }
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
    }
    return this.http.get<ExamRegistrationsListResponse>(
      `${this.apiBaseUrl}/admissions/entrance-exams/${examId}/registrations`,
      { params: httpParams }
    );
  }

  exportPaidApplicationsWithFullDetails(): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/admissions/applications/paid/export`,
      { responseType: 'blob' }
    );
  }

  /** Full-form Excel export for all submitted applications (paid or unpaid). */
  exportSubmittedApplicationsExcel(): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/admissions/applications/export/excel`,
      { responseType: 'blob' }
    );
  }

  downloadOnlineApplicationPdf(accountId: string): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/pdf`,
      { responseType: 'blob' }
    );
  }

  approveOnlineApplication(
    accountId: string,
    payload?: { remarks?: string | null; notifyApplicant?: boolean }
  ): Observable<OnlineApplicationDetailDto> {
    return this.http.post<OnlineApplicationDetailDto>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/approve`,
      payload ?? {}
    );
  }

  rejectOnlineApplication(
    accountId: string,
    payload?: { remarks?: string | null; notifyApplicant?: boolean }
  ): Observable<OnlineApplicationDetailDto> {
    return this.http.post<OnlineApplicationDetailDto>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/reject`,
      payload ?? {}
    );
  }

  /** Provision or link ERP student (idempotent). */
  syncApplicationToErp(accountId: string): Observable<AdmissionErpSyncResult> {
    return this.http.post<AdmissionErpSyncResult>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/erp-sync`,
      {}
    );
  }

  // Payment Management

  listPayments(params?: {
    page?: number;
    pageSize?: number;
    isPaymentCompleted?: boolean;
    searchTerm?: string;
    paymentDateFrom?: string;
    paymentDateTo?: string;
    minAmount?: number;
    maxAmount?: number;
  }): Observable<PaymentsListResponse> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.page !== undefined) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize !== undefined) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
      if (params.isPaymentCompleted !== undefined) {
        httpParams = httpParams.set('isPaymentCompleted', params.isPaymentCompleted.toString());
      }
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
      if (params.paymentDateFrom) {
        httpParams = httpParams.set('paymentDateFrom', params.paymentDateFrom);
      }
      if (params.paymentDateTo) {
        httpParams = httpParams.set('paymentDateTo', params.paymentDateTo);
      }
      if (params.minAmount !== undefined) {
        httpParams = httpParams.set('minAmount', params.minAmount.toString());
      }
      if (params.maxAmount !== undefined) {
        httpParams = httpParams.set('maxAmount', params.maxAmount.toString());
      }
    }
    return this.http.get<PaymentsListResponse>(
      `${this.apiBaseUrl}/admissions/payments`,
      { params: httpParams }
    );
  }

  verifyPaymentManually(accountId: string, payload: {
    transactionId: string;
    amount: number;
    remarks?: string | null;
  }): Observable<{ accountId: string }> {
    return this.http.post<{ accountId: string }>(
      `${this.apiBaseUrl}/admissions/payments/${accountId}/verify`,
      payload
    );
  }

  // Document Management

  getApplicationDocuments(accountId: string): Observable<ApplicationDocumentsDto> {
    return this.http.get<ApplicationDocumentsDto>(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/documents`
    );
  }

  downloadDocument(accountId: string, documentType: string): Observable<Blob> {
    return this.http.get(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/documents/${documentType}/download`,
      { responseType: 'blob' }
    );
  }

  verifyDocument(
    accountId: string,
    documentType: string,
    isVerified: boolean,
    remarks?: string,
    verifiedBy?: string
  ): Observable<any> {
    return this.http.post(
      `${this.apiBaseUrl}/admissions/applications/${accountId}/documents/${documentType}/verify`,
      { isVerified, remarks, verifiedBy }
    );
  }

  // Payment Reports

  getPaymentReport(params?: {
    fromDate?: string;
    toDate?: string;
    isPaymentCompleted?: boolean;
  }): Observable<PaymentReportDto> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.fromDate) {
        httpParams = httpParams.set('fromDate', params.fromDate);
      }
      if (params.toDate) {
        httpParams = httpParams.set('toDate', params.toDate);
      }
      if (params.isPaymentCompleted !== undefined) {
        httpParams = httpParams.set('isPaymentCompleted', params.isPaymentCompleted.toString());
      }
    }
    return this.http.get<PaymentReportDto>(
      `${this.apiBaseUrl}/admissions/payments/report`,
      { params: httpParams }
    );
  }

  // Bulk Communication

  sendBulkCommunication(payload: {
    subject: string;
    message: string;
    channel: string;
    filter?: {
      statuses?: string[];
      isApplicationSubmitted?: boolean | null;
      isPaymentCompleted?: boolean | null;
      specificAccountIds?: string[];
      searchTerm?: string | null;
    } | null;
  }): Observable<{
    totalRecipients: number;
    emailsSent: number;
    smsSent: number;
    emailsFailed: number;
    smsFailed: number;
    errors: Array<{
      accountId: string;
      applicationNumber: string;
      errorMessage: string;
      channel: string;
    }>;
  }> {
    return this.http.post<{
      totalRecipients: number;
      emailsSent: number;
      smsSent: number;
      emailsFailed: number;
      smsFailed: number;
      errors: Array<{
        accountId: string;
        applicationNumber: string;
        errorMessage: string;
        channel: string;
      }>;
    }>(`${this.apiBaseUrl}/admissions/communications/bulk`, payload);
  }

  // Analytics

  getAdmissionsAnalytics(params?: {
    fromDate?: string;
    toDate?: string;
  }): Observable<AdmissionsAnalyticsDto> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.fromDate) {
        httpParams = httpParams.set('fromDate', params.fromDate);
      }
      if (params.toDate) {
        httpParams = httpParams.set('toDate', params.toDate);
      }
    }
    return this.http.get<AdmissionsAnalyticsDto>(
      `${this.apiBaseUrl}/admissions/analytics`,
      { params: httpParams }
    );
  }

  generateMeritList(shift?: string, majorSubject?: string): Observable<GenerateMeritListResponse> {
    return this.http.post<GenerateMeritListResponse>(
      `${this.apiBaseUrl}/admissions/merit-list/generate`,
      { shift, majorSubject }
    );
  }

  getMeritList(
    shift?: string,
    majorSubject?: string,
    page = 1,
    pageSize = 50
  ): Observable<MeritListResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    
    if (shift) {
      params = params.set('shift', shift);
    }
    if (majorSubject) {
      params = params.set('majorSubject', majorSubject);
    }

    return this.http.get<MeritListResponse>(
      `${this.apiBaseUrl}/admissions/merit-list`,
      { params }
    );
  }

  createAdmissionOffer(
    accountId: string,
    expiryDate: string,
    remarks?: string
  ): Observable<CreateAdmissionOfferResponse> {
    return this.http.post<CreateAdmissionOfferResponse>(
      `${this.apiBaseUrl}/admissions/offers`,
      { accountId, expiryDate, remarks }
    );
  }

  createDirectAdmissionOffers(payload: {
    minimumPercentage?: number;
    admissionFeeAmount?: number;
    expiryDate?: string | null;
  }): Observable<CreateDirectAdmissionOffersResponse> {
    return this.http.post<CreateDirectAdmissionOffersResponse>(
      `${this.apiBaseUrl}/admissions/offers/direct-admission`,
      payload
    );
  }

  sendIndividualAdmissionOffer(payload: {
    applicationNumber: string;
    admissionFeeAmount: number;
    expiryDays: number;
    remarks?: string | null;
  }): Observable<SendIndividualAdmissionOfferResponse> {
    return this.http.post<SendIndividualAdmissionOfferResponse>(
      `${this.apiBaseUrl}/admissions/offers/send-individual`,
      payload
    );
  }

  /** Issue offline form slip (no applicant account yet): returns PDF receipt (blob). */
  issueOfflineAdmissionForm(payload: {
    formNumber: string;
    studentName: string;
    mobileNumber: string;
    shift: string;
    cuetApplied: boolean;
    applicationFeeAmount: number;
  }): Observable<Blob> {
    return this.http
      .post(
        `${this.apiBaseUrl}/admissions/admin/offline-forms/issue`,
        {
          formNumber: payload.formNumber,
          studentName: payload.studentName,
          mobileNumber: payload.mobileNumber,
          shift: payload.shift,
          cuetApplied: payload.cuetApplied,
          applicationFeeAmount: payload.applicationFeeAmount,
        },
        { responseType: 'blob' }
      )
      .pipe(catchError((e: HttpErrorResponse) => catchBlobHttpError(e)));
  }

  /** Look up issued slip or existing offline applicant (before receive). */
  getOfflineFormIssuancePreview(formNumber: string): Observable<OfflineFormIssuancePreviewDto | null> {
    return this.http
      .get<OfflineFormIssuancePreviewDto>(
        `${this.apiBaseUrl}/admissions/admin/offline-forms/${encodeURIComponent(formNumber)}/preview`
      )
      .pipe(
        catchError((e: HttpErrorResponse) => (e.status === 404 ? of(null) : throwError(() => e)))
      );
  }

  /** Receive physical form: creates applicant account with final major subject. */
  receiveOfflineAdmissionForm(body: {
    formNumber: string;
    majorSubject: string;
  }): Observable<ReceiveOfflineAdmissionFormResponse> {
    return this.http.post<ReceiveOfflineAdmissionFormResponse>(
      `${this.apiBaseUrl}/admissions/admin/offline-forms/receive`,
      body
    );
  }

  getOfflineFormReceiptPdf(formNumber: string): Observable<Blob> {
    return this.http
      .get(
        `${this.apiBaseUrl}/admissions/admin/offline-forms/${encodeURIComponent(formNumber)}/receipt`,
        { responseType: 'blob' }
      )
      .pipe(catchError((e: HttpErrorResponse) => catchBlobHttpError(e)));
  }

  assignSelectionListRound(
    applicationId: string,
    round: 'First' | 'Second' | 'Third'
  ): Observable<AssignSelectionListRoundResponse> {
    return this.http.post<AssignSelectionListRoundResponse>(
      `${this.apiBaseUrl}/admissions/applications/${applicationId}/selection-list-round`,
      { round }
    );
  }

  publishSelectionList(body: {
    round: 'First' | 'Second' | 'Third';
    sendSmsNotifications: boolean;
  }): Observable<PublishSelectionListResponse> {
    return this.http.post<PublishSelectionListResponse>(
      `${this.apiBaseUrl}/admissions/admin/selection-list/publish`,
      body
    );
  }
}

export interface GenerateMeritListResponse {
  totalApplicantsProcessed: number;
  meritScoresCreated: number;
  generatedOnUtc: string;
}

export interface MeritListResponse {
  meritScores: MeritScoreDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface MeritScoreDto {
  id: string;
  accountId: string;
  applicationNumber: string;
  fullName: string;
  classXIIPercentage: number;
  cuetScore?: number | null;
  entranceExamScore?: number | null;
  totalScore: number;
  rank: number;
  shift: string;
  majorSubject: string;
  calculatedOnUtc: string;
}

export interface CreateAdmissionOfferResponse {
  offerId: string;
  applicationNumber: string;
  fullName: string;
  meritRank: number;
  offerDate: string;
  expiryDate: string;
}

export interface CreateDirectAdmissionOffersResponse {
  totalOffersCreated: number;
  createdOfferIds: string[];
  errors: string[];
}

export interface SendIndividualAdmissionOfferResponse {
  offerId: string;
  applicationNumber: string;
  fullName: string;
  email: string;
  offerDate: string;
  expiryDate: string;
  admissionFeeAmount: number;
}

export interface OfflineFormIssuancePreviewDto {
  formNumber: string;
  studentName: string;
  mobileNumber: string;
  applicationFeeAmount: number;
  issuedOnUtc: string;
  applicantAccountCreated: boolean;
  applicantAccountId: string | null;
  /** Display label e.g. Shift-I */
  shiftLabel: string | null;
  /** Recorded at issuance; null for legacy accounts where not stored */
  cuetApplied: boolean | null;
}

export interface ReceiveOfflineAdmissionFormResponse {
  accountId: string;
  formNumber: string;
  studentName: string;
  majorSubject: string;
  receivedOnUtc: string;
}

export interface AssignSelectionListRoundResponse {
  applicationId: string;
  formNumber: string;
  round: string;
}

export interface PublishSelectionListResponse {
  publishedCount: number;
  smsSent: number;
  smsFailed: number;
}






