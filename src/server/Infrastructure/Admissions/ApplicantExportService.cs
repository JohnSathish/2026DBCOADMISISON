using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using ERP.Application.Admissions;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.ViewModels;
using ERP.Domain.Admissions.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

namespace ERP.Infrastructure.Admissions;

public class ApplicantExportService : IApplicantExportService
{
    private readonly IAdmissionsReadRepository _readRepository;
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly IApplicantApplicationRepository _applicationRepository;
    private static bool _questPdfLicenseConfigured;
    private static readonly object LicenseLock = new();
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public ApplicantExportService(
        IAdmissionsReadRepository readRepository,
        IApplicantAccountRepository accountRepository,
        IApplicantApplicationRepository applicationRepository)
    {
        _readRepository = readRepository;
        _accountRepository = accountRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<ApplicantExportResult> ExportAsync(ApplicantExportFormat format, CancellationToken cancellationToken = default)
    {
        var applicants = await _readRepository.ListAllApplicantsAsync(cancellationToken);

        return format switch
        {
            ApplicantExportFormat.Csv => GenerateCsv(applicants),
            ApplicantExportFormat.Excel => GenerateExcel(applicants),
            ApplicantExportFormat.Pdf => GeneratePdf(applicants),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
        };
    }

    private static ApplicantExportResult GenerateCsv(IReadOnlyCollection<ApplicantDto> applicants)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.WriteRecords(applicants.Select(MapApplicantExportRow));
        writer.Flush();

        return new ApplicantExportResult(
            $"applicants-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv",
            stream.ToArray());
    }

    private static ApplicantExportResult GenerateExcel(IReadOnlyCollection<ApplicantDto> applicants)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Applicants");

