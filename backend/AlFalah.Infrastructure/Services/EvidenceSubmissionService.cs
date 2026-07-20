using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Keeps the file ledger and the matrix summary in lockstep. This service never
/// uploads file bytes; it is called only after Graph has returned a DriveItemId.
/// </summary>
public sealed class EvidenceSubmissionService : IEvidenceSubmissionService
{
    private readonly AlFalahDbContext _context;
    private readonly AuditLogWriter _audit;

    public EvidenceSubmissionService(AlFalahDbContext context, AuditLogWriter audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<EvidenceUploadCatalogDto> GetUploadCatalogAsync(CancellationToken cancellationToken = default)
    {
        var year = await GetActiveAcademicYearAsync(cancellationToken);
        var tasks = await _context.EvidenceTasks.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CategorySortOrder).ThenBy(x => x.SortOrder)
            .Select(x => new EvidenceTaskDto(x.Id, x.Code, x.NameAr, x.Category, x.CategorySortOrder, x.SortOrder))
            .ToListAsync(cancellationToken);
        return new(new(year.Id, year.Code, year.NameAr, year.IsActive), tasks);
    }

    public async Task<EvidenceUploadReservationDto> ReserveUploadAsync(
        int teacherId,
        int schoolId,
        int taskId,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 80)
            throw new ArgumentException("معرّف طلب الرفع مطلوب وغير صالح.");

        var taskExists = await _context.EvidenceTasks.AnyAsync(x => x.Id == taskId && x.IsActive, cancellationToken);
        if (!taskExists) throw new KeyNotFoundException("المهمة المختارة غير موجودة أو غير نشطة.");
        var year = await GetActiveAcademicYearAsync(cancellationToken);

        var existing = await _context.EvidenceUploadOperations
            .SingleOrDefaultAsync(x => x.TeacherId == teacherId && x.RequestId == requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.TaskId != taskId || existing.AcademicYearId != year.Id)
                throw new InvalidOperationException("لا يمكن إعادة استخدام معرّف الرفع لمهمة أو سنة دراسية أخرى.");

            if (existing.Status == EvidenceUploadOperationStatus.Completed && existing.SubmissionId.HasValue)
            {
                var submission = await _context.TeacherEvidenceSubmissions.AsNoTracking()
                    .SingleAsync(x => x.Id == existing.SubmissionId.Value, cancellationToken);
                return new(existing.Id, year.Id, ToUploadResult(submission));
            }

            if (existing.Status == EvidenceUploadOperationStatus.Pending)
                throw new InvalidOperationException("طلب الرفع نفسه قيد التنفيذ. انتظر لحظات ثم حدّث الصفحة.");

