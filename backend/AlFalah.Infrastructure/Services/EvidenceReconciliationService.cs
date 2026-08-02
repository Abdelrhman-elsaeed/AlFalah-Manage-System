using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Keeps the evidence ledger honest about Google Drive.
///
/// A teacher (or an administrator working directly in Drive) can delete or trash a file
/// outside the application, which would otherwise leave the matrix showing a checkmark for
/// evidence that no longer exists. This pass re-checks every linked file and flips
/// <c>IsMissingFromDrive</c> in either direction, then recomputes the affected cells.
///
/// It never infers a task from a file name — only already-linked file ids are verified.
/// </summary>
public sealed class EvidenceReconciliationService : IEvidenceReconciliationService
{
    private readonly AlFalahDbContext _context;
    private readonly IGoogleDriveClient _drive;
    private readonly AuditLogWriter _audit;
    private readonly EvidenceSubmissionService _submissions;
    private readonly ILogger<EvidenceReconciliationService> _logger;

    public EvidenceReconciliationService(
        AlFalahDbContext context,
        IGoogleDriveClient drive,
        AuditLogWriter audit,
        EvidenceSubmissionService submissions,
        ILogger<EvidenceReconciliationService> logger)
    {
        _context = context;
        _drive = drive;
        _audit = audit;
        _submissions = submissions;
        _logger = logger;
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Only schools with a live connection can be checked. Skipping the rest keeps an
        // unconfigured school from logging a token error on every pass.
        var connectedSchoolIds = await _context.SchoolGoogleDrives.AsNoTracking()
            .Where(x => x.IsEnabled)
            .Select(x => x.SchoolId)
            .ToListAsync(cancellationToken);
        if (connectedSchoolIds.Count == 0) return 0;

        var candidates = await _context.TeacherEvidenceSubmissions
            .Where(x => x.UploadStatus == EvidenceUploadStatus.Completed
                && !x.IsDeleted
                && x.TaskId != null
                && x.AcademicYearId != null
                && connectedSchoolIds.Contains(x.SchoolId))
            .ToListAsync(cancellationToken);

        var changed = new List<(int TeacherId, int SchoolId, int TaskId, int AcademicYearId)>();
        // One unreachable school must not abort the whole sweep, so failures are tracked per
        // school and the rest still get reconciled.
        var unreachableSchools = new HashSet<int>();

        foreach (var submission in candidates)
        {
            if (unreachableSchools.Contains(submission.SchoolId)) continue;

            bool missing;
            try
            {
                var file = await _drive.GetFileAsync(submission.SchoolId, submission.DriveItemId, cancellationToken);
                // Trashed counts as missing: the file is no longer usable as evidence even
                // though Drive still answers for its id.
                missing = file is null || file.Trashed;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A transport or credential failure says nothing about whether the file
                // exists, so the flag must be left exactly as it was.
                unreachableSchools.Add(submission.SchoolId);
                _logger.LogWarning(ex, "Skipping Google Drive reconciliation for school {SchoolId}.", submission.SchoolId);
                continue;
            }

            if (submission.IsMissingFromDrive == missing) continue;
            submission.IsMissingFromDrive = missing;
            submission.MissingFromDriveAtUtc = missing ? DateTimeOffset.UtcNow : null;
            changed.Add((submission.TeacherId, submission.SchoolId, submission.TaskId!.Value, submission.AcademicYearId!.Value));
            _audit.Write(submission.SchoolId, null,
                missing ? "TeacherEvidence.MissingFromDrive" : "TeacherEvidence.RestoredOnDrive",
                "TeacherEvidenceSubmission", submission.Id.ToString(), null,
                new { submission.TeacherId, submission.TaskId, submission.AcademicYearId, submission.DriveItemId });
        }

        if (changed.Count == 0) return 0;
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var group in changed.Distinct())
            await _submissions.RecalculateTaskStatusAsync(group.TeacherId, group.SchoolId, group.TaskId, group.AcademicYearId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return changed.Count;
    }
}
