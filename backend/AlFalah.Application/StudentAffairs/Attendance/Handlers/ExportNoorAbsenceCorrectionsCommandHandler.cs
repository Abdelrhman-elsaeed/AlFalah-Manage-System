using System.Security.Cryptography;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class ExportNoorAbsenceCorrectionsCommandHandler
    : IRequestHandler<ExportNoorAbsenceCorrectionsCommand, ApiResponse<NoorExportFileDto>>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly INoorExportRepository _repository;
    private readonly INoorWorkbookWriter _writer;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ExportNoorAbsenceCorrectionsCommandHandler(
        INoorExportRepository repository,
        INoorWorkbookWriter writer,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _writer = writer;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<NoorExportFileDto>> Handle(
        ExportNoorAbsenceCorrectionsCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<NoorExportFileDto>.Fail("An authenticated actor and active school are required");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 200)
            return ApiResponse<NoorExportFileDto>.Fail("A valid Idempotency-Key header is required");

        var existing = await _repository.GetBatchAsync(
            schoolId.Value, command.IdempotencyKey.Trim(), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return ApiResponse<NoorExportFileDto>.Success(ToFile(existing), "Existing Noor export returned");

        var weekEndsOn = command.WeekStartsOn.AddDays(6);
        var sourceRows = await _repository.GetAcceptedExcusesAsync(
            schoolId.Value, command.WeekStartsOn, weekEndsOn, cancellationToken).ConfigureAwait(false);
        var missingNationalIds = sourceRows.Where(row => string.IsNullOrWhiteSpace(row.NationalId)).ToArray();
        if (missingNationalIds.Length > 0)
            return ApiResponse<NoorExportFileDto>.Fail(
                $"{missingNationalIds.Length} accepted absence row(s) cannot be exported because the student National ID is missing");

        var now = _timeProvider.GetUtcNow();
        var fileName = $"noor-absence-corrections-{command.WeekStartsOn:yyyy-MM-dd}.xlsx";
        var batch = new NoorAbsenceCorrectionBatch
        {
            SchoolId = schoolId.Value,
            WeekStartsOn = command.WeekStartsOn,
            WeekEndsOn = weekEndsOn,
            IdempotencyKey = command.IdempotencyKey.Trim(),
            Status = NoorAbsenceCorrectionBatchStatus.Created,
            RowCount = sourceRows.Count,
            FileName = fileName,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        foreach (var row in sourceRows)
        {
            batch.Items.Add(new NoorAbsenceCorrectionBatchItem
            {
                SchoolId = schoolId.Value,
                DailyStudentAttendanceId = row.DailyStudentAttendanceId,
                StudentId = row.StudentId,
                StudentNameSnapshot = row.StudentName,
                NationalIdSnapshot = row.NationalId!.Trim(),
                AttendanceDate = row.AttendanceDate,
                ExcuseStatusSnapshot = row.ExcuseStatus
            });
        }

        var content = Write(batch.Items);
        batch.Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        batch.Status = NoorAbsenceCorrectionBatchStatus.Exported;
        batch.ExportedAt = now;
        batch.ExportedByUserId = userId;
        _repository.Add(batch);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ApiResponse<NoorExportFileDto>.Success(
            new NoorExportFileDto(batch.Id, batch.RowCount, content, ExcelContentType, fileName),
            "Noor absence correction batch exported");
    }

    private NoorExportFileDto ToFile(NoorAbsenceCorrectionBatch batch)
    {
        var content = Write(batch.Items);
        return new NoorExportFileDto(
            batch.Id,
            batch.RowCount,
            content,
            ExcelContentType,
            batch.FileName ?? $"noor-absence-corrections-{batch.WeekStartsOn:yyyy-MM-dd}.xlsx");
    }

    private byte[] Write(IEnumerable<NoorAbsenceCorrectionBatchItem> items) =>
        _writer.Write(items.OrderBy(item => item.AttendanceDate).ThenBy(item => item.StudentNameSnapshot)
            .Select(item => new NoorWorkbookRow(
                item.StudentNameSnapshot,
                item.NationalIdSnapshot,
                item.AttendanceDate,
                item.ExcuseStatusSnapshot.ToString()))
            .ToArray());
}
