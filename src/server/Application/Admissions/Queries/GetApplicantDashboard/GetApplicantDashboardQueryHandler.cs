using System;
using System.Linq;
using System.Text.Json;
using ERP.Application.Admissions;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.ViewModels;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Queries.GetApplicantDashboard;

public sealed class GetApplicantDashboardQueryHandler : IRequestHandler<GetApplicantDashboardQuery, ApplicantDashboardDto?>
{
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IApplicantApplicationRepository _applicationRepository;
    private readonly IAdmissionsRepository _admissionsRepository;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public GetApplicantDashboardQueryHandler(
        IApplicantAccountRepository accountRepository,
        IApplicantApplicationRepository applicationRepository,
        IAdmissionsRepository admissionsRepository)
    {
        _accountRepository = accountRepository;
        _applicationRepository = applicationRepository;
        _admissionsRepository = admissionsRepository;
    }

    public async Task<ApplicantDashboardDto?> Handle(GetApplicantDashboardQuery request, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var profile = new ApplicantProfileDto(
            account.Id,
            account.UniqueId,
            account.FullName,
            account.DateOfBirth,
            account.Gender,
            account.Email,
            account.MobileNumber,
            account.Shift,
            account.PhotoUrl,
            account.CreatedOnUtc);

        var documents = BuildDocumentStatuses(account);
        var notifications = BuildNotifications(account);

        var (draft, draftUpdatedOnUtc) = await LoadDraftAsync(account.Id, cancellationToken);
        var application = BuildApplicationInfo(account, draft);
        var payment = await BuildPaymentInfoAsync(account, application, cancellationToken);
        var courseSelection = BuildCourseSelectionSummary(account, draft, draftUpdatedOnUtc);
        var offer = await BuildOfferInfoAsync(account.Id, cancellationToken);

        return new ApplicantDashboardDto(profile, documents, notifications, application, payment, courseSelection, offer);
    }

    private static IReadOnlyCollection<ApplicantDocumentStatusDto> BuildDocumentStatuses(Domain.Admissions.Entities.StudentApplicantAccount account)
    {
        var documents = new List<ApplicantDocumentStatusDto>
        {
            new ApplicantDocumentStatusDto(
                "Registration Form",
                "Completed",
                "Your online registration details have been recorded.",
                true),
            new ApplicantDocumentStatusDto(
                "Profile Photo",
                account.PhotoUrl is not null ? "Uploaded" : "Pending",
                account.PhotoUrl is not null
                    ? "A profile photo is on file. You may update it after login if needed."
                    : "Upload a recent passport-size photograph to complete your profile.",
                account.PhotoUrl is not null)
        };

        return documents;
    }

    private static IReadOnlyCollection<ApplicantNotificationDto> BuildNotifications(Domain.Admissions.Entities.StudentApplicantAccount account)
    {
        var notifications = new List<ApplicantNotificationDto>
        {
            new(
                "Registration Received",
                $"Welcome {account.FullName}! Your application number is {account.UniqueId}. " +
                "Use this ID for all future correspondence. You can continue completing your profile and submit required documents from the portal.",
                account.CreatedOnUtc)
        };

        return notifications;
    }

