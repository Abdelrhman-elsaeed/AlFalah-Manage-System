using AlFalah.Application.DTOs.Visits;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// D-41 / Task 6 — bulk-export visits as a single ZIP of individual PDFs.
///
/// Implementations MUST reuse the same scoped query the list endpoint uses
/// so the caller only ever receives visits they are allowed to see
/// (school-scope, moderator own-only, global admin bypass). Each PDF inside
/// the ZIP carries the SAME watermark rules as the single-visit endpoint
/// (D-41: non-Approved visits stamped "مسودة — غير معتمدة").
///
/// The ZIP is returned as a single in-memory byte stream so the controller
/// can stream it back via <c>File(...)</c> with <c>application/zip</c>.
/// </summary>
public interface IVisitsBulkExportService
{
    /// <summary>
    /// Renders one PDF per visit visible to the caller (according to the
    /// supplied filters + visibility gates) and packs them into a single
    /// ZIP archive. The ZIP's internal entry names follow the pattern
    /// <c>{teacher} - {year} - {visitType}.pdf</c>, sanitized for
    /// filesystem safety; duplicates are disambiguated by appending the
    /// visit id.
    /// </summary>
    /// <param name="query">Same filters as <c>GET /api/v1/visits</c>
    /// (status / category / instructor / date range). Empty query = every
    /// visit visible to the caller.</param>
    /// <param name="cancellationToken">Forwarded to QuestPDF's async
    /// pipeline + the EF Core bulk query.</param>
    /// <returns>The full ZIP archive as a single in-memory byte array, plus
    /// the suggested ZIP-level filename (so the controller can set a clean
    /// <c>Content-Disposition</c>).</returns>
    Task<BulkExportResult> ExportVisitsZipAsync(
        VisitListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Suggested ZIP filename for the given caller context.</summary>
    string SuggestedZipFilename(string? schoolName, DateTimeOffset generatedAt);
}

/// <summary>
/// Result wrapper for the bulk export so the controller can read both the
/// archive bytes and the recommended filename in one round-trip.
/// </summary>
public class BulkExportResult
{
    public byte[] ZipBytes { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = "visits.zip";
    public int VisitCount { get; set; }
}