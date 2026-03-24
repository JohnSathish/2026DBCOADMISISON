using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ERP.Application.Admissions.Commands.ChangeStudentApplicantPassword;
using ERP.Application.Admissions.Commands.LoginStudentApplicant;
using ERP.Application.Admissions.Commands.RefreshStudentApplicantToken;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/auth/applicants")]
public class ApplicantAuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginStudentApplicantResponse>> Login(
        [FromBody] LoginStudentApplicantRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            request.Password is null)
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        try
        {
            var command = new LoginStudentApplicantCommand(request.Username.Trim(), request.Password);
            var result = await mediator.Send(command, cancellationToken);

            return Ok(MapToResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginStudentApplicantResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new RefreshStudentApplicantTokenCommand(request.RefreshToken);
            var result = await mediator.Send(command, cancellationToken);

            return Ok(MapToResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Applicant")]
    [HttpPost("change-password")]
    public async Task<ActionResult<LoginStudentApplicantResponse>> ChangePassword(
        [FromBody] ChangeApplicantPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(accountIdClaim, out var accountId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new ChangeStudentApplicantPasswordCommand(accountId, request.CurrentPassword, request.NewPassword);
            var result = await mediator.Send(command, cancellationToken);

            return Ok(MapToResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    private static LoginStudentApplicantResponse MapToResponse(LoginStudentApplicantResult result) =>
        new(
            result.Token,
            result.ExpiresAtUtc,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc,
            result.UniqueId,
            result.Email,
            result.FullName,
            result.MustChangePassword);
}

public sealed record LoginStudentApplicantRequest(string Username, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record ChangeApplicantPasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LoginStudentApplicantResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string UniqueId,
    string Email,
    string FullName,
    bool MustChangePassword);

