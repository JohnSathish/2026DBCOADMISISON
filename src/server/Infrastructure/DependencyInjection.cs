using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Options;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Admissions.Entities;
using ERP.Infrastructure.Admissions;
using ERP.Infrastructure.Authentication;
using ERP.Infrastructure.Notifications;
using ERP.Infrastructure.Payments;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Security;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DefaultConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DefaultConnectionStringName}' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAdmissionsRepository, AdmissionsRepository>();
        services.AddScoped<IAdmissionsReadRepository, AdmissionsReadRepository>();
        services.AddScoped<IApplicantAccountRepository, ApplicantAccountRepository>();
        services.AddScoped<IOfflineFormIssuanceRepository, OfflineFormIssuanceRepository>();
        services.AddScoped<IApplicantApplicationRepository, ApplicantApplicationRepository>();
        services.AddScoped<IEntranceExamRepository, EntranceExamRepository>();
        services.AddScoped<IExamRegistrationRepository, ExamRegistrationRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ERP.Application.Academics.Interfaces.IAcademicTermRepository, ERP.Infrastructure.Academics.AcademicTermRepository>();
        services.AddScoped<ERP.Application.Academics.Interfaces.IClassSectionRepository, ERP.Infrastructure.Academics.ClassSectionRepository>();
        services.AddScoped<ERP.Application.Academics.Interfaces.ITimetableSlotRepository, ERP.Infrastructure.Academics.TimetableSlotRepository>();
        services.AddScoped<ERP.Application.Academics.Interfaces.IRoomRepository, ERP.Infrastructure.Academics.RoomRepository>();
        services.AddScoped<ERP.Application.Academics.Interfaces.ITimetableService, ERP.Infrastructure.Academics.TimetableService>();
        services.AddScoped<IFeeStructureRepository, FeeStructureRepository>();
        services.AddScoped<IFeeComponentRepository, FeeComponentRepository>();
        services.AddScoped<IAdmissionFeeService, AdmissionFeeService>();
        services.AddScoped<IAdmissionWorkflowSettingsService, AdmissionWorkflowSettingsService>();
        services.AddScoped<IClassXiiSubjectCatalogRepository, ClassXiiSubjectCatalogRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IStudentRepository, ERP.Infrastructure.Students.StudentRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IGuardianRepository, ERP.Infrastructure.Students.GuardianRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IAcademicHistoryRepository, ERP.Infrastructure.Students.AcademicHistoryRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IStudentTransferRepository, ERP.Infrastructure.Students.StudentTransferRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IStudentExitRepository, ERP.Infrastructure.Students.StudentExitRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IDisciplineRecordRepository, ERP.Infrastructure.Students.DisciplineRecordRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.ICounselingRecordRepository, ERP.Infrastructure.Students.CounselingRecordRepository>();
        services.AddScoped<ERP.Application.Students.Interfaces.IGradeCalculationService, ERP.Infrastructure.Students.GradeCalculationService>();
        services.AddScoped<ERP.Application.Students.Interfaces.ITranscriptService, ERP.Infrastructure.Students.TranscriptService>();
        services.AddScoped<ERP.Application.Students.Interfaces.IAcademicProgressionService, ERP.Infrastructure.Students.AcademicProgressionService>();
        
        // Fees Management
        services.AddScoped<ERP.Application.Fees.Interfaces.IInvoiceRepository, ERP.Infrastructure.Fees.InvoiceRepository>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IPaymentRepository, ERP.Infrastructure.Fees.PaymentRepository>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IReceiptRepository, ERP.Infrastructure.Fees.ReceiptRepository>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IRefundRepository, ERP.Infrastructure.Fees.RefundRepository>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IScholarshipRepository, ERP.Infrastructure.Fees.ScholarshipRepository>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IInvoiceService, ERP.Infrastructure.Fees.InvoiceService>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IPaymentService, ERP.Infrastructure.Fees.PaymentService>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IRefundService, ERP.Infrastructure.Fees.RefundService>();
        services.AddScoped<ERP.Application.Fees.Interfaces.IFeeLedgerService, ERP.Infrastructure.Fees.FeeLedgerService>();
        
        // Attendance Management
        services.AddScoped<ERP.Application.Attendance.Interfaces.IAttendanceSessionRepository, ERP.Infrastructure.Attendance.AttendanceSessionRepository>();
        services.AddScoped<ERP.Application.Attendance.Interfaces.IAttendanceRecordRepository, ERP.Infrastructure.Attendance.AttendanceRecordRepository>();
        services.AddScoped<ERP.Application.Attendance.Interfaces.IAttendanceDeviceEventRepository, ERP.Infrastructure.Attendance.AttendanceDeviceEventRepository>();
        services.AddScoped<ERP.Application.Attendance.Interfaces.IAttendanceService, ERP.Infrastructure.Attendance.AttendanceService>();
        
        // Examinations Management
        services.AddScoped<ERP.Application.Examinations.Interfaces.IAssessmentRepository, ERP.Infrastructure.Examinations.AssessmentRepository>();
        services.AddScoped<ERP.Application.Examinations.Interfaces.IAssessmentComponentRepository, ERP.Infrastructure.Examinations.AssessmentComponentRepository>();
        services.AddScoped<ERP.Application.Examinations.Interfaces.IMarkEntryRepository, ERP.Infrastructure.Examinations.MarkEntryRepository>();
        services.AddScoped<ERP.Application.Examinations.Interfaces.IResultSummaryRepository, ERP.Infrastructure.Examinations.ResultSummaryRepository>();
        services.AddScoped<ERP.Application.Examinations.Interfaces.IGradingService, ERP.Infrastructure.Examinations.GradingService>();
        services.AddScoped<ERP.Application.Examinations.Interfaces.IResultProcessingService, ERP.Infrastructure.Examinations.ResultProcessingService>();
        
        // Staff Management
        services.AddScoped<ERP.Application.Staff.Interfaces.IStaffRepository, ERP.Infrastructure.Staff.StaffRepository>();
        
        // Library Management
        services.AddScoped<ERP.Application.Library.Interfaces.IBookRepository, ERP.Infrastructure.Library.BookRepository>();
        services.AddScoped<ERP.Application.Library.Interfaces.IBookIssueRepository, ERP.Infrastructure.Library.BookIssueRepository>();
        
        // Transport Management
        services.AddScoped<ERP.Application.Transport.Interfaces.IVehicleRepository, ERP.Infrastructure.Transport.VehicleRepository>();
        
        // Hostel Management
        services.AddScoped<ERP.Application.Hostel.Interfaces.IHostelRoomRepository, ERP.Infrastructure.Hostel.HostelRoomRepository>();
        services.AddScoped<ERP.Application.Hostel.Interfaces.IRoomAllocationRepository, ERP.Infrastructure.Hostel.RoomAllocationRepository>();
        
        services.AddScoped<ERP.Application.Common.Interfaces.IUserAccountService, ERP.Infrastructure.Identity.UserAccountService>();
        services.AddScoped<IApplicantApplicationPdfService, ApplicantApplicationPdfService>();
        services.AddScoped<IOfflineFormReceiptPdfService, OfflineFormReceiptPdfService>();
        services.AddScoped<IApplicantPhotoStorageService, ApplicantPhotoStorageService>();
        services.AddScoped<IApplicantExportService, ApplicantExportService>();
        services.Configure<AdmissionErpSyncOptions>(configuration.GetSection(AdmissionErpSyncOptions.SectionName));
        services.Configure<ReceiptBrandingOptions>(configuration.GetSection("Admissions:ReceiptBranding"));
        services.AddScoped<IAdmissionErpSyncService, AdmissionErpSyncService>();
        services.AddScoped<IApplicantIdGenerator, ApplicantIdGenerator>();
        services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
        services.AddScoped<IPasswordHasher<StudentApplicantAccount>, PasswordHasher<StudentApplicantAccount>>();
        services.AddScoped<IApplicantPasswordHasher, ApplicantPasswordHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        // Admin services
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
        services.AddScoped<IAdminPasswordHasher, AdminPasswordHasher>();

        services.Configure<JwtSettings>(configuration.GetSection("Authentication:Jwt"));
        services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<JwtSettings>>().Value);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IAdminJwtTokenGenerator, AdminJwtTokenGenerator>();

        services.Configure<SmsSettings>(configuration.GetSection("Notifications:Sms"));
        services.Configure<EmailSettings>(configuration.GetSection("Notifications:Email"));
        services.AddHttpClient<IApplicantNotificationService, RegistrationNotificationService>();

        services.Configure<LoginRateLimitSettings>(configuration.GetSection("Security:ApplicantLoginRateLimit"));
        services.AddSingleton<IApplicantLoginRateLimiter, ApplicantLoginRateLimiter>();

        services.Configure<RazorpaySettings>(configuration.GetSection("Razorpay"));
        services.Configure<ApplicantApplicationFeeOptions>(
            configuration.GetSection(ApplicantApplicationFeeOptions.ConfigurationSectionName));
        services.AddHttpClient<IRazorpayService, RazorpayService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.BaseAddress = new Uri("https://api.razorpay.com/v1/");
            });

        return services;
    }
}
