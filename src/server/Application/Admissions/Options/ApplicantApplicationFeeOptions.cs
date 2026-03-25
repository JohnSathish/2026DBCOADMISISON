namespace ERP.Application.Admissions.Options;

/// <summary>
/// Binds the <c>Razorpay</c> section for <see cref="ApplicationFeeAmount"/> — the <strong>online application form fee</strong> (INR),
/// distinct from <see cref="AdmissionsWorkflowOptions.PostSelectionAdmissionFeeAmount"/> (payable only if selected for admission).
/// </summary>
public sealed class ApplicantApplicationFeeOptions
{
    public const string ConfigurationSectionName = "Razorpay";

    public decimal ApplicationFeeAmount { get; set; } = 700m;
}
