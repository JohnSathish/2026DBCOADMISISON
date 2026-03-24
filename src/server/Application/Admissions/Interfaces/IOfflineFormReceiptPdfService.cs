namespace ERP.Application.Admissions.Interfaces;

public interface IOfflineFormReceiptPdfService
{
    /// <param name="majorSubject">Null on fee receipt at issuance (subject chosen at receive).</param>
    /// <param name="mobileNumberForReceipt">Null to omit mobile from the PDF (issuance receipt).</param>
    /// <param name="shiftDisplay">e.g. Shift-I (canonical codes mapped for display).</param>
    /// <param name="cuetApplied">Null when not recorded (legacy).</param>
    Task<(byte[] Content, string FileName)> GenerateAsync(
        string formNumber,
        string studentName,
        string? majorSubject,
        decimal amountPaid,
        DateTime issuedOnUtc,
        string? mobileNumberForReceipt,
        string shiftDisplay,
        bool? cuetApplied,
        CancellationToken cancellationToken = default);
}
