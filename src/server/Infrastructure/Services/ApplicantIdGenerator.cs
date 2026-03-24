using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class ApplicantIdGenerator : IApplicantIdGenerator
{
    private readonly ApplicationDbContext _context;

    public ApplicantIdGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        // Admissions year 2026 application series (was DBCT25 for 2025).
        var prefix = "DBCT26";

        // Find the highest existing number for this prefix
        var existingIds = await _context.StudentApplicantAccounts
            .AsNoTracking()
            .Where(x => x.UniqueId.StartsWith(prefix + "-"))
            .Select(x => x.UniqueId)
            .ToListAsync(cancellationToken);

        int nextNumber = 1;
        if (existingIds.Any())
        {
            var numbers = existingIds
                .Select(id => id.Replace(prefix + "-", ""))
                .Where(num => int.TryParse(num, out _))
                .Select(int.Parse)
                .ToList();
            
            if (numbers.Any())
            {
                nextNumber = numbers.Max() + 1;
            }
        }

        string candidate;
        var exists = true;
        do
        {
            candidate = $"{prefix}-{nextNumber:D4}"; // e.g. DBCT26-0001, DBCT26-0002
            exists = await _context.StudentApplicantAccounts
                .AsNoTracking()
                .AnyAsync(x => x.UniqueId == candidate, cancellationToken);
            
            if (exists)
            {
                nextNumber++;
            }
        } while (exists);

        return candidate;
    }
}

