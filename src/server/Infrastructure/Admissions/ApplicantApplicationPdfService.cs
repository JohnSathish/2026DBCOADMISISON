using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Admissions;

public class ApplicantApplicationPdfService : IApplicantApplicationPdfService
{
    private static readonly object LicenseLock = new();
    private static bool _questPdfLicenseConfigured;
    private readonly record struct StepProgress(string Key, string Title, bool IsComplete);
    private static readonly IReadOnlyDictionary<string, string> ShiftDisplayNames =
         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
         {
            ["ShiftI"] = "Shift - I (6:30 am to 9:30 am)",
            ["ShiftII"] = "Shift - II (9:45 am to 3:30 pm)",
            ["ShiftIII"] = "Shift - III (2:45 pm to 5:45 pm)",
            ["Morning"] = "Shift - I (Morning)",
            ["Day"] = "Shift - II (Day)",
            ["Evening"] = "Shift - III (Evening)",
            ["SHIFT - I (TIMING : 7.30 AM - 1.15 PM)"] = "Shift - I (Legacy)",
            ["SHIFT - II (TIMING : 9.45 AM - 3.30 PM)"] = "Shift - II (Legacy)",
            ["SHIFT - III (TIMING : 1.30 PM - 6.15 PM)"] = "Shift - III (Legacy)"
         };

    private static readonly IReadOnlyDictionary<string, string> CourseOptionDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

    public Task<ApplicantApplicationPdfResult> GenerateAsync(
        ApplicantApplicationDraftDto payload,
        bool isPaymentCompleted,
        decimal? paymentAmount,
        string? photoUrl = null,
        string? transactionId = null,
        string? applicationNumber = null,
        DateTime? submittedOnUtc = null,
        CancellationToken cancellationToken = default)
    {
        ConfigureQuestPdfLicense();

        var generatedOn = DateTime.UtcNow;
        var fileName = $"admission-application-{generatedOn:yyyyMMddHHmmss}.pdf";
        const decimal applicationFee = 10m;

        var pdf = Document.Create(container =>
        {
            // Page 1 — application data (no embedded document previews)
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(12).FontColor(LabelMuted));

                page.Header()
                    .Element(header => BuildFormHeader(header, applicationNumber, submittedOnUtc ?? generatedOn));

                page.Content().Column(column =>
                {
                    column.Spacing(18);

                    column.Item().PaddingTop(2).PaddingBottom(6).Text("FOUR YEAR UNDERGRADUATE PROGRAMME (FYUP) SEMESTER I")
                        .Bold()
                        .FontSize(13)
                        .FontColor(BrandPrimary)
                        .LetterSpacing(0.3f);

                    column.Item().Element(inner => BuildSectionCardWithPhoto(
                        inner,
                        "Personal Information",
                        payload,
                        photoUrl,
                        new[]
                        {
                            ("Name (as per Class X Admit Card)", payload.PersonalInformation.NameAsPerAdmitCard),
                            ("Date of Birth", FormatDate(payload.PersonalInformation.DateOfBirth)),
                            ("Gender", payload.PersonalInformation.Gender),
                            ("Marital Status", payload.PersonalInformation.MaritalStatus),
                            ("Blood Group", payload.PersonalInformation.BloodGroup),
                            ("Category", payload.PersonalInformation.Category),
                            ("Race / Tribe", payload.PersonalInformation.RaceOrTribe),
                            ("Religion", payload.PersonalInformation.Religion),
                            ("Denomination", FormatDenominationForPdf(payload.PersonalInformation.Religion, payload.PersonalInformation.Denomination)),
                            ("Differently Abled", FormatBoolean(payload.PersonalInformation.IsDifferentlyAbled)),
                            ("Economically Weaker Section", FormatBoolean(payload.PersonalInformation.IsEconomicallyWeaker))
                        }));

                    column.Item().Element(inner => BuildSectionCard(
                        inner,
                        "Address & Identity",
                        new[]
                        {
                            ("State", payload.Address.State),
                            ("Aadhaar Number", payload.Address.AadhaarNumber),
                            ("E mail", payload.Address.Email),
                            ("Same as Tura Address", FormatBoolean(payload.Address.SameAsTura)),
                            ("Address in Tura", payload.Address.AddressInTura),
                            ("Home Address", payload.Address.HomeAddress)
                        }));

                    column.Item().Element(inner => BuildAcademicSection(inner, payload.Academics));

                    column.Item().Element(inner => BuildSectionCard(
                        inner,
                        "Board Examination Details",
                        new[]
                        {
                            ("Board / University", payload.Academics.BoardExamination.BoardName),
                            ("Roll Number", payload.Academics.BoardExamination.RollNumber),
                            ("Year", payload.Academics.BoardExamination.Year),
                            ("Division", payload.Academics.BoardExamination.Division),
                            ("Mode of Study", payload.Academics.BoardExamination.RegistrationType),
                            ("Last Institution Attended", payload.Academics.LastInstitutionAttended)
                        }));

                    column.Item().Element(inner => BuildCourseHighlightCard(inner, payload.Courses, payload.PersonalInformation.Shift));

                    column.Item().Element(inner => BuildPaymentOfficialSection(
                        inner,
                        applicationFee,
                        isPaymentCompleted,
                        paymentAmount,
                        transactionId));

                    column.Item().Element(inner => BuildFamilyCardsSection(inner, payload.Contacts));
                    // Do not add a trailing "see next page" strip here: in QuestPDF this flow spans multiple
                    // pages and a short block at the end often orphans onto its own nearly blank page.
                    // The uploads section is a separate page with its own header and intro text.
                });

                page.Footer().Element(footer => BuildPdfFooter(footer, generatedOn));
            });

            // Page 2 — uploaded files with previews where possible
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.PageColor("#fafbfc");
                page.DefaultTextStyle(t => t.FontSize(11).FontColor(LabelMuted));

                page.Header()
                    .Element(h => BuildDocumentsPageHeader(h, applicationNumber, submittedOnUtc ?? generatedOn));

                page.Content().Element(c => BuildUploadedDocumentsPageContent(c, payload.Uploads, payload));

