using ERP.Application.Admissions;
using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Queries.GetOfflineFormIssuancePreview;

public sealed class GetOfflineFormIssuancePreviewQueryHandler
    : IRequestHandler<GetOfflineFormIssuancePreviewQuery, OfflineFormIssuancePreviewDto?>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IOfflineFormIssuanceRepository _issuanceRepository;

    public GetOfflineFormIssuancePreviewQueryHandler(
        IApplicantAccountRepository accountRepository,
        IOfflineFormIssuanceRepository issuanceRepository)
    {
        _accountRepository = accountRepository;
        _issuanceRepository = issuanceRepository;
    }

    public async Task<OfflineFormIssuancePreviewDto?> Handle(
        GetOfflineFormIssuancePreviewQuery request,
        CancellationToken cancellationToken)
    {
        var formNumber = NormalizeFormNumber(request.FormNumber);
        if (formNumber is null)
        {
            return null;
        }

        var account = await _accountRepository.GetByUniqueIdAsync(formNumber, cancellationToken);
        if (account is not null && account.AdmissionChannel == AdmissionChannel.Offline)
        {
            return new OfflineFormIssuancePreviewDto(
                formNumber,
                account.FullName,
                account.MobileNumber,
                account.PaymentAmount ?? 0m,
                account.PaymentCompletedOnUtc ?? account.CreatedOnUtc,
                ApplicantAccountCreated: true,
                account.Id,
                OfflineAdmissionShift.ToDisplayLabel(account.Shift),
                account.CuetAppliedAtIssue);
        }

        var issuance = await _issuanceRepository.GetByFormNumberAsync(formNumber, cancellationToken);
        if (issuance is not null)
        {
            var shiftLabel = string.IsNullOrWhiteSpace(issuance.Shift)
                ? null
                : OfflineAdmissionShift.ToDisplayLabel(
                    OfflineAdmissionShift.TryNormalize(issuance.Shift) ?? issuance.Shift.Trim());

            return new OfflineFormIssuancePreviewDto(
                issuance.FormNumber,
                issuance.StudentName,
                issuance.MobileNumber,
                issuance.ApplicationFeeAmount,
                issuance.IssuedOnUtc,
                ApplicantAccountCreated: issuance.ApplicantAccountId.HasValue,
                issuance.ApplicantAccountId,
                shiftLabel,
                issuance.CuetApplied);
        }

        return null;
    }

    private static string? NormalizeFormNumber(string raw)
    {
        var s = raw.Trim();
        if (s.Length != 6 || !s.All(char.IsDigit))
        {
            return null;
        }

        return s;
    }
}
