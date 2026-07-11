using AlFalah.Application.DTOs.Reports;

namespace AlFalah.Application.Interfaces;

/// <summary>
/// Phase 6 / Stage 1 — server-side Arabic PDF report builder.
///
/// Implementation lives in <c>AlFalah.Infrastructure.Services.PdfReportService</c>
/// using QuestPDF (Community license). The interface is here in Application so
/// the Api controller can depend on it without referencing Infrastructure
/// directly.
///
/// The PDF is rendered with an Arabic-capable font (Amiri) embedded as a
/// project asset — never relying on system fonts — so the output renders
/// correctly across every deployment.
/// </summary>
public interface IPdfReportService
{
    /// <summary>
    /// Renders the visit's approved report into an in-memory PDF byte stream.
    /// Caller is responsible for streaming the bytes back via <c>File(...)</c>
    /// with <c>application/pdf</c>.
    /// </summary>
    /// <param name="dto">Snapshot-driven payload — see <see cref="VisitReportDto"/>.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to QuestPDF's
    /// async generation pipeline.</param>
    /// <returns>The PDF byte stream (a fresh in-memory copy).</returns>
    Task<byte[]> RenderAsync(VisitReportDto dto, CancellationToken cancellationToken = default);
}
