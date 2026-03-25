using MediatR;

namespace ERP.Application.Admissions.Commands.CreateDirectAdmissionOffers;

public sealed record CreateDirectAdmissionOffersCommand(
    decimal MinimumPercentage = 60.0m,
    decimal AdmissionFeeAmount = 9500.0m,
    DateTime? ExpiryDate = null,
    string? CreatedBy = null) : IRequest<CreateDirectAdmissionOffersResult>;

public sealed record CreateDirectAdmissionOffersResult(
    int TotalOffersCreated,
    List<Guid> CreatedOfferIds,
    List<string> Errors);




