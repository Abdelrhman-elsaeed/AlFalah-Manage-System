using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance;

public sealed record NoorAcceptedExcuseSnapshot(
    int DailyStudentAttendanceId,
    int StudentId,
    string StudentName,
    string? NationalId,
    DateOnly AttendanceDate,
    AbsenceExcuseStatus ExcuseStatus);

public sealed record NoorWorkbookRow(
    string StudentName,
    string NationalId,
    DateOnly Date,
    string ExcuseStatus);

public sealed record NoorExportFileDto(
    int BatchId,
    int RowCount,
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record ExportNoorAbsenceCorrectionsCommand(DateOnly WeekStartsOn, string IdempotencyKey)
    : IRequest<ApiResponse<NoorExportFileDto>>;

public interface INoorWorkbookWriter
{
    byte[] Write(IReadOnlyList<NoorWorkbookRow> rows);
}

public interface INoorExportRepository
{
    Task<NoorAbsenceCorrectionBatch?> GetBatchAsync(
        int schoolId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NoorAcceptedExcuseSnapshot>> GetAcceptedExcusesAsync(
        int schoolId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    void Add(NoorAbsenceCorrectionBatch batch);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
