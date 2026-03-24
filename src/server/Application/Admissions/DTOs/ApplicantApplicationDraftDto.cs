using System.Text.Json.Serialization;

namespace ERP.Application.Admissions.DTOs;

public class ApplicantApplicationDraftDto
{
    public PersonalInformationSection PersonalInformation { get; set; } = new();
    public AddressSection Address { get; set; } = new();
    public ContactSection Contacts { get; set; } = new();
    public AcademicSection Academics { get; set; } = new();
    public CoursePreferencesSection Courses { get; set; } = new();
    public UploadSection Uploads { get; set; } = new();
    public bool DeclarationAccepted { get; set; }
    public bool CoursesLocked { get; set; }
    public Dictionary<string, DocumentVerificationStatusDto>? DocumentVerificationStatus { get; set; }

    public static ApplicantApplicationDraftDto Empty => new();
}

public class PersonalInformationSection
{
    public string NameAsPerAdmitCard { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Shift { get; set; }
    public string MaritalStatus { get; set; } = string.Empty;
    public string BloodGroup { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RaceOrTribe { get; set; } = string.Empty;
    public string Religion { get; set; } = string.Empty;

    /// <summary>Christian denominations; null/empty when religion is not Christian.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Denomination { get; set; }

    public bool IsDifferentlyAbled { get; set; }
    public bool IsEconomicallyWeaker { get; set; }
}

public class AddressSection
{
    public string AddressInTura { get; set; } = string.Empty;
    public string HomeAddress { get; set; } = string.Empty;
    public bool SameAsTura { get; set; }
    public string AadhaarNumber { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ContactSection
{
    public ParentOrGuardian Father { get; set; } = new();
    public ParentOrGuardian Mother { get; set; } = new();
    public ParentOrGuardian LocalGuardian { get; set; } = new();
    public string HouseholdAreaType { get; set; } = string.Empty;
}

public class ParentOrGuardian
{
    public string Name { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string ContactNumber1 { get; set; } = string.Empty;
}

public class AcademicSection
{
    /// <summary>Canonical board code for Class XII subject selection: MBOSE, CBSE, ISC, OTHER.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ClassXiiBoardCode { get; set; }

    /// <summary>Canonical stream: ARTS, SCIENCE, COMMERCE.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ClassXiiStreamCode { get; set; }

    /// <summary>Dynamic subject rows (board/stream driven or manual for OTHER).</summary>
    public List<ClassXiiSubjectRowDto> ClassXiiSubjects { get; set; } = new();

    /// <summary>Legacy list; kept in sync with <see cref="ClassXiiSubjects"/> for PDFs and exports.</summary>
    public List<SubjectMarkDto> ClassXII { get; set; } = new();
    public BoardExaminationDetail BoardExamination { get; set; } = new();
    public CuetDetail Cuet { get; set; } = new();
    public string LastInstitutionAttended { get; set; } = string.Empty;
}

public class SubjectMarkDto
{
    public string Subject { get; set; } = string.Empty;
    public string Marks { get; set; } = string.Empty;
}

public class ClassXiiSubjectRowDto
{
    public string Subject { get; set; } = string.Empty;
    public string Marks { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid? SubjectMasterId { get; set; }

    /// <summary>master = chosen from catalog; manual = typed (OTHER board or legacy).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? EntryMode { get; set; }
}

public class BoardExaminationDetail
{
    public string RollNumber { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string TotalMarks { get; set; } = string.Empty;
    public string Percentage { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public string RegistrationType { get; set; } = string.Empty;
}

public class CuetDetail
{
    public string Marks { get; set; } = string.Empty;
    public string RollNumber { get; set; } = string.Empty;
}

public class CoursePreferencesSection
{
    public string Shift { get; set; } = string.Empty;
    public string MajorSubject { get; set; } = string.Empty;
    public string MinorSubject { get; set; } = string.Empty;
    public string MultidisciplinaryChoice { get; set; } = string.Empty;
    public string AbilityEnhancementChoice { get; set; } = string.Empty;
    public string SkillEnhancementChoice { get; set; } = string.Empty;
    public string ValueAddedChoice { get; set; } = "VAC 140";
}

public class UploadSection
{
    public FileAttachmentDto? StdXMarksheet { get; set; }
    public FileAttachmentDto? StdXIIMarksheet { get; set; }
    public FileAttachmentDto? CuetMarksheet { get; set; }
    public FileAttachmentDto? DifferentlyAbledProof { get; set; }
    public FileAttachmentDto? EconomicallyWeakerProof { get; set; }
}

public class FileAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class DocumentVerificationStatusDto
{
    public bool IsVerified { get; set; }
    public DateTime VerifiedOnUtc { get; set; }
    public string? VerifiedBy { get; set; }
    public string? Remarks { get; set; }
}

public sealed class ApplicantApplicationDraftResponse
{
    public ApplicantApplicationDraftDto Payload { get; init; } = ApplicantApplicationDraftDto.Empty;
    public DateTime UpdatedOnUtc { get; init; }
}

