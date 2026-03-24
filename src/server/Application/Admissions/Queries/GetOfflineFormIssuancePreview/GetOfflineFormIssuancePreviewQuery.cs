using MediatR;

namespace ERP.Application.Admissions.Queries.GetOfflineFormIssuancePreview;

public sealed record GetOfflineFormIssuancePreviewQuery(string FormNumber)
    : IRequest<OfflineFormIssuancePreviewDto?>;

public sealed record OfflineFormIssuancePreviewDto(
    string FormNumber,
    string StudentName,
    string MobileNumber,
    decimal ApplicationFeeAmount,
    DateTime IssuedOnUtc,
    bool ApplicantAccountCreated,
    Guid? ApplicantAccountId,
    string? ShiftLabel,
    bool? CuetApplied);
