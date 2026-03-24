using ERP.Domain.Admissions.Entities;

namespace ERP.Application.Admissions.Interfaces;

public interface IApplicantAccountRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsForOtherAccountAsync(
        string email,
        Guid excludeAccountId,
        CancellationToken cancellationToken = default);

    Task<bool> MobileExistsAsync(string mobileNumber, CancellationToken cancellationToken = default);

    Task AddAsync(StudentApplicantAccount account, CancellationToken cancellationToken = default);

    Task<StudentApplicantAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<StudentApplicantAccount?> GetByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default);

    Task<StudentApplicantAccount?> GetByUniqueIdForUpdateAsync(string uniqueId, CancellationToken cancellationToken = default);

    Task<StudentApplicantAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StudentApplicantAccount?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(ApplicantRefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<ApplicantRefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(ApplicantRefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokensByAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task UpdatePasswordAsync(Guid accountId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default);

    Task UpdateShiftAsync(Guid accountId, string shift, CancellationToken cancellationToken = default);

    Task UpdateClassXiPercentageAsync(Guid accountId, decimal? percentage, CancellationToken cancellationToken = default);

    Task UpdateAsync(StudentApplicantAccount account, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StudentApplicantAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<StudentApplicantAccount> Accounts, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        bool? isApplicationSubmitted = null,
        bool? isPaymentCompleted = null,
        string? searchTerm = null,
        ApplicationStatus? applicationStatus = null,
        string? shift = null,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        string sortBy = "createdOnUtc",
        bool sortDescending = true,
        decimal? minClassXiiPercentage = null,
        decimal? maxClassXiiPercentage = null,
        string? admissionPath = null,
        string? admissionChannel = null,
        string? applicationLifecycleStage = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StudentApplicantAccount>> GetAccountsForSelectionListPublishAsync(
        AdmissionSelectionListRound round,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StudentApplicantAccount>> GetPublishedSelectionListAsync(
        AdmissionSelectionListRound? round,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StudentApplicantAccount>> GetEligibleForMeritListAsync(
        string? shift = null,
        string? majorSubject = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<StudentApplicantAccount> Accounts, int TotalCount)> GetAdmittedStudentsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default);
}

