using ERP.Application.Admissions.ViewModels;
using MediatR;

namespace ERP.Application.Admissions.Queries.ListOnlineApplications;

public sealed record ListOnlineApplicationsQuery(
    int Page = 1,
    int PageSize = 50,
    string? Status = null,
    string? SearchTerm = null,
    bool? IsApplicationSubmitted = null,
    bool? IsPaymentCompleted = null,
    string? Shift = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    string? SortBy = null,
    bool? SortDescending = null,
    decimal? MinClassXiiPercentage = null,
    decimal? MaxClassXiiPercentage = null,
    string? AdmissionPath = null,
    string? AdmissionChannel = null,
    string? ApplicationLifecycleStage = null) : IRequest<OnlineApplicationsListResponse>;

public sealed record OnlineApplicationsListResponse(
    IReadOnlyCollection<OnlineApplicationDto> Applications,
    int TotalCount,
    int Page,
    int PageSize);


