        var headers = ApplicantExportRow.Headers;
        for (var col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var applicant in applicants.Select(MapApplicantExportRow))
        {
            worksheet.Cell(row, 1).Value = applicant.ApplicationNumber;
            worksheet.Cell(row, 2).Value = applicant.FullName;
            worksheet.Cell(row, 3).Value = applicant.Email;
            worksheet.Cell(row, 4).Value = applicant.MobileNumber;
            worksheet.Cell(row, 5).Value = applicant.DateOfBirth;
            worksheet.Cell(row, 6).Value = applicant.Program;
            worksheet.Cell(row, 7).Value = applicant.Status;
            worksheet.Cell(row, 8).Value = applicant.StatusUpdatedOnUtc;
            worksheet.Cell(row, 9).Value = applicant.CreatedOnUtc;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ApplicantExportResult(
            $"applicants-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    private static ApplicantExportResult GeneratePdf(IReadOnlyCollection<ApplicantDto> applicants)
    {
        ConfigureQuestPdfLicense();

        var rows = applicants.Select(MapApplicantExportRow).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Text($"Applicant Export · Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                    .SemiBold().FontSize(12);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(120); // Application Number
                        columns.RelativeColumn();    // Full Name
                        columns.RelativeColumn();    // Email
                        columns.ConstantColumn(110); // Mobile
                        columns.ConstantColumn(100); // Date of Birth
                        columns.RelativeColumn(0.8f); // Program
                        columns.ConstantColumn(110); // Status
                        columns.ConstantColumn(140); // Status Updated
                        columns.ConstantColumn(120); // Created On
                    });

                    table.Header(header =>
                    {
                        foreach (var headerName in ApplicantExportRow.Headers)
                        {
                            header.Cell().Element(CellStyle).Text(headerName).SemiBold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Element(CellStyle).Text(row.ApplicationNumber);
                        table.Cell().Element(CellStyle).Text(row.FullName);
                        table.Cell().Element(CellStyle).Text(row.Email);
                        table.Cell().Element(CellStyle).Text(row.MobileNumber);
                        table.Cell().Element(CellStyle).Text(row.DateOfBirth);
                        table.Cell().Element(CellStyle).Text(row.Program);
                        table.Cell().Element(CellStyle).Text(row.Status);
                        table.Cell().Element(CellStyle).Text(row.StatusUpdatedOnUtc);
                        table.Cell().Element(CellStyle).Text(row.CreatedOnUtc);
                    }

                    static IContainer CellStyle(IContainer container) =>
                        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(4);
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by Don Bosco ERP Admissions").FontSize(9);
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return new ApplicantExportResult(
            $"applicants-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            "application/pdf",
            pdfBytes);
    }

    private static ApplicantExportRow MapApplicantExportRow(ApplicantDto dto) =>
        new(
            dto.ApplicationNumber,
            dto.FullName,
            dto.Email,
            dto.MobileNumber,
            dto.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dto.ProgramCode,
            dto.Status,
            dto.StatusUpdatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            dto.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

    private static void ConfigureQuestPdfLicense()
    {
        if (_questPdfLicenseConfigured)
        {
            return;
        }

        lock (LicenseLock)
        {
            if (_questPdfLicenseConfigured)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            _questPdfLicenseConfigured = true;
        }
    }

    private static readonly Dictionary<string, string> CourseCodeToNameMap = new()
    {
        ["MDC 111"] = "MDC 111 — CULTURE AND SOCIETY",
        ["MDC 112"] = "MDC 112 — FUNDAMENTALS OF COMPUTER SYSTEMS",
        ["MDC 116"] = "MDC 116 — INTRODUCTION TO NATIONAL CADET CORPS",
        ["MDC 118"] = "MDC 118 — MATHEMATICS IN DAILY LIFE",
        ["MDC 119"] = "MDC 119 — PHILOSOPHY OF CULTURE",
        ["MDC 110"] = "MDC 110 — COMMERCIAL ARITHMETIC & ELEMENTARY STATISTICS",
        ["MDC 115"] = "MDC 115 — INTRODUCTION TO LIFE SCIENCES",
        ["AEC 120"] = "AEC 120 — ALTERNATIVE ENGLISH",
        ["AEC 123"] = "AEC 123 — MIL-I: GARO",
        ["SEC 131"] = "SEC 131 — MOTIVATION",
        ["SEC 132"] = "SEC 132 — PERSONALITY DEVELOPMENT",
        ["SEC 133"] = "SEC 133 — PUBLIC SPEAKING",
        ["VAC 140"] = "VAC 140 — ENVIRONMENT STUDIES",
    };

    private static string GetCourseName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }
        return CourseCodeToNameMap.TryGetValue(code.Trim(), out var name) ? name : code;
    }

    public async Task<ApplicantExportResult> ExportPaidApplicationsWithFullDetailsAsync(CancellationToken cancellationToken = default)
    {
        // Get all paid applications
        var (accounts, _) = await _accountRepository.GetPagedAsync(
            page: 1,
            pageSize: int.MaxValue,
            isApplicationSubmitted: true,
            isPaymentCompleted: true,
            searchTerm: null,
            cancellationToken: cancellationToken);

        // Load application drafts for each account
        var exportData = new List<PaidApplicationExportRow>();
        
        // Collect all unique Class XII subjects across all applications
        var allSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var account in accounts)
        {
            ApplicantApplicationDraftDto? draft = null;
            if (account.IsApplicationSubmitted)
            {
                var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(account.Id, cancellationToken);
                if (draftEntity is not null)
                {
                    draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                           ?? ApplicantApplicationDraftDto.Empty;
                    ApplyClassXiiDraftSync(draft);

                    // Collect subjects
                    if (draft.Academics?.ClassXII != null)
                    {
                        foreach (var subject in draft.Academics.ClassXII)
                        {
                            if (!string.IsNullOrWhiteSpace(subject.Subject))
                            {
                                allSubjects.Add(subject.Subject.Trim());
                            }
                        }
                    }
                }
            }
        }

        // Sort subjects for consistent column ordering
        var sortedSubjects = allSubjects.OrderBy(s => s).ToList();

        // Map all accounts to export rows
        foreach (var account in accounts)
        {
            ApplicantApplicationDraftDto? draft = null;
            if (account.IsApplicationSubmitted)
            {
                var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(account.Id, cancellationToken);
                if (draftEntity is not null)
                {
                    draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                           ?? ApplicantApplicationDraftDto.Empty;
                    ApplyClassXiiDraftSync(draft);
                }
            }

            exportData.Add(MapToPaidApplicationExportRow(account, draft, sortedSubjects));
        }

        return GeneratePaidApplicationsExcel(exportData, sortedSubjects);
    }

    public async Task<ApplicantExportResult> ExportSubmittedApplicationsWithFullDetailsAsync(CancellationToken cancellationToken = default)
    {
        var (accounts, _) = await _accountRepository.GetPagedAsync(
            page: 1,
            pageSize: int.MaxValue,
            isApplicationSubmitted: true,
            isPaymentCompleted: null,
            searchTerm: null,
            cancellationToken: cancellationToken);

        var exportData = new List<PaidApplicationExportRow>();
        var allSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in accounts)
        {
            ApplicantApplicationDraftDto? draft = null;
            if (account.IsApplicationSubmitted)
            {
                var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(account.Id, cancellationToken);
                if (draftEntity is not null)
                {
                    draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                           ?? ApplicantApplicationDraftDto.Empty;
                    ApplyClassXiiDraftSync(draft);

                    if (draft.Academics?.ClassXII != null)
                    {
                        foreach (var subject in draft.Academics.ClassXII)
                        {
                            if (!string.IsNullOrWhiteSpace(subject.Subject))
                            {
                                allSubjects.Add(subject.Subject.Trim());
                            }
                        }
                    }
                }
            }
        }

        var sortedSubjects = allSubjects.OrderBy(s => s).ToList();

        foreach (var account in accounts)
        {
            ApplicantApplicationDraftDto? draft = null;
            if (account.IsApplicationSubmitted)
            {
                var draftEntity = await _applicationRepository.GetDraftByAccountIdAsync(account.Id, cancellationToken);
                if (draftEntity is not null)
                {
                    draft = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(draftEntity.Data, _serializerOptions)
                           ?? ApplicantApplicationDraftDto.Empty;
                    ApplyClassXiiDraftSync(draft);
                }
            }

            exportData.Add(MapToPaidApplicationExportRow(account, draft, sortedSubjects));
        }

        var result = GeneratePaidApplicationsExcel(exportData, sortedSubjects);
        return new ApplicantExportResult(
            $"submitted-applications-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            result.ContentType,
            result.Content);
    }

