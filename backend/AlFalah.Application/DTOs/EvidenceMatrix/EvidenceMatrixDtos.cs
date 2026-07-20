using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.EvidenceMatrix;

public sealed record EvidenceTaskDto(int Id, string Code, string NameAr, string Category, int CategorySortOrder, int SortOrder);
public sealed record AcademicYearDto(int Id, string Code, string NameAr, bool IsActive);
public sealed record EvidenceUploadCatalogDto(AcademicYearDto AcademicYear, IReadOnlyList<EvidenceTaskDto> Tasks);

public sealed class EvidenceMatrixFilterDto
{
    public int? SchoolId { get; init; }
    public int? AcademicYearId { get; init; }
    public int? TeacherId { get; init; }
    public string? Category { get; init; }
    public EvidenceCellStatus? CompletionStatus { get; init; }
}

public sealed record EvidenceMatrixDto(
    AcademicYearDto AcademicYear,
    IReadOnlyList<EvidenceTaskDto> Tasks,
    IReadOnlyList<EvidenceMatrixTeacherRowDto> Rows,
    int TotalTasks);

public sealed record EvidenceMatrixTeacherRowDto(
    int TeacherId,
    string TeacherName,
    int SchoolId,
    string SchoolName,
    int CompletedTasksCount,
    IReadOnlyList<EvidenceMatrixCellDto> Cells);

public sealed record EvidenceMatrixCellDto(
    int TaskId,
    EvidenceCellStatus Status,
    bool IsChecked,
    int ActiveFilesCount);

public sealed record EvidenceCellFilesDto(
    int TeacherId,
    int TaskId,
    int AcademicYearId,
    EvidenceCellStatus Status,
    IReadOnlyList<EvidenceSubmissionFileDto> Files);

public sealed record EvidenceSubmissionFileDto(
    long SubmissionId,
    string FileName,
    string? FileExtension,
    long SizeInBytes,
    string? WebUrl,
    EvidenceReviewStatus ReviewStatus,
    bool IsDeleted,
    bool IsMissingFromDrive,
    DateTimeOffset UploadedAtUtc,
    string? ReviewNote);

public sealed record ReviewEvidenceSubmissionRequest(EvidenceReviewStatus ReviewStatus, string? Note);
public sealed record EvidenceMatrixExportResult(byte[] Bytes, string ContentType, string FileName);
