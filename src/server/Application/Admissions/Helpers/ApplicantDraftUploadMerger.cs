using ERP.Application.Admissions.DTOs;

namespace ERP.Application.Admissions.Helpers;

/// <summary>
/// Fills missing upload base64 from the draft already stored server-side so submit requests can omit heavy <c>Data</c> fields.
/// If the client sends a different file name than the stored draft (new file selected but draft not saved), throws so the user saves first.
/// </summary>
public static class ApplicantDraftUploadMerger
{
    public static void MergeUploadBinaryFromStoredDraftIfMissing(
        ApplicantApplicationDraftDto incoming,
        ApplicantApplicationDraftDto? stored)
    {
        if (stored?.Uploads is null)
        {
            return;
        }

        incoming.Uploads ??= new UploadSection();
        var s = stored.Uploads;

        incoming.Uploads.StdXMarksheet = MergeFile(incoming.Uploads.StdXMarksheet, s.StdXMarksheet);
        incoming.Uploads.StdXIIMarksheet = MergeFile(incoming.Uploads.StdXIIMarksheet, s.StdXIIMarksheet);
        incoming.Uploads.CuetMarksheet = MergeFile(incoming.Uploads.CuetMarksheet, s.CuetMarksheet);
        incoming.Uploads.DifferentlyAbledProof = MergeFile(incoming.Uploads.DifferentlyAbledProof, s.DifferentlyAbledProof);
        incoming.Uploads.EconomicallyWeakerProof = MergeFile(incoming.Uploads.EconomicallyWeakerProof, s.EconomicallyWeakerProof);
    }

    private static FileAttachmentDto? MergeFile(FileAttachmentDto? incoming, FileAttachmentDto? stored)
    {
        if (incoming is not null && !string.IsNullOrWhiteSpace(incoming.Data))
        {
            return incoming;
        }

        if (stored is null || string.IsNullOrWhiteSpace(stored.Data))
        {
            return incoming;
        }

        // Client omitted bytes; require file identity to match stored draft unless client sent no filename hint.
        if (incoming is not null
            && !string.IsNullOrWhiteSpace(incoming.FileName)
            && !string.IsNullOrWhiteSpace(stored.FileName)
            && !string.Equals(incoming.FileName.Trim(), stored.FileName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Your documents are not synced with the server. Please click \"Save Draft\", wait for it to finish, then submit again.");
        }

        return new FileAttachmentDto
        {
            FileName = PreferNonEmpty(incoming?.FileName, stored.FileName),
            ContentType = PreferNonEmpty(incoming?.ContentType, stored.ContentType),
            Data = stored.Data
        };
    }

    private static string PreferNonEmpty(string? a, string b)
    {
        return string.IsNullOrWhiteSpace(a) ? b : a;
    }
}