            existing.Status = EvidenceUploadOperationStatus.Pending;
            await _context.SaveChangesAsync(cancellationToken);
            return new(existing.Id, year.Id, null);
        }

        var operation = new EvidenceUploadOperation
        {
            TeacherId = teacherId,
            SchoolId = schoolId,
            TaskId = taskId,
            AcademicYearId = year.Id,
            RequestId = requestId.Trim(),
            Status = EvidenceUploadOperationStatus.Pending
        };
        _context.EvidenceUploadOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);
        return new(operation.Id, year.Id, null);
    }

    public async Task<UploadFileResultDto> RecordCompletedUploadAsync(
        long operationId,
        int teacherId,
        int schoolId,
        string driveId,
        string parentItemId,
        DriveItemDto item,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.ItemId)) throw new ArgumentException("لم تُرجع OneDrive معرف الملف.");

        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var operation = await _context.EvidenceUploadOperations
            .SingleOrDefaultAsync(x => x.Id == operationId && x.TeacherId == teacherId, cancellationToken)
            ?? throw new KeyNotFoundException("عملية الرفع غير موجودة.");

        if (operation.SchoolId != schoolId || operation.Status != EvidenceUploadOperationStatus.Pending)
        {
            if (operation.Status == EvidenceUploadOperationStatus.Completed && operation.SubmissionId.HasValue)
            {
                var completed = await _context.TeacherEvidenceSubmissions.AsNoTracking()
                    .SingleAsync(x => x.Id == operation.SubmissionId.Value, cancellationToken);
                return ToUploadResult(completed);
            }
            throw new InvalidOperationException("عملية الرفع غير صالحة للحفظ.");
        }

        var existing = await _context.TeacherEvidenceSubmissions
            .SingleOrDefaultAsync(x => x.DriveId == driveId && x.DriveItemId == item.ItemId, cancellationToken);
        if (existing is not null)
        {
            if (existing.TeacherId != teacherId || existing.TaskId != operation.TaskId || existing.AcademicYearId != operation.AcademicYearId)
                throw new InvalidOperationException("ملف OneDrive مرتبط مسبقًا بسجل دليل مختلف.");

            operation.Status = EvidenceUploadOperationStatus.Completed;
            operation.SubmissionId = existing.Id;
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToUploadResult(existing);
        }

        var submission = new TeacherEvidenceSubmission
        {
            TeacherId = teacherId,
            SchoolId = schoolId,
            TaskId = operation.TaskId,
            AcademicYearId = operation.AcademicYearId,
            DriveId = driveId,
            DriveItemId = item.ItemId,
            ParentItemId = parentItemId,
            FileName = item.Name,
            FileExtension = item.Extension,
            MimeType = item.MimeType,
            SizeInBytes = item.Size ?? 0,
            WebUrl = item.WebUrl,
            ETag = item.ETag,
            UploadStatus = EvidenceUploadStatus.Completed,
            ReviewStatus = EvidenceReviewStatus.PendingReview,
            UploadedAtUtc = DateTimeOffset.UtcNow
        };
        _context.TeacherEvidenceSubmissions.Add(submission);

        // Save while the transaction is open so the generated SubmissionId is
        // available to the idempotency operation and the aggregate query sees
        // the completed file.
        await _context.SaveChangesAsync(cancellationToken);

        await RecalculateTaskStatusAsync(teacherId, schoolId, operation.TaskId, operation.AcademicYearId, cancellationToken);
        operation.Status = EvidenceUploadOperationStatus.Completed;
        operation.SubmissionId = submission.Id;
        _audit.Write(schoolId, null, "TeacherEvidence.UploadCompleted", "TeacherEvidenceSubmission", item.ItemId, null,
            new { teacherId, operation.TaskId, operation.AcademicYearId, item.ItemId, item.Name, item.Size });

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new(submission.Id, item);
    }

    public async Task MarkUploadFailedAsync(long operationId, string reason, CancellationToken cancellationToken = default)
    {
        var operation = await _context.EvidenceUploadOperations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        if (operation is null || operation.Status == EvidenceUploadOperationStatus.Completed) return;
        operation.Status = EvidenceUploadOperationStatus.Failed;
        _audit.Write(operation.SchoolId, null, "TeacherEvidence.UploadFailed", "EvidenceUploadOperation", operationId.ToString(), reason,
            new { operation.TeacherId, operation.TaskId, operation.AcademicYearId });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDeletedAsync(int teacherId, long submissionId, string? deletedByUserId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var submission = await _context.TeacherEvidenceSubmissions
            .SingleOrDefaultAsync(x => x.Id == submissionId && x.TeacherId == teacherId, cancellationToken)
            ?? throw new KeyNotFoundException("ملف الدليل غير موجود.");
        if (submission.IsDeleted) return;

        submission.IsDeleted = true;
        submission.DeletedAtUtc = DateTimeOffset.UtcNow;
        submission.DeletedByUserId = deletedByUserId;
        // Persist the soft delete before querying the active-file aggregate;
        // otherwise a database query cannot see the in-memory mutation.
        await _context.SaveChangesAsync(cancellationToken);
        if (submission.TaskId.HasValue && submission.AcademicYearId.HasValue)
            await RecalculateTaskStatusAsync(teacherId, submission.SchoolId, submission.TaskId.Value, submission.AcademicYearId.Value, cancellationToken);
        _audit.Write(submission.SchoolId, deletedByUserId, "TeacherEvidence.Deleted", "TeacherEvidenceSubmission", submissionId.ToString(), null,
            new { teacherId, submission.TaskId, submission.AcademicYearId, submission.DriveId, submission.DriveItemId });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    internal async Task RecalculateTaskStatusAsync(
        int teacherId,
        int schoolId,
        int taskId,
        int academicYearId,
        CancellationToken cancellationToken,
        string? reviewedByUserId = null)
    {
        var activeFiles = await _context.TeacherEvidenceSubmissions
            .Where(x => x.TeacherId == teacherId && x.TaskId == taskId && x.AcademicYearId == academicYearId
                && x.UploadStatus == EvidenceUploadStatus.Completed && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        var status = await _context.TeacherTaskStatuses.SingleOrDefaultAsync(
            x => x.TeacherId == teacherId && x.TaskId == taskId && x.AcademicYearId == academicYearId,
            cancellationToken);
        if (status is null)
        {
            status = new TeacherTaskStatus
            {
                TeacherId = teacherId,
                SchoolId = schoolId,
                TaskId = taskId,
                AcademicYearId = academicYearId
            };
            _context.TeacherTaskStatuses.Add(status);
        }

        status.SchoolId = schoolId;
        status.ActiveFilesCount = activeFiles.Count;
        status.CellStatus = CalculateCellStatus(activeFiles);
        status.LastSubmissionAtUtc = activeFiles.FirstOrDefault()?.UploadedAtUtc;
        if (reviewedByUserId is not null)
        {
            status.LastReviewedAtUtc = DateTimeOffset.UtcNow;
            status.LastReviewedByUserId = reviewedByUserId;
        }
    }

    internal static EvidenceCellStatus CalculateCellStatus(IReadOnlyCollection<TeacherEvidenceSubmission> activeFiles)
    {
        if (activeFiles.Count == 0) return EvidenceCellStatus.NotUploaded;
        var presentFiles = activeFiles.Where(x => !x.IsMissingFromDrive).ToList();
        if (presentFiles.Count == 0) return EvidenceCellStatus.MissingFromDrive;
        if (presentFiles.Any(x => x.ReviewStatus == EvidenceReviewStatus.Approved)) return EvidenceCellStatus.Approved;
        if (presentFiles.Any(x => x.ReviewStatus == EvidenceReviewStatus.PendingReview)) return EvidenceCellStatus.PendingReview;
        if (presentFiles.Any(x => x.ReviewStatus == EvidenceReviewStatus.Rejected)) return EvidenceCellStatus.Rejected;
        return EvidenceCellStatus.Uploaded;
    }

    private async Task<AcademicYear> GetActiveAcademicYearAsync(CancellationToken cancellationToken) =>
        await _context.AcademicYears.SingleOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("لا توجد سنة دراسية نشطة لرفع الأدلة.");

    private static UploadFileResultDto ToUploadResult(TeacherEvidenceSubmission submission) => new(
        submission.Id,
        new DriveItemDto(submission.DriveItemId, submission.FileName, false, null, submission.FileExtension,
            submission.MimeType, submission.SizeInBytes, submission.UpdatedAtUtc, null, submission.WebUrl,
            submission.ETag, submission.ReviewStatus.ToString()));

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational()) return null;
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
