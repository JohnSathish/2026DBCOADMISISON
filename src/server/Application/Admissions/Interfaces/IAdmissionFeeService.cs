namespace ERP.Application.Admissions.Interfaces;

public interface IAdmissionFeeService
{
    /// <summary>
    /// Post-selection <strong>admission fee</strong> (for candidates who are offered / selected for admission).
    /// This is not the online application form fee (Razorpay <c>ApplicationFeeAmount</c> / applicant portal).
    /// </summary>
    /// <param name="majorSubject">Reserved for future stream-specific rules; currently ignored.</param>
    decimal GetAdmissionFee(string? majorSubject);

    /// <summary>
    /// Whether the paid amount meets the required post-selection admission fee.
    /// </summary>
    bool IsPaymentAmountValid(decimal? paidAmount, string? majorSubject);
}


