namespace ERP.Application.Admissions;

/// <summary>Canonical shift codes (aligned with applicant portal / courses form).</summary>
public static class OfflineAdmissionShift
{
    public const string ShiftI = "ShiftI";
    public const string ShiftII = "ShiftII";
    public const string ShiftIII = "ShiftIII";

    /// <summary>Normalizes API/UI values to ShiftI / ShiftII / ShiftIII.</summary>
    public static string? TryNormalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        return s.ToUpperInvariant() switch
        {
            "SHIFTI" or "SHIFT-I" or "SHIFT_1" => ShiftI,
            "SHIFTII" or "SHIFT-II" or "SHIFT_2" => ShiftII,
            "SHIFTIII" or "SHIFT-III" or "SHIFT_3" => ShiftIII,
            _ => null,
        };
    }

    public static string ToDisplayLabel(string code)
    {
        return code switch
        {
            ShiftI => "Shift-I",
            ShiftII => "Shift-II",
            ShiftIII => "Shift-III",
            _ => code,
        };
    }
}
