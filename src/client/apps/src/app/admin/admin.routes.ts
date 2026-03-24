import { Route } from '@angular/router';
import { adminAuthGuard } from '../auth/auth.guard';

export const adminRoutes: Route[] = [
  {
    path: '',
    canActivate: [adminAuthGuard],
    loadComponent: () =>
      import('./admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/admin-dashboard-home.component').then(
            (m) => m.AdminDashboardHomeComponent
          ),
      },
      // Online Admission Routes
      {
        path: 'admissions',
        children: [
          {
            path: 'exams',
            loadComponent: () =>
              import('./admissions/exams/admin-entrance-exams-list.component').then(
                (m) => m.AdminEntranceExamsListComponent
              ),
          },
          {
            path: 'exams/new',
            loadComponent: () =>
              import('./admissions/exams/admin-entrance-exam-form.component').then(
                (m) => m.AdminEntranceExamFormComponent
              ),
          },
          {
            path: 'exams/:id',
            loadComponent: () =>
              import('./admissions/exams/admin-entrance-exam-detail.component').then(
                (m) => m.AdminEntranceExamDetailComponent
              ),
          },
          {
            path: 'exams/:id/edit',
            loadComponent: () =>
              import('./admissions/exams/admin-entrance-exam-form.component').then(
                (m) => m.AdminEntranceExamFormComponent
              ),
          },
          {
            path: 'exams/:id/registrations',
            loadComponent: () =>
              import('./admissions/exams/admin-exam-registrations.component').then(
                (m) => m.AdminExamRegistrationsComponent
              ),
          },
          {
            path: 'workflow',
            loadComponent: () =>
              import('./admissions/workflow/admission-workflow-guide.component').then(
                (m) => m.AdmissionWorkflowGuideComponent
              ),
          },
          {
            path: 'offline',
            loadComponent: () =>
              import('./admissions/offline/admin-offline-admission.component').then(
                (m) => m.AdminOfflineAdmissionComponent
              ),
          },
          {
            path: 'applications',
            loadComponent: () =>
              import('./admissions/applications/admin-online-applications-list.component').then(
                (m) => m.AdminOnlineApplicationsListComponent
              ),
          },
          {
            path: 'admitted-students',
            loadComponent: () =>
              import('./admissions/applications/admin-admitted-students.component').then(
                (m) => m.AdminAdmittedStudentsComponent
              ),
          },
          {
            path: 'applications/:id',
            loadComponent: () =>
              import('./admissions/applications/admin-online-application-detail.component').then(
                (m) => m.AdminOnlineApplicationDetailComponent
              ),
          },
          {
            path: 'online/:id',
            loadComponent: () =>
              import('./admissions/applications/admin-online-application-detail.component').then(
                (m) => m.AdminOnlineApplicationDetailComponent
              ),
          },
          {
            path: 'verification',
            loadComponent: () =>
              import('./admissions/verification/admin-document-verification.component').then(
                (m) => m.AdminDocumentVerificationComponent
              ),
          },
          {
            path: 'merit-list',
            loadComponent: () =>
              import('./admissions/merit-list/admin-merit-list.component').then(
                (m) => m.AdminMeritListComponent
              ),
          },
          {
            path: 'approval',
            loadComponent: () =>
              import('./admissions/approval/admin-admission-approval.component').then(
                (m) => m.AdminAdmissionApprovalComponent
              ),
          },
          {
            path: 'payment-verification',
            loadComponent: () =>
              import('./admissions/payment/admin-payment-verification.component').then(
                (m) => m.AdminPaymentVerificationComponent
              ),
          },
          {
            path: 'payments',
            loadComponent: () =>
              import('./admissions/payment/admin-payment-management.component').then(
                (m) => m.AdminPaymentManagementComponent
              ),
          },
          {
            path: 'analytics',
            loadComponent: () =>
              import('./admissions/analytics/admin-admissions-analytics.component').then(
                (m) => m.AdminAdmissionsAnalyticsComponent
              ),
          },
          {
            path: 'direct-admission',
            loadComponent: () =>
              import('./admissions/direct-admission/admin-direct-admission.component').then(
                (m) => m.AdminDirectAdmissionComponent
              ),
          },
          {
            path: 'send-offer',
            loadComponent: () =>
              import('./admissions/send-offer/admin-send-offer.component').then(
                (m) => m.AdminSendOfferComponent
              ),
          },
        ],
      },
      // Student Management Routes
      {
        path: 'students',
        children: [
          {
            path: 'list',
            loadComponent: () =>
              import('./students/list/admin-student-list.component').then(
                (m) => m.AdminStudentListComponent
              ),
          },
          // Static paths must be registered before :id, or routes like /students/class-allocation
          // match :id and the detail page calls GET /api/students/class-allocation (404).
          {
            path: 'class-allocation',
            loadComponent: () =>
              import('./students/class-allocation/admin-class-allocation.component').then(
                (m) => m.AdminClassAllocationComponent
              ),
          },
          {
            path: 'attendance',
            loadComponent: () =>
              import('./students/attendance/admin-student-attendance.component').then(
                (m) => m.AdminStudentAttendanceComponent
              ),
          },
          {
            path: 'promotion',
            loadComponent: () =>
              import('./students/promotion/admin-student-promotion.component').then(
                (m) => m.AdminStudentPromotionComponent
              ),
          },
          {
            path: 'id-cards',
            loadComponent: () =>
              import('./students/id-cards/admin-id-cards.component').then(
                (m) => m.AdminIdCardsComponent
              ),
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./students/list/admin-student-form.component').then(
                (m) => m.AdminStudentFormComponent
              ),
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./students/list/admin-student-detail.component').then(
                (m) => m.AdminStudentDetailComponent
              ),
          },
        ],
      },
      // Staff Management Routes
      {
        path: 'staff',
        children: [
          {
            path: 'list',
            loadComponent: () =>
              import('./staff/list/admin-staff-list.component').then(
                (m) => m.AdminStaffListComponent
              ),
          },
          {
            path: 'new',
            loadComponent: () =>
              import('./staff/list/admin-staff-form.component').then(
                (m) => m.AdminStaffFormComponent
              ),
          },
          {
            path: 'teaching',
            loadComponent: () =>
              import('./staff/teaching/admin-teaching-staff.component').then(
                (m) => m.AdminTeachingStaffComponent
              ),
          },
          {
            path: 'non-teaching',
            loadComponent: () =>
              import('./staff/non-teaching/admin-non-teaching-staff.component').then(
                (m) => m.AdminNonTeachingStaffComponent
              ),
          },
          {
            path: 'attendance',
            loadComponent: () =>
              import('./staff/attendance/admin-staff-attendance.component').then(
                (m) => m.AdminStaffAttendanceComponent
              ),
          },
          {
            path: 'leave',
            loadComponent: () =>
              import('./staff/leave/admin-staff-leave.component').then(
                (m) => m.AdminStaffLeaveComponent
              ),
          },
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./staff/list/admin-staff-form.component').then(
                (m) => m.AdminStaffFormComponent
              ),
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./staff/list/admin-staff-detail.component').then(
                (m) => m.AdminStaffDetailComponent
              ),
          },
        ],
      },
      // Academics Routes
      {
        path: 'academics',
        children: [
          {
            path: 'lesson-plan',
            loadComponent: () =>
              import('./academics/lesson-plan/admin-lesson-plan.component').then(
                (m) => m.AdminLessonPlanComponent
              ),
          },
          {
            path: 'syllabus',
            loadComponent: () =>
              import('./academics/syllabus/admin-syllabus.component').then(
                (m) => m.AdminSyllabusComponent
              ),
          },
          {
            path: 'timetable',
            loadComponent: () =>
              import('./academics/timetable/admin-timetable.component').then(
                (m) => m.AdminTimetableComponent
              ),
          },
          {
            path: 'terms',
            loadComponent: () =>
              import('./academics/academic-terms/admin-academic-terms-list.component').then(
                (m) => m.AdminAcademicTermsListComponent
              ),
          },
          {
            path: 'homework',
            loadComponent: () =>
              import('./academics/homework/admin-homework.component').then(
                (m) => m.AdminHomeworkComponent
              ),
          },
          {
            path: 'question-bank',
            loadComponent: () =>
              import('./academics/question-bank/admin-question-bank.component').then(
                (m) => m.AdminQuestionBankComponent
              ),
          },
        ],
      },
      // Examination Routes
      {
        path: 'examination',
        children: [
          {
            path: 'assessments',
            loadComponent: () =>
              import('./examination/assessments/admin-assessments-list.component').then(
                (m) => m.AdminAssessmentsListComponent
              ),
          },
          {
            path: 'assessments/new',
            loadComponent: () =>
              import('./examination/assessments/admin-assessment-form.component').then(
                (m) => m.AdminAssessmentFormComponent
              ),
          },
          {
            path: 'assessments/:id/edit',
            loadComponent: () =>
              import('./examination/assessments/admin-assessment-form.component').then(
                (m) => m.AdminAssessmentFormComponent
              ),
          },
          {
            path: 'assessments/:id',
            loadComponent: () =>
              import('./examination/assessments/admin-assessment-detail.component').then(
                (m) => m.AdminAssessmentDetailComponent
              ),
          },
          {
            path: 'schedule',
            loadComponent: () =>
              import('./examination/schedule/admin-exam-schedule.component').then(
                (m) => m.AdminExamScheduleComponent
              ),
          },
          {
            path: 'marks-entry',
            loadComponent: () =>
              import('./examination/marks-entry/admin-marks-entry.component').then(
                (m) => m.AdminMarksEntryComponent
              ),
          },
          {
            path: 'result-processing',
            loadComponent: () =>
              import('./examination/result-processing/admin-result-processing.component').then(
                (m) => m.AdminResultProcessingComponent
              ),
          },
          {
            path: 'report-cards',
            loadComponent: () =>
              import('./examination/report-cards/admin-report-cards.component').then(
                (m) => m.AdminReportCardsComponent
              ),
          },
        ],
      },
      // Fee Management Routes
      {
        path: 'fees',
        children: [
          {
            path: 'collection',
            loadComponent: () =>
              import('./fees/collection/admin-fee-collection.component').then(
                (m) => m.AdminFeeCollectionComponent
              ),
          },
          {
            path: 'reports',
            loadComponent: () =>
              import('./fees/reports/admin-fee-reports.component').then(
                (m) => m.AdminFeeReportsComponent
              ),
          },
          {
            path: 'concession',
            loadComponent: () =>
              import('./fees/concession/admin-fee-concession.component').then(
                (m) => m.AdminFeeConcessionComponent
              ),
          },
          {
            path: 'pending-dues',
            loadComponent: () =>
              import('./fees/pending-dues/admin-pending-dues.component').then(
                (m) => m.AdminPendingDuesComponent
              ),
          },
        ],
      },
      // Communication Routes
      {
        path: 'communication',
        children: [
          {
            path: 'notice-board',
            loadComponent: () =>
              import('./communication/notice-board/admin-notice-board.component').then(
                (m) => m.AdminNoticeBoardComponent
              ),
          },
          {
            path: 'sms-email',
            loadComponent: () =>
              import('./communication/sms-email/admin-sms-email.component').then(
                (m) => m.AdminSmsEmailComponent
              ),
          },
          {
            path: 'circulars',
            loadComponent: () =>
              import('./communication/circulars/admin-circulars.component').then(
                (m) => m.AdminCircularsComponent
              ),
          },
          {
            path: 'parent-notifications',
            loadComponent: () =>
              import('./communication/parent-notifications/admin-parent-notifications.component').then(
                (m) => m.AdminParentNotificationsComponent
              ),
          },
        ],
      },
      // Front Office Routes
      {
        path: 'front-office',
        children: [
          {
            path: 'visitors',
            loadComponent: () =>
              import('./front-office/visitors/admin-visitor-management.component').then(
                (m) => m.AdminVisitorManagementComponent
              ),
          },
          {
            path: 'enquiry',
            loadComponent: () =>
              import('./front-office/enquiry/admin-enquiry-register.component').then(
                (m) => m.AdminEnquiryRegisterComponent
              ),
          },
          {
            path: 'complaints',
            loadComponent: () =>
              import('./front-office/complaints/admin-complaint-register.component').then(
                (m) => m.AdminComplaintRegisterComponent
              ),
          },
        ],
      },
      // Transport Routes
      {
        path: 'transport',
        children: [
          {
            path: 'vehicles',
            loadComponent: () =>
              import('./transport/list/admin-transport-list.component').then(
                (m) => m.AdminTransportListComponent
              ),
          },
          {
            path: 'vehicles/new',
            loadComponent: () =>
              import('./transport/list/admin-transport-form.component').then(
                (m) => m.AdminTransportFormComponent
              ),
          },
          {
            path: 'vehicles/:id',
            loadComponent: () =>
              import('./transport/list/admin-transport-detail.component').then(
                (m) => m.AdminTransportDetailComponent
              ),
          },
          {
            path: 'routes',
            loadComponent: () =>
              import('./transport/routes/admin-routes.component').then(
                (m) => m.AdminRoutesComponent
              ),
          },
          {
            path: 'drivers',
            loadComponent: () =>
              import('./transport/drivers/admin-driver-information.component').then(
                (m) => m.AdminDriverInformationComponent
              ),
          },
        ],
      },
      // Library Routes
      {
        path: 'library',
        children: [
          {
            path: 'books',
            loadComponent: () =>
              import('./library/list/admin-library-list.component').then(
                (m) => m.AdminLibraryListComponent
              ),
          },
          {
            path: 'books/new',
            loadComponent: () =>
              import('./library/list/admin-library-form.component').then(
                (m) => m.AdminLibraryFormComponent
              ),
          },
          {
            path: 'issue/:bookId',
            loadComponent: () =>
              import('./library/list/admin-library-issue.component').then(
                (m) => m.AdminLibraryIssueComponent
              ),
          },
          {
            path: 'issue-return',
            loadComponent: () =>
              import('./library/issue-return/admin-issue-return.component').then(
                (m) => m.AdminIssueReturnComponent
              ),
          },
          {
            path: 'reports',
            loadComponent: () =>
              import('./library/reports/admin-library-reports.component').then(
                (m) => m.AdminLibraryReportsComponent
              ),
          },
        ],
      },
      // Hostel Routes
      {
        path: 'hostel',
        children: [
          {
            path: 'rooms',
            loadComponent: () =>
              import('./hostel/rooms/admin-hostel-rooms.component').then(
                (m) => m.AdminHostelRoomsComponent
              ),
          },
          {
            path: 'rooms/new',
            loadComponent: () =>
              import('./hostel/rooms/admin-hostel-room-form.component').then(
                (m) => m.AdminHostelRoomFormComponent
              ),
          },
          {
            path: 'rooms/:id',
            loadComponent: () =>
              import('./hostel/rooms/admin-hostel-room-detail.component').then(
                (m) => m.AdminHostelRoomDetailComponent
              ),
          },
          {
            path: 'student-allocation',
            loadComponent: () =>
              import('./hostel/student-allocation/admin-hostel-student-allocation.component').then(
                (m) => m.AdminHostelStudentAllocationComponent
              ),
          },
          {
            path: 'fees',
            loadComponent: () =>
              import('./hostel/fees/admin-hostel-fees.component').then(
                (m) => m.AdminHostelFeesComponent
              ),
          },
        ],
      },
      // Settings Routes (nested outlet so all /admin/settings/* children render reliably)
      {
        path: 'settings',
        loadComponent: () =>
          import('./settings/admin-settings-shell.component').then(
            (m) => m.AdminSettingsShellComponent
          ),
        children: [
          {
            path: 'users',
            loadComponent: () =>
              import('./settings/users/admin-user-management.component').then(
                (m) => m.AdminUserManagementComponent
              ),
          },
          {
            path: 'roles',
            loadComponent: () =>
              import('./settings/roles/admin-roles-permissions.component').then(
                (m) => m.AdminRolesPermissionsComponent
              ),
          },
          {
            path: 'academic-year',
            loadComponent: () =>
              import('./settings/academic-year/admin-academic-year-settings.component').then(
                (m) => m.AdminAcademicYearSettingsComponent
              ),
          },
          {
            path: 'programs',
            loadComponent: () =>
              import('./settings/programs/admin-programs-list.component').then(
                (m) => m.AdminProgramsListComponent
              ),
          },
          {
            path: 'courses',
            loadComponent: () =>
              import('./settings/courses/admin-courses-list.component').then(
                (m) => m.AdminCoursesListComponent
              ),
          },
          {
            path: 'fee-structures',
            loadComponent: () =>
              import('./settings/fees/admin-fee-structures-list.component').then(
                (m) => m.AdminFeeStructuresListComponent
              ),
          },
        ],
      },
    ],
  },
];
