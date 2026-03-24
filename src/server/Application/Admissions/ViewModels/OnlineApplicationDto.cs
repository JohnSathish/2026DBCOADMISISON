using ERP.Domain.Admissions.Entities;

namespace ERP.Application.Admissions.ViewModels;

public sealed record OnlineApplicationDto(
    Guid Id,
    string UniqueId,
    string FullName,
    string Email,
    string MobileNumber,
    DateOnly DateOfBirth,
    string Gender,
    string Shift,
    bool IsApplicationSubmitted,
    bool IsPaymentCompleted,
    string? PaymentTransactionId,
    decimal? PaymentAmount,
    DateTime? PaymentCompletedOnUtc,
    DateTime CreatedOnUtc,
    string? PhotoUrl,
    ApplicationStatus Status,
    DateTime StatusUpdatedOnUtc,
    string? StatusUpdatedBy,
    string? StatusRemarks,
    DateTime? EntranceExamScheduledOnUtc,
    string? EntranceExamVenue,
    string? EntranceExamInstructions,
    string? Category = null,
    string? MajorSubject = null,
    Guid? ErpStudentId = null,
    DateTime? ErpSyncedOnUtc = null,
    string? ErpSyncLastError = null,
    decimal? ClassXIIPercentage = null,
    AdmissionChannel AdmissionChannel = AdmissionChannel.Online,
    string? OfflineIssuedMajorSubject = null,
    DateTime? OfflineFormReceivedOnUtc = null,
    AdmissionSelectionListRound? SelectionListRound = null,
    DateTime? SelectionListPublishedAtUtc = null,
    bool? CuetAppliedAtIssue = null,
    bool HasApplicationDraft = false);





