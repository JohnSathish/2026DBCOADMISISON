using System.Globalization;
using ERP.Application.Admissions.Interfaces;
using ERP.Application.Admissions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Admissions;

public sealed class OfflineFormReceiptPdfService : IOfflineFormReceiptPdfService
{
    private static readonly object LicenseLock = new();
    private static bool _questPdfLicenseConfigured;

    /// <summary>210mm × 148.5mm — one half of an A4 sheet (full width, half height).</summary>
    private static readonly PageSize HalfA4Landscape = new(
        MillimetresToPoints(210f),
        MillimetresToPoints(148.5f));

    private readonly ReceiptBrandingOptions _branding;
    private readonly byte[]? _logoBytes;

    public OfflineFormReceiptPdfService(IOptions<ReceiptBrandingOptions> branding, IHostEnvironment hostEnvironment)
    {
        _branding = branding.Value;
        var relative = _branding.LogoRelativePath;
        if (!string.IsNullOrWhiteSpace(relative))
        {
            var full = Path.Combine(hostEnvironment.ContentRootPath, relative.Trim());
            if (File.Exists(full))
            {
                _logoBytes = File.ReadAllBytes(full);
            }
        }
    }

    public Task<(byte[] Content, string FileName)> GenerateAsync(
        string formNumber,
        string studentName,
        string? majorSubject,
        decimal amountPaid,
        DateTime issuedOnUtc,
        string? mobileNumberForReceipt,
        string shiftDisplay,
        bool? cuetApplied,
        CancellationToken cancellationToken = default)
    {
        lock (LicenseLock)
        {
            if (!_questPdfLicenseConfigured)
            {
                QuestPDF.Settings.License = LicenseType.Community;
                _questPdfLicenseConfigured = true;
            }
        }

        const float marginMm = 11f;

        var issuedIndia = ToIndiaTime(issuedOnUtc);
        var receiptNo = $"AF-{formNumber}-{issuedIndia:yyyy}";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(HalfA4Landscape);
                page.Margin(marginMm, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(12).FontColor(Colors.Black));
                page.PageColor(Colors.White);

                page.Content().Element(c => ComposeStudentReceipt(
                    c,
                    receiptNo,
                    formNumber,
                    studentName,
                    majorSubject,
                    mobileNumberForReceipt,
                    shiftDisplay,
                    cuetApplied,
                    amountPaid,
                    issuedIndia));
            });
        });

        var bytes = doc.GeneratePdf();
        var fileName = $"application-fee-receipt-{formNumber}.pdf";
        return Task.FromResult((bytes, fileName));
    }

    private void ComposeStudentReceipt(
        IContainer container,
        string receiptNo,
        string formNumber,
        string studentName,
        string? majorSubject,
        string? mobileNumberForReceipt,
        string shiftDisplay,
        bool? cuetApplied,
        decimal amountPaid,
        DateTime issuedIndia)
    {
        var watermarkText = ShortCollegeWatermark(_branding.CollegeName);

        container
            .Layers(layers =>
            {
                layers.Layer()
                    .AlignMiddle()
                    .AlignCenter()
                    .Rotate(-38)
                    .Text(watermarkText)
                    .FontSize(30)
                    .Bold()
                    .FontColor("#F3F3F3");

                layers.PrimaryLayer()
                    .Border(1f)
                    .BorderColor("#222222")
                    .Padding(8, Unit.Millimetre)
                    .Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().Column(header =>
                        {
                            header.Spacing(2);

                            if (_logoBytes is { Length: > 0 })
                            {
                                header.Item().AlignCenter()
                                    .Width(14, Unit.Millimetre)
                                    .Height(14, Unit.Millimetre)
                                    .Image(_logoBytes)
                                    .FitArea();
                            }

                            header.Item().AlignCenter().Text(FormatCollegeTitle(_branding.CollegeName))
                                .Bold()
                                .FontSize(14)
                                .FontColor("#000000");
                            if (!string.IsNullOrWhiteSpace(_branding.AddressLine1))
                            {
                                header.Item().AlignCenter().Text(_branding.AddressLine1).FontSize(10);
                            }

                            if (!string.IsNullOrWhiteSpace(_branding.AddressLine2))
                            {
                                header.Item().AlignCenter().Text(_branding.AddressLine2).FontSize(10);
                            }

                            var contact = string.Join(
                                " | ",
                                new[]
                                    {
                                        FormatContact("Ph", _branding.Phone),
                                        FormatContact("Email", _branding.Email),
                                        FormatContact("Web", _branding.Website),
                                    }
                                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                            if (!string.IsNullOrWhiteSpace(contact))
                            {
                                header.Item().AlignCenter().Text(contact).FontSize(9).FontColor("#333333");
                            }
                        });

                        col.Item().PaddingTop(2).AlignCenter().Text("STUDENT COPY")
                            .Bold()
                            .FontSize(8)
                            .FontColor("#444444")
                            .LetterSpacing(0.5f);

                        col.Item().AlignCenter().Text("APPLICATION FEE RECEIPT")
                            .Bold()
                            .FontSize(15)
                            .FontColor("#000000")
                            .LetterSpacing(0.3f);

                        col.Item().PaddingTop(2).Row(meta =>
                        {
                            meta.RelativeItem().Text($"Receipt No: {receiptNo}").FontSize(10).SemiBold();
                            meta.RelativeItem().AlignRight()
                                .Text($"Date of Issue: {issuedIndia:dd MMM yyyy, hh:mm tt} IST")
                                .FontSize(10);
                        });

                        col.Item().PaddingTop(2).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                            });

                            void Row(string label, string value)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                                    .AlignMiddle().Text(label).SemiBold().FontSize(12);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                                    .AlignMiddle().AlignRight().Text(value).FontSize(12);
                            }

                            Row("Form Number", formNumber);
                            Row("Student Name", studentName);
                            Row("Shift", string.IsNullOrWhiteSpace(shiftDisplay) ? "—" : shiftDisplay);
                            Row(
                                "CUET Application Status",
                                cuetApplied is null ? "—" : (cuetApplied.Value ? "Yes" : "No"));
                            if (!string.IsNullOrWhiteSpace(majorSubject))
                            {
                                Row("Subject / Course", majorSubject);
                            }

                            if (!string.IsNullOrWhiteSpace(mobileNumberForReceipt))
                            {
                                Row("Mobile Number", mobileNumberForReceipt);
                            }

                            Row("Amount Paid (₹)", amountPaid.ToString("N2", CultureInfo.InvariantCulture));
                        });

                        col.Item().PaddingTop(4).AlignCenter()
                            .Text("This receipt confirms payment of application fee.")
                            .FontSize(9)
                            .Italic()
                            .FontColor("#555555");
                    });
            });
    }

    private static string ShortCollegeWatermark(string collegeName)
    {
        var s = collegeName.Trim();
        if (s.Length <= 24)
        {
            return s.ToUpperInvariant();
        }

        return "DON BOSCO COLLEGE";
    }

    private static string FormatCollegeTitle(string collegeName)
    {
        var t = collegeName.Trim().ToUpperInvariant();
        if (t.Contains("DON BOSCO", StringComparison.Ordinal) && t.Contains("TURA", StringComparison.Ordinal))
        {
            return "DON BOSCO COLLEGE, TURA";
        }

        return t;
    }

    private static float MillimetresToPoints(float mm) => mm * 72f / 25.4f;

    private static string? FormatContact(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value.Trim()}";
    }

    private static DateTime ToIndiaTime(DateTime utc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
    }
}
