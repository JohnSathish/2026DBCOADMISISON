using System.Text.Json;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.ViewModels;
using MediatR;

namespace ERP.Application.Admissions.Queries.GetOnlineApplicationById;

public sealed class GetOnlineApplicationByIdQueryHandler : IRequestHandler<GetOnlineApplicationByIdQuery, OnlineApplicationDetailDto?>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IApplicantApplicationRepository _applicationRepository;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public GetOnlineApplicationByIdQueryHandler(
        IApplicantAccountRepository accountRepository,
        IApplicantApplicationRepository applicationRepository)
    {
        _accountRepository = accountRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<OnlineApplicationDetailDto?> Handle(GetOnlineApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        ApplicantApplicationDraftDto? draft = null;
        if (account.IsApplicationSubmitted)
        {
            var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(request.AccountId, cancellationToken);
            if (draftEntity is not null)
            {
                draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                    ?? ApplicantApplicationDraftDto.Empty;
            }
        }

        return new OnlineApplicationDetailDto(
            account.Id,
            account.UniqueId,
            account.FullName,
            account.Email,
            account.MobileNumber,
            account.DateOfBirth,
            account.Gender,
            account.Shift,
            account.IsApplicationSubmitted,
            account.IsPaymentCompleted,
            account.PaymentTransactionId,
            account.PaymentAmount,
            account.PaymentCompletedOnUtc,
            account.CreatedOnUtc,
            account.PhotoUrl,
            account.Status,
            account.StatusUpdatedOnUtc,
            account.StatusUpdatedBy,
            account.StatusRemarks,
            account.EntranceExamScheduledOnUtc,
            account.EntranceExamVenue,
            account.EntranceExamInstructions,
            draft,
            account.ErpStudentId,
            account.ErpSyncedOnUtc,
            account.ErpSyncLastError,
            account.AdmissionChannel,
            account.OfflineIssuedMajorSubject,
            account.OfflineFormReceivedOnUtc,
            account.SelectionListRound,
            account.SelectionListPublishedAtUtc,
            account.CuetAppliedAtIssue);
    }
}





