using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Admissions;

public class ApplicantAccountRepository : IApplicantAccountRepository
{
    private readonly ApplicationDbContext _context;

    public ApplicantAccountRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .AnyAsync(x => x.Email == email, cancellationToken);
    }

    public async Task<bool> EmailExistsForOtherAccountAsync(
        string email,
        Guid excludeAccountId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .AnyAsync(x => x.Email == email && x.Id != excludeAccountId, cancellationToken);
    }

    public async Task<bool> MobileExistsAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .AnyAsync(x => x.MobileNumber == mobileNumber, cancellationToken);
    }

    public async Task AddAsync(StudentApplicantAccount account, CancellationToken cancellationToken = default)
    {
        await _context.StudentApplicantAccounts.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudentApplicantAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public async Task<StudentApplicantAccount?> GetByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UniqueId == uniqueId, cancellationToken);
    }

    public async Task<StudentApplicantAccount?> GetByUniqueIdForUpdateAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .FirstOrDefaultAsync(x => x.UniqueId == uniqueId, cancellationToken);
    }

    public async Task<StudentApplicantAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<StudentApplicantAccount?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddRefreshTokenAsync(ApplicantRefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await _context.ApplicantRefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApplicantRefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.ApplicantRefreshTokens
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(ApplicantRefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        refreshToken.Revoke();
        _context.ApplicantRefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokensByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await _context.ApplicantRefreshTokens
            .Where(x => x.AccountId == accountId && x.RevokedOnUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedOnUtc, DateTime.UtcNow), cancellationToken);
    }

    public async Task UpdatePasswordAsync(Guid accountId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
    {
        await _context.StudentApplicantAccounts
            .Where(x => x.Id == accountId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.PasswordHash, passwordHash)
                .SetProperty(a => a.MustChangePassword, mustChangePassword), cancellationToken);
    }

    public async Task UpdateShiftAsync(Guid accountId, string shift, CancellationToken cancellationToken = default)
    {
        await _context.StudentApplicantAccounts
            .Where(x => x.Id == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Shift, shift), cancellationToken);
    }

    public async Task UpdateClassXiPercentageAsync(Guid accountId, decimal? percentage, CancellationToken cancellationToken = default)
    {
        await _context.StudentApplicantAccounts
            .Where(x => x.Id == accountId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.ClassXIIPercentage, percentage),
                cancellationToken);
    }

    public async Task UpdateAsync(StudentApplicantAccount account, CancellationToken cancellationToken = default)
    {
        _context.StudentApplicantAccounts.Update(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudentApplicantAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<StudentApplicantAccount> Accounts, int TotalCount)> GetPagedAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _context.StudentApplicantAccounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(applicationLifecycleStage))
        {
            var stage = applicationLifecycleStage.Trim().ToLowerInvariant();
            var draftAccountIds = _context.ApplicantApplicationDrafts.AsNoTracking().Select(d => d.AccountId);
            query = stage switch
            {
                "registered" => query.Where(a =>
                    !a.IsApplicationSubmitted && !draftAccountIds.Contains(a.Id)),
                "forminprogress" => query.Where(a =>
                    !a.IsApplicationSubmitted && draftAccountIds.Contains(a.Id)),
                "paid" => query.Where(a => a.IsApplicationSubmitted && a.IsPaymentCompleted),
                "paymentpending" => query.Where(a => a.IsApplicationSubmitted && !a.IsPaymentCompleted),
                "submitted" => query.Where(a => a.IsApplicationSubmitted),
                _ => query
            };
        }
        else
        {
            if (isApplicationSubmitted.HasValue)
            {
                query = query.Where(x => x.IsApplicationSubmitted == isApplicationSubmitted.Value);
            }

            if (isPaymentCompleted.HasValue)
            {
                query = query.Where(x => x.IsPaymentCompleted == isPaymentCompleted.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.UniqueId.ToLowerInvariant().Contains(search) ||
                x.FullName.ToLowerInvariant().Contains(search) ||
                x.Email.ToLowerInvariant().Contains(search) ||
                x.MobileNumber.Contains(search));
        }

        if (applicationStatus.HasValue)
        {
            query = query.Where(x => x.Status == applicationStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var s = shift.Trim();
            query = query.Where(x => x.Shift == s);
        }

        if (createdFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedOnUtc >= createdFromUtc.Value);
        }

        if (createdToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedOnUtc <= createdToUtc.Value);
        }

        if (minClassXiiPercentage.HasValue)
        {
            query = query.Where(x => x.ClassXIIPercentage != null && x.ClassXIIPercentage >= minClassXiiPercentage.Value);
        }

        if (maxClassXiiPercentage.HasValue)
        {
            query = query.Where(x => x.ClassXIIPercentage != null && x.ClassXIIPercentage <= maxClassXiiPercentage.Value);
        }

        if (!string.IsNullOrWhiteSpace(admissionPath))
        {
            var p = admissionPath.Trim().ToLowerInvariant();
            if (p == "direct")
            {
                query = query.Where(x =>
                    x.Status == ApplicationStatus.DirectAdmissionGranted ||
                    x.Status == ApplicationStatus.AdmissionFeePaid);
            }
            else if (p == "normal")
            {
                query = query.Where(x =>
                    x.Status != ApplicationStatus.DirectAdmissionGranted &&
                    x.Status != ApplicationStatus.AdmissionFeePaid);
            }
        }

        if (!string.IsNullOrWhiteSpace(admissionChannel))
        {
            var c = admissionChannel.Trim().ToLowerInvariant();
            if (c == "offline")
            {
                query = query.Where(x => x.AdmissionChannel == AdmissionChannel.Offline);
            }
            else if (c == "online")
            {
                query = query.Where(x => x.AdmissionChannel == AdmissionChannel.Online);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortKey = (sortBy ?? "createdOnUtc").Trim().ToLowerInvariant();
        query = sortKey switch
        {
            "uniqueid" => sortDescending
                ? query.OrderByDescending(x => x.UniqueId)
                : query.OrderBy(x => x.UniqueId),
            "fullname" => sortDescending
                ? query.OrderByDescending(x => x.FullName)
                : query.OrderBy(x => x.FullName),
            "status" => sortDescending
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),
            "classxiipercentage" => sortDescending
                ? query.OrderByDescending(x => x.ClassXIIPercentage ?? -1)
                : query.OrderBy(x => x.ClassXIIPercentage ?? 101),
            _ => sortDescending
                ? query.OrderByDescending(x => x.CreatedOnUtc)
                : query.OrderBy(x => x.CreatedOnUtc),
        };

        var accounts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (accounts, totalCount);
    }

    public async Task<IReadOnlyCollection<StudentApplicantAccount>> GetAccountsForSelectionListPublishAsync(
        AdmissionSelectionListRound round,
        CancellationToken cancellationToken = default)
    {
        return await _context.StudentApplicantAccounts
            .Where(x =>
                x.SelectionListRound == round &&
                x.SelectionListPublishedAtUtc == null &&
                x.IsApplicationSubmitted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudentApplicantAccount>> GetPublishedSelectionListAsync(
        AdmissionSelectionListRound? round,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StudentApplicantAccounts
            .AsNoTracking()
            .Where(x => x.SelectionListPublishedAtUtc != null);

        if (round.HasValue)
        {
            query = query.Where(x => x.SelectionListRound == round);
        }

        return await query
            .OrderBy(x => x.UniqueId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<StudentApplicantAccount> Accounts, int TotalCount)> GetAdmittedStudentsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var p = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, 200);

        var query = _context.StudentApplicantAccounts
            .AsNoTracking()
            .Where(a =>
                a.Status == ApplicationStatus.AdmissionFeePaid ||
                a.Status == ApplicationStatus.Enrolled);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.UniqueId.ToLower().Contains(s) ||
                a.FullName.ToLower().Contains(s) ||
                a.Email.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var accounts = await query
            .OrderByDescending(a => a.PaymentCompletedOnUtc ?? a.StatusUpdatedOnUtc)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync(cancellationToken);

        return (accounts, totalCount);
    }

    public async Task<IReadOnlyCollection<StudentApplicantAccount>> GetEligibleForMeritListAsync(
        string? shift = null,
        string? majorSubject = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StudentApplicantAccounts
            .AsNoTracking()
            .Where(x => x.IsApplicationSubmitted && x.IsPaymentCompleted);

        if (!string.IsNullOrWhiteSpace(shift))
        {
            query = query.Where(x => x.Shift == shift);
        }

        return await query.ToListAsync(cancellationToken);
    }
}