    private static PaidApplicationExportRow MapToPaidApplicationExportRow(
        StudentApplicantAccount account,
        ApplicantApplicationDraftDto? draft,
        List<string> allSubjects)
    {
        draft ??= ApplicantApplicationDraftDto.Empty;
        var personal = draft.PersonalInformation ?? new PersonalInformationSection();
        var address = draft.Address ?? new AddressSection();
        var contacts = draft.Contacts ?? new ContactSection();
        var academics = draft.Academics ?? new AcademicSection();
        var courses = draft.Courses ?? new CoursePreferencesSection();

        // Create a dictionary for Class XII subjects for quick lookup
        var subjectMarksDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (academics.ClassXII != null)
        {
            foreach (var subject in academics.ClassXII)
            {
                if (!string.IsNullOrWhiteSpace(subject.Subject))
                {
                    subjectMarksDict[subject.Subject.Trim()] = subject.Marks ?? "";
                }
            }
        }

        // Format board exam details
        var board = academics.BoardExamination ?? new BoardExaminationDetail();

        return new PaidApplicationExportRow(
            // Basic Info
            account.UniqueId,
            account.FullName,
            account.Email,
            account.MobileNumber,
            account.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            account.Gender,
            account.Shift,
            
            // Personal Information
            personal.NameAsPerAdmitCard,
            personal.MaritalStatus,
            personal.BloodGroup,
            personal.Category,
            personal.RaceOrTribe,
            personal.Religion,
            personal.Denomination ?? "",
            academics.ClassXiiBoardCode ?? "",
            academics.ClassXiiStreamCode ?? "",
            personal.IsDifferentlyAbled ? "Yes" : "No",
            personal.IsEconomicallyWeaker ? "Yes" : "No",
            
            // Address
            address.AddressInTura,
            address.HomeAddress,
            address.SameAsTura ? "Yes" : "No",
            address.AadhaarNumber,
            address.State,
            
            // Family/Guardian
            contacts.Father?.Name ?? "",
            contacts.Father?.Age ?? "",
            contacts.Father?.Occupation ?? "",
            contacts.Father?.ContactNumber1 ?? "",
            contacts.Mother?.Name ?? "",
            contacts.Mother?.Age ?? "",
            contacts.Mother?.Occupation ?? "",
            contacts.Mother?.ContactNumber1 ?? "",
            contacts.LocalGuardian?.Name ?? "",
            contacts.LocalGuardian?.Age ?? "",
            contacts.LocalGuardian?.Occupation ?? "",
            contacts.LocalGuardian?.ContactNumber1 ?? "",
            contacts.HouseholdAreaType,
            
            // Academic
            board.RollNumber,
            board.Year,
            board.TotalMarks,
            board.Percentage,
            board.Division,
            board.BoardName,
            board.RegistrationType,
            subjectMarksDict, // Pass dictionary instead of string
            allSubjects, // Pass list of all subjects for column ordering
            academics.Cuet?.RollNumber ?? "",
            academics.Cuet?.Marks ?? "",
            academics.LastInstitutionAttended,
            
            // Course Preferences
            courses.MajorSubject,
            courses.MinorSubject,
            GetCourseName(courses.MultidisciplinaryChoice), // Convert code to name
            GetCourseName(courses.AbilityEnhancementChoice), // Convert code to name
            GetCourseName(courses.SkillEnhancementChoice), // Convert code to name
            GetCourseName(courses.ValueAddedChoice), // Convert code to name
            
            // Payment & Status
            account.IsPaymentCompleted ? "Yes" : "No",
            account.PaymentAmount?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
            account.PaymentTransactionId ?? "",
            account.PaymentCompletedOnUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
            account.Status.ToString(),
            account.StatusUpdatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            account.StatusRemarks ?? "",
            account.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
    }

    private static ApplicantExportResult GeneratePaidApplicationsExcel(
        IReadOnlyCollection<PaidApplicationExportRow> data,
        List<string> allSubjects)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Paid Applications");

        // Build headers dynamically
        var headers = new List<string>
        {
            "Application Number", "Full Name", "Email", "Mobile Number", "Date of Birth", "Gender", "Shift",
            "Name as per Admit Card", "Marital Status", "Blood Group", "Category", "Race/Tribe", "Religion",
            "Denomination", "Class XII Board", "Class XII Stream",
            "Differently Abled", "Economically Weaker",
            "Address in Tura", "Home Address", "Same as Tura", "Aadhaar Number", "State",
            "Father Name", "Father Age", "Father Occupation", "Father Contact",
            "Mother Name", "Mother Age", "Mother Occupation", "Mother Contact",
            "Local Guardian Name", "Local Guardian Age", "Local Guardian Occupation", "Local Guardian Contact",
            "Household Area Type",
            "Board Roll Number", "Board Year", "Board Total Marks", "Board Percentage", "Board Division",
            "Board Name", "Board Registration Type"
        };

        // Add Class XII subject columns (Subject Name and Marks for each subject)
        foreach (var subject in allSubjects)
        {
            headers.Add($"Class XII {subject}");
            headers.Add($"Class XII {subject} Marks");
        }

        headers.AddRange(new[]
        {
            "CUET Roll Number", "CUET Marks",
            "Last Institution Attended",
            "Major Subject", "Minor Subject", "Multidisciplinary Choice", "Ability Enhancement Choice",
            "Skill Enhancement Choice", "Value Added Choice",
            "Payment Completed", "Payment Amount", "Payment Transaction ID", "Payment Date",
            "Application Status", "Status Updated On", "Status Remarks", "Created On"
        });

        // Set headers
        for (var col = 0; col < headers.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Add data
        var row = 2;
        foreach (var item in data)
        {
            var col = 1;
            worksheet.Cell(row, col++).Value = item.ApplicationNumber;
            worksheet.Cell(row, col++).Value = item.FullName;
            worksheet.Cell(row, col++).Value = item.Email;
            worksheet.Cell(row, col++).Value = item.MobileNumber;
            worksheet.Cell(row, col++).Value = item.DateOfBirth;
            worksheet.Cell(row, col++).Value = item.Gender;
            worksheet.Cell(row, col++).Value = item.Shift;
            worksheet.Cell(row, col++).Value = item.NameAsPerAdmitCard;
            worksheet.Cell(row, col++).Value = item.MaritalStatus;
            worksheet.Cell(row, col++).Value = item.BloodGroup;
            worksheet.Cell(row, col++).Value = item.Category;
            worksheet.Cell(row, col++).Value = item.RaceOrTribe;
            worksheet.Cell(row, col++).Value = item.Religion;
            worksheet.Cell(row, col++).Value = item.Denomination;
            worksheet.Cell(row, col++).Value = item.ClassXiiBoardCode;
            worksheet.Cell(row, col++).Value = item.ClassXiiStreamCode;
            worksheet.Cell(row, col++).Value = item.IsDifferentlyAbled;
            worksheet.Cell(row, col++).Value = item.IsEconomicallyWeaker;
            worksheet.Cell(row, col++).Value = item.AddressInTura;
            worksheet.Cell(row, col++).Value = item.HomeAddress;
            worksheet.Cell(row, col++).Value = item.SameAsTura;
            worksheet.Cell(row, col++).Value = item.AadhaarNumber;
            worksheet.Cell(row, col++).Value = item.State;
            worksheet.Cell(row, col++).Value = item.FatherName;
            worksheet.Cell(row, col++).Value = item.FatherAge;
            worksheet.Cell(row, col++).Value = item.FatherOccupation;
            worksheet.Cell(row, col++).Value = item.FatherContact;
            worksheet.Cell(row, col++).Value = item.MotherName;
            worksheet.Cell(row, col++).Value = item.MotherAge;
            worksheet.Cell(row, col++).Value = item.MotherOccupation;
            worksheet.Cell(row, col++).Value = item.MotherContact;
            worksheet.Cell(row, col++).Value = item.LocalGuardianName;
            worksheet.Cell(row, col++).Value = item.LocalGuardianAge;
            worksheet.Cell(row, col++).Value = item.LocalGuardianOccupation;
            worksheet.Cell(row, col++).Value = item.LocalGuardianContact;
            worksheet.Cell(row, col++).Value = item.HouseholdAreaType;
            worksheet.Cell(row, col++).Value = item.BoardRollNumber;
            worksheet.Cell(row, col++).Value = item.BoardYear;
            worksheet.Cell(row, col++).Value = item.BoardTotalMarks;
            worksheet.Cell(row, col++).Value = item.BoardPercentage;
            worksheet.Cell(row, col++).Value = item.BoardDivision;
            worksheet.Cell(row, col++).Value = item.BoardName;
            worksheet.Cell(row, col++).Value = item.BoardRegistrationType;

            // Add Class XII subjects (subject name and marks for each)
            foreach (var subject in allSubjects)
            {
                // Subject name column
                worksheet.Cell(row, col++).Value = subject;
                // Marks column
                worksheet.Cell(row, col++).Value = item.ClassXiiSubjects.TryGetValue(subject, out var marks) ? marks : "";
            }

            worksheet.Cell(row, col++).Value = item.CuetRollNumber;
            worksheet.Cell(row, col++).Value = item.CuetMarks;
            worksheet.Cell(row, col++).Value = item.LastInstitutionAttended;
            worksheet.Cell(row, col++).Value = item.MajorSubject;
            worksheet.Cell(row, col++).Value = item.MinorSubject;
            worksheet.Cell(row, col++).Value = item.MultidisciplinaryChoice;
            worksheet.Cell(row, col++).Value = item.AbilityEnhancementChoice;
            worksheet.Cell(row, col++).Value = item.SkillEnhancementChoice;
            worksheet.Cell(row, col++).Value = item.ValueAddedChoice;
            worksheet.Cell(row, col++).Value = item.PaymentCompleted;
            worksheet.Cell(row, col++).Value = item.PaymentAmount;
            worksheet.Cell(row, col++).Value = item.PaymentTransactionId;
            worksheet.Cell(row, col++).Value = item.PaymentDate;
            worksheet.Cell(row, col++).Value = item.ApplicationStatus;
            worksheet.Cell(row, col++).Value = item.StatusUpdatedOn;
            worksheet.Cell(row, col++).Value = item.StatusRemarks;
            worksheet.Cell(row, col++).Value = item.CreatedOn;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1); // Freeze header row

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ApplicantExportResult(
            $"paid-applications-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray());
    }

    private static void ApplyClassXiiDraftSync(ApplicantApplicationDraftDto? draft)
    {
        if (draft?.Academics is null)
        {
            return;
        }

        AcademicSectionDraftSync.SyncBidirectional(draft.Academics);
    }

    private sealed record PaidApplicationExportRow(
        // Basic Info
        string ApplicationNumber,
        string FullName,
        string Email,
        string MobileNumber,
        string DateOfBirth,
        string Gender,
        string Shift,
        
        // Personal Information
        string NameAsPerAdmitCard,
        string MaritalStatus,
        string BloodGroup,
        string Category,
        string RaceOrTribe,
        string Religion,
        string Denomination,
        string ClassXiiBoardCode,
        string ClassXiiStreamCode,
        string IsDifferentlyAbled,
        string IsEconomicallyWeaker,
        
        // Address
        string AddressInTura,
        string HomeAddress,
        string SameAsTura,
        string AadhaarNumber,
        string State,
        
        // Family/Guardian
        string FatherName,
        string FatherAge,
        string FatherOccupation,
        string FatherContact,
        string MotherName,
        string MotherAge,
        string MotherOccupation,
        string MotherContact,
        string LocalGuardianName,
        string LocalGuardianAge,
        string LocalGuardianOccupation,
        string LocalGuardianContact,
        string HouseholdAreaType,
        
        // Academic
        string BoardRollNumber,
        string BoardYear,
        string BoardTotalMarks,
        string BoardPercentage,
        string BoardDivision,
        string BoardName,
        string BoardRegistrationType,
        Dictionary<string, string> ClassXiiSubjects, // Changed from string to Dictionary
        List<string> AllSubjects, // Added for column ordering
        string CuetRollNumber,
        string CuetMarks,
        string LastInstitutionAttended,
        
        // Course Preferences
        string MajorSubject,
        string MinorSubject,
        string MultidisciplinaryChoice,
        string AbilityEnhancementChoice,
        string SkillEnhancementChoice,
        string ValueAddedChoice,
        
        // Payment & Status
        string PaymentCompleted,
        string PaymentAmount,
        string PaymentTransactionId,
        string PaymentDate,
        string ApplicationStatus,
        string StatusUpdatedOn,
        string StatusRemarks,
        string CreatedOn);

    private sealed record ApplicantExportRow(
        string ApplicationNumber,
        string FullName,
        string Email,
        string MobileNumber,
        string DateOfBirth,
        string Program,
        string Status,
        string StatusUpdatedOnUtc,
        string CreatedOnUtc)
    {
        public static readonly string[] Headers =
        {
            "Application Number",
            "Full Name",
            "Email",
            "Mobile Number",
            "Date of Birth",
            "Program",
            "Status",
            "Status Updated (UTC)",
            "Submitted On (UTC)"
        };
    }
}

