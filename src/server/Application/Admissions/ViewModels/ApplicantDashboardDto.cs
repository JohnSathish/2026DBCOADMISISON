using System.Text.Json.Serialization;

namespace ERP.Application.Admissions.ViewModels;

public sealed record ApplicantDashboardDto(
    ApplicantProfileDto Profile,
    IReadOnlyCollection<ApplicantDocumentStatusDto> Documents,
    IReadOnlyCollection<ApplicantNotificationDto> Notifications,
    ApplicantDashboardApplicationDto Application,
    ApplicantDashboardPaymentDto Payment,
    ApplicantCourseSelectionSummaryDto? CourseSelection = null,
    ApplicantDashboardOfferDto? Offer = null);

/// <summary>Elective line for dashboard: code, display name, and full catalog text (tooltip).</summary>
public sealed record ApplicantElectiveSubjectDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>Populated when application is submitted and application fee payment is completed.</summary>
public sealed record ApplicantCourseSelectionSummaryDto(
    string PreferredShiftCode,
    string PreferredShiftLabel,
    string MajorSubject,
    string MinorSubject,
    ApplicantElectiveSubjectDto Mdc,
    ApplicantElectiveSubjectDto Aec,
    ApplicantElectiveSubjectDto Sec,
    ApplicantElectiveSubjectDto Vac,
    DateTime? ApplicationFeePaidOnUtc,
    DateTime? DraftLastUpdatedOnUtc);

public sealed record ApplicantProfileDto(
    Guid AccountId,
    string UniqueId,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Email,
    string MobileNumber,
    string Shift,
    string? PhotoUrl,
    DateTime CreatedOnUtc);

public sealed record ApplicantDocumentStatusDto(
    string Name,
    string Status,
    string Description,
    bool IsComplete);

public sealed record ApplicantNotificationDto(
    string Title,
    string Message,
    DateTime CreatedOnUtc);

public sealed record ApplicantDashboardApplicationDto(
    bool IsSubmitted,
    bool CoursesLocked,
    string Status,
    IReadOnlyCollection<ApplicantDashboardApplicationStepDto> Steps);

public sealed record ApplicantDashboardApplicationStepDto(
    string Key,
    string Title,
    bool IsComplete,
    string? Description);

/// <summary>Includes reference amounts: application fee (online) vs post-selection admission fee (if selected).</summary>
public sealed record ApplicantDashboardPaymentDto(
    decimal AmountDue,
    decimal AmountPaid,
    string Status,
    bool CanPay,
    string? TransactionId = null,
    decimal? ApplicationFeeAmount = null,
    decimal? PostSelectionAdmissionFeeAmount = null);

public sealed record ApplicantDashboardOfferDto(
    Guid OfferId,
    string ApplicationNumber,
    int MeritRank,
    string Shift,
    string MajorSubject,
    string Status,
    DateTime OfferDate,
    DateTime ExpiryDate,
    DateTime? AcceptedOnUtc,
    DateTime? RejectedOnUtc,
    bool IsExpired,
    int DaysUntilExpiry);

