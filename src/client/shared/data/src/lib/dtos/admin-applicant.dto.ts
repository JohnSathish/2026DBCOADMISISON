export type ApplicantStatus =
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'WaitingList'
  | 'EntranceExam'
  | 'Enrolled'
  | 'DirectAdmissionGranted'
  | 'AdmissionFeePaid';

export interface AdminApplicantDto {
  id: string;
  applicationNumber: string;
  fullName: string;
  email: string;
  mobileNumber: string;
  dateOfBirth: string;
  programCode: string;
  status: ApplicantStatus;
  statusUpdatedOnUtc: string;
  statusUpdatedBy?: string | null;
  statusRemarks?: string | null;
  entranceExamScheduledOnUtc?: string | null;
  entranceExamVenue?: string | null;
  entranceExamInstructions?: string | null;
  createdOnUtc: string;
}

export interface CreateApplicantPayload {
  applicationNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  dateOfBirth: string;
  programCode: string;
  mobileNumber: string;
}

export interface UpdateApplicantStatusPayload {
  status: ApplicantStatus;
  notifyApplicant: boolean;
  remarks?: string | null;
  entranceExam?: EntranceExamPayload | null;
}

export interface EntranceExamPayload {
  scheduledOnUtc?: string | null;
  venue?: string | null;
  instructions?: string | null;
}

// Entrance Exam Management DTOs
export interface EntranceExamDto {
  id: string;
  examName: string;
  examCode: string;
  description?: string | null;
  examDate: string;
  examStartTime: string;
  examEndTime: string;
  venue: string;
  venueAddress?: string | null;
  instructions?: string | null;
  maxCapacity: number;
  currentRegistrations: number;
  isActive: boolean;
  createdOnUtc: string;
  createdBy?: string | null;
  updatedOnUtc?: string | null;
  updatedBy?: string | null;
}

