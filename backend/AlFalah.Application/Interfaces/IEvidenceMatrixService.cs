using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Interfaces;

public interface IEvidenceMatrixService
{
    Task<IReadOnlyList<AcademicYearDto>> GetAcademicYearsAsync(CancellationToken cancellationToken = default);
    Task<EvidenceMatrixDto> GetAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default);
    Task<EvidenceCellFilesDto> GetCellFilesAsync(int teacherId, int taskId, int academicYearId, CancellationToken cancellationToken = default);
    Task ReviewAsync(long submissionId, EvidenceReviewStatus reviewStatus, string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams an evidence file to a supervisor so they can actually read what they are
    /// approving. Scoped to their school, exactly like <see cref="GetCellFilesAsync"/>.
    /// </summary>
    Task<AlFalah.Application.DTOs.TeacherDrive.DriveFileContentDto> DownloadSubmissionAsync(long submissionId, CancellationToken cancellationToken = default);
    Task<EvidenceMatrixExportResult> ExportExcelAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default);
    Task<EvidenceMatrixExportResult> ExportPdfAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default);
}

public interface IEvidenceReconciliationService
{
    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);
}
