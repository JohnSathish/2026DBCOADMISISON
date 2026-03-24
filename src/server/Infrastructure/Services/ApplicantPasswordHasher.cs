using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using Microsoft.AspNetCore.Identity;

namespace ERP.Infrastructure.Services;

public class ApplicantPasswordHasher : IApplicantPasswordHasher
{
    private readonly IPasswordHasher<StudentApplicantAccount> _passwordHasher;

    public ApplicantPasswordHasher(IPasswordHasher<StudentApplicantAccount> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public string HashPassword(StudentApplicantAccount account, string password)
    {
        return _passwordHasher.HashPassword(account, password);
    }

    public bool VerifyPassword(StudentApplicantAccount account, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(account, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}

