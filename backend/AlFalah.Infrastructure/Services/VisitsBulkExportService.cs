using System.IO.Compression;
using System.Text;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// D-41 / Task 6 — bulk-export implementation. Uses <see cref="ZipArchive"/>
/// over an in-memory stream, and re-uses the existing
/// <see cref="VisitService.GetVisitReportAsync"/> + <see cref="IPdfReportService"/>
/// to render each PDF (so every guard, snapshot-fidelity rule, and watermark
/// is identical to the single-visit endpoint).
///
/// The ZIP is buffered fully in memory; this matches the existing single-PDF
/// pattern (a fresh in-memory copy per request). For very large exports a
/// streaming archive could replace this; deferred to a follow-up if needed.
/// </summary>
public class VisitsBulkExportService : IVisitsBulkExportService
{
    private readonly IVisitService _visitService;
    private readonly IPdfReportService _pdfReportService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VisitsBulkExportService> _logger;

    public VisitsBulkExportService(
        IVisitService visitService,
        IPdfReportService pdfReportService,
        ICurrentUserService currentUser,
        ILogger<VisitsBulkExportService> logger)
    {
        _visitService = visitService;
        _pdfReportService = pdfReportService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<BulkExportResult> ExportVisitsZipAsync(
        VisitListQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1) Reuse the SAME scoped query the list endpoint uses so we only
        //    ever export visits the caller can see (school-scope, moderator
        //    own-only, global admin bypass — D-24 / D-28 / D-37).
        var ids = await _visitService.ListScopedVisitIdsForExportAsync(query, cancellationToken);

        if (ids.Count == 0)
        {
            _logger.LogInformation(
                "Bulk export produced an empty archive (no visits matched the scope); user={UserId}",
                _currentUser.UserId);
        }

        // 2) Pre-resolve the caller's school name for the ZIP-level filename.
        //    For school-scoped callers this is their ActiveSchoolId; for
        //    global admins we use a generic placeholder. We pull it from the
        //    ICurrentUserService interface — fall back to empty when the
        //    service doesn't expose it directly.
        var schoolName = TryGetActiveSchoolName();
        var generatedAt = DateTimeOffset.UtcNow;
        var zipFileName = SuggestedZipFilename(schoolName, generatedAt);

        // 3) Render one PDF per visit, packaging them into a single ZIP.
        //    We track used filenames so duplicate names are disambiguated by
        //    appending the visit id (D-41).
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var archiveBytes = new byte[0];

        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var dto = await _visitService.GetVisitReportAsync(id, cancellationToken);
                        var pdfBytes = await _pdfReportService.RenderAsync(dto, cancellationToken);

                        var baseName = PdfReportService.BuildPdfFilename(
                            dto.InstructorFullName,
                            dto.VisitDate,
                            dto.VisitCategoryLabelAr);

                        var entryName = DeduplicateFilename(baseName, id, nameCounts);

                        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using (var es = entry.Open())
                        {
                            await es.WriteAsync(pdfBytes, cancellationToken);
                        }

                        // Keep track so two visits with the same teacher / year / type
                        // don't collide.
                        usedNames.Add(entryName);
                    }
                    catch (Exception ex)
                    {
                        // D-41 fail-soft — a single broken PDF must NEVER abort the
                        // whole export. Log + skip so the caller still receives a
                        // ZIP with the remaining visits.
                        _logger.LogWarning(ex,
                            "Bulk export: skipping visit {VisitId} due to render/lookup error.",
                            id);
                    }
                }
            }

            archiveBytes = ms.ToArray();
        }

        _logger.LogInformation(
            "Bulk export complete: {Count} visits packaged by user={UserId}",
            ids.Count, _currentUser.UserId);

        return new BulkExportResult
        {
            ZipBytes = archiveBytes,
            FileName = zipFileName,
            VisitCount = ids.Count
        };
    }

    public string SuggestedZipFilename(string? schoolName, DateTimeOffset generatedAt)
        => PdfReportService.BuildZipFilename(schoolName, generatedAt);

    /// <summary>
    /// Disambiguates duplicate filenames inside the ZIP by appending the
    /// visit id (D-41 requirement). The pattern is:
    ///   first occurrence  → "{baseName}"
    ///   second+          → "{baseName-without-.pdf} ({visitId}).pdf"
    /// </summary>
    private static string DeduplicateFilename(string baseName, int visitId, Dictionary<string, int> nameCounts)
    {
        if (!nameCounts.TryGetValue(baseName, out var count))
        {
            nameCounts[baseName] = 1;
            return baseName;
        }

        nameCounts[baseName] = count + 1;
        var withoutExt = baseName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? baseName.Substring(0, baseName.Length - 4)
            : baseName;
        return $"{withoutExt} ({visitId}).pdf";
    }

    /// <summary>
    /// Reads the caller's school name for the ZIP filename. We deliberately
    /// rely on the existing <see cref="ICurrentUserService"/> surface so we
    /// don't introduce a new dependency on the School repository here. When
    /// the school name is unavailable (e.g. global admin), the ZIP filename
    /// falls back to "زيارات-{date}.zip" via
    /// <see cref="PdfReportService.BuildZipFilename"/>.
    /// </summary>
    private string? TryGetActiveSchoolName()
    {
        try
        {
            // ICurrentUserService exposes school id but not school name on
            // purpose — the controller has access to it through its own scope.
            // We keep the public interface minimal here and let the filename
            // helper fall back to the date-only form when this is null.
            return null;
        }
        catch
        {
            return null;
        }
    }
}