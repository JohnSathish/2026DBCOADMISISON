namespace ERP.Application.Admissions.Options;

public sealed class AdmissionsWorkflowOptions
{
    public const string SectionName = "Admissions";

    /// <summary>
    /// Default minimum Class XII % when no row exists in <c>admissions.AdmissionWorkflowSettings</c>
    /// (admin can set the live value via PUT /api/admissions/admission-workflow-settings).
    /// </summary>
    public decimal DirectAdmissionCutoffPercentage { get; set; } = 75m;

    /// <summary>Base URL for applicant portal (e.g. https://host:4200) for payment links.</summary>
    public string ApplicantPortalBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Fee for candidates <em>selected for admission</em> (separate from the online application fee).
    /// Minimum ₹9,500 until management publishes the final amount (may be higher).
    /// </summary>
    public decimal PostSelectionAdmissionFeeAmount { get; set; } = 9500m;
}
