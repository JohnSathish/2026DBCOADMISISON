namespace ERP.Domain.Admissions.Entities;

public class StudentApplicantAccount
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string UniqueId { get; private set; }

    public string FullName { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public string Gender { get; private set; }

    public string Email { get; private set; }

    public string MobileNumber { get; private set; }

    public string Shift { get; private set; }

    public string PasswordHash { get; private set; }

    public string? PhotoUrl { get; private set; }

    public DateTime CreatedOnUtc { get; private set; } = DateTime.UtcNow;

    public bool MustChangePassword { get; private set; } = true;

    public bool IsApplicationSubmitted { get; private set; } = false;

    public bool IsPaymentCompleted { get; private set; } = false;

    public string? PaymentOrderId { get; private set; }

    public string? PaymentTransactionId { get; private set; }

    public DateTime? PaymentCompletedOnUtc { get; private set; }

    public decimal? PaymentAmount { get; private set; }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Submitted;

    public DateTime StatusUpdatedOnUtc { get; private set; } = DateTime.UtcNow;

    public string? StatusUpdatedBy { get; private set; }

    public string? StatusRemarks { get; private set; }

    public DateTime? EntranceExamScheduledOnUtc { get; private set; }

    public string? EntranceExamVenue { get; private set; }

    public string? EntranceExamInstructions { get; private set; }

    public DateTime? EnrolledOnUtc { get; private set; }

    public string? EnrollmentRemarks { get; private set; }

    /// <summary>ERP student record ID (e.g. students.Students.Id) after admission→ERP sync.</summary>
    public Guid? ErpStudentId { get; private set; }

    public DateTime? ErpSyncedOnUtc { get; private set; }

    public string? ErpSyncLastError { get; private set; }

    /// <summary>Class XII % from application draft (synced on save) for sorting and direct admission.</summary>
    public decimal? ClassXIIPercentage { get; private set; }

    public AdmissionChannel AdmissionChannel { get; private set; } = AdmissionChannel.Online;

    /// <summary>Major subject recorded when an offline form was issued (receipt / tracking).</summary>
    public string? OfflineIssuedMajorSubject { get; private set; }

    /// <summary>CUET status captured at offline form issue (null = online / not recorded).</summary>
    public bool? CuetAppliedAtIssue { get; private set; }

    /// <summary>When the physical form was received at the office (same-day workflow).</summary>
    public DateTime? OfflineFormReceivedOnUtc { get; private set; }

    /// <summary>Assigned selection list round before publication.</summary>
    public AdmissionSelectionListRound? SelectionListRound { get; private set; }

    /// <summary>When this applicant&apos;s selection was published on the website (null = not yet published).</summary>
    public DateTime? SelectionListPublishedAtUtc { get; private set; }

    private readonly List<ApplicantRefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<ApplicantRefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private StudentApplicantAccount()
    {
        UniqueId = string.Empty;
        FullName = string.Empty;
        Gender = string.Empty;
        Email = string.Empty;
        MobileNumber = string.Empty;
        Shift = string.Empty;
        PasswordHash = string.Empty;
    }

    public StudentApplicantAccount(
        string uniqueId,
        string fullName,
        DateOnly dateOfBirth,
        string gender,
        string email,
        string mobileNumber,
        string shift,
        string? photoUrl = null)
    {
        UniqueId = uniqueId;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Email = email;
        MobileNumber = mobileNumber;
        Shift = shift;
        PhotoUrl = photoUrl;
        PasswordHash = string.Empty;
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        MustChangePassword = true;
    }

    public void SetPhotoUrl(string? photoUrl)
    {
        PhotoUrl = photoUrl;
    }

    public void UpdateShift(string shift)
    {
        Shift = shift;
    }

    public void MarkPasswordChanged()
    {
        MustChangePassword = false;
    }

    public void AddRefreshToken(ApplicantRefreshToken refreshToken)
    {
        _refreshTokens.Add(refreshToken);
    }

    public void RevokeRefreshTokens()
    {
        foreach (var token in _refreshTokens)
        {
            token.Revoke();
        }
    }

    public void MarkApplicationSubmitted()
    {
        IsApplicationSubmitted = true;
    }

    public void SetPaymentOrderId(string orderId)
    {
        PaymentOrderId = orderId;
    }

    public void MarkPaymentCompleted(string transactionId, decimal amount)
    {
        IsPaymentCompleted = true;
        PaymentTransactionId = transactionId;
        PaymentCompletedOnUtc = DateTime.UtcNow;
        PaymentAmount = amount;
    }

    public void UpdateStatus(
        ApplicationStatus status,
        string? updatedBy,
        DateTime utcNow,
        string? remarks = null,
        DateTime? entranceExamScheduledOnUtc = null,
        string? entranceExamVenue = null,
        string? entranceExamInstructions = null)
    {
        if (!IsStatusTransitionAllowed(Status, status))
        {
            throw new InvalidOperationException(
                $"Cannot transition application {UniqueId} from {Status} to {status}.");
        }

        Status = status;
        StatusUpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim();
        StatusUpdatedOnUtc = utcNow;
        StatusRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        if (status == ApplicationStatus.EntranceExam)
        {
            EntranceExamScheduledOnUtc = entranceExamScheduledOnUtc;
            EntranceExamVenue = string.IsNullOrWhiteSpace(entranceExamVenue) ? null : entranceExamVenue.Trim();
            EntranceExamInstructions = string.IsNullOrWhiteSpace(entranceExamInstructions)
                ? null
                : entranceExamInstructions.Trim();
        }
        else
        {
            EntranceExamScheduledOnUtc = null;
            EntranceExamVenue = null;
            EntranceExamInstructions = null;
        }
    }

    public void RecordErpStudentLink(Guid erpStudentId, DateTime utcNow)
    {
        ErpStudentId = erpStudentId;
        ErpSyncedOnUtc = utcNow;
        ErpSyncLastError = null;
    }

    public void RecordErpSyncFailure(string? message)
    {
        ErpSyncLastError = string.IsNullOrWhiteSpace(message)
            ? "Sync failed."
            : (message.Length > 2000 ? message[..2000] : message.Trim());
    }

    public void SetClassXiPercentage(decimal? percentage)
    {
        ClassXIIPercentage = percentage.HasValue ? Math.Round(Math.Clamp(percentage.Value, 0, 100), 2) : null;
    }

    public void MarkAsOfflineIssued(string majorSubject)
    {
        AdmissionChannel = AdmissionChannel.Offline;
        OfflineIssuedMajorSubject = string.IsNullOrWhiteSpace(majorSubject) ? null : majorSubject.Trim();
    }

    public void SetCuetAppliedAtIssue(bool cuetApplied)
    {
        CuetAppliedAtIssue = cuetApplied;
    }

    public void MarkOfflinePhysicalFormReceived(DateTime utcNow)
    {
        if (AdmissionChannel != AdmissionChannel.Offline)
        {
            throw new InvalidOperationException("Only offline-issued applications can be marked as received.");
        }

        OfflineFormReceivedOnUtc = utcNow;
    }

    public void AssignSelectionListRound(AdmissionSelectionListRound round)
    {
        SelectionListRound = round;
        SelectionListPublishedAtUtc = null;
    }

    public void MarkSelectionListPublished(DateTime utcNow)
    {
        if (!SelectionListRound.HasValue)
        {
            throw new InvalidOperationException("Assign a selection list round before publishing.");
        }

        SelectionListPublishedAtUtc = utcNow;
    }

    /// <summary>Sync account identity from submitted application (e.g. offline placeholders replaced by draft data).</summary>
    public void ApplySubmissionProfile(string fullName, DateOnly dateOfBirth, string gender, string? emailFromDraft)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            FullName = fullName.Trim();
        }

        DateOfBirth = dateOfBirth;

        if (!string.IsNullOrWhiteSpace(gender))
        {
            Gender = gender.Trim();
        }

        if (!string.IsNullOrWhiteSpace(emailFromDraft) &&
            emailFromDraft.Contains('@', StringComparison.Ordinal))
        {
            Email = emailFromDraft.Trim();
        }
    }

    public void Enroll(string? enrolledBy, DateTime utcNow, string? remarks = null)
    {
        if (Status != ApplicationStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Cannot enroll application {UniqueId}. Current status is {Status}. Only Approved applications can be enrolled.");
        }

        if (!IsPaymentCompleted)
        {
            throw new InvalidOperationException(
                $"Cannot enroll application {UniqueId}. Payment has not been completed.");
        }

        Status = ApplicationStatus.Enrolled;
        StatusUpdatedBy = string.IsNullOrWhiteSpace(enrolledBy) ? null : enrolledBy.Trim();
        StatusUpdatedOnUtc = utcNow;
        EnrolledOnUtc = utcNow;
        EnrollmentRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        StatusRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
    }

    private static bool IsStatusTransitionAllowed(ApplicationStatus current, ApplicationStatus next)
    {
        // Don't allow transitioning to the same status
        if (current == next)
        {
            return false;
        }

        return current switch
        {
            ApplicationStatus.Submitted => next is ApplicationStatus.Approved
                or ApplicationStatus.Rejected
                or ApplicationStatus.WaitingList
                or ApplicationStatus.EntranceExam
                or ApplicationStatus.DirectAdmissionGranted,
            ApplicationStatus.WaitingList => next is ApplicationStatus.Approved
                or ApplicationStatus.Rejected
                or ApplicationStatus.EntranceExam,
            ApplicationStatus.EntranceExam => next is ApplicationStatus.Approved
                or ApplicationStatus.Rejected
                or ApplicationStatus.WaitingList,
            ApplicationStatus.Approved => next is ApplicationStatus.Enrolled
                or ApplicationStatus.WaitingList
                or ApplicationStatus.Rejected
                or ApplicationStatus.EntranceExam,
            ApplicationStatus.Rejected => next is ApplicationStatus.Approved
                or ApplicationStatus.WaitingList
                or ApplicationStatus.EntranceExam,
            ApplicationStatus.DirectAdmissionGranted => next is ApplicationStatus.AdmissionFeePaid
                or ApplicationStatus.Rejected,
            ApplicationStatus.AdmissionFeePaid => next is ApplicationStatus.Approved
                or ApplicationStatus.Rejected,
            ApplicationStatus.Enrolled => false, // Cannot transition from Enrolled
            _ => false
        };
    }
}

