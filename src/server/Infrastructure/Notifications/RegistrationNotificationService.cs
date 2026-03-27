using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Options;
using ERP.Domain.Admissions.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Notifications;

public class RegistrationNotificationService : IApplicantNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly SmsSettings _smsSettings;
    private readonly EmailSettings _emailSettings;
    private readonly AdmissionsWorkflowOptions _admissions;
    private readonly ILogger<RegistrationNotificationService> _logger;

    public RegistrationNotificationService(
        HttpClient httpClient,
        IOptions<SmsSettings> smsOptions,
        IOptions<EmailSettings> emailOptions,
        IOptions<AdmissionsWorkflowOptions> admissionsOptions,
        ILogger<RegistrationNotificationService> logger)
    {
        _httpClient = httpClient;
        _smsSettings = smsOptions.Value;
        _emailSettings = emailOptions.Value;
        _admissions = admissionsOptions.Value;
        _logger = logger;
    }

    /// <summary>Configured applicant portal origin (no trailing slash), e.g. https://admissionsdbctura.com</summary>
    private string ApplicantBaseUrl => _admissions.ApplicantPortalBaseUrl.TrimEnd('/');

    /// <summary>Login page for emails/SMS, e.g. https://admissionsdbctura.com/login</summary>
    private string ApplicantLoginUrl => $"{ApplicantBaseUrl}/login";

    public async Task SendRegistrationNotificationsAsync(
        string fullName,
        string uniqueId,
        string email,
        string mobileNumber,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        await SendSmsAsync(fullName, uniqueId, email, mobileNumber, temporaryPassword, cancellationToken);
        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Admission Registration",
            BuildRegistrationEmailBody(fullName, uniqueId, email, temporaryPassword),
            cancellationToken);
    }

    public Task SendPasswordChangeNotificationAsync(
        string fullName,
        string email,
        CancellationToken cancellationToken = default)
    {
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .alert {{ background: #d1ecf1; border: 1px solid #bee5eb; padding: 15px; margin: 20px 0; border-radius: 4px; color: #0c5460; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Password Changed</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>Your password for the Don Bosco College Tura admissions portal has been changed successfully.</p>
            
            <div class=""alert"">
                <strong>Security Notice:</strong> If you did not initiate this password change, please contact the admissions office immediately.
            </div>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
        </div>
    </div>
</body>
</html>";

        return SendEmailAsync(
            email,
            "Don Bosco College Tura - Password Changed",
            body,
            cancellationToken);
    }

    public async Task SendPasswordResetNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        string temporaryPassword,
        string resetBy,
        CancellationToken cancellationToken = default)
    {
        var loginUrl = ApplicantLoginUrl;
        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .password-box {{ background: #fff3cd; border: 2px solid #ffc107; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .password-value {{ font-size: 32px; font-weight: bold; color: #d9534f; margin: 10px 0; letter-spacing: 4px; }}
        .alert {{ background: #d1ecf1; border: 1px solid #bee5eb; padding: 15px; margin: 20px 0; border-radius: 4px; color: #0c5460; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; color: #856404; }}
        .button {{ display: inline-block; background: #007bff; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🔐 Password Reset - Don Bosco College Tura</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>Your password for the Don Bosco College Tura admissions portal has been reset by <strong>{resetBy}</strong>.</p>
            
            <div class=""password-box"">
                <p><strong>Your New Temporary Password:</strong></p>
                <div class=""password-value"">{temporaryPassword}</div>
                <p>Please use this password to log in and change it to a secure password of your choice.</p>
            </div>

            <div class=""warning"">
                <strong>⚠️ Important:</strong> For security reasons, please change this temporary password immediately after logging in.
            </div>

            <div style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">Log In Now</a>
            </div>

            <p><strong>Login Instructions:</strong></p>
            <ol>
                <li>Go to: <a href=""{loginUrl}"">{loginUrl}</a></li>
                <li>Enter your Application Number: <strong>{applicationNumber}</strong></li>
                <li>Enter your temporary password: <strong>{temporaryPassword}</strong></li>
                <li>After logging in, you will be prompted to change your password</li>
            </ol>

            <div class=""alert"">
                <strong>Security Notice:</strong> If you did not request this password reset, please contact the admissions office immediately at your earliest convenience.
            </div>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Password Reset",
            emailBody,
            cancellationToken);

        // Send SMS notification
        var smsMessage = $"Your password for Don Bosco College Tura admissions portal has been reset. New password: {temporaryPassword}. Login: {loginUrl} Application: {applicationNumber}";

        try
        {
            var requestUri = BuildSmsUri(mobileNumber, smsMessage);
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to send password reset SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                    applicationNumber,
                    response.StatusCode,
                    content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending password reset SMS for application {ApplicationNumber}.",
                applicationNumber);
        }
    }

    public async Task SendStatusUpdateNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        ApplicationStatus status,
        string? remarks,
        DateTime? entranceExamScheduledOnUtc,
        string? entranceExamVenue,
        string? entranceExamInstructions,
        string? majorSubject = null,
        DateTime? paymentDeadlineUtc = null,
        CancellationToken cancellationToken = default)
    {
        var emailBody = BuildStatusEmailBody(
            fullName,
            applicationNumber,
            status,
            remarks,
            entranceExamScheduledOnUtc,
            entranceExamVenue,
            entranceExamInstructions,
            majorSubject,
            paymentDeadlineUtc);

        await SendEmailAsync(
            email,
            $"Don Bosco College Tura - Application Status {status}",
            emailBody,
            cancellationToken);

        await SendStatusSmsAsync(
            mobileNumber,
            applicationNumber,
            status,
            entranceExamScheduledOnUtc,
            entranceExamVenue,
            majorSubject,
            paymentDeadlineUtc,
            cancellationToken);
    }

    public Task SendApplicationSubmissionNotificationAsync(
        string fullName,
        string email,
        string applicationNumber,
        byte[] pdfContent,
        string pdfFileName,
        CancellationToken cancellationToken = default)
    {
        var loginUrl = ApplicantLoginUrl;

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .success-box {{ background: #d4edda; border: 1px solid #c3e6cb; padding: 20px; margin: 20px 0; border-radius: 4px; color: #155724; }}
        .app-number {{ font-size: 18px; font-weight: bold; color: #1b5e9d; text-align: center; padding: 15px; background: white; border-radius: 4px; margin: 20px 0; }}
        .button {{ display: inline-block; background: #1b5e9d; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .info-box {{ background: #e7f3ff; border-left: 4px solid #1b5e9d; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Application Submitted Successfully</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <div class=""success-box"">
                <strong>✓ Your admission application has been submitted successfully!</strong>
            </div>
            
            <div class=""app-number"">
                Application Number: {applicationNumber}
            </div>
            
            <p>Please find the attached PDF summary of your application for your records.</p>
            
            <div class=""info-box"">
                <strong>Next Steps:</strong>
                <ul>
                    <li>You can log in to the admissions portal at any time to review your application status</li>
                    <li>Complete the payment process when the payment link becomes available</li>
                    <li>You will be notified via email when your application status changes</li>
                </ul>
            </div>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">View Application Status</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        var attachments = new[]
        {
            (pdfFileName, pdfContent, "application/pdf")
        };

        return SendEmailAsync(
            email,
            "Don Bosco College Tura - Application Submitted Successfully",
            body,
            cancellationToken,
            attachments);
    }

    public Task SendApplicationFeePaidConfirmationAsync(
        string fullName,
        string email,
        string applicationNumber,
        decimal amountPaid,
        string transactionId,
        DateTime paidOnUtc,
        CancellationToken cancellationToken = default)
    {
        var paidText = paidOnUtc.ToString("dd MMMM yyyy, hh:mm tt 'UTC'", CultureInfo.InvariantCulture);
        var dashboardUrl = $"{ApplicantBaseUrl}/app/dashboard";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #166534 0%, #15803d 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border: 1px solid #e5e7eb; }}
        .success {{ background: #ecfdf5; border: 2px solid #22c55e; padding: 20px; margin: 20px 0; border-radius: 8px; color: #14532d; }}
        .ref {{ font-size: 18px; font-weight: bold; color: #1b5e9d; text-align: center; padding: 12px; background: white; border-radius: 6px; margin: 16px 0; }}
        .detail {{ background: white; border-left: 4px solid #22c55e; padding: 14px 16px; margin: 16px 0; border-radius: 4px; }}
        .button {{ display: inline-block; background: #1b5e9d; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 16px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Payment successful</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            <div class=""success"">
                <strong>Your admission application fee has been received.</strong><br/>
                Your application is submitted and will be reviewed by the admissions office.
            </div>
            <div class=""ref"">Application reference: {applicationNumber}</div>
            <div class=""detail"">
                <p><strong>Amount paid:</strong> ₹{amountPaid:N2}</p>
                <p><strong>Transaction ID:</strong> {transactionId}</p>
                <p><strong>Paid on:</strong> {paidText}</p>
            </div>
            <p><strong>Next steps</strong></p>
            <ul>
                <li>We will review your application and notify you about the admission status.</li>
                <li>Please check this email and your registered inbox regularly for updates.</li>
                <li>You can download your application form from the admissions portal after payment.</li>
            </ul>
            <p style=""text-align:center;"">
                <a href=""{dashboardUrl}"" class=""button"">Open dashboard</a>
            </p>
            <p>Best regards,<br/><strong>Admissions Office</strong><br/>Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        return SendEmailAsync(
            email,
            "Don Bosco College Tura - Payment received & application complete",
            body,
            cancellationToken);
    }

    public async Task SendEnrollmentNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        DateTime enrolledOnUtc,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var enrollmentDate = enrolledOnUtc.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var loginUrl = ApplicantLoginUrl;
        var remarksHtml = !string.IsNullOrWhiteSpace(remarks)
            ? $@"<p><strong>Remarks:</strong> {remarks.Trim().Replace("\n", "<br>")}</p>"
            : string.Empty;

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .success-box {{ background: #d4edda; border: 2px solid #28a745; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .success-box h2 {{ margin: 0; color: #155724; font-size: 24px; }}
        .info-box {{ background: white; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .button {{ display: inline-block; background: #28a745; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎓 Welcome to Don Bosco College Tura!</h1>
            <p>Enrollment Confirmation</p>
        </div>
        <div class=""content"">
            <div class=""success-box"">
                <h2>Congratulations on Your Enrollment!</h2>
            </div>
            
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>We are delighted to inform you that you have been successfully <strong>ENROLLED</strong> at <strong>Don Bosco College Tura</strong>.</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Enrollment Date:</strong> {enrollmentDate}</p>
                {remarksHtml}
            </div>
            
            <p>You are now officially a student of Don Bosco College Tura. We look forward to supporting you throughout your academic journey.</p>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">Access Student Portal</a>
            </p>
            
            <p>If you have any questions or need assistance, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Enrollment Confirmation",
            emailBody,
            cancellationToken);

        // Send SMS notification
        if (_smsSettings.Enabled && !string.IsNullOrWhiteSpace(mobileNumber))
        {
            var smsMessage = new StringBuilder()
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DBCT: Dear {0}, Congratulations! You are successfully ENROLLED at Don Bosco College Tura. ", fullName)
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Application No: {0}. Enrollment Date: {1}. ", applicationNumber, enrollmentDate)
                .Append("Welcome to DBCT! -Admission Committee, DBCT")
                .ToString();

            try
            {
                var requestUri = BuildSmsUri(mobileNumber, smsMessage);
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to send enrollment SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                        applicationNumber,
                        response.StatusCode,
                        content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception while sending enrollment SMS for application {ApplicationNumber}.",
                    applicationNumber);
            }
        }
    }

    private async Task SendSmsAsync(
        string fullName,
        string uniqueId,
        string email,
        string mobileNumber,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        if (!_smsSettings.Enabled)
        {
            _logger.LogInformation("SMS notifications disabled. Skipping message for applicant {UniqueId}.", uniqueId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_smsSettings.BaseUrl))
        {
            _logger.LogWarning("SMS base URL not configured. Skipping SMS notification for applicant {UniqueId}.", uniqueId);
            return;
        }

        var loginUrl = string.IsNullOrWhiteSpace(_smsSettings.LoginUrl)
            ? ApplicantLoginUrl
            : _smsSettings.LoginUrl.Trim();

        // Use registered template content if provided, otherwise use default message
        string message;
        if (!string.IsNullOrWhiteSpace(_smsSettings.TemplateContent))
        {
            // DLT Template variables are replaced in order:
            // 1. Application Number (uniqueId)
            // 2. Login URL (loginUrl)
            // 3. Username/Email (email)
            // 4. Temporary Password (temporaryPassword)
            
            // Replace {#var#} placeholders sequentially (order: uniqueId, loginUrl, email, temporaryPassword)
            var template = _smsSettings.TemplateContent;
            var parts = template.Split(new[] { "{#var#}" }, StringSplitOptions.None);
            
            if (parts.Length == 5) // 4 variables = 5 parts
            {
                // Variables in order: 1. Application Number, 2. Login URL, 3. Username/Email, 4. Password
                message = parts[0] + uniqueId + 
                         parts[1] + loginUrl + 
                         parts[2] + email + 
                         parts[3] + temporaryPassword + 
                         parts[4];
            }
            else
            {
                // Fallback: Replace sequentially using index-based replacement
                message = template;
                var values = new[] { uniqueId, loginUrl, email, temporaryPassword };
                for (int i = 0; i < values.Length; i++)
                {
                    var index = message.IndexOf("{#var#}", StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        message = message.Substring(0, index) + values[i] + message.Substring(index + 7);
                    }
                }
            }
            
            _logger.LogInformation(
                "Using DLT template. Template ID: {TemplateId}, Variables: UniqueId={UniqueId}, LoginUrl={LoginUrl}, Email={Email}",
                _smsSettings.DltTemplateId,
                uniqueId,
                loginUrl,
                email);
        }
        else
        {
            // Default message format (fallback)
            message = new StringBuilder()
                .Append("Dear Applicant, Your Registration for Admission at Don Bosco College Tura is Successful. ")
                .AppendFormat(CultureInfo.InvariantCulture, "Your Application No is {0}. ", uniqueId)
                .AppendFormat(CultureInfo.InvariantCulture, "You can login using the Link {0} with Username {1} and Password {2}. ", loginUrl, email, temporaryPassword)
                .Append("Thank You. Management-DBCTURA")
                .ToString();
        }

        try
        {
            var requestUri = BuildSmsUri(mobileNumber, message);
            _logger.LogInformation(
                "Sending SMS to {MobileNumber}. Request URI: {RequestUri}",
                mobileNumber,
                requestUri.ToString().Replace(_smsSettings.Password, "****"));
            
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "SMS provider response for applicant {UniqueId}. Status: {StatusCode}, Response: {Response}",
                uniqueId,
                response.StatusCode,
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"SMS provider returned error status {response.StatusCode}: {content}";
                _logger.LogError(
                    "Failed to send SMS notification for applicant {UniqueId}. {ErrorMessage}",
                    uniqueId,
                    errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Check if provider response indicates failure (common patterns: "error", "fail", "invalid")
            var responseLower = content.ToLowerInvariant();
            if (responseLower.Contains("error") || 
                responseLower.Contains("fail") || 
                responseLower.Contains("invalid") ||
                responseLower.Contains("denied"))
            {
                var errorMsg = $"SMS provider returned error: {content}";
                _logger.LogError(
                    "SMS provider error for applicant {UniqueId}. Response: {Response}",
                    uniqueId,
                    content);
                throw new InvalidOperationException(errorMsg);
            }

            _logger.LogInformation(
                "SMS notification sent successfully for applicant {UniqueId}. Provider response: {Response}",
                uniqueId,
                content);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending SMS notification for applicant {UniqueId}.",
                uniqueId);
            throw; // Re-throw to allow callers to handle the error
        }
    }

    private async Task SendStatusSmsAsync(
        string mobileNumber,
        string applicationNumber,
        ApplicationStatus status,
        DateTime? entranceExamScheduledOnUtc,
        string? entranceExamVenue,
        string? majorSubject = null,
        DateTime? paymentDeadlineUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!_smsSettings.Enabled)
        {
            _logger.LogInformation(
                "SMS notifications disabled. Skipping status update for application {ApplicationNumber}.",
                applicationNumber);
            return;
        }

        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            _logger.LogWarning(
                "No mobile number available for application {ApplicationNumber}. Skipping status SMS.",
                applicationNumber);
            return;
        }

        string message;
        
        if (status == ApplicationStatus.Approved)
        {
            var majorSubjectText = !string.IsNullOrWhiteSpace(majorSubject) ? majorSubject : "your selected program";
            var paymentDeadlineText = paymentDeadlineUtc.HasValue 
                ? paymentDeadlineUtc.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "the specified date";
            
            message = new StringBuilder()
                .AppendFormat(CultureInfo.InvariantCulture, "DBCT: Dear Candidate, Congratulations! You are ADMITTED to DON BOSCO COLLEGE TURA in {0}. ", majorSubjectText)
                .AppendFormat(CultureInfo.InvariantCulture, "Kindly pay the admission fee by {0}. If not, you will forfeit your seat. ", paymentDeadlineText)
                .Append("Please see the guidelines for payment on the college website. -Admission Committee, DBCT")
                .ToString();
        }
        else
        {
            message = new StringBuilder()
                .AppendFormat(CultureInfo.InvariantCulture, "DBCT: Application {0} status updated to {1}. ", applicationNumber, status)
                .ToString();

            if (status == ApplicationStatus.EntranceExam && entranceExamScheduledOnUtc is not null)
            {
                message = new StringBuilder(message)
                    .AppendFormat(
                        CultureInfo.InvariantCulture,
                        "Entrance exam on {0:dd MMM yyyy HH:mm} UTC",
                        entranceExamScheduledOnUtc.Value)
                    .ToString();

                if (!string.IsNullOrWhiteSpace(entranceExamVenue))
                {
                    message += $" at {entranceExamVenue}.";
                }
            }
        }

        try
        {
            var requestUri = BuildSmsUri(mobileNumber, message);
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to send status SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                    applicationNumber,
                    response.StatusCode,
                    content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending status SMS for application {ApplicationNumber}.",
                applicationNumber);
        }
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken,
        IEnumerable<(string FileName, byte[] Content, string ContentType)>? attachments = null)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email notifications disabled. Skipping email to {Email}.", toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) || string.IsNullOrWhiteSpace(_emailSettings.FromAddress))
        {
            _logger.LogWarning("Email settings incomplete. Skipping email to {Email}.", toEmail);
            return;
        }

        // Sanitize and validate email address
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Recipient email address is empty. Skipping email send.");
            return;
        }

        // Remove spaces and trim the email address
        var sanitizedEmail = toEmail.Replace(" ", string.Empty).Trim();
        
        // Basic email validation (check for @ symbol)
        if (!sanitizedEmail.Contains("@", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid email address format: {Email} (original: {OriginalEmail}). Skipping email send.", 
                sanitizedEmail, toEmail);
            return;
        }

        try
        {
            // Process password: For Gmail App Passwords, remove spaces if present
            var password = (_emailSettings.Password ?? string.Empty).Trim();
            if (_emailSettings.SmtpHost.Contains("gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                password = password.Replace(" ", string.Empty);
            }
            
            _logger.LogInformation(
                "Attempting to send email via SMTP. Host: {Host}, Port: {Port}, Username: {Username}, UseSsl: {UseSsl}",
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort,
                _emailSettings.Username,
                _emailSettings.UseSsl);

            // Create MimeMessage (MailKit equivalent of MailMessage)
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromAddress));
            message.To.Add(new MailboxAddress(string.Empty, sanitizedEmail));
            message.Subject = subject;
            
            // Add Reply-To address (matching PHP implementation)
            message.ReplyTo.Add(new MailboxAddress("Online Admission", _emailSettings.FromAddress));

            // Create HTML body
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };

            // Add attachments if any
            if (attachments is not null)
            {
                foreach (var (fileName, content, contentType) in attachments)
                {
                    bodyBuilder.Attachments.Add(fileName, content, ContentType.Parse(contentType));
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            
            // Disable SSL certificate validation for development
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            
            // Determine SSL option based on port
            var sslOption = _emailSettings.UseSsl
                ? (_emailSettings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;
            
            _logger.LogInformation(
                "Connecting to SMTP server. Host: {Host}, Port: {Port}, SSL Option: {SslOption}",
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort,
                sslOption);
            
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, sslOption, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
            {
                var authMethods = client.AuthenticationMechanisms;
                if (authMethods.Count == 0)
                {
                    var errorMsg = $"SMTP server does not support authentication. Host: {_emailSettings.SmtpHost}, Port: {_emailSettings.SmtpPort}";
                    _logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
                
                _logger.LogInformation("Authenticating with username: {Username}", _emailSettings.Username);
                await client.AuthenticateAsync(_emailSettings.Username, password, cancellationToken);
                _logger.LogInformation("Authentication successful");
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email notification sent successfully to {Email}.", toEmail);
        }
        catch (MailKit.Net.Smtp.SmtpCommandException smtpEx)
        {
            _logger.LogError(
                smtpEx,
                "SMTP error sending email to {Email}. Status: {StatusCode}, Message: {Message}",
                toEmail,
                smtpEx.StatusCode,
                smtpEx.Message);
            throw; // Re-throw to allow callers to handle the error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to {Email}. Error: {ErrorMessage}", toEmail, ex.Message);
            throw; // Re-throw to allow callers to handle the error
        }
    }

    private string BuildRegistrationEmailBody(string fullName, string uniqueId, string username, string temporaryPassword)
    {
        var loginUrl = ApplicantLoginUrl;
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .credentials {{ background: white; padding: 20px; margin: 20px 0; border-left: 4px solid #1b5e9d; border-radius: 4px; }}
        .credentials strong {{ color: #1b5e9d; }}
        .button {{ display: inline-block; background: #1b5e9d; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; color: #856404; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Don Bosco College Tura</h1>
            <p>Admissions Portal - Registration Confirmation</p>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>Welcome to <strong>Don Bosco College Tura</strong>!</p>
            
            <p>Your registration for the <strong>Four Year Undergraduate Programme (FYUP) Semester I</strong> has been completed successfully.</p>
            
            <div class=""credentials"">
                <h3 style=""margin-top: 0; color: #1b5e9d;"">Your Login Credentials</h3>
                <p><strong>Application Number:</strong> {uniqueId}</p>
                <p><strong>Username:</strong> {username}</p>
                <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
            </div>
            
            <div class=""warning"">
                <strong>⚠️ Important:</strong> Please log in to the admissions portal and change your password immediately for security purposes.
            </div>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">Login to Portal</a>
            </p>
            
            <p>If you have any questions or need assistance, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string BuildStatusEmailBody(
        string fullName,
        string applicationNumber,
        ApplicationStatus status,
        string? remarks,
        DateTime? entranceExamScheduledOnUtc,
        string? entranceExamVenue,
        string? entranceExamInstructions,
        string? majorSubject = null,
        DateTime? paymentDeadlineUtc = null)
    {
        var statusColor = status switch
        {
            ApplicationStatus.Approved => "#28a745",
            ApplicationStatus.Rejected => "#dc3545",
            ApplicationStatus.WaitingList => "#ffc107",
            ApplicationStatus.EntranceExam => "#17a2b8",
            _ => "#6c757d"
        };

        var statusText = status switch
        {
            ApplicationStatus.Approved => "Accepted",
            ApplicationStatus.Rejected => "Rejected",
            ApplicationStatus.WaitingList => "Waitlisted",
            ApplicationStatus.EntranceExam => "Entrance Exam Scheduled",
            _ => "Under Review"
        };

        var loginUrl = ApplicantLoginUrl;

        // Special format for Approved status
        if (status == ApplicationStatus.Approved)
        {
            var majorSubjectText = !string.IsNullOrWhiteSpace(majorSubject) ? majorSubject : "your selected program";
            var paymentDeadlineText = paymentDeadlineUtc.HasValue 
                ? paymentDeadlineUtc.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
                : "the specified date";

            var sb = new StringBuilder($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .congratulations {{ background: #d4edda; border: 2px solid #28a745; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .congratulations h2 {{ margin: 0; color: #155724; font-size: 24px; }}
        .info-box {{ background: white; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .payment-deadline {{ background: #fff3cd; border: 2px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; color: #856404; }}
        .payment-deadline strong {{ color: #d9534f; font-size: 18px; }}
        .button {{ display: inline-block; background: #28a745; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 Congratulations! You Are Admitted</h1>
        </div>
        <div class=""content"">
            <div class=""congratulations"">
                <h2>Congratulations!</h2>
            </div>
            
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>Congratulations! You are <strong>ADMITTED</strong> to <strong>DON BOSCO COLLEGE TURA</strong> in <strong>{majorSubjectText}</strong>.</p>
            
            <div class=""payment-deadline"">
                <p><strong>⚠️ IMPORTANT:</strong> Kindly pay the admission fee by <strong>{paymentDeadlineText}</strong>. If not, you will forfeit your seat.</p>
            </div>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Major Subject:</strong> {majorSubjectText}</p>
                <p><strong>Payment Deadline:</strong> {paymentDeadlineText}</p>
            </div>
            
            <p>Please see the guidelines for payment on the college website.</p>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">View Application & Make Payment</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admission Committee, DBCT</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>");
            return sb.ToString();
        }

        // Standard format for other statuses
        var sbStandard = new StringBuilder($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .status-badge {{ display: inline-block; background: {statusColor}; color: white; padding: 10px 20px; border-radius: 5px; font-weight: bold; margin: 15px 0; }}
        .info-box {{ background: white; border-left: 4px solid {statusColor}; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .exam-details {{ background: #e7f3ff; border: 1px solid #1b5e9d; padding: 20px; margin: 20px 0; border-radius: 4px; }}
        .button {{ display: inline-block; background: #1b5e9d; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Application Status Update</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>Your application status has been updated:</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Status:</strong> <span class=""status-badge"">{statusText}</span></p>
            </div>");

        if (!string.IsNullOrWhiteSpace(remarks))
        {
            sbStandard.Append($@"
            <div class=""info-box"">
                <strong>Remarks:</strong><br>
                {remarks.Trim().Replace("\n", "<br>")}
            </div>");
        }

        if (status == ApplicationStatus.EntranceExam && entranceExamScheduledOnUtc is not null)
        {
            sbStandard.Append($@"
            <div class=""exam-details"">
                <h3 style=""margin-top: 0; color: #1b5e9d;"">Entrance Exam Details</h3>
                <p><strong>Date & Time:</strong> {entranceExamScheduledOnUtc:dddd, MMMM dd, yyyy 'at' HH:mm} UTC</p>");

            if (!string.IsNullOrWhiteSpace(entranceExamVenue))
            {
                sbStandard.Append($@"<p><strong>Venue:</strong> {entranceExamVenue}</p>");
            }

            if (!string.IsNullOrWhiteSpace(entranceExamInstructions))
            {
                sbStandard.Append($@"
                <p><strong>Instructions:</strong></p>
                <p>{entranceExamInstructions.Replace("\n", "<br>")}</p>");
            }

            sbStandard.Append("</div>");
        }

        sbStandard.Append($@"
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">View Application Details</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>");

        return sbStandard.ToString();
    }

    public async Task SendAdmissionOfferNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        int meritRank,
        string shift,
        string majorSubject,
        DateTime offerDate,
        DateTime expiryDate,
        CancellationToken cancellationToken = default)
    {
        var offerDateText = offerDate.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var expiryDateText = expiryDate.ToString("dd MMMM yyyy 'at' HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var daysUntilExpiry = (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays);
        var loginUrl = ApplicantLoginUrl;
        var offerUrl = $"{ApplicantBaseUrl}/app/offer";

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .congratulations {{ background: #d4edda; border: 2px solid #28a745; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .congratulations h2 {{ margin: 0; color: #155724; font-size: 24px; }}
        .info-box {{ background: white; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .expiry-warning {{ background: #fff3cd; border: 2px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; color: #856404; }}
        .expiry-warning strong {{ color: #d9534f; font-size: 18px; }}
        .button {{ display: inline-block; background: #28a745; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 Admission Offer - Don Bosco College Tura</h1>
        </div>
        <div class=""content"">
            <div class=""congratulations"">
                <h2>Congratulations on Your Admission Offer!</h2>
            </div>
            
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>We are pleased to inform you that you have been selected for admission to <strong>Don Bosco College Tura</strong> based on your merit ranking.</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Merit Rank:</strong> #{meritRank}</p>
                <p><strong>Program:</strong> {majorSubject}</p>
                <p><strong>Shift:</strong> {shift}</p>
                <p><strong>Offer Date:</strong> {offerDateText}</p>
            </div>
            
            <div class=""expiry-warning"">
                <p><strong>⚠️ IMPORTANT:</strong> This offer expires on <strong>{expiryDateText}</strong> ({daysUntilExpiry} days remaining).</p>
                <p>Please accept or reject this offer before the expiry date to secure your admission.</p>
            </div>
            
            <p style=""text-align: center;"">
                <a href=""{offerUrl}"" class=""button"">View & Respond to Offer</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Admission Offer",
            emailBody,
            cancellationToken);

        // Send SMS notification
        if (_smsSettings.Enabled && !string.IsNullOrWhiteSpace(mobileNumber))
        {
            var smsMessage = new StringBuilder()
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DBCT: Dear {0}, Congratulations! You have received an admission offer. ", fullName)
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Application No: {0}, Merit Rank: #{1}. ", applicationNumber, meritRank)
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Offer expires on {0}. ", expiryDateText)
                .Append("Please log in to accept/reject. -Admission Committee, DBCT")
                .ToString();

            try
            {
                var requestUri = BuildSmsUri(mobileNumber, smsMessage);
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to send offer SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                        applicationNumber,
                        response.StatusCode,
                        content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception while sending offer SMS for application {ApplicationNumber}.",
                    applicationNumber);
            }
        }
    }

    public async Task SendOfferAcceptedNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        DateTime acceptedOnUtc,
        CancellationToken cancellationToken = default)
    {
        var acceptedDateText = acceptedOnUtc.ToString("dd MMMM yyyy 'at' HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var loginUrl = ApplicantLoginUrl;

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .success-box {{ background: #d4edda; border: 2px solid #28a745; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .success-box h2 {{ margin: 0; color: #155724; font-size: 24px; }}
        .info-box {{ background: white; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .button {{ display: inline-block; background: #28a745; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>✓ Offer Accepted Successfully</h1>
        </div>
        <div class=""content"">
            <div class=""success-box"">
                <h2>Thank You for Accepting Your Admission Offer!</h2>
            </div>
            
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>We have received your acceptance of the admission offer. Your seat has been confirmed.</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Accepted On:</strong> {acceptedDateText}</p>
            </div>
            
            <p>You will receive further instructions via email regarding payment, enrollment, and orientation.</p>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">Access Admissions Portal</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Offer Accepted",
            emailBody,
            cancellationToken);

        // Send SMS notification
        if (_smsSettings.Enabled && !string.IsNullOrWhiteSpace(mobileNumber))
        {
            var smsMessage = new StringBuilder()
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DBCT: Dear {0}, Your admission offer acceptance has been confirmed. ", fullName)
                .AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Application No: {0}. ", applicationNumber)
                .Append("You will receive further instructions via email. -Admission Committee, DBCT")
                .ToString();

            try
            {
                var requestUri = BuildSmsUri(mobileNumber, smsMessage);
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to send offer accepted SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                        applicationNumber,
                        response.StatusCode,
                        content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception while sending offer accepted SMS for application {ApplicationNumber}.",
                    applicationNumber);
            }
        }
    }

    public async Task SendOfferRejectedNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        DateTime rejectedOnUtc,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var rejectedDateText = rejectedOnUtc.ToString("dd MMMM yyyy 'at' HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var reasonHtml = !string.IsNullOrWhiteSpace(reason)
            ? $@"<div class=""info-box"">
                <p><strong>Reason:</strong> {reason.Trim().Replace("\n", "<br>")}</p>
            </div>"
            : string.Empty;

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #6c757d 0%, #5a6268 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .info-box {{ background: white; border-left: 4px solid #6c757d; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Admission Offer Rejected</h1>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>We have received your decision to reject the admission offer.</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Rejected On:</strong> {rejectedDateText}</p>
            </div>
            
            {reasonHtml}
            
            <p>We respect your decision and wish you the best in your future endeavors.</p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            "Don Bosco College Tura - Offer Rejected",
            emailBody,
            cancellationToken);
    }

    public async Task SendAdmissionFeePaymentNotificationAsync(
        string fullName,
        string email,
        string mobileNumber,
        string applicationNumber,
        decimal classXIIPercentage,
        string shift,
        string majorSubject,
        decimal admissionFeeAmount,
        DateTime offerDate,
        DateTime expiryDate,
        CancellationToken cancellationToken = default)
    {
        var offerDateText = offerDate.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var expiryDateText = expiryDate.ToString("dd MMMM yyyy 'at' HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var daysUntilExpiry = (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays);
        var loginUrl = ApplicantLoginUrl;
        var paymentUrl = $"{ApplicantBaseUrl}/app/dashboard";

        var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #007bff 0%, #0056b3 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .congratulations {{ background: #d4edda; border: 2px solid #28a745; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .congratulations h2 {{ margin: 0; color: #155724; font-size: 24px; }}
        .info-box {{ background: white; border-left: 4px solid #007bff; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .fee-box {{ background: #fff3cd; border: 2px solid #ffc107; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }}
        .fee-box h3 {{ margin: 0; color: #856404; font-size: 28px; }}
        .fee-amount {{ font-size: 36px; font-weight: bold; color: #d9534f; margin: 10px 0; }}
        .expiry-warning {{ background: #fff3cd; border: 2px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 4px; color: #856404; }}
        .expiry-warning strong {{ color: #d9534f; font-size: 18px; }}
        .button {{ display: inline-block; background: #007bff; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎓 Direct Admission Offer - Don Bosco College Tura</h1>
        </div>
        <div class=""content"">
            <div class=""congratulations"">
                <h2>Congratulations on Your Direct Admission Offer!</h2>
            </div>
            
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <p>We are pleased to inform you that you have been selected for <strong>Direct Admission</strong> to Don Bosco College Tura based on your outstanding Class XII performance.</p>
            
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Class XII Percentage:</strong> {classXIIPercentage:F2}%</p>
                <p><strong>Shift:</strong> {shift}</p>
                <p><strong>Major Subject:</strong> {majorSubject}</p>
                <p><strong>Offer Date:</strong> {offerDateText}</p>
            </div>

            <div class=""fee-box"">
                <h3>Admission Fee Payment Required</h3>
                <p>To complete your admission, please pay the admission fee:</p>
                <div class=""fee-amount"">₹{admissionFeeAmount:N2}</div>
                <p>Once payment is completed, you will be automatically enrolled as a student.</p>
            </div>

            <div class=""expiry-warning"">
                <p><strong>⚠️ Important:</strong> This offer expires on <strong>{expiryDateText}</strong></p>
                <p>You have <strong>{daysUntilExpiry} day(s)</strong> remaining to complete your payment.</p>
            </div>

            <div style=""text-align: center;"">
                <a href=""{paymentUrl}"" class=""button"">Pay Admission Fee Now</a>
            </div>

            <div class=""info-box"" style=""background: #e7f3ff; border-left: 4px solid #007bff; padding: 15px; margin: 20px 0; border-radius: 4px;"">
                <p><strong>🔑 Access Your Account:</strong></p>
                <p>Login to your admission portal to view your offer and make payment:</p>
                <p style=""text-align: center; margin: 15px 0;"">
                    <a href=""{loginUrl}"" class=""button"" style=""background: #28a745; margin: 10px;"">Login to Portal</a>
                </p>
                <p style=""font-size: 0.9em; color: #666;"">Login URL: <a href=""{loginUrl}"">{loginUrl}</a></p>
            </div>

            <p><strong>Next Steps:</strong></p>
            <ol>
                <li>Click the ""Login to Portal"" button above or visit: <a href=""{loginUrl}"">{loginUrl}</a></li>
                <li>Log in using your registered email and password</li>
                <li>On your dashboard, click the ""Pay Admission Fee"" button (₹{admissionFeeAmount:N2})</li>
                <li>Complete the payment within {daysUntilExpiry} day(s) (before {expiryDateText})</li>
                <li>Upon successful payment, you will be automatically enrolled</li>
                <li>You will receive a confirmation email with your student details</li>
            </ol>

            <p>If you have any questions or need assistance, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(
            email,
            $"Don Bosco College Tura - Direct Admission Offer - Pay ₹{admissionFeeAmount:N2}",
            emailBody,
            cancellationToken);

        // Send SMS notification
        var smsMessage = $"Congratulations {fullName}! You have been selected for Direct Admission to Don Bosco College Tura. Please pay admission fee ₹{admissionFeeAmount:N2} to complete enrollment. Visit: {paymentUrl}";

        try
        {
            var requestUri = BuildSmsUri(mobileNumber, smsMessage);
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to send admission fee payment SMS for application {ApplicationNumber}. Status: {StatusCode}. Response: {Response}",
                    applicationNumber,
                    response.StatusCode,
                    content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending admission fee payment SMS for application {ApplicationNumber}.",
                applicationNumber);
        }
    }

    private Uri BuildSmsUri(string destination, string message)
    {
        // Format: YYYY-MM-DD HH:MM:SS (e.g., "2024-01-15 14:30:00")
        // For immediate sending, use current time
        var scheduledTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        // Build API URL - use base URL as-is (provider may have different endpoint structure)
        var baseUrl = _smsSettings.BaseUrl.TrimEnd('/');
        // If base URL already contains an endpoint (like .aspx), use it as-is
        // Otherwise, append the standard endpoint
        var apiUrl = baseUrl.Contains(".aspx", StringComparison.OrdinalIgnoreCase) ||
                     baseUrl.Contains("/API/", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : $"{baseUrl}/API/SendMsg.aspx";
        
        var queryParams = new Dictionary<string, string>
        {
            ["uname"] = _smsSettings.Username,
            ["pass"] = _smsSettings.Password,
            ["send"] = _smsSettings.Sender,
            ["dest"] = destination,
            ["msg"] = message,
            ["priority"] = _smsSettings.Priority,
            ["schtm"] = scheduledTime
        };

        // Add DLT Template ID if provided (required for DLT compliance)
        if (!string.IsNullOrWhiteSpace(_smsSettings.DltTemplateId))
        {
            queryParams["dltid"] = _smsSettings.DltTemplateId;
            // Some providers use: "templateid", "dlt_template_id", "tid", "dltid"
        }

        // Add CTA ID if provided (required for DLT registered URLs)
        if (!string.IsNullOrWhiteSpace(_smsSettings.CtaId))
        {
            queryParams["ctaid"] = _smsSettings.CtaId;
            // Some providers use: "cta_id", "ctaid", "urlid"
        }
        
        var builder = new UriBuilder(apiUrl);
        var query = string.Join("&", queryParams
            .Where(kvp => kvp.Value is not null && !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}"));
        builder.Query = query;
        return builder.Uri;
    }

    public async Task SendBulkEmailAsync(
        string fullName,
        string email,
        string subject,
        string message,
        CancellationToken cancellationToken = default)
    {
        var emailBody = BuildBulkEmailBody(fullName, message);
        await SendEmailAsync(email, subject, emailBody, cancellationToken);
    }

    public async Task SendBulkSmsAsync(
        string mobileNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!_smsSettings.Enabled)
        {
            _logger.LogInformation(
                "SMS notifications disabled. Skipping bulk SMS to {MobileNumber}.",
                mobileNumber);
            return;
        }

        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            _logger.LogWarning(
                "No mobile number provided. Skipping bulk SMS.");
            return;
        }

        try
        {
            var requestUri = BuildSmsUri(mobileNumber, message);
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to send bulk SMS to {MobileNumber}. Status: {StatusCode}. Response: {Response}",
                    mobileNumber,
                    response.StatusCode,
                    content);
                throw new InvalidOperationException($"SMS provider returned error status {response.StatusCode}: {content}");
            }

            // Check if provider response indicates failure
            var responseLower = content.ToLowerInvariant();
            if (responseLower.Contains("error") ||
                responseLower.Contains("fail") ||
                responseLower.Contains("invalid") ||
                responseLower.Contains("denied"))
            {
                _logger.LogError(
                    "SMS provider error for bulk SMS to {MobileNumber}. Response: {Response}",
                    mobileNumber,
                    content);
                throw new InvalidOperationException($"SMS provider returned error: {content}");
            }

            _logger.LogInformation(
                "Bulk SMS sent successfully to {MobileNumber}",
                mobileNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception while sending bulk SMS to {MobileNumber}.",
                mobileNumber);
            throw;
        }
    }

    private string BuildBulkEmailBody(string fullName, string message)
    {
        var loginUrl = ApplicantLoginUrl;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1b5e9d 0%, #284b7a 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; }}
        .message-box {{ background: white; padding: 20px; margin: 20px 0; border-left: 4px solid #1b5e9d; border-radius: 4px; }}
        .button {{ display: inline-block; background: #1b5e9d; color: white !important; font-weight: bold; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Don Bosco College Tura</h1>
            <p>Admissions Office</p>
        </div>
        <div class=""content"">
            <p>Dear <strong>{fullName}</strong>,</p>
            
            <div class=""message-box"">
                {message.Replace("\n", "<br>")}
            </div>
            
            <p style=""text-align: center;"">
                <a href=""{loginUrl}"" class=""button"">Access Admissions Portal</a>
            </p>
            
            <p>If you have any questions, please contact the Admissions Office.</p>
            
            <p>Best regards,<br>
            <strong>Admissions Office</strong><br>
            Don Bosco College Tura</p>
        </div>
        <div class=""footer"">
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>© {DateTime.UtcNow.Year} Don Bosco College Tura. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
}