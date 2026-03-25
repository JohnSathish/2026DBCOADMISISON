namespace ERP.Application.Admissions;

/// <summary>
/// Preferred shift for new applications (online draft/submit and offline form issuance).
/// Shift III is retained in <see cref="OfflineAdmissionShift"/> only for legacy data display.
/// </summary>
public static class AdmissionPreferredShiftPolicy
{
    /// <summary>Throws if shift is Shift III (canonical). Other legacy labels are not blocked here.</summary>
    public static void EnsureAllowedForNewApplicationOrThrow(string? shift)
    {
        if (string.IsNullOrWhiteSpace(shift))
        {
            return;
        }

        var normalized = OfflineAdmissionShift.TryNormalize(shift) ?? shift.Trim();
        if (normalized == OfflineAdmissionShift.ShiftIII)
        {
            throw new InvalidOperationException(
                "Shift III is no longer offered. Please select Shift I or Shift II.");
        }
    }
}
