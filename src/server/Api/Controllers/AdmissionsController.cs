using ERP.Application.Admissions.Commands.CreateApplicant;
using ERP.Application.Admissions.Commands.CreateEntranceExam;
using ERP.Application.Admissions.Commands.RegisterApplicantForExam;
using ERP.Application.Admissions.Commands.UpdateApplicantStatus;
using ERP.Application.Admissions.Commands.UpdateEntranceExam;
using ERP.Application.Admissions.Commands.DeleteEntranceExam;
using ERP.Application.Admissions.Commands.ToggleExamStatus;
using ERP.Application.Admissions.Commands.MarkExamAttendance;
using ERP.Application.Admissions.Commands.EnterExamScore;
using ERP.Application.Admissions.Commands.UpdateOnlineApplicationStatus;
using ERP.Application.Admissions.Commands.EnrollApplication;
using ERP.Application.Admissions.Commands.SendBulkCommunication;
using ERP.Application.Admissions.Commands.VerifyPaymentManually;
using ERP.Application.Admissions.Commands.VerifyDocument;
using ERP.Application.Admissions.Commands.GenerateMeritList;
using ERP.Application.Admissions.Commands.CreateAdmissionOffer;
using ERP.Application.Admissions.Commands.CreateBulkAdmissionOffers;
using ERP.Application.Admissions.Commands.CreateDirectAdmissionOffers;
using ERP.Application.Admissions.Commands.SendIndividualAdmissionOffer;
using ERP.Application.Admissions.Commands.AcceptAdmissionOffer;
using ERP.Application.Admissions.Commands.RejectAdmissionOffer;
using ERP.Application.Admissions.Commands.ResetStudentApplicantPassword;
using ERP.Application.Admissions.Queries.GetMeritList;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Queries.ListClassXiiSubjects;
using ERP.Application.Admissions.Queries.GetAdminDashboard;
using ERP.Application.Admissions.Queries.GetApplicantById;
using ERP.Application.Admissions.Queries.GetEntranceExamById;
using ERP.Application.Admissions.Queries.GetOnlineApplicationById;
using ERP.Application.Admissions.Queries.GetApplicationDocuments;
using ERP.Application.Admissions.Queries.DownloadDocument;
using ERP.Application.Admissions.Queries.GetPaymentReport;
using ERP.Application.Admissions.Queries.GetAdmissionsAnalytics;
using ERP.Application.Admissions.Queries.GetAdmissionFee;
using ERP.Application.Admissions.Queries.ListApplicants;
using ERP.Application.Admissions.Queries.ListEntranceExams;
using ERP.Application.Admissions.Queries.ListExamRegistrations;
using ERP.Application.Admissions.Commands.ConfirmAdmissionFeePayment;
using ERP.Application.Admissions.Commands.GrantDirectAdmission;
using ERP.Application.Admissions.Commands.IssueOfflineAdmissionForm;
using ERP.Application.Admissions.Commands.ReplaceClassXiiSubjectCatalog;
using ERP.Application.Admissions.Commands.ReceiveOfflineAdmissionForm;
using ERP.Application.Admissions.Commands.AssignSelectionListRound;
using ERP.Application.Admissions.Commands.PublishSelectionList;
using ERP.Application.Admissions.Queries.ListAdmittedStudents;
using ERP.Application.Admissions.Queries.ListOnlineApplications;
using ERP.Application.Admissions.Queries.GetOfflineFormIssuancePreview;
using ERP.Application.Admissions.Queries.GetOfflineFormReceiptPdf;
using ERP.Application.Admissions.Queries.ListPayments;
using ERP.Application.Admissions.ViewModels;
using ERP.Application.Admissions.DTOs;
using ERP.Domain.Admissions.Entities;
using ERP.Application.Admissions.Options;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdmissionsController(
    IMediator mediator,
    IApplicantExportService exportService,
    IAdmissionsReadRepository readRepository,
    IApplicantApplicationPdfService applicationPdfService,
    IApplicantAccountRepository applicantAccountRepository,
    IApplicantApplicationRepository applicantApplicationRepository,
    IAdmissionErpSyncService admissionErpSyncService) : ControllerBase
{
    private readonly IApplicantExportService _exportService = exportService;
    private readonly IAdmissionsReadRepository _readRepository = readRepository;
    private readonly IApplicantApplicationPdfService _applicationPdfService = applicationPdfService;
    private readonly IApplicantAccountRepository _applicantAccountRepository = applicantAccountRepository;
    private readonly IApplicantApplicationRepository _applicantApplicationRepository = applicantApplicationRepository;
    private readonly IAdmissionErpSyncService _admissionErpSyncService = admissionErpSyncService;

    [HttpPost("applicants")]
    public async Task<ActionResult<Guid>> CreateApplicant(
        [FromBody] CreateApplicantRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateApplicantCommand(
            request.ApplicationNumber,
            request.FirstName,
            request.LastName,
            request.Email,
            request.DateOfBirth,
            request.ProgramCode,
            request.MobileNumber);

        var applicantId = await mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetApplicantById), new { id = applicantId }, applicantId);
    }

    [HttpGet("applicants/{id:guid}")]
    public async Task<ActionResult<ApplicantDto>> GetApplicantById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetApplicantByIdQuery(id), cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("applicants")]
    public async Task<ActionResult<IReadOnlyCollection<ApplicantDto>>> ListApplicants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListApplicantsQuery(page, pageSize), cancellationToken);

        return Ok(result);
    }

    [HttpGet("admin/dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAdminDashboardQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Class XII subject dropdown options for MBOSE, CBSE, or ISC (not for OTHER board).</summary>
    [HttpGet("class-xii-subjects")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetClassXiiSubjects(
        [FromQuery] string board,
        [FromQuery] string stream,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await mediator.Send(new ListClassXiiSubjectsQuery(board, stream), cancellationToken);
            return Ok(new { items });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Replaces the entire <c>admissions.subjects_master</c> catalog (UTF-8 CSV → JSON rows).</summary>
    [HttpPost("admin/class-xii-subjects")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReplaceClassXiiSubjectCatalogResult>> ReplaceClassXiiSubjectCatalog(
        [FromBody] ReplaceClassXiiSubjectCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var rows = (request.Rows ?? Array.Empty<ReplaceClassXiiSubjectCatalogRowRequest>())
            .Select(r => new ClassXiiSubjectCatalogRow(
                r.BoardCode,
                r.StreamCode,
                r.SubjectName,
                r.SortOrder)).ToList();

        var result = await mediator.Send(new ReplaceClassXiiSubjectCatalogCommand(rows), cancellationToken);
        return Ok(result);
    }

    [HttpGet("online-applications")]
    [HttpGet("applications")]
    public async Task<ActionResult<OnlineApplicationsListResponse>> ListOnlineApplications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isApplicationSubmitted = null,
        [FromQuery] bool? isPaymentCompleted = null,
        [FromQuery] string? shift = null,
        [FromQuery] DateTime? createdFromUtc = null,
        [FromQuery] DateTime? createdToUtc = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool? sortDescending = null,
        [FromQuery] decimal? minClassXiiPercentage = null,
        [FromQuery] decimal? maxClassXiiPercentage = null,
        [FromQuery] string? admissionPath = null,
        [FromQuery] string? admissionChannel = null,
        [FromQuery] string? applicationLifecycleStage = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListOnlineApplicationsQuery(
            page,
            pageSize,
            status,
            searchTerm,
            isApplicationSubmitted,
            isPaymentCompleted,
            shift,
            createdFromUtc,
            createdToUtc,
            sortBy,
            sortDescending,
            minClassXiiPercentage,
            maxClassXiiPercentage,
            admissionPath,
            admissionChannel,
            applicationLifecycleStage);

        var result = await mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>Issue an offline application slip and fee receipt only (no applicant account until receive).</summary>
    [HttpPost("admin/offline-forms/issue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> IssueOfflineAdmissionForm(
        [FromBody] IssueOfflineAdmissionFormRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new IssueOfflineAdmissionFormCommand(
                    request.FormNumber,
                    request.StudentName,
                    request.MobileNumber,
                    request.Shift,
                    request.CuetApplied,
                    request.ApplicationFeeAmount),
                cancellationToken);

            return File(result.ReceiptPdfContent, "application/pdf", result.ReceiptFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Receive the physical form: creates applicant account (form # + mobile password) and records final major.</summary>
    [HttpPost("admin/offline-forms/receive")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReceiveOfflineAdmissionFormResult>> ReceiveOfflineAdmissionForm(
        [FromBody] ReceiveOfflineAdmissionFormRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new ReceiveOfflineAdmissionFormCommand(request.FormNumber, request.MajorSubject),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Look up issued form details before receive (or existing applicant).</summary>
    [HttpGet("admin/offline-forms/{formNumber}/preview")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OfflineFormIssuancePreviewDto>> GetOfflineFormIssuancePreview(
        string formNumber,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOfflineFormIssuancePreviewQuery(formNumber), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Reprint application fee receipt PDF for an offline form.</summary>
    [HttpGet("admin/offline-forms/{formNumber}/receipt")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOfflineFormReceiptPdf(
        string formNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new GetOfflineFormReceiptPdfQuery(formNumber), cancellationToken);
            return File(result.Content, "application/pdf", result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Assign First / Second / Third selection list (before publication).</summary>
    [HttpPost("applications/{id:guid}/selection-list-round")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AssignSelectionListRoundResult>> AssignSelectionListRound(
        Guid id,
        [FromBody] AssignSelectionListRoundRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new AssignSelectionListRoundCommand(id, request.Round),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Publish a selection list round on the website and optionally notify by SMS.</summary>
    [HttpPost("admin/selection-list/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PublishSelectionListResult>> PublishSelectionList(
        [FromBody] PublishSelectionListRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new PublishSelectionListCommand(request.Round, request.SendSmsNotifications),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Students who paid admission fee (direct path) or are enrolled.</summary>
    [HttpGet("admitted-students")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdmittedStudentsListResponse>> ListAdmittedStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListAdmittedStudentsQuery(page, pageSize, searchTerm), cancellationToken);
        return Ok(result);
    }

    /// <summary>Grant direct admission (Class XII % must meet cutoff; notifies applicant).</summary>
    [HttpPost("applications/{id:guid}/direct-admission")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GrantDirectAdmissionResult>> GrantDirectAdmission(
        Guid id,
        [FromBody] GrantDirectAdmissionRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new GrantDirectAdmissionCommand(id, User?.Identity?.Name, body?.NotifyApplicant ?? true),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Record admission fee payment for direct-admission flow (admin / reconciliation).</summary>
    [HttpPost("applications/{id:guid}/confirm-admission-fee")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConfirmAdmissionFeePaymentResult>> ConfirmAdmissionFeePayment(
        Guid id,
        [FromBody] ConfirmAdmissionFeeRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new ConfirmAdmissionFeePaymentCommand(id, User?.Identity?.Name, body?.NotifyApplicant ?? true),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Returns the applicant portal payment URL for an application (direct admission).</summary>
    [HttpGet("applications/{id:guid}/payment-link")]
    [Authorize(Roles = "Admin")]
    public ActionResult<object> GetAdmissionPaymentLink(Guid id, [FromServices] IOptions<AdmissionsWorkflowOptions> opts)
    {
        var baseUrl = opts.Value.ApplicantPortalBaseUrl.TrimEnd('/');
        return Ok(new { url = $"{baseUrl}/pay-admission/{id}" });
    }

    [HttpGet("admission-workflow-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetAdmissionWorkflowSettings(
        [FromServices] IAdmissionWorkflowSettingsService workflowSettings,
        [FromServices] IOptions<AdmissionsWorkflowOptions> opts,
        CancellationToken cancellationToken)
    {
        var pct = await workflowSettings.GetMeritClassXiiCutoffPercentageAsync(cancellationToken);
        return Ok(new
        {
            directAdmissionCutoffPercentage = pct,
            applicantPortalBaseUrl = opts.Value.ApplicantPortalBaseUrl
        });
    }

    /// <summary>Updates minimum Class XII % for merit list and direct admission (persisted in database).</summary>
    [HttpPut("admission-workflow-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UpdateAdmissionWorkflowSettings(
        [FromBody] UpdateAdmissionWorkflowSettingsRequest request,
        [FromServices] IAdmissionWorkflowSettingsService workflowSettings,
        [FromServices] IOptions<AdmissionsWorkflowOptions> opts,
        CancellationToken cancellationToken)
    {
        if (request.DirectAdmissionCutoffPercentage < 0 || request.DirectAdmissionCutoffPercentage > 100)
        {
            return BadRequest("directAdmissionCutoffPercentage must be between 0 and 100.");
        }

        await workflowSettings.UpdateMeritClassXiiCutoffPercentageAsync(
            request.DirectAdmissionCutoffPercentage,
            User?.Identity?.Name,
            cancellationToken);

        var pct = await workflowSettings.GetMeritClassXiiCutoffPercentageAsync(cancellationToken);
        return Ok(new
        {
            directAdmissionCutoffPercentage = pct,
            applicantPortalBaseUrl = opts.Value.ApplicantPortalBaseUrl
        });
    }

    public sealed class UpdateAdmissionWorkflowSettingsRequest
    {
        public decimal DirectAdmissionCutoffPercentage { get; set; }
    }

    public sealed class GrantDirectAdmissionRequest
    {
        public bool NotifyApplicant { get; set; } = true;
    }

    public sealed class ConfirmAdmissionFeeRequest
    {
        public bool NotifyApplicant { get; set; } = true;
    }

    [HttpGet("online-applications/{id:guid}")]
    [HttpGet("applications/{id:guid}")]
    public async Task<ActionResult<OnlineApplicationDetailDto>> GetOnlineApplicationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOnlineApplicationByIdQuery(id), cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("online-applications/{id:guid}/pdf")]
    [HttpGet("applications/{id:guid}/pdf")]
    public async Task<IActionResult> GetOnlineApplicationPdf(Guid id, CancellationToken cancellationToken)
    {
        var account = await _applicantAccountRepository.GetByIdAsync(id, cancellationToken);
        if (account is null || !account.IsApplicationSubmitted)
        {
            return NotFound();
        }

        var draftEntity = await _applicantApplicationRepository.GetDraftByAccountIdAsync(id, cancellationToken);
        if (draftEntity is null)
        {
            return NotFound();
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        };
        var draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, jsonOptions)
            ?? ApplicantApplicationDraftDto.Empty;

        var pdf = await _applicationPdfService.GenerateAsync(
            draft,
            account.IsPaymentCompleted,
            account.PaymentAmount,
            photoUrl: account.PhotoUrl,
            transactionId: account.PaymentTransactionId,
            applicationNumber: account.UniqueId,
            submittedOnUtc: draftEntity.UpdatedOnUtc,
            cancellationToken: cancellationToken);

        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    [HttpPost("online-applications/{id:guid}/approve")]
    [HttpPost("applications/{id:guid}/approve")]
    public async Task<ActionResult<OnlineApplicationDetailDto>> ApproveOnlineApplication(
        Guid id,
        [FromBody] ApproveRejectApplicationRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOnlineApplicationStatusCommand(
            id,
            ApplicationStatus.Approved,
            User?.Identity?.Name,
            request?.Remarks,
            null,
            request?.NotifyApplicant ?? true,
            null);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var updated = await mediator.Send(new GetOnlineApplicationByIdQuery(result.AccountId), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("online-applications/{id:guid}/reject")]
    [HttpPost("applications/{id:guid}/reject")]
    public async Task<ActionResult<OnlineApplicationDetailDto>> RejectOnlineApplication(
        Guid id,
        [FromBody] ApproveRejectApplicationRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOnlineApplicationStatusCommand(
            id,
            ApplicationStatus.Rejected,
            User?.Identity?.Name,
            request?.Remarks,
            null,
            request?.NotifyApplicant ?? true,
            null);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var updated = await mediator.Send(new GetOnlineApplicationByIdQuery(result.AccountId), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Provision or link ERP student for an approved application (idempotent; retry after failures).</summary>
    [HttpPost("online-applications/{id:guid}/erp-sync")]
    [HttpPost("applications/{id:guid}/erp-sync")]
    public async Task<ActionResult<AdmissionErpSyncResult>> SyncApplicationToErp(Guid id, CancellationToken cancellationToken)
    {
        var result = await _admissionErpSyncService.TrySyncApprovedApplicationAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("online-applications/{id:guid}/status")]
    public async Task<ActionResult<OnlineApplicationDetailDto>> UpdateOnlineApplicationStatus(
        Guid id,
        [FromBody] UpdateOnlineApplicationStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ApplicationStatus>(request.Status, true, out var status))
        {
            return BadRequest("Invalid status. Supported values: Submitted, Approved, Rejected, WaitingList, EntranceExam.");
        }

        var command = new UpdateOnlineApplicationStatusCommand(
            id,
            status,
            User?.Identity?.Name,
            request.Remarks,
            request.EntranceExam is null
                ? null
                : new ERP.Application.Admissions.Commands.UpdateOnlineApplicationStatus.EntranceExamDetails(
                    request.EntranceExam.ScheduledOnUtc,
                    request.EntranceExam.Venue,
                    request.EntranceExam.Instructions),
            request.NotifyApplicant,
            request.PaymentDeadlineUtc);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var updated = await mediator.Send(new GetOnlineApplicationByIdQuery(result.AccountId), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("online-applications/{id:guid}/admission-fee")]
    public async Task<ActionResult<AdmissionFeeDto>> GetAdmissionFee(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetAdmissionFeeQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("online-applications/{id:guid}/enroll")]
    public async Task<ActionResult<EnrollApplicationResult>> EnrollApplication(
        Guid id,
        [FromBody] EnrollApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EnrollApplicationCommand(
            id,
            User?.Identity?.Name ?? request.EnrolledBy,
            request.Remarks,
            request.NotifyApplicant);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("communications/bulk")]
    public async Task<ActionResult<BulkCommunicationResult>> SendBulkCommunication(
        [FromBody] SendBulkCommunicationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CommunicationChannel>(request.Channel, true, out var channel))
        {
            return BadRequest("Invalid channel. Supported values: Email, Sms, Both.");
        }

        var filter = new BulkCommunicationRecipientFilter(
            request.Filter?.Statuses?.Select(s => Enum.Parse<ApplicationStatus>(s, true)).ToList(),
            request.Filter?.IsApplicationSubmitted,
            request.Filter?.IsPaymentCompleted,
            request.Filter?.SpecificAccountIds,
            request.Filter?.SearchTerm);

        var command = new SendBulkCommunicationCommand(
            request.Subject,
            request.Message,
            channel,
            filter,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("applicants/export")]
    public async Task<IActionResult> ExportApplicants(
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ApplicantExportFormat>(format, true, out var parsedFormat))
        {
            return BadRequest("Invalid export format. Supported values: csv, excel, pdf.");
        }

        var export = await _exportService.ExportAsync(parsedFormat, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("applications/paid/export")]
    public async Task<IActionResult> ExportPaidApplicationsWithFullDetails(
        CancellationToken cancellationToken = default)
    {
        var export = await _exportService.ExportPaidApplicationsWithFullDetailsAsync(cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("applications/export/excel")]
    public async Task<IActionResult> ExportSubmittedApplicationsExcel(CancellationToken cancellationToken = default)
    {
        var export = await _exportService.ExportSubmittedApplicationsWithFullDetailsAsync(cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PaymentsListResponse>> ListPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? isPaymentCompleted = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? paymentDateFrom = null,
        [FromQuery] DateTime? paymentDateTo = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListPaymentsQuery(
            page,
            pageSize,
            isPaymentCompleted,
            searchTerm,
            paymentDateFrom,
            paymentDateTo,
            minAmount,
            maxAmount);

        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("payments/{accountId:guid}/verify")]
    public async Task<ActionResult<VerifyPaymentManuallyResult>> VerifyPaymentManually(
        Guid accountId,
        [FromBody] VerifyPaymentManuallyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new VerifyPaymentManuallyCommand(
            accountId,
            request.TransactionId,
            request.Amount,
            User?.Identity?.Name,
            request.Remarks);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("applications/{accountId:guid}/documents")]
    public async Task<ActionResult<ApplicationDocumentsDto>> GetApplicationDocuments(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var query = new GetApplicationDocumentsQuery(accountId);
        var result = await mediator.Send(query, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("applications/{accountId:guid}/documents/{documentType}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid accountId,
        string documentType,
        CancellationToken cancellationToken)
    {
        var query = new DownloadDocumentQuery(accountId, documentType);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("applications/{accountId:guid}/documents/{documentType}/verify")]
    public async Task<IActionResult> VerifyDocument(
        Guid accountId,
        string documentType,
        [FromBody] VerifyDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new VerifyDocumentCommand(
            accountId,
            documentType,
            request.IsVerified,
            request.Remarks,
            request.VerifiedBy);
        
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result)
        {
            return NotFound();
        }

        return Ok(new { message = "Document verification status updated successfully." });
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<AdmissionsAnalyticsDto>> GetAdmissionsAnalytics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdmissionsAnalyticsQuery(fromDate, toDate);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("applicants/{id:guid}/status")]
    public async Task<ActionResult<ApplicantDto>> UpdateApplicantStatus(
        Guid id,
        [FromBody] UpdateApplicantStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ApplicationStatus>(request.Status, true, out var status))
        {
            return BadRequest("Invalid status. Supported values: Submitted, Approved, Rejected, WaitingList, EntranceExam.");
        }

        var command = new UpdateApplicantStatusCommand(
            id,
            status,
            User?.Identity?.Name,
            request.Remarks,
            request.EntranceExam is null
                ? null
                : new ERP.Application.Admissions.Commands.UpdateApplicantStatus.EntranceExamDetails(
                    request.EntranceExam.ScheduledOnUtc,
                    request.EntranceExam.Venue,
                    request.EntranceExam.Instructions),
            request.NotifyApplicant);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var updated = await _readRepository.GetApplicantByIdAsync(result.ApplicantId, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Entrance Exam Management Endpoints

    [HttpPost("entrance-exams")]
    public async Task<ActionResult<Guid>> CreateEntranceExam(
        [FromBody] CreateEntranceExamRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeOnly.TryParse(request.ExamStartTime, out var startTime))
        {
            return BadRequest($"Invalid start time format: {request.ExamStartTime}. Expected format: HH:mm");
        }

        if (!TimeOnly.TryParse(request.ExamEndTime, out var endTime))
        {
            return BadRequest($"Invalid end time format: {request.ExamEndTime}. Expected format: HH:mm");
        }

        var command = new CreateEntranceExamCommand(
            request.ExamName,
            request.ExamCode,
            request.ExamDate,
            startTime,
            endTime,
            request.Venue,
            request.MaxCapacity,
            request.Description,
            request.VenueAddress,
            request.Instructions,
            User?.Identity?.Name);

        try
        {
            var examId = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetEntranceExamById), new { id = examId }, examId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("entrance-exams")]
    public async Task<ActionResult<EntranceExamsListResponse>> ListEntranceExams(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? isActive = null,
        [FromQuery] DateTime? examDateFrom = null,
        [FromQuery] DateTime? examDateTo = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListEntranceExamsQuery(
            page,
            pageSize,
            isActive,
            examDateFrom,
            examDateTo,
            searchTerm);

        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("entrance-exams/{id:guid}")]
    public async Task<ActionResult<EntranceExamDto>> GetEntranceExamById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEntranceExamByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("entrance-exams/{id:guid}")]
    public async Task<ActionResult> UpdateEntranceExam(
        Guid id,
        [FromBody] UpdateEntranceExamRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeOnly.TryParse(request.ExamStartTime, out var startTime))
        {
            return BadRequest($"Invalid start time format: {request.ExamStartTime}. Expected format: HH:mm");
        }

        if (!TimeOnly.TryParse(request.ExamEndTime, out var endTime))
        {
            return BadRequest($"Invalid end time format: {request.ExamEndTime}. Expected format: HH:mm");
        }

        var command = new UpdateEntranceExamCommand(
            id,
            request.ExamName,
            request.ExamDate,
            startTime,
            endTime,
            request.Venue,
            request.MaxCapacity,
            request.Description,
            request.VenueAddress,
            request.Instructions,
            User?.Identity?.Name);

        try
        {
            await mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("entrance-exams/{examId:guid}/register/{applicantId:guid}")]
    public async Task<ActionResult<Guid>> RegisterApplicantForExam(
        Guid examId,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        var command = new RegisterApplicantForExamCommand(
            examId,
            applicantId,
            User?.Identity?.Name);

        try
        {
            var registrationId = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetEntranceExamById), new { id = examId }, registrationId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("entrance-exams/{examId:guid}/registrations")]
    public async Task<ActionResult<ExamRegistrationsListResponse>> ListExamRegistrations(
        Guid examId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? isPresent = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListExamRegistrationsQuery(
            examId,
            page,
            pageSize,
            isPresent,
            searchTerm);

        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("entrance-exams/{id:guid}")]
    public async Task<IActionResult> DeleteEntranceExam(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteEntranceExamCommand(id, User?.Identity?.Name);
            var result = await mediator.Send(command, cancellationToken);
            
            if (!result)
            {
                return NotFound();
            }

            return Ok(new { message = "Entrance exam deleted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("entrance-exams/{id:guid}/status")]
    public async Task<IActionResult> ToggleExamStatus(
        Guid id,
        [FromBody] ToggleExamStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ToggleExamStatusCommand(id, request.IsActive, User?.Identity?.Name);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result)
        {
            return NotFound();
        }

        return Ok(new { message = $"Exam {(request.IsActive ? "activated" : "deactivated")} successfully." });
    }

    [HttpPost("exam-registrations/{registrationId:guid}/attendance")]
    public async Task<IActionResult> MarkExamAttendance(
        Guid registrationId,
        [FromBody] MarkExamAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MarkExamAttendanceCommand(registrationId, request.IsPresent, User?.Identity?.Name);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result)
        {
            return NotFound();
        }

        return Ok(new { message = "Attendance marked successfully." });
    }

    [HttpPost("exam-registrations/{registrationId:guid}/score")]
    public async Task<IActionResult> EnterExamScore(
        Guid registrationId,
        [FromBody] EnterExamScoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new EnterExamScoreCommand(registrationId, request.Score, User?.Identity?.Name);
            var result = await mediator.Send(command, cancellationToken);
            
            if (!result)
            {
                return NotFound();
            }

            return Ok(new { message = "Score entered successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("merit-list/generate")]
    public async Task<ActionResult<GenerateMeritListResult>> GenerateMeritList(
        [FromBody] GenerateMeritListRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GenerateMeritListCommand(
            request.Shift,
            request.MajorSubject,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("merit-list")]
    public async Task<ActionResult<MeritListResponse>> GetMeritList(
        [FromQuery] string? shift = null,
        [FromQuery] string? majorSubject = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMeritListQuery(shift, majorSubject, page, pageSize);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("offers")]
    public async Task<ActionResult<CreateAdmissionOfferResult>> CreateAdmissionOffer(
        [FromBody] CreateAdmissionOfferRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAdmissionOfferCommand(
            request.AccountId,
            request.ExpiryDate,
            request.Remarks,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("offers/bulk")]
    public async Task<ActionResult<CreateBulkAdmissionOffersResult>> CreateBulkAdmissionOffers(
        [FromBody] CreateBulkAdmissionOffersRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateBulkAdmissionOffersCommand(
            request.Shift,
            request.MajorSubject,
            request.TopNRanks,
            request.ExpiryDate,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // IMPORTANT: Specific routes (like "direct-admission" and "send-individual") must come before 
    // parameterized routes (like "{offerId:guid}/accept") to avoid route conflicts
    [HttpPost("offers/direct-admission")]
    public async Task<ActionResult<CreateDirectAdmissionOffersResult>> CreateDirectAdmissionOffers(
        [FromBody] CreateDirectAdmissionOffersRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDirectAdmissionOffersCommand(
            request.MinimumPercentage,
            request.AdmissionFeeAmount,
            request.ExpiryDate,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // IMPORTANT: This route must come before offers/{offerId:guid}/accept to avoid route conflicts
    [HttpPost("offers/send-individual")]
    public async Task<ActionResult<SendIndividualAdmissionOfferResult>> SendIndividualAdmissionOffer(
        [FromBody] SendIndividualAdmissionOfferRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendIndividualAdmissionOfferCommand(
            request.ApplicationNumber,
            request.AdmissionFeeAmount,
            request.ExpiryDays,
            request.Remarks,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("applicants/{applicationNumber}/reset-password")]
    public async Task<ActionResult<ResetStudentApplicantPasswordResult>> ResetApplicantPassword(
        string applicationNumber,
        CancellationToken cancellationToken)
    {
        var command = new ResetStudentApplicantPasswordCommand(
            applicationNumber,
            User?.Identity?.Name);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("offers/{offerId:guid}/accept")]
    public async Task<ActionResult<AcceptAdmissionOfferResult>> AcceptAdmissionOffer(
        Guid offerId,
        CancellationToken cancellationToken)
    {
        var command = new AcceptAdmissionOfferCommand(offerId);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("offers/{offerId:guid}/reject")]
    public async Task<ActionResult<RejectAdmissionOfferResult>> RejectAdmissionOffer(
        Guid offerId,
        [FromBody] RejectAdmissionOfferRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new RejectAdmissionOfferCommand(offerId, request?.Reason);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public sealed record CreateApplicantRequest(
    string ApplicationNumber,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    string ProgramCode,
    string MobileNumber);

public sealed record UpdateApplicantStatusRequest(
    string Status,
    bool NotifyApplicant,
    string? Remarks,
    EntranceExamRequest? EntranceExam);

public sealed record EntranceExamRequest(
    DateTime? ScheduledOnUtc,
    string? Venue,
    string? Instructions);

public sealed record UpdateOnlineApplicationStatusRequest(
    DateTime? PaymentDeadlineUtc,
    string Status,
    bool NotifyApplicant,
    string? Remarks,
    EntranceExamRequest? EntranceExam);

public sealed record ApproveRejectApplicationRequest(string? Remarks = null, bool NotifyApplicant = true);

public sealed record CreateEntranceExamRequest(
    string ExamName,
    string ExamCode,
    DateTime ExamDate,
    string ExamStartTime,
    string ExamEndTime,
    string Venue,
    int MaxCapacity,
    string? Description = null,
    string? VenueAddress = null,
    string? Instructions = null);

public sealed record UpdateEntranceExamRequest(
    string ExamName,
    DateTime ExamDate,
    string ExamStartTime,
    string ExamEndTime,
    string Venue,
    int MaxCapacity,
    string? Description = null,
    string? VenueAddress = null,
    string? Instructions = null);

public sealed record VerifyPaymentManuallyRequest(
    string TransactionId,
    decimal Amount,
    string? Remarks);

public sealed record VerifyDocumentRequest(
    bool IsVerified,
    string? Remarks = null,
    string? VerifiedBy = null);

public sealed record ToggleExamStatusRequest(bool IsActive);

public sealed record MarkExamAttendanceRequest(bool IsPresent);

public sealed record EnterExamScoreRequest(decimal Score);

public sealed record EnrollApplicationRequest(
    string? Remarks = null,
    bool NotifyApplicant = true,
    string? EnrolledBy = null);

public sealed record SendBulkCommunicationRequest(
    string Subject,
    string Message,
    string Channel,
    BulkCommunicationRecipientFilterRequest? Filter = null);

public sealed record BulkCommunicationRecipientFilterRequest(
    List<string>? Statuses = null,
    bool? IsApplicationSubmitted = null,
    bool? IsPaymentCompleted = null,
    List<Guid>? SpecificAccountIds = null,
    string? SearchTerm = null);

public sealed record GenerateMeritListRequest(
    string? Shift = null,
    string? MajorSubject = null);

public sealed record CreateAdmissionOfferRequest(
    Guid AccountId,
    DateTime ExpiryDate,
    string? Remarks = null);

public sealed record RejectAdmissionOfferRequest(
    string? Reason = null);

public sealed record CreateBulkAdmissionOffersRequest(
    string? Shift = null,
    string? MajorSubject = null,
    int? TopNRanks = null,
    DateTime? ExpiryDate = null);

public sealed record CreateDirectAdmissionOffersRequest(
    decimal MinimumPercentage = 60.0m,
    decimal AdmissionFeeAmount = 10.0m,
    DateTime? ExpiryDate = null);

public sealed record SendIndividualAdmissionOfferRequest(
    string ApplicationNumber,
    decimal AdmissionFeeAmount,
    int ExpiryDays = 2,
    string? Remarks = null);

public sealed record IssueOfflineAdmissionFormRequest(
    string FormNumber,
    string StudentName,
    string MobileNumber,
    string Shift,
    bool CuetApplied,
    decimal ApplicationFeeAmount);

public sealed record ReceiveOfflineAdmissionFormRequest(string FormNumber, string MajorSubject);

public sealed record AssignSelectionListRoundRequest(AdmissionSelectionListRound Round);

public sealed record PublishSelectionListRequest(
    AdmissionSelectionListRound Round,
    bool SendSmsNotifications = true);

public sealed record ReplaceClassXiiSubjectCatalogRequest(
    IReadOnlyList<ReplaceClassXiiSubjectCatalogRowRequest> Rows);

public sealed record ReplaceClassXiiSubjectCatalogRowRequest(
    string BoardCode,
    string StreamCode,
    string SubjectName,
    int SortOrder);

