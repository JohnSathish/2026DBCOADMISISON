using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Options;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Admissions;

/// <summary>
/// Post-selection admission fee (selected candidates). Configured via <see cref="AdmissionsWorkflowOptions.PostSelectionAdmissionFeeAmount"/>.
/// The online <em>application</em> fee is separate (Razorpay <c>ApplicationFeeAmount</c>).
/// </summary>
public sealed class AdmissionFeeService : IAdmissionFeeService
{
    private readonly decimal _postSelectionAdmissionFee;

    public AdmissionFeeService(IOptions<AdmissionsWorkflowOptions> admissionsOptions)
    {
        _postSelectionAdmissionFee = admissionsOptions.Value.PostSelectionAdmissionFeeAmount;
    }

    public decimal GetAdmissionFee(string? majorSubject)
    {
        _ = majorSubject;
        return _postSelectionAdmissionFee;
    }

    public bool IsPaymentAmountValid(decimal? paidAmount, string? majorSubject)
    {
        if (!paidAmount.HasValue || paidAmount.Value <= 0)
        {
            return false;
        }

        var requiredFee = GetAdmissionFee(majorSubject);
        return paidAmount.Value >= requiredFee;
    }
}


