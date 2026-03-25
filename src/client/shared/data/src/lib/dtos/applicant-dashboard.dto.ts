export interface ApplicantProfileDto {
  accountId: string;
  uniqueId: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  email: string;
  mobileNumber: string;
  shift: string;
  photoUrl?: string | null;
  createdOnUtc: string;
}

export interface ApplicantDocumentStatusDto {
  name: string;
  status: string;
  description: string;
  isComplete: boolean;
}

export interface ApplicantNotificationDto {
  title: string;
  message: string;
  createdOnUtc: string;
}

/** Elective row: subject name + code; description = full catalog line for tooltips. */
export interface ApplicantElectiveSubjectDto {
  code: string;
  name: string;
  description: string | null;
}

/** Present when application is submitted and application fee payment completed. */
export interface ApplicantCourseSelectionSummaryDto {
  preferredShiftCode: string;
  preferredShiftLabel: string;
  majorSubject: string;
  minorSubject: string;
  mdc: ApplicantElectiveSubjectDto;
  aec: ApplicantElectiveSubjectDto;
  sec: ApplicantElectiveSubjectDto;
  vac: ApplicantElectiveSubjectDto;
  applicationFeePaidOnUtc: string | null;
  draftLastUpdatedOnUtc: string | null;
}

export interface ApplicantDashboardDto {
  profile: ApplicantProfileDto;
  documents: ApplicantDocumentStatusDto[];
  notifications: ApplicantNotificationDto[];
  application: ApplicantDashboardApplicationDto;
  payment: ApplicantDashboardPaymentDto;
  /** Present when application is submitted and fee payment is completed. */
  courseSelection?: ApplicantCourseSelectionSummaryDto | null;
}

export interface ApplicantDashboardApplicationDto {
  isSubmitted: boolean;
  coursesLocked: boolean;
  status: string;
  steps: ApplicantDashboardApplicationStepDto[];
}

export interface ApplicantDashboardApplicationStepDto {
  key: string;
  title: string;
  isComplete: boolean;
  description?: string;
}

export interface ApplicantDashboardPaymentDto {
  transactionId?: string | null;
  amountDue: number;
  amountPaid: number;
  status: string;
  canPay: boolean;
  /** Online application form fee (Razorpay). */
  applicationFeeAmount?: number | null;
  /** Minimum if selected for admission; management may publish a higher final amount. */
  postSelectionAdmissionFeeAmount?: number | null;
}
