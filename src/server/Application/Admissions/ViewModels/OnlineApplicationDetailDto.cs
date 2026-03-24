using ERP.Application.Admissions.DTOs;
using ERP.Domain.Admissions.Entities;

namespace ERP.Application.Admissions.ViewModels;

public sealed record OnlineApplicationDetailDto(
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
    ApplicantApplicationDraftDto? ApplicationDraft,
    Guid? ErpStudentId = null,
    DateTime? ErpSyncedOnUtc = null,
    string? ErpSyncLastError = null,
    AdmissionChannel AdmissionChannel = AdmissionChannel.Online,
    string? OfflineIssuedMajorSubject = null,
    DateTime? OfflineFormReceivedOnUtc = null,
    AdmissionSelectionListRound? SelectionListRound = null,
    DateTime? SelectionListPublishedAtUtc = null,
    bool? CuetAppliedAtIssue = null);





