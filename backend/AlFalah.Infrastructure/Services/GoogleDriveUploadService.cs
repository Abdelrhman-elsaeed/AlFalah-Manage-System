using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Write side of a teacher's evidence folder. The ordering is deliberate: reserve an
/// idempotent operation, validate, push the bytes to Drive, and only then record the
/// submission that ticks the matrix cell. A file therefore never counts as evidence unless
/// it really reached Drive, and a retried request never produces a second upload.
/// </summary>
public sealed class GoogleDriveUploadService : IGoogleDriveUploadService
{
    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };

    private readonly ITeacherDriveIdentityService _identity;
    private readonly ITeacherDriveMappingService _mappings;
    private readonly IGoogleDriveClient _drive;
    private readonly TeacherDriveFolderGuard _guard;
    private readonly IEvidenceSubmissionService _submissions;
    private readonly IConfiguration _configuration;
    private readonly AlFalahDbContext _context;

    public GoogleDriveUploadService(
        ITeacherDriveIdentityService identity,
        ITeacherDriveMappingService mappings,
        IGoogleDriveClient drive,
        TeacherDriveFolderGuard guard,
        IEvidenceSubmissionService submissions,
        IConfiguration configuration,
        AlFalahDbContext context)
    {
        _identity = identity;
        _mappings = mappings;
        _drive = drive;
        _guard = guard;
        _submissions = submissions;
        _configuration = configuration;
        _context = context;
    }

    public async Task<UploadFileResultDto> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var teacher = await _identity.ResolveCurrentTeacherAsync(cancellationToken);
        var mapping = await _mappings.GetForTeacherAsync(teacher.TeacherId, cancellationToken);
        if (mapping.SchoolId != teacher.SchoolId) throw new TeacherDriveAccessDeniedException();

        var destination = string.IsNullOrWhiteSpace(request.ParentItemId) ? mapping.RootItemId : request.ParentItemId!;
        // Authorize the destination BEFORE reserving the operation, so a rejected folder
        // does not leave a Pending row that blocks the teacher's next legitimate retry.
        await _guard.EnsureFolderWithinGrantAsync(mapping, destination, cancellationToken);

        var reservation = await _submissions.ReserveUploadAsync(
            teacher.TeacherId, teacher.SchoolId, request.TaskId, request.RequestId, cancellationToken);
        if (reservation.ExistingResult is not null) return reservation.ExistingResult;

        GoogleDriveFile uploaded;
        try
        {
            uploaded = await _drive.UploadAsync(teacher.SchoolId, new GoogleDriveUploadRequest(
                request.Content,
                request.FileName.Trim(),
                string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType!,
                destination,
                string.IsNullOrWhiteSpace(mapping.DriveId) ? null : mapping.DriveId), cancellationToken);
        }
        catch (Exception ex)
        {
            await _submissions.MarkUploadFailedAsync(reservation.OperationId, ex.Message, cancellationToken);
            throw;
        }

        // If this transaction fails the reservation stays Pending, which blocks a retry
        // instead of uploading the same evidence to Drive a second time.
        return await _submissions.RecordCompletedUploadAsync(
            reservation.OperationId, teacher.TeacherId, teacher.SchoolId, mapping.DriveId, destination,
            GoogleDriveBrowserService.ToDto(uploaded), cancellationToken);
    }

    public async Task DeleteAsync(long submissionId, CancellationToken cancellationToken = default)
    {
        var teacher = await _identity.ResolveCurrentTeacherAsync(cancellationToken);
        var submission = await _context.TeacherEvidenceSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == submissionId && x.TeacherId == teacher.TeacherId, cancellationToken)
            ?? throw new KeyNotFoundException("ملف الدليل غير موجود.");
        if (submission.IsDeleted) return;

        var mapping = await _mappings.GetForTeacherAsync(teacher.TeacherId, cancellationToken);
        if (mapping.SchoolId != teacher.SchoolId) throw new TeacherDriveAccessDeniedException();

        // Prove the file is still inside the teacher's CURRENT grant before deleting it.
        // Ownership of the ledger row is not sufficient on its own: if a teacher's grant was
        // moved, an old row must not become a lever for deleting a file they no longer own.
        await _guard.EnsureWithinGrantAsync(mapping, submission.DriveItemId, cancellationToken);

        await _drive.TrashAsync(teacher.SchoolId, submission.DriveItemId,
            string.IsNullOrWhiteSpace(mapping.DriveId) ? null : mapping.DriveId, cancellationToken);
        // Clear the local ledger even when Drive reported the file as already gone: the
        // desired end state is absence, so the matrix checkmark must come down either way.
        await _submissions.MarkDeletedAsync(teacher.TeacherId, submissionId, null, cancellationToken);
    }

    private void Validate(UploadFileRequest request)
    {
        var maxBytes = _configuration.GetValue<long?>("TeacherDrive:MaxUploadBytes") ?? 250L * 1024 * 1024;
        if (request.Content is null || request.Length <= 0) throw new ArgumentException("لا يمكن رفع ملف فارغ.");
        if (request.Length > maxBytes) throw new ArgumentException("حجم الملف أكبر من الحد المسموح.");
        if (string.IsNullOrWhiteSpace(request.FileName)
            || request.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || request.FileName.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("اسم الملف غير صالح.");

        var extension = Path.GetExtension(request.FileName);
        var configured = _configuration.GetSection("TeacherDrive:AllowedExtensions").Get<string[]>();
        var allowed = configured is { Length: > 0 }
            ? new HashSet<string>(configured.Select(x => x.StartsWith('.') ? x : "." + x), StringComparer.OrdinalIgnoreCase)
            : DefaultExtensions;
        if (!allowed.Contains(extension)) throw new ArgumentException("نوع الملف غير مسموح.");
    }
}
