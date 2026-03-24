using System.Text.Json;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Helpers;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.ViewModels;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Queries.ListOnlineApplications;

public sealed class ListOnlineApplicationsQueryHandler : IRequestHandler<ListOnlineApplicationsQuery, OnlineApplicationsListResponse>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IApplicantApplicationRepository _applicationRepository;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public ListOnlineApplicationsQueryHandler(
        IApplicantAccountRepository accountRepository,
        IApplicantApplicationRepository applicationRepository)
    {
        _accountRepository = accountRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<OnlineApplicationsListResponse> Handle(ListOnlineApplicationsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        ApplicationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ApplicationStatus>(request.Status.Trim(), ignoreCase: true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var useLifecycle = !string.IsNullOrWhiteSpace(request.ApplicationLifecycleStage);
        var (accounts, totalCount) = await _accountRepository.GetPagedAsync(
            page,
            pageSize,
            useLifecycle ? null : request.IsApplicationSubmitted,
            useLifecycle ? null : request.IsPaymentCompleted,
            request.SearchTerm,
            statusFilter,
            request.Shift,
            request.CreatedFromUtc,
            request.CreatedToUtc,
            string.IsNullOrWhiteSpace(request.SortBy) ? "createdOnUtc" : request.SortBy.Trim(),
            request.SortDescending ?? true,
            request.MinClassXiiPercentage,
            request.MaxClassXiiPercentage,
            request.AdmissionPath,
            request.AdmissionChannel,
            useLifecycle ? request.ApplicationLifecycleStage!.Trim() : null,
            cancellationToken);

        var accountList = accounts.ToList();
        var idList = accountList.Select(a => a.Id).ToList();
        var drafts = await _applicationRepository.GetDraftsByAccountIdsAsync(idList, cancellationToken);
        var draftAccountIdSet = drafts.Select(d => d.AccountId).ToHashSet();

        var categoryMajorByAccount = new Dictionary<Guid, (string? Category, string? MajorSubject, decimal? ParsedPct)>();
        foreach (var draft in drafts)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draft.Data, _serializerOptions);
                if (dto is null)
                {
                    continue;
                }

                var cat = string.IsNullOrWhiteSpace(dto.PersonalInformation?.Category)
                    ? null
                    : dto.PersonalInformation.Category.Trim();
                var major = string.IsNullOrWhiteSpace(dto.Courses?.MajorSubject)
                    ? null
                    : dto.Courses.MajorSubject.Trim();
                var parsedPct = AcademicPercentageParser.ParseClassXiiPercentage(dto.Academics?.BoardExamination?.Percentage);
                var pct = parsedPct > 0 ? parsedPct : (decimal?)null;
                categoryMajorByAccount[draft.AccountId] = (cat, major, pct);
            }
            catch (JsonException)
            {
                // ignore malformed draft
            }
        }

        var applications = accountList.Select(account =>
        {
            _ = categoryMajorByAccount.TryGetValue(account.Id, out var cm);
            var pctOut = account.ClassXIIPercentage ?? cm.ParsedPct;
            return new OnlineApplicationDto(
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
                cm.Category,
                cm.MajorSubject,
                account.ErpStudentId,
                account.ErpSyncedOnUtc,
                account.ErpSyncLastError,
                pctOut,
                account.AdmissionChannel,
                account.OfflineIssuedMajorSubject,
                account.OfflineFormReceivedOnUtc,
                account.SelectionListRound,
                account.SelectionListPublishedAtUtc,
                account.CuetAppliedAtIssue,
                draftAccountIdSet.Contains(account.Id));
        }).ToList();

        return new OnlineApplicationsListResponse(applications, totalCount, page, pageSize);
    }
}
