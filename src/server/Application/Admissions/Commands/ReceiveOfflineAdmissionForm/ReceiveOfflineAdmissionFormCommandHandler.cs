using ERP.Application.Admissions;
using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Commands.ReceiveOfflineAdmissionForm;

public sealed class ReceiveOfflineAdmissionFormCommandHandler
    : IRequestHandler<ReceiveOfflineAdmissionFormCommand, ReceiveOfflineAdmissionFormResult>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IOfflineFormIssuanceRepository _issuanceRepository;
    private readonly IApplicantPasswordHasher _passwordHasher;

    public ReceiveOfflineAdmissionFormCommandHandler(
        IApplicantAccountRepository accountRepository,
        IOfflineFormIssuanceRepository issuanceRepository,
        IApplicantPasswordHasher passwordHasher)
    {
        _accountRepository = accountRepository;
        _issuanceRepository = issuanceRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<ReceiveOfflineAdmissionFormResult> Handle(
        ReceiveOfflineAdmissionFormCommand request,
        CancellationToken cancellationToken)
    {
        var formNumber = NormalizeFormNumber(request.FormNumber);
        if (formNumber is null)
        {
            throw new InvalidOperationException("Form number must be exactly 6 digits.");
        }

        var major = request.MajorSubject.Trim();
        if (string.IsNullOrWhiteSpace(major))
        {
            throw new InvalidOperationException("Major subject is required when receiving the form (final course choice).");
        }

        var issuance = await _issuanceRepository.GetByFormNumberForUpdateAsync(formNumber, cancellationToken);
        if (issuance is not null)
        {
            if (issuance.ApplicantAccountId.HasValue)
            {
                throw new InvalidOperationException(
                    $"An applicant account already exists for form number {formNumber}. The form was already received.");
            }

            var email = $"offline-{formNumber}@applicant.local";
            if (await _accountRepository.EmailExistsAsync(email, cancellationToken))
            {
                throw new InvalidOperationException("Email conflict while creating account; contact support.");
            }

            var shiftCode = string.IsNullOrWhiteSpace(issuance.Shift)
                ? OfflineAdmissionShift.ShiftI
                : (OfflineAdmissionShift.TryNormalize(issuance.Shift) ?? issuance.Shift.Trim());

            var account = new StudentApplicantAccount(
                formNumber,
                issuance.StudentName.Trim(),
                new DateOnly(2000, 1, 1),
                "Pending",
                email,
                issuance.MobileNumber.Trim(),
                shiftCode,
                photoUrl: null);

            account.MarkAsOfflineIssued(major);
            account.SetCuetAppliedAtIssue(issuance.CuetApplied);

            var hash = _passwordHasher.HashPassword(account, issuance.MobileNumber.Trim());
            account.SetPasswordHash(hash);

            var txn = $"OFFLINE-CASH-{formNumber}";
            account.MarkPaymentCompleted(txn, issuance.ApplicationFeeAmount);

            var utc = DateTime.UtcNow;
            account.MarkOfflinePhysicalFormReceived(utc);

            await _accountRepository.AddAsync(account, cancellationToken);

            issuance.ApplicantAccountId = account.Id;
            await _issuanceRepository.UpdateAsync(issuance, cancellationToken);

            return new ReceiveOfflineAdmissionFormResult(
                account.Id,
                account.UniqueId,
                account.FullName,
                major,
                account.OfflineFormReceivedOnUtc ?? utc);
        }

        var existing = await _accountRepository.GetByUniqueIdForUpdateAsync(formNumber, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"No issued form found for {formNumber}. Issue a form first, or check the number.");

        if (existing.AdmissionChannel != AdmissionChannel.Offline)
        {
            throw new InvalidOperationException("This form number is not an offline-issued application.");
        }

        if (existing.OfflineFormReceivedOnUtc is not null)
        {
            throw new InvalidOperationException("Physical form was already marked as received.");
        }

        if (string.IsNullOrWhiteSpace(existing.OfflineIssuedMajorSubject))
        {
            existing.MarkAsOfflineIssued(major);
        }

        var legacyUtc = DateTime.UtcNow;
        existing.MarkOfflinePhysicalFormReceived(legacyUtc);
        await _accountRepository.UpdateAsync(existing, cancellationToken);

        return new ReceiveOfflineAdmissionFormResult(
            existing.Id,
            existing.UniqueId,
            existing.FullName,
            existing.OfflineIssuedMajorSubject ?? major,
            existing.OfflineFormReceivedOnUtc ?? legacyUtc);
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
