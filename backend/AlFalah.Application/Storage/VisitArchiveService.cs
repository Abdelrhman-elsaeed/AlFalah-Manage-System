using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Storage;

public sealed class VisitArchiveService(IVisitArchiveRepository repository, IStorageRepository storage,
    IStorageAuthorizationService authorization, ICurrentUserService user, IFeatureFlagService features,
    VisitArchiveDriveService drive, IStorageProvider provider, IOptions<StorageOptions> options, TimeProvider time) : IVisitArchiveService
{
    private int School => user.ActiveSchoolId ?? throw new UnauthorizedSchoolAccessException("لا توجد مدرسة نشطة.");
    private void Enabled()
    {
        if (!options.Value.AdministrationEnabled || !options.Value.ReadModelEnabled || !features.IsVisitsV2Enabled(School))
            throw new KeyNotFoundException("الأرشيف غير متاح حاليًا.");
    }
    public async Task<IReadOnlyList<VisitArchiveTeacherDto>> TeachersAsync(CancellationToken ct = default)
    {
        Enabled(); await RequireManagementAsync(ct);
        return await repository.TeachersAsync(School, ct);
    }
    private async Task RequireManagementAsync(CancellationToken ct)
    {
        var scope = await authorization.RequireScopeAsync(School, ct);
        if ((user.IsInRole(RoleNames.Instructor) || user.IsInRole(RoleNames.Moderator)) && scope.ManagerUserId != scope.UserId) throw Denied();
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageViewArchive, ct);
        if (!await storage.HasPermissionAsync(scope.UserId, School, PermissionNames.VisitView, ct)) throw Denied();
    }
    public async Task<VisitArchiveOperationsDto> OperationsStatusAsync(CancellationToken ct = default)
    {
        Enabled();
        await RequireManagementAsync(ct);
        var flags = options.Value;
        return new(flags.ArchiveWorkerEnabled, flags.ArchiveExternalWritesEnabled,
            flags.AdministrationEnabled && flags.ReadModelEnabled && flags.ArchiveWorkerEnabled && flags.ArchiveExternalWritesEnabled);
    }
    private async Task<bool> AuthorizeAsync(ArchiveVisitScope visit, bool management, CancellationToken ct)
    {
        var scope = await authorization.RequireScopeAsync(School, ct);
        // Same V2 approved-only teacher gate, with live membership/permission checks.
        var teacher = user.IsInRole(RoleNames.Instructor) && scope.ManagerUserId != scope.UserId && !user.IsInRole(RoleNames.Moderator);
        if (teacher)
        {
            if (management || visit.InstructorId != scope.UserId || visit.Status != VisitStatus.Approved ||
                !await storage.HasPermissionAsync(scope.UserId, School, PermissionNames.StorageViewOwn, ct)) throw Denied();
            return false;
        }
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageViewArchive, ct);
        if (!await storage.HasPermissionAsync(scope.UserId, School, PermissionNames.VisitView, ct)) throw Denied();
        if (user.IsInRole(RoleNames.Moderator) && scope.ManagerUserId != scope.UserId && visit.CreatorId != scope.UserId) throw Denied();
        return true;
    }
    public async Task<StoragePage<VisitArchiveDto>> ListAsync(VisitArchiveQuery query, CancellationToken ct = default)
    {
        Enabled();
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.TeacherId?.Length > 450 ||
            query.From > query.To || query.Status != null && query.Status != "MissingFromDrive" &&
            (!Enum.TryParse<VisitArchiveStatus>(query.Status, out var status) || !Enum.IsDefined(status))) throw new ArgumentException("مرشحات الأرشيف غير صالحة.");
        await RequireManagementAsync(ct);
        drive.Reset();
        var page = await repository.ListAsync(School, query, ct);
        var batch = await repository.ReadBatchAsync(School, page.Items.Select(v => v.VisitId).ToArray(), ct);
        var items = new List<VisitArchiveDto>();
        foreach (var visit in page.Items) items.Add(await BuildAsync(visit, true, batch, ct));
        await RequireManagementAsync(ct);
        drive.Reset();
        if (batch.Artifacts.Count != 0) await drive.RootAsync(School, null, ct);
        return new(items, page.Total, page.Page, page.PageSize);
    }
    public async Task<VisitArchiveDto> GetAsync(int visitId, CancellationToken ct = default)
    {
        Enabled();
        var visit = await repository.VisitAsync(School, visitId, ct) ?? throw new KeyNotFoundException("الزيارة غير موجودة.");
        var manage = await AuthorizeAsync(visit, false, ct);
        drive.Reset();
        var result = await BuildAsync(visit, manage, await repository.ReadBatchAsync(School, [visitId], ct), ct);
        await AuthorizeAsync(await repository.VisitAsync(School, visitId, ct) ?? throw Denied(), false, ct);
        drive.Reset();
        return result;
    }
    private async Task<VisitArchiveDto> BuildAsync(ArchiveVisitScope visit, bool manage, ArchiveReadBatch batch, CancellationToken ct)
    {
        var revisions = new List<VisitArchiveRevisionDto>();
        foreach (var operation in batch.Operations.Where(o => o.VisitId == visit.VisitId && (manage || o.ApprovalRevision == visit.ApprovalRevision)))
        {
            var row = batch.Artifacts.SingleOrDefault(a => a.Artifact.OperationId == operation.Id);
            var missing = false;
            if (row != null && operation.Status == VisitArchiveStatus.Completed)
            {
                try { missing = !await drive.VerifyAsync(operation, row.Version.DriveItemId, row.Version.SHA256!, row.Version.SizeInBytes, ct); }
                catch (ArchiveIdentityException) { missing = true; }
                await repository.SetAvailabilityAsync(row, missing, time.GetUtcNow(), ct);
            }
            var versions = row == null ? [] : batch.Versions.Where(v => v.StoredFileId == row.Version.StoredFileId).OrderByDescending(v => v.VersionNumber)
                .Select(v => new VisitArchiveVersionDto(v.Id, v.VersionNumber, v.UploadedAtUtc,
                    v.Id == row.Version.Id && operation.Status == VisitArchiveStatus.Completed ? missing ? "MissingFromDrive" : "Available" : v.Availability == StoredFileAvailability.Missing ? "MissingFromDrive" : "Available", v.SizeInBytes)).ToList();
            var available = operation.Status == VisitArchiveStatus.Completed && row != null && !missing;
            revisions.Add(new(operation.ApprovalRevision, missing ? "MissingFromDrive" : operation.Status.ToString(),
                manage ? operation.ApprovalSource : "", operation.ApprovedAtUtc, manage ? operation.Attempts : 0,
                manage ? operation.LastAttemptAtUtc : null, operation.CompletedAtUtc, manage ? operation.NextAttemptAtUtc : null,
                manage ? operation.LastErrorCode : null, available && visit.Status == VisitStatus.Approved && visit.ApprovalRevision == operation.ApprovalRevision,
                manage && (missing || operation.Status is VisitArchiveStatus.NeedsAttention or VisitArchiveStatus.RetryScheduled),
                manage ? versions : versions.Take(1).ToList()));
        }
        return new(visit.VisitId, visit.InstructorName, visit.ApprovalRevision, visit.Status == VisitStatus.Approved, manage, revisions);
    }
    public async Task<VisitArchiveDto> RetryAsync(int visitId, RetryVisitArchiveRequest request, CancellationToken ct = default)
    {
        Enabled();
        var visit = await repository.VisitAsync(School, visitId, ct) ?? throw new KeyNotFoundException("الزيارة غير موجودة.");
        await AuthorizeAsync(visit, true, ct);
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageRetryArchive, ct);
        if (request.ApprovalRevision < 1 || request.Reason?.Length > 1000) throw new ArgumentException("طلب المحاولة غير صالح.");
        // Live reconcile first: SQL alone is never evidence that a successful file exists.
        await GetAsync(visitId, ct);
        await AuthorizeAsync(visit, true, ct);
        await authorization.RequireSchoolPermissionAsync(School, PermissionNames.StorageRetryArchive, ct);
        await repository.RetryAsync(School, visitId, request, user.UserId!, ct);
        return await GetAsync(visitId, ct);
    }
    public async Task<DriveFileContentDto> ContentAsync(int visitId, int revision, int? versionId, CancellationToken ct = default)
    {
        Enabled();
        drive.Reset();
        var visit = await repository.VisitAsync(School, visitId, ct) ?? throw new KeyNotFoundException("الزيارة غير موجودة.");
        var manage = await AuthorizeAsync(visit, false, ct);
        if (!manage && revision != visit.ApprovalRevision) throw Denied();
        var operation = (await repository.OperationsAsync(School, visitId, ct)).SingleOrDefault(o => o.ApprovalRevision == revision)
            ?? throw new KeyNotFoundException("هذا الاعتماد غير مؤرشف.");
        var row = (await repository.ArtifactsAsync(School, visitId, ct)).SingleOrDefault(a => a.Artifact.ApprovalRevision == revision)
            ?? throw new KeyNotFoundException("التقرير لم يتوفر بعد.");
        // Historical recreation versions are addressable by an internal ID only, and
        // remain subject to the original visit visibility gate.
        if (versionId.HasValue && versionId != row.Version.Id)
        {
            if (!manage) throw Denied();
            var version = await repository.VersionAsync(School, row.Version.StoredFileId, versionId.Value, ct) ?? throw new KeyNotFoundException("النسخة غير موجودة.");
            row = row with { Version = version };
        }
        operation.UploadIdentity = $"{operation.SchoolId}:{operation.VisitId}:{revision}:{row.Version.VersionNumber - 1}";
        operation.PdfSHA256 = row.Version.SHA256;
        if (!await drive.VerifyAsync(operation, row.Version.DriveItemId, row.Version.SHA256!, row.Version.SizeInBytes, ct))
        {
            await repository.SetAvailabilityAsync(row, true, time.GetUtcNow(), ct);
            throw new KeyNotFoundException("التقرير مفقود من التخزين.");
        }
        var content = await provider.ContentAsync(School, row.Version.DriveItemId, ct);
        try
        {
            var currentVisit = await repository.VisitAsync(School, visitId, ct) ?? throw Denied();
            await AuthorizeAsync(currentVisit, false, ct);
            if (!manage && currentVisit.ApprovalRevision != revision) throw Denied();
            drive.Reset();
            await drive.RootAsync(School, operation.ArchiveFolderItemId, ct);
            return content with { FileName = $"تقرير-زيارة-{visitId}-اعتماد-{revision}.pdf" };
        }
        catch { await content.Content.DisposeAsync(); throw; }
    }
    private static UnauthorizedSchoolAccessException Denied() => new("لا تملك صلاحية الوصول إلى أرشيف هذه الزيارة.");
}
