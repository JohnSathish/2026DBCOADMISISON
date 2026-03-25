using System.Text.Json;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Queries.GetAdmissionFee;
using MediatR;

namespace ERP.Application.Admissions.Queries.GetAdmissionFee;

public sealed class GetAdmissionFeeQueryHandler : IRequestHandler<GetAdmissionFeeQuery, AdmissionFeeDto>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IApplicantApplicationRepository _applicationRepository;
    private readonly IAdmissionFeeService _admissionFeeService;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public GetAdmissionFeeQueryHandler(
        IApplicantAccountRepository accountRepository,
        IApplicantApplicationRepository applicationRepository,
        IAdmissionFeeService admissionFeeService)
    {
        _accountRepository = accountRepository;
        _applicationRepository = applicationRepository;
        _admissionFeeService = admissionFeeService;
    }

    public async Task<AdmissionFeeDto> Handle(GetAdmissionFeeQuery request, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException($"Account with ID {request.AccountId} not found.");
        }

        // Get the major subject from application draft
        string? majorSubject = null;
        if (account.IsApplicationSubmitted)
        {
            var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(request.AccountId, cancellationToken);
            if (draftEntity is not null)
            {
                var draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions);
                majorSubject = draft?.Courses?.MajorSubject;
            }
        }

        var requiredAmount = _admissionFeeService.GetAdmissionFee(majorSubject);
        var paidAmount = account.PaymentAmount;
        var isPaymentValid = _admissionFeeService.IsPaymentAmountValid(paidAmount, majorSubject);

        var message = isPaymentValid
            ? $"Payment of ₹{paidAmount:N2} meets the post-selection admission fee requirement."
            : $"Required post-selection admission fee: ₹{requiredAmount:N2} (minimum; management may set a higher final amount). " +
              $"{(paidAmount.HasValue ? $"Recorded payment: ₹{paidAmount:N2}. " : "No payment recorded. ")}" +
              $"Please pay the required amount to proceed with enrollment.";

        return new AdmissionFeeDto(
            requiredAmount,
            paidAmount,
            isPaymentValid,
            majorSubject,
            message);
    }
}


