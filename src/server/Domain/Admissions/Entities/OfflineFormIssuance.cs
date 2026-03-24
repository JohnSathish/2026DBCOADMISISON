namespace ERP.Domain.Admissions.Entities;

/// <summary>
/// Records a paper form + fee issued before an applicant account exists.
/// <see cref="ApplicantAccountId"/> is set when the form is received and the portal account is created.
/// </summary>
public class OfflineFormIssuance
{
    public Guid Id { get; set; }

    public string FormNumber { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public decimal ApplicationFeeAmount { get; set; }

    /// <summary>Canonical shift: ShiftI, ShiftII, or ShiftIII (set at issuance).</summary>
    public string Shift { get; set; } = string.Empty;

    /// <summary>Whether the student had applied for CUET at issuance.</summary>
    public bool CuetApplied { get; set; }

    public DateTime IssuedOnUtc { get; set; }

    /// <summary>When the corresponding <see cref="StudentApplicantAccount"/> is created at receive.</summary>
    public Guid? ApplicantAccountId { get; set; }
}