    private async Task<(ApplicantApplicationDraftDto Draft, DateTime? DraftUpdatedOnUtc)> LoadDraftAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(accountId, cancellationToken);
        if (draftEntity is null)
        {
            return (ApplicantApplicationDraftDto.Empty, null);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                      ?? ApplicantApplicationDraftDto.Empty;
            ApplicantCourseSubjectNormalizer.Normalize(dto);
            AcademicSectionDraftSync.HydrateFromLegacy(dto.Academics);
            return (dto, draftEntity.UpdatedOnUtc);
        }
        catch
        {
            return (ApplicantApplicationDraftDto.Empty, draftEntity.UpdatedOnUtc);
        }
    }

    private static ApplicantCourseSelectionSummaryDto? BuildCourseSelectionSummary(
        StudentApplicantAccount account,
        ApplicantApplicationDraftDto draft,
        DateTime? draftUpdatedOnUtc)
    {
        if (!account.IsApplicationSubmitted || !account.IsPaymentCompleted)
        {
            return null;
        }

        var courses = draft.Courses;
        if (courses is null)
        {
            return null;
        }

        var shiftCode = string.IsNullOrWhiteSpace(courses.Shift) ? account.Shift : courses.Shift;
        var vacRaw = string.IsNullOrWhiteSpace(courses.ValueAddedChoice)
            ? "VAC 140 — ENVIRONMENT STUDIES (Auto Assigned)"
            : courses.ValueAddedChoice.Trim();
        return new ApplicantCourseSelectionSummaryDto(
            PreferredShiftCode: shiftCode.Trim(),
            PreferredShiftLabel: MapShiftToDisplayLabel(shiftCode),
            MajorSubject: courses.MajorSubject?.Trim() ?? string.Empty,
            MinorSubject: courses.MinorSubject?.Trim() ?? string.Empty,
            Mdc: ApplicantElectiveSubjectCatalog.FromStoredChoice(courses.MultidisciplinaryChoice),
            Aec: ApplicantElectiveSubjectCatalog.FromStoredChoice(courses.AbilityEnhancementChoice),
            Sec: ApplicantElectiveSubjectCatalog.FromStoredChoice(courses.SkillEnhancementChoice),
            Vac: ApplicantElectiveSubjectCatalog.FromStoredChoice(vacRaw),
            ApplicationFeePaidOnUtc: account.PaymentCompletedOnUtc,
            DraftLastUpdatedOnUtc: draftUpdatedOnUtc);
    }

    private static string MapShiftToDisplayLabel(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode))
        {
            return "—";
        }

        var key = shiftCode.Trim();
        return key switch
        {
            "ShiftI" => "Shift - I (6:30 am – 9:30 am)",
            "ShiftII" => "Shift - II (9:45 am – 3:30 pm)",
            "ShiftIII" => "Shift - III (2:45 pm – 5:45 pm)",
            "Morning" => "Shift - I (Morning)",
            "Day" => "Shift - II (Day)",
            "Evening" => "Shift - III (Evening)",
            _ => key
        };
    }

    private static ApplicantDashboardApplicationDto BuildApplicationInfo(
        StudentApplicantAccount account,
        ApplicantApplicationDraftDto draft)
    {
        draft ??= ApplicantApplicationDraftDto.Empty;

        var steps = new List<ApplicantDashboardApplicationStepDto>
        {
            new("registration", "Registration Created", true, "Your applicant account has been created."), // step 1
            new("personal", "Personal Information", IsPersonalInformationComplete(draft.PersonalInformation), "Complete your basic personal details."), // step 2
            new("address", "Addresses & Identity", IsAddressInformationComplete(draft.Address), "Provide your contact and identity information."), // step 3
            new("family", "Family & Guardian", AreGuardianDetailsComplete(draft.Contacts), "Add your parent or guardian contact information."), // step 4
            new("academics", "Academic Records", AreAcademicDetailsComplete(draft.Academics), "Fill in your Class XII marks and board details."), // step 5
            new("courses", "Course Preferences", AreCoursePreferencesComplete(draft.Courses), "Select your shift, major, minor and elective courses."), // step 6
            new("uploads", "Uploads & Declaration", AreUploadsComplete(draft), "Upload required documents and accept the applicant declaration."), // step 7
        };

        // Use actual account status instead of hardcoded status
        var status = account.Status.ToString();
        var statusText = account.Status switch
        {
            ApplicationStatus.Approved => "Approved",
            ApplicationStatus.Rejected => "Rejected",
            ApplicationStatus.WaitingList => "WaitingList",
            ApplicationStatus.EntranceExam => "EntranceExam",
            ApplicationStatus.Submitted => "Under Review",
            _ => draft.CoursesLocked ? "Under Review" : "Draft Pending"
        };

        return new ApplicantDashboardApplicationDto(
            IsApplicationSubmitted(draft),
            draft.CoursesLocked,
            statusText,
            steps);
    }

    private async Task<ApplicantDashboardPaymentDto> BuildPaymentInfoAsync(
        Domain.Admissions.Entities.StudentApplicantAccount account,
        ApplicantDashboardApplicationDto application,
        CancellationToken cancellationToken)
    {
        // Check if there's a pending admission offer
        var offer = await _admissionsRepository.GetOfferByAccountIdAsync(account.Id, cancellationToken);
        var hasPendingOffer = offer != null 
            && offer.Status == Domain.Admissions.Entities.OfferStatus.Pending
            && DateTime.UtcNow <= offer.ExpiryDate;

        // For admission fee payment, use the offer's admission fee amount if available
        // Otherwise, use the standard application fee
        const decimal applicationFee = 10m;
        decimal amountDue = applicationFee;
        
        // If there's a pending offer and account is approved, they need to pay admission fee
        // The admission fee is typically the same as application fee (₹10) for now
        // In the future, this could be stored in the offer or account
        
        var amountPaid = account.PaymentAmount ?? 0m;
        var isPaymentCompleted = account.IsPaymentCompleted;
        
        // Can pay if:
        // 1. Application is submitted AND payment not completed AND amount paid < fee, OR
        // 2. There's a pending offer (approved status) AND payment not completed
        var canPay = (application.IsSubmitted && !isPaymentCompleted && amountPaid < amountDue)
            || (hasPendingOffer && account.Status == Domain.Admissions.Entities.ApplicationStatus.Approved && !isPaymentCompleted);
        
        string status;
        if (isPaymentCompleted)
        {
            status = "Completed";
        }
        else if (canPay)
        {
            status = hasPendingOffer ? "Admission Fee Pending" : "Pending";
        }
        else if (application.IsSubmitted)
        {
            status = "Awaiting Processing";
        }
        else
        {
            status = "Unavailable";
        }

        return new ApplicantDashboardPaymentDto(
            amountDue,
            amountPaid,
            status,
            canPay,
            account.PaymentTransactionId);
    }

    private static bool IsApplicationSubmitted(ApplicantApplicationDraftDto draft) =>
        draft?.CoursesLocked ?? false;

    private static bool IsPersonalInformationComplete(PersonalInformationSection personal)
    {
        if (personal is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(personal.Religion)
            && personal.Religion.Trim().Equals("Christian", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(personal.Denomination))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(personal.NameAsPerAdmitCard)
               && !string.IsNullOrWhiteSpace(personal.DateOfBirth)
               && !string.IsNullOrWhiteSpace(personal.Gender)
               && !string.IsNullOrWhiteSpace(personal.MaritalStatus)
               && !string.IsNullOrWhiteSpace(personal.BloodGroup)
               && !string.IsNullOrWhiteSpace(personal.Category)
               && !string.IsNullOrWhiteSpace(personal.RaceOrTribe)
               && !string.IsNullOrWhiteSpace(personal.Religion);
    }

    private static bool IsAddressInformationComplete(AddressSection address)
    {
        if (address is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(address.AddressInTura)
               && !string.IsNullOrWhiteSpace(address.HomeAddress)
               && !string.IsNullOrWhiteSpace(address.AadhaarNumber)
               && !string.IsNullOrWhiteSpace(address.State)
               && !string.IsNullOrWhiteSpace(address.Email);
    }

    private static bool AreGuardianDetailsComplete(ContactSection contacts)
    {
        if (contacts is null)
        {
            return false;
        }

        return IsGuardianComplete(contacts.Father)
               && IsGuardianComplete(contacts.Mother)
               && IsGuardianComplete(contacts.LocalGuardian)
               && !string.IsNullOrWhiteSpace(contacts.HouseholdAreaType);
    }

    private static bool IsGuardianComplete(ParentOrGuardian guardian)
    {
        if (guardian is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(guardian.Name)
               && !string.IsNullOrWhiteSpace(guardian.Age)
               && !string.IsNullOrWhiteSpace(guardian.Occupation)
               && !string.IsNullOrWhiteSpace(guardian.ContactNumber1);
    }

    private static bool AreAcademicDetailsComplete(AcademicSection academics)
    {
        if (academics is null)
        {
            return false;
        }

        var board = academics.BoardExamination;
        var hasBoardDetails =
            board is not null &&
            !string.IsNullOrWhiteSpace(board.RollNumber) &&
            !string.IsNullOrWhiteSpace(board.Year) &&
            !string.IsNullOrWhiteSpace(board.Percentage) &&
            !string.IsNullOrWhiteSpace(board.Division) &&
            !string.IsNullOrWhiteSpace(board.BoardName) &&
            !string.IsNullOrWhiteSpace(board.RegistrationType);

        // New Class XII model: at least 5 subject rows when board+stream are set, or when board is OTHER (manual entry; stream optional).
        var boardCode = academics.ClassXiiBoardCode?.Trim();
        var streamCode = academics.ClassXiiStreamCode?.Trim();
        var usesNewSubjectRules =
            (!string.IsNullOrWhiteSpace(boardCode) && !string.IsNullOrWhiteSpace(streamCode))
            || string.Equals(boardCode, "OTHER", StringComparison.OrdinalIgnoreCase);

        var filledSubjectRows = CountFilledClassXiiSubjectRows(academics);
        var hasSubjects = usesNewSubjectRules
            ? filledSubjectRows >= 5
            : academics.ClassXII is not null
              && academics.ClassXII.Any(s =>
                  !string.IsNullOrWhiteSpace(s.Subject) &&
                  !string.IsNullOrWhiteSpace(s.Marks));

        var hasLastInstitution = !string.IsNullOrWhiteSpace(academics.LastInstitutionAttended);

        return hasBoardDetails && hasSubjects && hasLastInstitution;
    }

    private static int CountFilledClassXiiSubjectRows(AcademicSection academics)
    {
        if (academics.ClassXiiSubjects is { Count: > 0 })
        {
            return academics.ClassXiiSubjects.Count(s =>
                !string.IsNullOrWhiteSpace(s.Subject) && !string.IsNullOrWhiteSpace(s.Marks));
        }

        if (academics.ClassXII is { Count: > 0 })
        {
            return academics.ClassXII.Count(s =>
                !string.IsNullOrWhiteSpace(s.Subject) && !string.IsNullOrWhiteSpace(s.Marks));
        }

        return 0;
    }

    private static bool AreCoursePreferencesComplete(CoursePreferencesSection courses)
    {
        if (courses is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(courses.Shift)
               && !string.IsNullOrWhiteSpace(courses.MajorSubject)
               && !string.IsNullOrWhiteSpace(courses.MinorSubject)
               && !string.IsNullOrWhiteSpace(courses.MultidisciplinaryChoice)
               && !string.IsNullOrWhiteSpace(courses.AbilityEnhancementChoice)
               && !string.IsNullOrWhiteSpace(courses.SkillEnhancementChoice);
    }

    private static bool AreUploadsComplete(ApplicantApplicationDraftDto draft)
    {
        var uploads = draft.Uploads;
        if (uploads is null)
        {
            return false;
        }

        var personal = draft.PersonalInformation ?? new PersonalInformationSection();

        var stdXUploaded = IsAttachmentUploaded(uploads.StdXMarksheet);
        var stdXIIUploaded = IsAttachmentUploaded(uploads.StdXIIMarksheet);
        var cuetUploaded = IsAttachmentUploaded(uploads.CuetMarksheet); // optional

        var differentlyAbledUploaded = !personal.IsDifferentlyAbled
            || IsAttachmentUploaded(uploads.DifferentlyAbledProof);

        var ewsUploaded = !personal.IsEconomicallyWeaker
            || IsAttachmentUploaded(uploads.EconomicallyWeakerProof);

        var declarationAccepted = draft.DeclarationAccepted;

        return stdXUploaded && stdXIIUploaded && differentlyAbledUploaded && ewsUploaded && declarationAccepted;
    }

    private static bool IsAttachmentUploaded(FileAttachmentDto? attachment) =>
        attachment is not null
        && !string.IsNullOrWhiteSpace(attachment.FileName)
        && !string.IsNullOrWhiteSpace(attachment.Data);

    private async Task<ApplicantDashboardOfferDto?> BuildOfferInfoAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var offer = await _admissionsRepository.GetOfferByAccountIdAsync(accountId, cancellationToken);
        if (offer == null)
        {
            return null;
        }

        // Mark expired if needed
        offer.MarkExpired();
        if (offer.Status == Domain.Admissions.Entities.OfferStatus.Expired)
        {
            await _admissionsRepository.UpdateAdmissionOfferAsync(offer, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var isExpired = offer.Status == Domain.Admissions.Entities.OfferStatus.Expired || now > offer.ExpiryDate;
        var daysUntilExpiry = isExpired ? 0 : (int)Math.Ceiling((offer.ExpiryDate - now).TotalDays);

        return new ApplicantDashboardOfferDto(
            offer.Id,
            offer.ApplicationNumber,
            offer.MeritRank,
            offer.Shift,
            offer.MajorSubject,
            offer.Status.ToString(),
            offer.OfferDate,
            offer.ExpiryDate,
            offer.AcceptedOnUtc,
            offer.RejectedOnUtc,
            isExpired,
            daysUntilExpiry);
    }
}