                page.Footer().Element(footer => BuildPdfFooter(footer, generatedOn));
            });
        }).GeneratePdf();

        var result = new ApplicantApplicationPdfResult(fileName, "application/pdf", pdf);
        return Task.FromResult(result);
    }

    private const string BrandPrimary = "#1e3a8a";
    private const string BrandSecondary = "#2563eb";
    private const string Slate900 = "#0f172a";
    private const string ValueDark = "#111827";
    private const string Slate700 = "#334155";
    private const string Slate600 = "#475569";
    private const string LabelMuted = "#6b7280";
    private const string Slate400 = "#94a3b8";
    private const string Slate300 = "#cbd5e1";
    private const string SectionBg = "#f8fafc";
    private const string BorderColor = "#e5e7eb";
    private const string Emerald500 = "#10b981";
    private const string Emerald600 = "#059669";
    private const string Amber500 = "#f59e0b";
    private const string Red500 = "#ef4444";
    private const string Green500 = "#16a34a";
    private const string CourseHighlightBg = "#eef2ff";
    private const string PaymentSuccessBg = "#ecfdf5";

    private static void BuildFormHeader(IContainer container, string? applicationNumber, DateTime submittedOnUtc)
    {
        container.PaddingBottom(10).Column(headerCol =>
        {
            headerCol.Spacing(6);

            headerCol.Item().Row(row =>
            {
                // Left: Logo
                row.ConstantItem(72).Height(72).Element(logo =>
                {
                    var logoPath = GetLogoPath();
                    if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    {
                        try
                        {
                            logo.Image(Image.FromFile(logoPath)).FitArea();
                        }
                        catch
                        {
                            logo.Background(BrandPrimary).AlignCenter().AlignMiddle()
                                .Text("DBC").Bold().FontSize(16).FontColor(Colors.White);
                        }
                    }
                    else
                    {
                        logo.Background(BrandPrimary).AlignCenter().AlignMiddle()
                            .Text("DBC").Bold().FontSize(16).FontColor(Colors.White);
                    }
                });

                // Center: College name, contact, form title
                row.RelativeItem().PaddingHorizontal(12).AlignCenter().Column(infoCol =>
                {
                    infoCol.Item().AlignCenter().Text("DON BOSCO COLLEGE, TURA")
                        .Bold().FontSize(20).FontColor(BrandPrimary);
                    infoCol.Item().PaddingTop(2).AlignCenter().Text("Tura, Meghalaya - 794002  |  Ph. 03651-222361, Mob. 9436308357")
                        .FontSize(9).FontColor(Slate600);
                    infoCol.Item().AlignCenter().Text("Email: principaldbct@gmail.com  |  www.donboscocollege.ac.in")
                        .FontSize(9).FontColor(Slate600);
                    infoCol.Item().PaddingTop(6).AlignCenter().Element(titleBar =>
                    {
                        titleBar.Background("#eff6ff").Border(1).BorderColor(BrandSecondary).PaddingHorizontal(16).PaddingVertical(8)
                            .AlignCenter()
                            .Text("Admission Application Form 2026–2027")
                            .SemiBold().FontSize(11.5f).FontColor(BrandPrimary);
                    });
                });

                // Right: Application ID, Date of submission
                row.ConstantItem(140).AlignRight().Column(rightCol =>
                {
                    rightCol.Spacing(4);
                    rightCol.Item().Column(c =>
                    {
                        c.Item().Text("Application ID").FontSize(8).FontColor(Slate600);
                        c.Item().Text(string.IsNullOrWhiteSpace(applicationNumber) ? "—" : applicationNumber)
                            .SemiBold().FontSize(10).FontColor(ValueDark);
                    });
                    rightCol.Item().Column(c =>
                    {
                        c.Item().Text("Date of submission").FontSize(8).FontColor(Slate600);
                        c.Item().Text(submittedOnUtc.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
                            .SemiBold().FontSize(10).FontColor(ValueDark);
                    });
                });
            });

            headerCol.Item().PaddingTop(8).BorderTop(1).BorderColor(BorderColor);
        });
    }

    private static string GetLogoPath()
    {
        // Primary: files copied next to the host (Api) — works in Development and published output.
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Logo.png"),
            Path.Combine(baseDir, "wwwroot", "branding", "college-logo.png"),
            Path.Combine(baseDir, "wwwroot", "vision-mission-1.png"),
            Path.Combine(baseDir, "vision-mission-1.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "branding", "college-logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "vision-mission-1.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "vision-mission-1.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "vision-mission-1.png"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return string.Empty;
    }

    private static void BuildPdfFooter(IContainer footer, DateTime generatedOnUtc)
    {
        footer.Height(44).PaddingTop(8).BorderTop(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Generated by BoscoConnect ERP")
                        .FontSize(8.5f).SemiBold().FontColor(Slate600);
                    left.Item().Text($"Generated {generatedOnUtc:dd MMM yyyy HH:mm} UTC")
                        .FontSize(7.5f).FontColor(Slate400);
                });
                row.AutoItem().Element(e => e.DefaultTextStyle(t => t.FontSize(9).FontColor(Slate600)).Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                }));
            });
            col.Item().PaddingTop(4).AlignCenter().Text("www.donboscocollege.ac.in")
                .FontSize(8).FontColor(Slate400);
        });
    }

    private static void BuildDocumentsPageHeader(IContainer container, string? applicationNumber, DateTime submittedOnUtc)
    {
        container.PaddingBottom(12).Column(headerCol =>
        {
            headerCol.Item().Row(row =>
            {
                row.ConstantItem(56).Height(56).Element(logo =>
                {
                    var logoPath = GetLogoPath();
                    if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    {
                        try
                        {
                            logo.Image(Image.FromFile(logoPath)).FitArea();
                        }
                        catch
                        {
                            logo.Background(BrandPrimary).AlignCenter().AlignMiddle()
                                .Text("DBC").Bold().FontSize(14).FontColor(Colors.White);
                        }
                    }
                    else
                    {
                        logo.Background(BrandPrimary).AlignCenter().AlignMiddle()
                            .Text("DBC").Bold().FontSize(14).FontColor(Colors.White);
                    }
                });

                row.RelativeItem().PaddingHorizontal(12).Column(infoCol =>
                {
                    infoCol.Item().Text("DON BOSCO COLLEGE, TURA")
                        .Bold().FontSize(14).FontColor(BrandPrimary);
                    infoCol.Item().PaddingTop(2).Text("Uploaded Documents — Admission Application 2026–2027")
                        .SemiBold().FontSize(11).FontColor(Slate700);
                });

                row.ConstantItem(120).AlignRight().Column(rightCol =>
                {
                    rightCol.Item().Text("Application ID").FontSize(7).FontColor(Slate600);
                    rightCol.Item().Text(string.IsNullOrWhiteSpace(applicationNumber) ? "—" : applicationNumber)
                        .SemiBold().FontSize(9).FontColor(ValueDark);
                    rightCol.Item().PaddingTop(4).Text("Submitted").FontSize(7).FontColor(Slate600);
                    rightCol.Item().Text(submittedOnUtc.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
                        .SemiBold().FontSize(9).FontColor(ValueDark);
                });
            });
            headerCol.Item().PaddingTop(8).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private static void BuildUploadedDocumentsPageContent(
        IContainer container,
        UploadSection uploads,
        ApplicantApplicationDraftDto payload)
    {
        var isCuetApplied = !string.IsNullOrWhiteSpace(payload.Academics.Cuet.Marks) ||
                            !string.IsNullOrWhiteSpace(payload.Academics.Cuet.RollNumber);

        var items = new List<(string Label, FileAttachmentDto? File, bool Required)>
        {
            ("STD X Marksheet", uploads.StdXMarksheet, true),
            ("STD XII Marksheet", uploads.StdXIIMarksheet, true),
            ("CUET Marksheet", uploads.CuetMarksheet, isCuetApplied),
            ("Disability Certificate", uploads.DifferentlyAbledProof, payload.PersonalInformation.IsDifferentlyAbled),
            ("Economically Weaker Section Proof", uploads.EconomicallyWeakerProof, payload.PersonalInformation.IsEconomicallyWeaker)
        };

        container.Column(column =>
        {
            column.Spacing(16);
            column.Item().Text(
                    "Official copies of certificates and proofs submitted with this application. PDF files show a file badge; images are previewed below.")
                .FontSize(9.5f).FontColor(Slate600).LineHeight(1.35f);

            foreach (var (label, file, required) in items)
            {
                if (!required && (file is null || string.IsNullOrWhiteSpace(file.FileName)))
                {
                    continue;
                }

                column.Item().Element(card => BuildDocumentPreviewCard(card, label, file, required));
            }
        });
    }

    private static void BuildDocumentPreviewCard(
        IContainer container,
        string label,
        FileAttachmentDto? attachment,
        bool isRequired)
    {
        container.Background(Colors.White)
            .Border(1).BorderColor(BorderColor)
            .Padding(12)
            .Column(cardCol =>
            {
                cardCol.Item().Row(row =>
                {
                    row.RelativeItem().Column(h =>
                    {
                        h.Item().Text(label).Bold().FontSize(12).FontColor(BrandPrimary);
                        h.Item().PaddingTop(2).Text(attachment?.FileName ?? "—").FontSize(9).FontColor(Slate600);
                    });
                    row.AutoItem().Element(badge =>
                    {
                        var ok = attachment is not null && !string.IsNullOrWhiteSpace(attachment.Data);
                        var badgeBg = ok ? Emerald500 : (isRequired ? Red500 : Slate400);
                        var txt = ok ? "Received" : (isRequired ? "Missing" : "N/A");
                        badge.Background(badgeBg).PaddingHorizontal(10).PaddingVertical(4)
                            .Text(txt).FontSize(8).FontColor(Colors.White).SemiBold();
                    });
                });

                cardCol.Item().PaddingTop(10).Element(preview =>
                {
                    if (attachment is null || string.IsNullOrWhiteSpace(attachment.Data))
                    {
                        preview.MinHeight(72).Background("#fff7ed").Border(1).BorderColor("#fed7aa").Padding(12)
                            .AlignCenter().AlignMiddle()
                            .Text(isRequired ? "Document not uploaded" : "Not applicable")
                            .FontSize(10).SemiBold().FontColor(Slate600);
                        return;
                    }

                    var isPdf = attachment.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
                        || attachment.FileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;

                    if (isPdf)
                    {
                        preview.MinHeight(100).Background(SectionBg).Border(2).BorderColor(BrandPrimary).Padding(16)
                            .Column(pdfCol =>
                            {
                                pdfCol.Item().AlignCenter().Text("PDF").Bold().FontSize(14).FontColor(BrandPrimary);
                                pdfCol.Item().PaddingTop(6).AlignCenter().Text("Preview not embedded — file is attached in the system.")
                                    .FontSize(9).FontColor(Slate600).AlignCenter();
                                pdfCol.Item().PaddingTop(4).AlignCenter().Text(attachment.FileName)
                                    .FontSize(8).FontColor(Slate400);
                            });
                        return;
                    }

                    try
                    {
                        var bytes = Convert.FromBase64String(attachment.Data);
                        preview.MaxHeight(320).Border(2).BorderColor(BrandPrimary).Padding(6)
                            .Element(img =>
                            {
                                img.Image(Image.FromBinaryData(bytes)).FitArea();
                            });
                    }
                    catch
                    {
                        preview.MinHeight(72).Background("#fef2f2").Padding(8).AlignCenter()
                            .Text("Preview unavailable")
                            .FontSize(9).FontColor(Slate600);
                    }
                });
            });
    }

    private static string GetPhotoPath(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return string.Empty;
        }

        // PhotoUrl is relative path like "/uploads/applicants/{accountId}.jpg"
        // Convert to absolute path
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photoUrl.TrimStart('/')), // From Api project
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", photoUrl.TrimStart('/')), // From bin folder
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "wwwroot", photoUrl.TrimStart('/')), // Two levels up
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return string.Empty;
    }

    private static void BuildSectionCardWithPhoto(
        IContainer container,
        string title,
        ApplicantApplicationDraftDto payload,
        string? photoUrl,
        (string Label, string Value)[] rows)
    {
        container.Background(Colors.White)
            .Border(1).BorderColor(BorderColor)
            .Column(column =>
            {
                column.Item().Background("#f1f5f9").BorderBottom(1).BorderColor(BorderColor)
                    .PaddingVertical(12).PaddingHorizontal(14)
                    .Text(title).Bold().FontSize(13).FontColor(BrandPrimary);
                column.Item().Background(SectionBg).PaddingVertical(16).PaddingHorizontal(18).Column(body =>
                {
                    body.Spacing(12);
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().Column(fieldsCol =>
                        {
                            fieldsCol.Spacing(10);
                            foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Label)))
                            {
                                fieldsCol.Item().Row(fieldRow =>
                                {
                                    fieldRow.ConstantItem(200).Text(label)
                                        .FontSize(11).FontColor(LabelMuted);
                                    fieldRow.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                                        .FontSize(11).SemiBold().FontColor(ValueDark);
                                });
                            }
                        });
                        row.ConstantItem(100).Height(118).AlignRight().Element(photoOuter =>
                        {
                            photoOuter.Border(2).BorderColor(BrandSecondary).Background(Colors.White).Padding(4)
                                .Element(photoInner =>
                                {
                                    var photoPath = GetPhotoPath(photoUrl);
                                    if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                                    {
                                        try
                                        {
                                            photoInner.Image(Image.FromFile(photoPath)).FitArea();
                                        }
                                        catch
                                        {
                                            photoInner.Background("#f1f5f9").AlignCenter().AlignMiddle()
                                                .Text("Photo").FontSize(8).FontColor(Slate400);
                                        }
                                    }
                                    else
                                    {
                                        photoInner.Background("#f1f5f9").AlignCenter().AlignMiddle()
                                            .Text("No photo").FontSize(8).FontColor(Slate400);
                                    }
                                });
                        });
                    });
                });
            });
    }

    private static void BuildSectionCard(
        IContainer container,
        string title,
        (string Label, string Value)[] rows)
    {
        container.Background(Colors.White)
            .Border(1).BorderColor(BorderColor)
            .Column(column =>
            {
                column.Item().Background("#f1f5f9").BorderBottom(1).BorderColor(BorderColor)
                    .PaddingVertical(12).PaddingHorizontal(14)
                    .Text(title).Bold().FontSize(13).FontColor(BrandPrimary);
                column.Item().Background(SectionBg).PaddingVertical(16).PaddingHorizontal(18).Column(body =>
                {
                    body.Spacing(10);
                    foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Label)))
                    {
                        body.Item().Row(row =>
                        {
                            row.ConstantItem(200).Text(label).FontSize(11).FontColor(LabelMuted);
                            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                                .FontSize(11).SemiBold().FontColor(ValueDark);
                        });
                    }
                });
            });
    }

    private static void BuildAcademicSection(IContainer container, AcademicSection academics)
    {
        var subjects = (academics.ClassXII ?? new List<SubjectMarkDto>())
            .Where(s => !string.IsNullOrWhiteSpace(s.Subject) || !string.IsNullOrWhiteSpace(s.Marks))
            .ToList();
        var totalMarks = 0;
        var hasNumericMarks = false;
        foreach (var s in subjects)
        {
            if (int.TryParse(s.Marks?.Trim(), out var m))
            {
                totalMarks += m;
                hasNumericMarks = true;
            }
        }
        var percentage = academics.BoardExamination?.Percentage;
        if (string.IsNullOrWhiteSpace(percentage) && hasNumericMarks && subjects.Count > 0)
        {
            var maxPerSubject = 100;
            var totalMax = subjects.Count * maxPerSubject;
            percentage = totalMax > 0 ? $"{(totalMarks * 100.0 / totalMax):F1}" : "—";
        }

        container.Background(SectionBg)
            .Border(1).BorderColor(BorderColor)
            .Padding(20)
            .Column(column =>
            {
                column.Item().Text("Academic Records").Bold().FontSize(15).FontColor(ValueDark);
                column.Spacing(12);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Background(BorderColor).Padding(8).Text("Subject")
                            .SemiBold().FontSize(11).FontColor(ValueDark);
                        header.Cell().Background(BorderColor).Padding(8).AlignCenter().Text("Marks")
                            .SemiBold().FontSize(11).FontColor(ValueDark);
                    });
                    for (var i = 0; i < subjects.Count; i++)
                    {
                        var s = subjects[i];
                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                        table.Cell().Background(bg).Padding(8).Text(s.Subject ?? "—").FontSize(11).FontColor(ValueDark);
                        table.Cell().Background(bg).Padding(8).AlignCenter().Text(s.Marks ?? "—").FontSize(11).FontColor(ValueDark);
                    }
                    if (hasNumericMarks || !string.IsNullOrWhiteSpace(percentage))
                    {
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(8).Text("Total")
                            .SemiBold().FontSize(11).FontColor(ValueDark);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter()
                            .Text(hasNumericMarks ? totalMarks.ToString() : "—").SemiBold().FontSize(11).FontColor(ValueDark);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(8).Text("% / Percentage")
                            .SemiBold().FontSize(11).FontColor(ValueDark);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter()
                            .Text(percentage ?? "—").SemiBold().FontSize(11).FontColor(ValueDark);
                    }
                });
            });
    }

    private static void BuildCourseHighlightCard(
        IContainer container,
        CoursePreferencesSection courses,
        string? legacyShift)
    {
        container.Background(CourseHighlightBg)
            .Border(1).BorderColor(BrandSecondary)
            .Padding(20)
            .Column(column =>
            {
                column.Item().Text("Course Preferences").Bold().FontSize(15).FontColor(ValueDark);
                column.Spacing(10);
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("Preferred Shift:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(FormatShift(courses.Shift, legacyShift)).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("Major:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(string.IsNullOrWhiteSpace(courses.MajorSubject) ? "—" : courses.MajorSubject).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("Minor:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(string.IsNullOrWhiteSpace(courses.MinorSubject) ? "—" : courses.MinorSubject).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("MDC:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(FormatCourseOption(courses.MultidisciplinaryChoice)).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("AEC:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(FormatCourseOption(courses.AbilityEnhancementChoice)).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("SEC:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(FormatCourseOption(courses.SkillEnhancementChoice)).FontSize(11).SemiBold().FontColor(ValueDark);
                });
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("VAC:").FontSize(11).FontColor(LabelMuted);
                    r.RelativeItem().Text(FormatCourseOption(courses.ValueAddedChoice)).FontSize(11).SemiBold().FontColor(ValueDark);
                });
            });
    }

    private static void BuildPaymentOfficialSection(
        IContainer container,
        decimal applicationFee,
        bool isPaymentCompleted,
        decimal? paymentAmount,
        string? transactionId)
    {
        container.Background(isPaymentCompleted ? PaymentSuccessBg : SectionBg)
            .Border(1).BorderColor(isPaymentCompleted ? Emerald500 : BorderColor)
            .Padding(20)
            .Column(column =>
            {
                column.Item().Text("Payment Details").Bold().FontSize(15).FontColor(ValueDark);
                if (isPaymentCompleted)
                {
                    column.Item().PaddingVertical(6).Background(Emerald500)
                        .PaddingHorizontal(12).PaddingVertical(4)
                        .Text("Payment Successful")
                        .SemiBold().FontSize(11).FontColor(Colors.White);
                }
                column.Spacing(10);
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("Application Fee:").FontSize(11).FontColor(LabelMuted);
                    r.AutoItem().Text($"₹{applicationFee:0}").FontSize(11).SemiBold().FontColor(ValueDark);
                });
                if (isPaymentCompleted && !string.IsNullOrWhiteSpace(transactionId))
                {
                    column.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Transaction ID:").FontSize(11).FontColor(LabelMuted);
                        r.RelativeItem().Text(transactionId).FontSize(10).SemiBold().FontColor(ValueDark).FontFamily("Courier");
                    });
                }
            });
    }

    private static void BuildFamilyCardsSection(IContainer container, ContactSection contacts)
    {
        container.Background(SectionBg)
            .Border(1).BorderColor(BorderColor)
            .Padding(20)
            .Column(column =>
            {
                column.Item().Text("Family & Guardian").Bold().FontSize(15).FontColor(ValueDark);
                column.Spacing(12);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => RenderGuardianMiniCard(c, "Father", contacts.Father));
                    row.RelativeItem().PaddingLeft(12).Element(c => RenderGuardianMiniCard(c, "Mother", contacts.Mother));
                });
                column.Item().Element(c => RenderGuardianMiniCard(c, "Local Guardian", contacts.LocalGuardian));
                if (!string.IsNullOrWhiteSpace(contacts.HouseholdAreaType))
                {
                    column.Item().PaddingTop(8).Row(r =>
                    {
                        r.ConstantItem(140).Text("Household Area:").FontSize(11).FontColor(LabelMuted);
                        r.RelativeItem().Text(contacts.HouseholdAreaType).FontSize(11).SemiBold().FontColor(ValueDark);
                    });
                }
            });
    }

    private static void RenderGuardianMiniCard(IContainer container, string title, ParentOrGuardian guardian)
    {
        container.Background(Colors.White)
            .Border(1).BorderColor(BorderColor)
            .Padding(12)
            .Column(col =>
            {
                col.Item().Text(title).Bold().FontSize(12).FontColor(BrandPrimary);
                col.Spacing(6);
                col.Item().Row(r => { r.ConstantItem(80).Text("Name").FontSize(10).FontColor(LabelMuted); r.RelativeItem().Text(guardian?.Name ?? "—").FontSize(10).SemiBold().FontColor(ValueDark); });
                col.Item().Row(r => { r.ConstantItem(80).Text("Age").FontSize(10).FontColor(LabelMuted); r.RelativeItem().Text(guardian?.Age ?? "—").FontSize(10).SemiBold().FontColor(ValueDark); });
                col.Item().Row(r => { r.ConstantItem(80).Text("Occupation").FontSize(10).FontColor(LabelMuted); r.RelativeItem().Text(guardian?.Occupation ?? "—").FontSize(10).SemiBold().FontColor(ValueDark); });
                col.Item().Row(r => { r.ConstantItem(80).Text("Contact").FontSize(10).FontColor(LabelMuted); r.RelativeItem().Text(guardian?.ContactNumber1 ?? "—").FontSize(10).SemiBold().FontColor(ValueDark); });
            });
    }

    private static void BuildApplicantSummaryCard(
        IContainer container,
        ApplicantApplicationDraftDto payload,
        DateTime generatedOn,
        int completedSteps,
        int totalSteps,
        string selectionStatus,
        decimal applicationFee,
        bool isPaymentCompleted,
        decimal? paymentAmount,
        string? photoUrl = null)
    {
        var name = string.IsNullOrWhiteSpace(payload.PersonalInformation.NameAsPerAdmitCard)
            ? "Applicant"
            : payload.PersonalInformation.NameAsPerAdmitCard;
        var initials = GetInitials(name);
        var dob = FormatDate(payload.PersonalInformation.DateOfBirth);
        var shift = FormatShift(payload.Courses.Shift, payload.PersonalInformation.Shift);
        var profileSummary = $"Profile {completedSteps} of {totalSteps} steps complete";
        string paymentStatus;
        if (isPaymentCompleted)
        {
            paymentStatus = "Completed";
        }
        else if (payload.CoursesLocked)
        {
            paymentStatus = "Pending";
        }
        else
        {
            paymentStatus = "Not Submitted";
        }

        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(24)
            .Column(column =>
            {
                column.Spacing(16);

                // Header section with photo and name
                column.Item().Row(row =>
                {
                    row.ConstantItem(75).Height(75).Element(avatar =>
                    {
                        var photoPath = GetPhotoPath(photoUrl);
                        if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                        {
                            try
                            {
                                // Display candidate photo - rounded rectangle with border
                                avatar
                                    .Border(3)
                                    .BorderColor(BrandPrimary)
                                    .Background(Colors.White)
                                    .Image(Image.FromFile(photoPath))
                                    .FitArea();
                            }
                            catch
                            {
                                // Fallback to initials if image fails to load
                                avatar
                                    .Background(BrandPrimary)
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .Text(initials)
                                    .Bold()
                                    .FontSize(24)
                                    .FontColor(Colors.White);
                            }
                        }
                        else
                        {
                            // Fallback to initials if no photo
                            avatar
                                .Background(BrandPrimary)
                                .AlignCenter()
                                .AlignMiddle()
                                .Text(initials)
                                .Bold()
                                .FontSize(24)
                                .FontColor(Colors.White);
                        }
                    });

                    row.RelativeItem().PaddingLeft(16).Column(info =>
                    {
                        info.Item().Text(name)
                            .Bold()
                            .FontSize(18)
                            .FontColor(Slate900)
                            .LetterSpacing(0.3f);
                        info.Item().PaddingTop(4).Text($"Date of Birth: {dob}")
                            .FontSize(11)
                            .FontColor(Slate700);
                        info.Item().Text($"Preferred Shift: {shift}")
                            .FontSize(11)
                            .FontColor(Slate700);
                    });

                    row.ConstantItem(130).AlignRight().Column(summary =>
                    {
                        summary.Item().Background(BrandPrimary)
                            .Padding(10)
                            .Border(1)
                            .BorderColor(BrandPrimary)
                            .Column(paymentCol =>
                            {
                                paymentCol.Item().AlignCenter().Text("Application Fee")
                                    .SemiBold()
                                    .FontSize(8.5f)
                                    .FontColor(Colors.White)
                                    .LetterSpacing(0.5f);
                                paymentCol.Item().PaddingTop(3).AlignCenter().Text($"₹{applicationFee:0}")
                                    .Bold()
                                    .FontSize(18)
                                    .FontColor(Colors.White);
                                paymentCol.Item().PaddingTop(5).AlignCenter().Element(badge =>
                                {
                                    var badgeColor = isPaymentCompleted ? Green500 : (payload.CoursesLocked ? Amber500 : Slate400);
                                    var badgeTextColor = Colors.White;
                                    RenderBadge(
                                        badge,
                                        paymentStatus,
                                        badgeColor,
                                        badgeTextColor);
                                });
                            });
                    });
                });

                // Status badges row
                column.Item().PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten3).PaddingTop(12).PaddingBottom(4).Row(row =>
                {
                    row.Spacing(10);
                    row.AutoItem().Element(b =>
                        RenderBadge(
                            b,
                            selectionStatus,
                            BrandPrimary,
                            Colors.White));
                    row.AutoItem().Element(b =>
                        RenderBadge(
                            b,
                            profileSummary,
                            Slate300,
                            Slate700));
                    row.RelativeItem().AlignRight().Text($"Last Updated: {generatedOn:dd MMM yyyy}")
                        .FontSize(9.5f)
                        .FontColor(Slate600)
                        .SemiBold();
                });
            });
    }

    private static void BuildStatusCard(IContainer container, string status)
    {
        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(12);
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .Text("Selection Status")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                column.Item().PaddingTop(4).Element(badge =>
                    RenderBadge(
                        badge,
                        status,
                        BrandSecondary,
                        Colors.White));

                column.Item().PaddingTop(8).Background(Colors.Grey.Lighten4)
                    .Padding(12)
                    .BorderLeft(3)
                    .BorderColor(BrandSecondary)
                    .Text(GetSelectionNote(status))
                    .FontSize(10.5f)
                    .FontColor(Slate700)
                    .LineHeight(1.4f);
            });
    }

    private static void BuildPaymentCard(
        IContainer container,
        decimal applicationFee,
        bool coursesLocked,
        bool isPaymentCompleted,
        decimal? paymentAmount,
        string? transactionId = null)
    {
        var amountPaid = paymentAmount ?? 0m;
        var balance = applicationFee - amountPaid;
        string status;
        if (isPaymentCompleted)
        {
            status = "Completed";
        }
        else if (coursesLocked)
        {
            status = "Pending";
        }
        else
        {
            status = "Not Available";
        }

        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(14);
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .Text("Payment Details")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                column.Item().PaddingTop(4).Element(badge =>
                    RenderBadge(
                        badge,
                        status,
                        isPaymentCompleted ? Green500 : (coursesLocked ? Amber500 : Slate400),
                        Colors.White));

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Background(Colors.Grey.Lighten5)
                        .Padding(12)
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten3)
                        .Column(col =>
                        {
                            col.Item().Text("Total Due")
                                .SemiBold()
                                .FontSize(9.5f)
                                .FontColor(Slate600)
                                .LetterSpacing(0.2f);
                            col.Item().PaddingTop(2).Text($"₹{applicationFee:0}")
                                .Bold()
                                .FontSize(14)
                                .FontColor(Slate900);
                        });
                    row.RelativeItem().PaddingLeft(8).Background(Colors.Grey.Lighten5)
                        .Padding(12)
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten3)
                        .Column(col =>
                        {
                            col.Item().Text("Amount Paid")
                                .SemiBold()
                                .FontSize(9.5f)
                                .FontColor(Slate600)
                                .LetterSpacing(0.2f);
                            col.Item().PaddingTop(2).Text($"₹{amountPaid:0}")
                                .Bold()
                                .FontSize(14)
                                .FontColor(isPaymentCompleted ? Green500 : Slate900);
                        });
                    row.RelativeItem().PaddingLeft(8).Background(Colors.Grey.Lighten5)
                        .Padding(12)
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten3)
                        .Column(col =>
                        {
                            col.Item().Text("Balance")
                                .SemiBold()
                                .FontSize(9.5f)
                                .FontColor(Slate600)
                                .LetterSpacing(0.2f);
                            col.Item().PaddingTop(2).Text($"₹{Math.Max(balance, 0):0}")
                                .Bold()
                                .FontSize(14)
                                .FontColor(BrandPrimary);
                        });
                });

                column.Item().PaddingTop(8).Background(isPaymentCompleted ? Colors.Green.Lighten5 : Colors.Grey.Lighten4)
                    .Padding(12)
                    .BorderLeft(3)
                    .BorderColor(isPaymentCompleted ? Green500 : Amber500)
                    .Text(isPaymentCompleted
                        ? "✓ Payment completed successfully. Your application is now complete."
                        : coursesLocked
                            ? "Note: Application will be considered complete only after payment confirmation."
                            : "Submit your application to unlock the payment option.")
                    .FontSize(10.5f)
                    .FontColor(isPaymentCompleted ? Emerald600 : Slate700)
                    .SemiBold()
                    .LineHeight(1.4f);

                if (isPaymentCompleted && !string.IsNullOrWhiteSpace(transactionId))
                {
                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.AutoItem().Text("Transaction ID: ")
                            .FontSize(10)
                            .FontColor(Slate600)
                            .SemiBold();
                        row.AutoItem().Text(transactionId)
                            .FontSize(10)
                            .FontColor(Slate900)
                            .Bold()
                            .FontFamily("Courier");
                    });
                }
            });
    }

    private static void BuildFamilyCard(IContainer container, ContactSection contacts)
    {
        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(14);
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .Text("Family & Guardian")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                column.Item().Element(inner => RenderGuardian(inner, "Father", contacts.Father));
                column.Item().Element(inner => RenderGuardian(inner, "Mother", contacts.Mother));
                column.Item().Element(inner => RenderGuardian(inner, "Local Guardian", contacts.LocalGuardian));
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Household Area")
                        .SemiBold()
                        .FontSize(10)
                        .FontColor(Slate600);
                    row.RelativeItem().AlignRight().Text(
                        string.IsNullOrWhiteSpace(contacts.HouseholdAreaType) ? "-" : contacts.HouseholdAreaType)
                        .FontSize(10.5f)
                        .FontColor(Slate900);
                });
            });
    }

    private static void RenderGuardian(IContainer container, string title, ParentOrGuardian guardian)
    {
        container.Background(Colors.Grey.Lighten5)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(12)
            .PaddingBottom(8)
            .Column(guardianCol =>
            {
                guardianCol.Item().Text(title)
                    .Bold()
                    .FontSize(11)
                    .FontColor(BrandPrimary)
                    .LetterSpacing(0.3f);
                
                guardianCol.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Name")
                            .SemiBold()
                            .FontSize(9.5f)
                            .FontColor(Slate600);
                        col.Item().Text(
                                string.IsNullOrWhiteSpace(guardian.Name)
                                    ? "-"
                                    : guardian.Name)
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Age")
                            .SemiBold()
                            .FontSize(9.5f)
                            .FontColor(Slate600);
                        col.Item().Text(string.IsNullOrWhiteSpace(guardian.Age) ? "-" : guardian.Age)
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Occupation")
                            .SemiBold()
                            .FontSize(9.5f)
                            .FontColor(Slate600);
                        col.Item().Text(string.IsNullOrWhiteSpace(guardian.Occupation) ? "-" : guardian.Occupation)
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Contact")
                            .SemiBold()
                            .FontSize(9.5f)
                            .FontColor(Slate600);
                        col.Item().Text(string.IsNullOrWhiteSpace(guardian.ContactNumber1) ? "-" : guardian.ContactNumber1)
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                    });
                });
            });
    }

    private static IEnumerable<StepProgress> BuildCompletionSteps(ApplicantApplicationDraftDto draft)
    {
        yield return new StepProgress("registration", "Registration Created", true);
        yield return new StepProgress("personal", "Personal Information", IsPersonalInformationComplete(draft.PersonalInformation));
        yield return new StepProgress("address", "Addresses & Identity", IsAddressInformationComplete(draft.Address));
        yield return new StepProgress("family", "Family & Guardian", AreGuardianDetailsComplete(draft.Contacts));
        yield return new StepProgress("academics", "Academic Records", AreAcademicDetailsComplete(draft.Academics));
        yield return new StepProgress("courses", "Course Preferences", AreCoursePreferencesComplete(draft.Courses));
        yield return new StepProgress("uploads", "Uploads & Declaration", AreUploadsComplete(draft));
    }

    private static void BuildSection(
        IContainer container,
        string title,
        IEnumerable<(string Label, string Value)> rows,
        string? note = null)
    {
        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(14);
                // Section header with background
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .PaddingBottom(14)
                    .Text(title)
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                foreach (var group in rows.Where(r => !string.IsNullOrWhiteSpace(r.Label)).Chunk(2))
                {
                    column.Item().PaddingBottom(10).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten4).Row(row =>
                    {
                        row.RelativeItem().Element(inner =>
                        {
                            var (label, value) = group[0];
                            inner.Column(col =>
                            {
                                col.Spacing(3);
                                col.Item().Text(label)
                                    .SemiBold()
                                    .FontSize(9.5f)
                                    .FontColor(Slate600)
                                    .LetterSpacing(0.2f);
                                col.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                                    .FontSize(11)
                                    .FontColor(Slate900)
                                    .SemiBold();
                            });
                        });

                        if (group.Length > 1)
                        {
                            row.RelativeItem().PaddingLeft(16).Element(inner =>
                            {
                                var (label, value) = group[1];
                                inner.Column(col =>
                                {
                                    col.Spacing(3);
                                    col.Item().Text(label)
                                        .SemiBold()
                                        .FontSize(9.5f)
                                        .FontColor(Slate600)
                                        .LetterSpacing(0.2f);
                                    col.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                                        .FontSize(11)
                                        .FontColor(Slate900)
                                        .SemiBold();
                                });
                            });
                        }
                        else
                        {
                            row.RelativeItem();
                        }
                    });
                }

                if (!string.IsNullOrWhiteSpace(note))
                {
                    column.Item().PaddingTop(8).Background(Colors.Grey.Lighten4)
                        .Padding(10)
                        .BorderLeft(3)
                        .BorderColor(Amber500)
                        .Text(note)
                        .FontSize(9.5f)
                        .FontColor(Slate700)
                        .Italic();
                }
            });
    }

    private static void BuildSubjectsSection(
        IContainer container,
        string title,
        IReadOnlyCollection<SubjectMarkDto> subjects)
    {
        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(14);
                // Section header with background
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .PaddingBottom(14)
                    .Text(title)
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(1);
                        });

                    table.Header(header =>
                    {
                        header.Cell()
                            .Background(BrandPrimary)
                            .Padding(10)
                            .Text("Subject")
                            .Bold()
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .LetterSpacing(0.3f);
                        header.Cell()
                            .Background(BrandPrimary)
                            .Padding(10)
                            .AlignCenter()
                            .Text("Marks")
                            .Bold()
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .LetterSpacing(0.3f);
                    });

                    var subjectList = subjects.Where(s =>
                        !string.IsNullOrWhiteSpace(s.Subject) ||
                        !string.IsNullOrWhiteSpace(s.Marks)).ToList();
                    
                    for (int i = 0; i < subjectList.Count; i++)
                    {
                        var subject = subjectList[i];
                        var isEven = i % 2 == 0;
                        var bgColor = isEven ? Colors.White : Colors.Grey.Lighten5;
                        
                        table.Cell()
                            .Background(bgColor)
                            .Padding(10)
                            .Text(subject.Subject ?? "-")
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                        table.Cell()
                            .Background(bgColor)
                            .Padding(10)
                            .AlignCenter()
                            .Text(subject.Marks ?? "-")
                            .FontSize(10.5f)
                            .FontColor(Slate900)
                            .SemiBold();
                    }
                });
            });
    }

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (DateTime.TryParse(value, out var date))
        {
            return date.ToString("yyyy-MM-dd");
        }

        return value;
    }

    private static string FormatBoolean(bool value) => value ? "Yes" : "No";

    private static string FormatDenominationForPdf(string? religion, string? denomination)
    {
        if (string.IsNullOrWhiteSpace(religion)
            || !religion.Trim().Equals("Christian", StringComparison.OrdinalIgnoreCase))
        {
            return "—";
        }

        return string.IsNullOrWhiteSpace(denomination) ? "—" : denomination.Trim();
    }

    private static string FormatShift(string? shift, string? legacy)
    {
        var value = string.IsNullOrWhiteSpace(shift) ? legacy : shift;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Pending selection";
        }

        return ShiftDisplayNames.TryGetValue(value, out var label) ? label : value;
    }

    private static string FormatCourseOption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return CourseOptionDisplayNames.TryGetValue(value.Trim(), out var label)
            ? label
            : value.Trim();
    }

    private static void BuildUploadsSection(
        IContainer container,
        UploadSection uploads,
        ApplicantApplicationDraftDto payload)
    {
        container.Background(Colors.White)
            .Border(1.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(14);
                column.Item().Background(BrandPrimary)
                    .Padding(10)
                    .PaddingBottom(14)
                    .Text("Uploads Summary")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Colors.White)
                    .LetterSpacing(0.5f);

                // STD X and XII are always required
                AddUploadRow(column, "STD X Marksheet", uploads.StdXMarksheet, isRequired: true);
                AddUploadRow(column, "STD XII Marksheet", uploads.StdXIIMarksheet, isRequired: true);
                
                // CUET Marksheet - only required if CUET was applied (has marks or roll number)
                var isCuetApplied = !string.IsNullOrWhiteSpace(payload.Academics.Cuet.Marks) ||
                                   !string.IsNullOrWhiteSpace(payload.Academics.Cuet.RollNumber);
                AddUploadRow(column, "CUET Marksheet", uploads.CuetMarksheet, isRequired: isCuetApplied);
                
                // Disability Certificate - only required if differently abled
                AddUploadRow(column, "Disability Certificate", uploads.DifferentlyAbledProof, 
                    isRequired: payload.PersonalInformation.IsDifferentlyAbled);
                
                // EWS Proof - only required if economically weaker
                AddUploadRow(column, "EWS Proof", uploads.EconomicallyWeakerProof, 
                    isRequired: payload.PersonalInformation.IsEconomicallyWeaker);
            });
    }

    private static void AddUploadRow(ColumnDescriptor column, string label, FileAttachmentDto? attachment, bool isRequired = true)
    {
        var isUploaded = attachment is not null && !string.IsNullOrWhiteSpace(attachment.FileName);
        
        string statusText;
        string badgeColor;
        string fileStatusText;
        
        if (isUploaded)
        {
            statusText = "Uploaded";
            badgeColor = Emerald500;
            fileStatusText = attachment!.FileName;
        }
        else if (!isRequired)
        {
            statusText = "Nil";
            badgeColor = Slate400;
            fileStatusText = "Not applicable";
        }
        else
        {
            statusText = "Missing";
            badgeColor = Red500;
            fileStatusText = "Not uploaded";
        }

        column.Item().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(label).SemiBold().FontSize(10.5f).FontColor(Slate600);
                col.Item().Text(fileStatusText)
                    .FontSize(10)
                    .FontColor(Slate900);
            });
            row.ConstantItem(80).AlignRight().Element(badge =>
            {
                RenderBadge(
                    badge,
                    statusText,
                    badgeColor,
                    Colors.White);
            });
        });

        // Skip image rendering in PDF to avoid layout constraints
        // Images are already shown in the web interface
        // Uncomment below if you want to show images in PDF (may cause layout issues)
        /*
        if (attachment is null || string.IsNullOrWhiteSpace(attachment.Data))
        {
            return;
        }

        if (!IsImage(attachment.ContentType))
        {
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(attachment.Data);
            column.Item().PaddingTop(4).PaddingLeft(12).Width(200).Image(bytes).FitArea();
        }
        catch
        {
            // ignore invalid base64; keep textual status
        }
        */
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) && contentType.Trim().StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static void RenderBadge(
        IContainer container,
        string text,
        string background,
        string foreground)
    {
        container
            .Background(background)
            .PaddingHorizontal(12)
            .PaddingVertical(6)
            .Text(text)
            .FontSize(10)
            .Bold()
            .FontColor(foreground)
            .LetterSpacing(0.3f);
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "A";
        }

        if (parts.Length == 1)
        {
            return parts[0].Substring(0, Math.Min(1, parts[0].Length)).ToUpperInvariant();
        }

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static string GetSelectionNote(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "accepted" => "Congratulations! Your application has been accepted.",
            "rejected" => "We’re sorry to inform you that your application was not successful.",
            "waitlisted" => "Your application is on the waiting list. We will notify you if a seat becomes available.",
            "entrance exam scheduled" => "An entrance exam has been scheduled. Please check your email for details.",
            _ => "Your application is under review by the admissions committee. You will be notified when a decision is made."
        };
    }

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

        var hasSubjects = academics.ClassXII is not null
                          && academics.ClassXII.Any(s =>
                              !string.IsNullOrWhiteSpace(s.Subject) &&
                              !string.IsNullOrWhiteSpace(s.Marks));

        var hasLastInstitution = !string.IsNullOrWhiteSpace(academics.LastInstitutionAttended);

        return hasBoardDetails && hasSubjects && hasLastInstitution;
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

    // Form-style section builders
    private static void BuildFormSectionWithPhoto(
        IContainer container,
        string title,
        ApplicantApplicationDraftDto payload,
        string? photoUrl,
        IEnumerable<(string Label, string Value)> rows)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(12);
                
                // Section title
                column.Item().Text(title)
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);
                
                column.Item().Row(row =>
                {
                    // Form fields on left
                    row.RelativeItem().Column(fieldsCol =>
                    {
                        fieldsCol.Spacing(8);
                        foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Label)))
                        {
                            fieldsCol.Item().Row(fieldRow =>
                            {
                                fieldRow.ConstantItem(180).Text(label + ":")
                                    .SemiBold()
                                    .FontSize(10)
                                    .FontColor(Slate700);
                                fieldRow.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                                    .FontSize(10)
                                    .FontColor(Slate900);
                            });
                        }
                    });
                    
                    // Photo on right - use ConstantItem with explicit size
                    row.ConstantItem(90).Height(90).AlignRight().Element(photoContainer =>
                    {
                        var photoPath = GetPhotoPath(photoUrl);
                        if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                        {
                            try
                            {
                                photoContainer
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Image(Image.FromFile(photoPath))
                                    .FitArea();
                            }
                            catch
                            {
                                // Empty container if image fails
                            }
                        }
                    });
                });
            });
    }

    private static void BuildFormSection(
        IContainer container,
        string title,
        IEnumerable<(string Label, string Value)> rows,
        string? note = null)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(title)
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);

                foreach (var (label, value) in rows.Where(r => !string.IsNullOrWhiteSpace(r.Label)))
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(180).Text(label + ":")
                            .SemiBold()
                            .FontSize(10)
                            .FontColor(Slate700);
                        row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                            .FontSize(10)
                            .FontColor(Slate900);
                    });
                }

                if (!string.IsNullOrWhiteSpace(note))
                {
                    column.Item().PaddingTop(4).Text(note)
                        .FontSize(9)
                        .FontColor(Slate600)
                        .Italic();
                }
            });
    }

    private static void BuildFormSubjectsSection(
        IContainer container,
        string title,
        IReadOnlyCollection<SubjectMarkDto> subjects)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(title)
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten4).Padding(6).Text("Subject")
                            .SemiBold().FontSize(10).FontColor(Slate900);
                        header.Cell().Background(Colors.Grey.Lighten4).Padding(6).AlignCenter().Text("Marks")
                            .SemiBold().FontSize(10).FontColor(Slate900);
                    });

                    foreach (var subject in subjects.Where(s =>
                        !string.IsNullOrWhiteSpace(s.Subject) ||
                        !string.IsNullOrWhiteSpace(s.Marks)))
                    {
                        table.Cell().Padding(6).Text(subject.Subject ?? "-").FontSize(10).FontColor(Slate900);
                        table.Cell().Padding(6).AlignCenter().Text(subject.Marks ?? "-").FontSize(10).FontColor(Slate900);
                    }
                });
            });
    }

    private static void BuildFormUploadsSection(
        IContainer container,
        UploadSection uploads,
        ApplicantApplicationDraftDto payload)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Text("Document Uploads")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);

                AddUploadRow(column, "STD X Marksheet", uploads.StdXMarksheet, isRequired: true);
                AddUploadRow(column, "STD XII Marksheet", uploads.StdXIIMarksheet, isRequired: true);
                
                var isCuetApplied = !string.IsNullOrWhiteSpace(payload.Academics.Cuet.Marks) ||
                                   !string.IsNullOrWhiteSpace(payload.Academics.Cuet.RollNumber);
                AddUploadRow(column, "CUET Marksheet", uploads.CuetMarksheet, isRequired: isCuetApplied);
                AddUploadRow(column, "Disability Certificate", uploads.DifferentlyAbledProof, 
                    isRequired: payload.PersonalInformation.IsDifferentlyAbled);
                AddUploadRow(column, "EWS Proof", uploads.EconomicallyWeakerProof, 
                    isRequired: payload.PersonalInformation.IsEconomicallyWeaker);
            });
    }

    private static void BuildFormPaymentSection(
        IContainer container,
        decimal applicationFee,
        bool coursesLocked,
        bool isPaymentCompleted,
        decimal? paymentAmount,
        string? transactionId = null)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Text("Payment")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);

                column.Item().Background(Colors.Grey.Lighten4)
                    .Padding(12)
                    .Column(paymentCol =>
                    {
                        paymentCol.Item().Text("Payment Summary")
                            .SemiBold()
                            .FontSize(11)
                            .FontColor(Slate900);
                        paymentCol.Item().PaddingTop(4).Row(row =>
                        {
                            row.AutoItem().Text("Application Fee:")
                                .FontSize(10)
                                .FontColor(Slate700);
                            row.AutoItem().Text($" ₹ {applicationFee:0}")
                                .Bold()
                                .FontSize(11)
                                .FontColor(Slate900);
                        });
                        
                        if (isPaymentCompleted && !string.IsNullOrWhiteSpace(transactionId))
                        {
                            paymentCol.Item().PaddingTop(6).Text($"Transaction ID: {transactionId}")
                                .FontSize(9)
                                .FontColor(Slate600);
                        }
                    });
            });
    }

    private static void BuildFormFamilySection(IContainer container, ContactSection contacts)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(16)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Text("Family & Guardian")
                    .Bold()
                    .FontSize(13)
                    .FontColor(Slate900);

                column.Item().Element(inner => RenderGuardian(inner, "Father", contacts.Father));
                column.Item().Element(inner => RenderGuardian(inner, "Mother", contacts.Mother));
                column.Item().Element(inner => RenderGuardian(inner, "Local Guardian", contacts.LocalGuardian));
            });
    }

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
            QuestPDF.Settings.EnableDebugging = true; // Enable debugging to identify layout issues
            _questPdfLicenseConfigured = true;
        }
    }
}

