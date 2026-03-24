using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ERP.Application.Admissions.Commands.EnrollApplication;
using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using ERP.Infrastructure.Payments;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Applicant")]
public class PaymentsController(
    IRazorpayService razorpayService,
    IApplicantAccountRepository accountRepository,
    IOptions<RazorpaySettings> razorpaySettings,
    IMediator mediator,
    IApplicantNotificationService notificationService,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost("create-order")]
    public async Task<ActionResult<CreateOrderResponse>> CreateOrder(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetAccountId(out var accountId))
            {
                return Unauthorized(new { message = "Unable to identify user account." });
            }

            var account = await accountRepository.GetByIdForUpdateAsync(accountId, cancellationToken);
            if (account == null)
            {
                return NotFound(new { message = "Applicant account not found." });
            }

            if (!account.IsApplicationSubmitted)
            {
                return BadRequest(new { message = "Application must be submitted before payment." });
            }

            if (account.IsPaymentCompleted)
            {
                return BadRequest(new { message = "Payment has already been completed." });
            }

            // Validate Razorpay settings
            if (razorpaySettings.Value == null)
            {
                return StatusCode(500, new { message = "Razorpay settings are not configured." });
            }

            if (string.IsNullOrWhiteSpace(razorpaySettings.Value.KeyId) || string.IsNullOrWhiteSpace(razorpaySettings.Value.KeySecret))
            {
                return StatusCode(500, new { message = "Razorpay credentials are not configured properly." });
            }

            var amount = razorpaySettings.Value.ApplicationFeeAmount;
            var receipt = $"APP-{account.UniqueId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var notes = new Dictionary<string, string>
            {
                { "applicationNumber", account.UniqueId },
                { "applicantName", account.FullName },
                { "email", account.Email }
            };

            var order = await razorpayService.CreateOrderAsync(amount, "INR", receipt, notes, cancellationToken);
            
            // Save order ID to account
            account.SetPaymentOrderId(order.Id);
            await accountRepository.UpdateAsync(account, cancellationToken);

            return Ok(new CreateOrderResponse
            {
                OrderId = order.Id,
                Amount = amount,
                Currency = order.Currency,
                KeyId = razorpaySettings.Value.KeyId,
                CheckoutLogoUrl = string.IsNullOrWhiteSpace(razorpaySettings.Value.CheckoutLogoUrl)
                    ? null
                    : razorpaySettings.Value.CheckoutLogoUrl.Trim()
            });
        }
        catch (Exception ex)
        {
            // Log the full exception for debugging
            logger.LogError(ex, "Failed to create payment order. AccountId: {AccountId}", 
                TryGetAccountId(out var accountId) ? accountId.ToString() : "Unknown");
            
            return StatusCode(500, new { 
                message = "Failed to create payment order", 
                error = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    [HttpPost("verify")]
    public async Task<ActionResult<VerifyPaymentResponse>> VerifyPayment(
        [FromBody] VerifyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        var account = await accountRepository.GetByIdForUpdateAsync(accountId, cancellationToken);
        if (account == null)
        {
            return NotFound("Applicant account not found.");
        }

        if (account.PaymentOrderId != request.OrderId)
        {
            return BadRequest("Order ID does not match.");
        }

        if (account.IsPaymentCompleted)
        {
            return Ok(new VerifyPaymentResponse
            {
                Success = true,
                Message = "Payment already verified."
            });
        }

        try
        {
            var isValid = await razorpayService.VerifyPaymentAsync(
                request.OrderId,
                request.PaymentId,
                request.Signature,
                cancellationToken);

            if (!isValid)
            {
                return BadRequest(new VerifyPaymentResponse
                {
                    Success = false,
                    Message = "Payment verification failed. Invalid signature."
                });
            }

            // Mark payment as completed
            var amount = razorpaySettings.Value.ApplicationFeeAmount;
            account.MarkPaymentCompleted(request.PaymentId, amount);
            await accountRepository.UpdateAsync(account, cancellationToken);

            try
            {
                await notificationService.SendApplicationFeePaidConfirmationAsync(
                    account.FullName,
                    account.Email,
                    account.UniqueId,
                    amount,
                    request.PaymentId,
                    account.PaymentCompletedOnUtc ?? DateTime.UtcNow,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to send application fee payment confirmation email for account {AccountId}.",
                    account.Id);
            }

            // Auto-enroll if this is a direct admission offer (status is Approved)
            if (account.Status == ApplicationStatus.Approved)
            {
                try
                {
                    var enrollCommand = new EnrollApplicationCommand(
                        account.Id,
                        "System (Auto-enrollment after payment)",
                        "Automatic enrollment after admission fee payment completion.",
                        true);

                    var enrollResult = await mediator.Send(enrollCommand, cancellationToken);
                    logger.LogInformation(
                        "Auto-enrolled applicant {ApplicationNumber} (AccountId: {AccountId}) after payment completion.",
                        enrollResult.ApplicationNumber,
                        account.Id);
                }
                catch (Exception ex)
                {
                    // Log error but don't fail payment verification
                    logger.LogError(
                        ex,
                        "Failed to auto-enroll applicant {ApplicationNumber} (AccountId: {AccountId}) after payment completion. Error: {ErrorMessage}",
                        account.UniqueId,
                        account.Id,
                        ex.Message);
                }
            }

            return Ok(new VerifyPaymentResponse
            {
                Success = true,
                Message = "Payment verified successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new VerifyPaymentResponse
            {
                Success = false,
                Message = $"Payment verification failed: {ex.Message}"
            });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<PaymentStatusResponse>> GetPaymentStatus(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account == null)
        {
            return NotFound("Applicant account not found.");
        }

        return Ok(new PaymentStatusResponse
        {
            IsApplicationSubmitted = account.IsApplicationSubmitted,
            IsPaymentCompleted = account.IsPaymentCompleted,
            ApplicationReference = account.UniqueId,
            PaymentOrderId = account.PaymentOrderId,
            PaymentTransactionId = account.PaymentTransactionId,
            PaymentAmount = account.PaymentAmount,
            PaymentCompletedOnUtc = account.PaymentCompletedOnUtc
        });
    }

    private bool TryGetAccountId(out Guid accountId)
    {
        var accountIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(accountIdClaim, out accountId);
    }
}

public class CreateOrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Optional absolute URL for Razorpay checkout <c>image</c> (college logo).</summary>
    public string? CheckoutLogoUrl { get; set; }
}

public class VerifyPaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class VerifyPaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PaymentStatusResponse
{
    public bool IsApplicationSubmitted { get; set; }
    public bool IsPaymentCompleted { get; set; }

    /// <summary>Applicant application number (same as UniqueId).</summary>
    public string? ApplicationReference { get; set; }

    public string? PaymentOrderId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public decimal? PaymentAmount { get; set; }
    public DateTime? PaymentCompletedOnUtc { get; set; }
}

