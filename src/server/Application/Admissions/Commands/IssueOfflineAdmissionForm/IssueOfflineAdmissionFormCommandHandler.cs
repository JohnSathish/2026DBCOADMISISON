using ERP.Application.Admissions;
using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Commands.IssueOfflineAdmissionForm;

public sealed class IssueOfflineAdmissionFormCommandHandler
    : IRequestHandler<IssueOfflineAdmissionFormCommand, IssueOfflineAdmissionFormResult>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IOfflineFormIssuanceRepository _issuanceRepository;
    private readonly IOfflineFormReceiptPdfService _receiptPdf;

    public IssueOfflineAdmissionFormCommandHandler(
        IApplicantAccountRepository accountRepository,
        IOfflineFormIssuanceRepository issuanceRepository,
        IOfflineFormReceiptPdfService receiptPdf)
    {
        _accountRepository = accountRepository;
        _issuanceRepository = issuanceRepository;
        _receiptPdf = receiptPdf;
    }

    public async Task<IssueOfflineAdmissionFormResult> Handle(
        IssueOfflineAdmissionFormCommand request,
        CancellationToken cancellationToken)
    {
        var formNumber = NormalizeFormNumber(request.FormNumber);
        if (formNumber is null)
        {
            throw new InvalidOperationException("Form number must be exactly 6 digits.");
        }

        if (await _accountRepository.GetByUniqueIdAsync(formNumber, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Form number {formNumber} is already registered.");
        }

        if (await _issuanceRepository.FormNumberExistsAsync(formNumber, cancellationToken))
        {
            throw new InvalidOperationException($"Form number {formNumber} has already been issued.");
        }

        var mobile = request.MobileNumber.Trim();
        if (mobile.Length == 0 || mobile.Length > 10)
        {
            throw new InvalidOperationException("Mobile number must be 1–10 characters (digits).");
        }

        if (await _accountRepository.MobileExistsAsync(mobile, cancellationToken))
        {
            throw new InvalidOperationException(
                "This mobile number is already registered to another applicant. Use a different number, or contact the office if you need to reuse this mobile.");
        }

        if (await _issuanceRepository.PendingMobileExistsAsync(mobile, cancellationToken))
        {
            throw new InvalidOperationException(
                "This mobile number is already tied to another issued form that has not been received yet.");
        }

        var name = request.StudentName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Student name is required.");
        }

        if (request.ApplicationFeeAmount < 0)
        {
            throw new InvalidOperationException("Application fee cannot be negative.");
        }

        var shiftCode = OfflineAdmissionShift.TryNormalize(request.Shift);
        if (shiftCode is null)
        {
            throw new InvalidOperationException("Shift must be Shift-I or Shift-II.");
        }

        AdmissionPreferredShiftPolicy.EnsureAllowedForNewApplicationOrThrow(shiftCode);

        var issuedOn = DateTime.UtcNow;
        var issuance = new OfflineFormIssuance
        {
            Id = Guid.NewGuid(),
            FormNumber = formNumber,
            StudentName = name,
            MobileNumber = mobile,
            ApplicationFeeAmount = request.ApplicationFeeAmount,
            Shift = shiftCode,
            CuetApplied = request.CuetApplied,
            IssuedOnUtc = issuedOn,
            ApplicantAccountId = null,
        };

        await _issuanceRepository.AddAsync(issuance, cancellationToken);

        var shiftDisplay = OfflineAdmissionShift.ToDisplayLabel(shiftCode);
        var (pdf, fileName) = await _receiptPdf.GenerateAsync(
            formNumber,
            name,
            majorSubject: null,
            request.ApplicationFeeAmount,
            issuedOn,
            mobileNumberForReceipt: mobile,
            shiftDisplay,
            request.CuetApplied,
            cancellationToken);

        return new IssueOfflineAdmissionFormResult(
            issuance.Id,
            formNumber,
            name,
            request.ApplicationFeeAmount,
            issuedOn,
            pdf,
            fileName);
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