export interface EntranceExamsListResponse {
  exams: EntranceExamDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateEntranceExamPayload {
  examName: string;
  examCode: string;
  examDate: string;
  examStartTime: string;
  examEndTime: string;
  venue: string;
  maxCapacity: number;
  description?: string | null;
  venueAddress?: string | null;
  instructions?: string | null;
}

export interface UpdateEntranceExamPayload {
  examName: string;
  examDate: string;
  examStartTime: string;
  examEndTime: string;
  venue: string;
  maxCapacity: number;
  description?: string | null;
  venueAddress?: string | null;
  instructions?: string | null;
}

export interface ExamRegistrationDto {
  id: string;
  examId: string;
  examName: string;
  examCode: string;
  applicantAccountId: string;
  applicantName: string;
  applicantUniqueId: string;
  hallTicketNumber?: string | null;
  isPresent: boolean;
  score?: number | null;
  registeredOnUtc: string;
  registeredBy?: string | null;
  attendanceMarkedOnUtc?: string | null;
  attendanceMarkedBy?: string | null;
  scoreEnteredOnUtc?: string | null;
  scoreEnteredBy?: string | null;
}

export interface ExamRegistrationsListResponse {
  registrations: ExamRegistrationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// Payment Management DTOs
export interface PaymentDto {
  accountId: string;
  applicationNumber: string;
  fullName: string;
  email: string;
  mobileNumber: string;
  isPaymentCompleted: boolean;
  paymentOrderId?: string | null;
  paymentTransactionId?: string | null;
  paymentAmount?: number | null;
  paymentCompletedOnUtc?: string | null;
  createdOnUtc: string;
  applicationStatus: ApplicantStatus;
}

export interface PaymentsListResponse {
  payments: PaymentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// Document Management DTOs
export interface DocumentDto {
  documentType: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  isUploaded: boolean;
  verificationStatus?: DocumentVerificationStatusDto | null;
}

export interface DocumentVerificationStatusDto {
  isVerified: boolean;
  verifiedOnUtc: string;
  verifiedBy?: string | null;
  remarks?: string | null;
}

export interface ApplicationDocumentsDto {
  accountId: string;
  applicationNumber: string;
  fullName: string;
  documents: DocumentDto[];
}

// Payment Report DTOs
export interface PaymentReportDto {
  generatedOnUtc: string;
  fromDate?: string | null;
  toDate?: string | null;
  summary: PaymentReportSummary;
  items: PaymentReportItemDto[];
}

export interface PaymentReportSummary {
  totalPayments: number;
  paidCount: number;
  pendingCount: number;
  totalRevenue: number;
  averagePaymentAmount: number;
  minPaymentAmount: number;
  maxPaymentAmount: number;
}

export interface PaymentReportItemDto {
  applicationNumber: string;
  fullName: string;
  email: string;
  mobileNumber: string;
  isPaymentCompleted: boolean;
  paymentAmount?: number | null;
  paymentTransactionId?: string | null;
  paymentCompletedOnUtc?: string | null;
  createdOnUtc: string;
  applicationStatus: string;
}

export interface OnlineApplicationDto {
  id: string;
  uniqueId: string;
  fullName: string;
  email: string;
  mobileNumber: string;
  dateOfBirth: string;
  gender: string;
  shift: string;
  isApplicationSubmitted: boolean;
  /** True when a draft row exists for this account (may still be true after submit). */
  hasApplicationDraft?: boolean;
  isPaymentCompleted: boolean;
  paymentTransactionId?: string | null;
  paymentAmount?: number | null;
  paymentCompletedOnUtc?: string | null;
  createdOnUtc: string;
  photoUrl?: string | null;
  status: ApplicantStatus;
  statusUpdatedOnUtc: string;
  statusUpdatedBy?: string | null;
  statusRemarks?: string | null;
  entranceExamScheduledOnUtc?: string | null;
  entranceExamVenue?: string | null;
  entranceExamInstructions?: string | null;
  /** Reservation / quota category from application form (when submitted). */
  category?: string | null;
  /** Preferred major / stream from application form (when submitted). */
  majorSubject?: string | null;
  /** ERP `students.Students.Id` after admission→ERP sync. */
  erpStudentId?: string | null;
  erpSyncedOnUtc?: string | null;
  erpSyncLastError?: string | null;
  /** Class XII % (from draft / synced on save). */
  classXIIPercentage?: number | null;
  /** CUET status captured when the offline form was issued (offline applicants). */
  cuetAppliedAtIssue?: boolean | null;
}

export interface OnlineApplicationsListResponse {
  applications: OnlineApplicationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Result of POST .../applications/{id}/erp-sync */
export interface AdmissionErpSyncResult {
  success: boolean;
  erpStudentId?: string | null;
  message: string;
}

export interface OnlineApplicationDetailDto extends OnlineApplicationDto {
  applicationDraft?: any | null; // ApplicantApplicationDraftDto - we can import the type later
}

export interface UpdateOnlineApplicationStatusPayload {
  paymentDeadlineUtc?: string | null;
  status: ApplicantStatus;
  notifyApplicant: boolean;
  remarks?: string | null;
  entranceExam?: EntranceExamPayload | null;
}

export interface AdminDashboardDto {
  totalApplications: number;
  submittedApplications: number;
  pendingApplications: number;
  approvedApplications: number;
  rejectedApplications: number;
  waitingListApplications: number;
  entranceExamApplications: number;
  paidApplications: number;
  unpaidApplications: number;
  /** Submitted applications not yet approved, rejected, or enrolled (admission pipeline). */
  pendingPipelineCount?: number;
  totalRevenue: number;
  statisticsByStatus: StatisticsByStatus;
  /** Offline admission KPIs (when API returns them). */
  offlineFormsIssued?: number;
  offlineFormsReceived?: number;
  offlineApplicationsSubmitted?: number;
}

export interface StatisticsByStatus {
  submitted: number;
  approved: number;
  rejected: number;
  waitingList: number;
  entranceExam: number;
}

// Analytics DTOs
export interface AdmissionsAnalyticsDto {
  generatedOnUtc: string;
  fromDate?: string | null;
  toDate?: string | null;
  trends: ApplicationTrendsDto;
  statusDistribution: StatusDistributionDto;
  paymentAnalytics: PaymentAnalyticsDto;
  documentVerification: DocumentVerificationStatsDto;
  programStatistics: ProgramStatisticsDto;
  dailyApplications: DailyApplicationCountDto[];
  monthlyApplications: MonthlyApplicationCountDto[];
}

export interface ApplicationTrendsDto {
  totalApplications: number;
  submittedThisPeriod: number;
  approvedThisPeriod: number;
  rejectedThisPeriod: number;
  approvalRate: number;
  rejectionRate: number;
  averageProcessingDays: number;
}

export interface StatusDistributionDto {
  submitted: number;
  approved: number;
  rejected: number;
  waitingList: number;
  entranceExam: number;
}

export interface PaymentAnalyticsDto {
  totalPayments: number;
  paidCount: number;
  pendingCount: number;
  totalRevenue: number;
  averagePaymentAmount: number;
  paymentCompletionRate: number;
  paymentTrend: PaymentTrendDto[];
}

export interface PaymentTrendDto {
  date: string;
  paidCount: number;
  pendingCount: number;
  revenue: number;
}

export interface DocumentVerificationStatsDto {
  totalDocuments: number;
  verifiedCount: number;
  pendingCount: number;
  rejectedCount: number;
  verificationRate: number;
}

export interface ProgramStatisticsDto {
  programs: ProgramStatDto[];
}

export interface ProgramStatDto {
  programCode: string;
  programName: string;
  applicationCount: number;
  approvedCount: number;
  approvalRate: number;
}

export interface DailyApplicationCountDto {
  date: string;
  count: number;
}

export interface MonthlyApplicationCountDto {
  year: number;
  month: number;
  count: number;
}

export interface GrantDirectAdmissionResultDto {
  accountId: string;
  paymentUrl: string;
  classXiiPercentage: number;
  status: string;
}

export interface AdmittedStudentRowDto {
  id: string;
  uniqueId: string;
  fullName: string;
  majorSubject?: string | null;
  isPaymentCompleted: boolean;
  status: string;
  paymentCompletedOnUtc?: string | null;
  createdOnUtc: string;
}

export interface AdmittedStudentsListResponse {
  items: AdmittedStudentRowDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdmissionWorkflowSettingsDto {
  directAdmissionCutoffPercentage: number;
  applicantPortalBaseUrl: string;
}






