using System.Text.Json;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class EvidenceRepository(AlFalahDbContext db) : IEvidenceRepository
{
    private IQueryable<EvidenceLink> Links(int school) => db.EvidenceLinks.Where(x => x.SchoolId == school);
    private IQueryable<EvidenceLink> Eligible(int school, int year, int? owner) => Links(school).AsNoTracking().Where(x =>
        x.AcademicYearId == year && x.IsActive && x.Requirement.IsActive && (owner == null || x.TeacherId == owner) &&
        !x.StoredFile.IsDeleted && x.StoredFile.SourceKind != StoredFileSourceKind.VisitArchive &&
        x.StoredFile.SourceKind != StoredFileSourceKind.HistoricalImport &&
        x.VersionId == x.StoredFile.CurrentVersionId &&
        db.SchoolGoogleDrives.Any(s => s.SchoolId == school && s.IsEnabled && (s.SharedDriveId ?? "") == x.Version.DriveId) &&
        (x.StoredFile.OwnerTeacherId == null || db.TeacherDriveFolders.Any(g => g.SchoolId == school && g.TeacherId == x.StoredFile.OwnerTeacherId && g.IsActive) &&
         db.InstructorProfiles.Any(t => t.Id == x.StoredFile.OwnerTeacherId && t.SchoolId == school && t.IsActive && t.User.IsActive &&
             db.UserSchoolRoles.Any(m => m.UserId == t.UserId && m.SchoolId == school && m.IsActive &&
                 db.RolePermissions.Any(p => p.RoleId == m.RoleId && p.Permission.Name == PermissionNames.StorageViewOwn)))) &&
        (x.TeacherId == null || db.InstructorProfiles.Any(t => t.Id == x.TeacherId && t.SchoolId == school && t.IsActive && t.User.IsActive)));
    private IQueryable<EvidenceLink> Approved(int school, int year, int? owner) => Eligible(school, year, owner).Where(x =>
        x.Status == EvidenceLinkStatus.Approved && x.Version.Availability == StoredFileAvailability.Available &&
        db.EvidenceReviewDecisions.Any(d => d.EvidenceLinkId == x.Id && d.VersionId == x.VersionId && d.Decision == EvidenceReviewStatus.Approved));
    public Task<bool> YearExistsAsync(int year, CancellationToken ct) => db.AcademicYears.AnyAsync(x => x.Id == year, ct);
    public async Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct) => await db.AcademicYears.AsNoTracking().OrderByDescending(x => x.StartsOn)
        .Select(x => new AcademicYearDto(x.Id, x.Code, x.NameAr, x.IsActive)).ToListAsync(ct);
    public async Task<IReadOnlyList<EvidenceTeacherDto>> TeachersAsync(int school, CancellationToken ct) => await db.InstructorProfiles.AsNoTracking()
        .Where(x => x.SchoolId == school && x.IsActive && x.User.IsActive).OrderBy(x => x.User.FirstName).ThenBy(x => x.Id)
        .Select(x => new EvidenceTeacherDto(x.Id, x.User.FirstName + " " + x.User.LastName)).ToListAsync(ct);
    public async Task<string> ActorNameAsync(string actor, CancellationToken ct) => await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == actor)
        .Select(x => x.FirstName + " " + x.LastName).SingleAsync(ct);
    public async Task<IReadOnlyList<EvidenceTask>> TasksAsync(CancellationToken ct) => await db.EvidenceTasks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync(ct);
    public async Task<IReadOnlyList<EvidenceRequirement>> RequirementsAsync(int school, int year, CancellationToken ct) =>
        await db.EvidenceRequirements.AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == year).ToListAsync(ct);
    public Task<EvidenceRequirement?> RequirementAsync(int school, int id, CancellationToken ct) =>
        db.EvidenceRequirements.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public Task<bool> ActiveTeacherAsync(int school, int id, CancellationToken ct) =>
        db.InstructorProfiles.AnyAsync(x => x.Id == id && x.SchoolId == school && x.IsActive && x.User.IsActive, ct);
    public Task<int?> OwnTeacherIdAsync(int school, string user, CancellationToken ct) =>
        db.InstructorProfiles.Where(x => x.UserId == user && x.SchoolId == school && x.IsActive && x.User.IsActive &&
            db.TeacherDriveFolders.Any(g => g.TeacherId == x.Id && g.SchoolId == school && g.IsActive)).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<int>> ApprovedFileIdsAsync(int school, int year, int? owner, CancellationToken ct, bool ownOnly = false) =>
        await Eligible(school, year, owner).Where(x => x.Status == EvidenceLinkStatus.Approved && (!ownOnly || x.StoredFile.OwnerTeacherId == owner)).Select(x => x.StoredFileId).Distinct().ToListAsync(ct);
    public async Task AddRequirementsAsync(IEnumerable<EvidenceRequirement> rows, CancellationToken ct)
    {
        db.EvidenceRequirements.AddRange(rows);
        await SaveAsync("", 0, "", "", null, null, ct);
    }
    public Task<StoredFile?> FileAsync(int school, int id, CancellationToken ct) =>
        db.StoredFiles.IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public Task<bool> HasApprovalHistoryAsync(int school, int file, CancellationToken ct) => db.StoredFiles.IgnoreQueryFilters().AnyAsync(f => f.SchoolId == school && f.Id == file &&
        (db.EvidenceReviewDecisions.Any(d => d.StoredFileId == file && d.SchoolId == school && d.Decision == EvidenceReviewStatus.Approved) ||
         db.EvidenceLinks.Any(l => l.StoredFileId == file && l.SchoolId == school && l.Status == EvidenceLinkStatus.Approved) ||
         db.TeacherEvidenceSubmissions.Any(s => s.Id == f.LegacySubmissionId && s.ReviewStatus == EvidenceReviewStatus.Approved)), ct);
    public Task<StoredFileVersion?> VersionAsync(int school, int file, int version, CancellationToken ct) =>
        db.StoredFileVersions.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.StoredFileId == file && x.Id == version, ct);
    public Task<EvidenceLink?> LinkAsync(int school, int id, CancellationToken ct) => Links(school).AsTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<EvidenceLink>> FileLinksAsync(int school, int file, CancellationToken ct) =>
        await Links(school).AsTracking().Where(x => x.StoredFileId == file).OrderBy(x => x.Id).ToListAsync(ct);
    public async Task<StoragePage<int>> LinkIdsAsync(int school, EvidenceQueueRequest r, CancellationToken ct)
    {
        var q = Links(school).AsNoTracking().Where(x => x.IsActive && x.StoredFile.SourceKind != StoredFileSourceKind.VisitArchive && x.StoredFile.SourceKind != StoredFileSourceKind.HistoricalImport && x.AcademicYearId == r.AcademicYearId &&
            (r.RequirementId == null || x.RequirementId == r.RequirementId) && (r.TeacherId == null || x.TeacherId == r.TeacherId) &&
            (r.StandardCode == null || x.Requirement.StandardCode == r.StandardCode) &&
            (r.Status == null ? x.Status == EvidenceLinkStatus.PendingReview || x.Status == EvidenceLinkStatus.Resubmitted : x.Status == r.Status));
        var total = await q.CountAsync(ct);
        var ids = await q.OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).Select(x => x.Id).ToListAsync(ct);
        return new(ids, total, r.Page, r.PageSize);
    }
    public async Task<EvidenceLinkDto> LinkDtoAsync(int school, int id, CancellationToken ct)
        => (await LinkDtosAsync(school, [id], ct)).Single();
    public async Task<IReadOnlyList<EvidenceLinkDto>> LinkDtosAsync(int school, IReadOnlyList<int> ids, CancellationToken ct)
    {
        var rows = await Links(school).IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new {
            x.Id, x.StoredFileId, x.RequirementId, x.AcademicYearId, x.TeacherId, x.VersionId, x.Status, x.Version.Availability,
            x.RowVersion, FileName = x.StoredFile.DisplayName, RequirementName = x.Requirement.DisplayName,
            TeacherName = db.InstructorProfiles.Where(t => t.Id == x.TeacherId).Select(t => t.User.FirstName + " " + t.User.LastName).FirstOrDefault() ?? "" }).ToListAsync(ct);
        var decisions = await db.EvidenceReviewDecisions.AsNoTracking().Where(x => x.SchoolId == school && ids.Contains(x.EvidenceLinkId))
            .OrderBy(x => x.Id).Select(x => new { x.EvidenceLinkId, x.ReviewerName,
                CurrentName = db.Users.IgnoreQueryFilters().Where(u => u.Id == x.ReviewedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                Dto = new EvidenceDecisionDto(x.Id, x.VersionId, x.Decision.ToString(), x.ReviewedByUserId, x.Note, x.ReviewedAtUtc, "") }).ToListAsync(ct);
        var lookup = decisions.ToLookup(x => x.EvidenceLinkId, x => x.Dto with { ReviewerName = x.ReviewerName != "" ? x.ReviewerName : x.CurrentName ?? "" });
        var map = rows.ToDictionary(row => row.Id, row => new EvidenceLinkDto(row.Id, row.StoredFileId, row.RequirementId, row.AcademicYearId, row.TeacherId, row.VersionId, row.FileName,
            row.RequirementName, row.TeacherName, row.Status.ToString(), row.Availability.ToString(), Convert.ToBase64String(row.RowVersion), lookup[row.Id].ToArray()));
        return ids.Where(map.ContainsKey).Select(id => map[id]).ToArray();
    }
    public void AddLink(EvidenceLink link) => db.EvidenceLinks.Add(link);
    public void AddDecision(EvidenceReviewDecision decision) => db.EvidenceReviewDecisions.Add(decision);
    public void AddChange(FileChangeRequest request) => db.Set<FileChangeRequest>().Add(request);
    public void AddChangeDecision(FileChangeDecision decision) => db.Set<FileChangeDecision>().Add(decision);
    public Task<FileChangeRequest?> ChangeAsync(int school, int id, CancellationToken ct) =>
        db.Set<FileChangeRequest>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public async Task<IReadOnlyList<FileChangeDto>> ChangesAsync(int school, int file, CancellationToken ct)
    {
        var rows = await db.Set<FileChangeRequest>().AsNoTracking().Where(x => x.SchoolId == school && x.StoredFileId == file).OrderByDescending(x => x.Id).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var decisions = await db.Set<FileChangeDecision>().AsNoTracking().Where(x => x.SchoolId == school && ids.Contains(x.FileChangeRequestId)).OrderBy(x => x.Id)
            .Select(x => new { x.FileChangeRequestId, x.ReviewerName,
                CurrentName = db.Users.IgnoreQueryFilters().Where(u => u.Id == x.ReviewedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                Dto = new FileChangeDecisionDto(x.Decision,x.ReviewedByUserId,x.Note,x.CreatedAtUtc, "") }).ToListAsync(ct);
        return rows.Select(x => new FileChangeDto(x.Id, x.StoredFileId, x.OriginalVersionId, x.CandidateVersionId, x.Kind, x.Status, x.Reason, x.RequestedByUserId,
            Convert.ToBase64String(x.RowVersion), decisions.Where(d => d.FileChangeRequestId == x.Id).Select(d => d.Dto with { ReviewerName = d.ReviewerName != "" ? d.ReviewerName : d.CurrentName ?? "" }).ToArray(), x.ReplaceBeforeReview)).ToArray();
    }
    public async Task<StoragePage<ChangeQueueItemDto>> ChangeQueueAsync(int school, int year, int page, string? status, CancellationToken ct)
    {
        var q = from c in db.Set<FileChangeRequest>().AsNoTracking()
                join f in db.StoredFiles.IgnoreQueryFilters().AsNoTracking() on c.StoredFileId equals f.Id
                where c.SchoolId == school && f.SchoolId == school && (status == null || c.Status == status) &&
                    f.SourceKind != StoredFileSourceKind.VisitArchive && f.SourceKind != StoredFileSourceKind.HistoricalImport &&
                    (!db.EvidenceLinks.Any(l => l.StoredFileId == f.Id) || db.EvidenceLinks.Any(l => l.StoredFileId == f.Id && l.AcademicYearId == year))
                select new ChangeQueueItemDto(c.Id, f.Id, f.DisplayName, c.Kind, c.Status, c.Reason);
        var total = await q.CountAsync(ct);
        return new(await q.OrderBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct), total, page, 25);
    }
    public async Task<StorageFileDetailsDto?> HistoryAsync(int school, int file, CancellationToken ct)
    {
        var f = await db.StoredFiles.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == file, ct);
        if (f == null) return null;
        var versions = await db.StoredFileVersions.AsNoTracking().Where(x => x.SchoolId == school && x.StoredFileId == file).OrderByDescending(x => x.VersionNumber)
            .Select(x => new StorageVersionDto(x.Id, x.VersionNumber, x.SizeInBytes, x.MimeType, x.UploadedAtUtc, x.Availability.ToString())).ToListAsync(ct);
        var current = versions.Single(x => x.VersionId == f.CurrentVersionId);
        return new(new(file, f.FolderId, f.DisplayName, current.Size, current.MimeType, current.UploadedAt, f.IsDeleted ? "Withdrawn" : "Managed", true, Convert.ToBase64String(f.RowVersion)), versions);
    }
    public async Task SaveAsync(string actor, int school, string action, string entityId, object? expected, byte[]? version, CancellationToken ct)
    {
        if (expected != null && version != null) db.Entry(expected).Property("RowVersion").OriginalValue = version;
        foreach (var entry in db.ChangeTracker.Entries<StoredFile>().Where(x => x.State == EntityState.Modified && x.Entity.LegacyProvenanceJson != null && x.Entity.SharedWriterFingerprint == null))
        {
            entry.Entity.SharedWriterProvenanceJson = entry.Entity.LegacyProvenanceJson;
            entry.Entity.SharedWriterFingerprint = entry.Entity.LegacyFingerprint;
        }
        if (action != "") db.AuditLogs.Add(new() { SchoolId = school, UserId = actor, Action = action, EntityName = "StorageEvidence", EntityId = entityId,
            OldValues = JsonSerializer.Serialize(db.ChangeTracker.Entries<EvidenceLink>().Where(x => x.State == EntityState.Modified).Select(x => new {
                x.Entity.Id, VersionId = x.OriginalValues.GetValue<int>(nameof(EvidenceLink.VersionId)), Status = x.OriginalValues.GetValue<EvidenceLinkStatus>(nameof(EvidenceLink.Status)) })),
            NewValues = JsonSerializer.Serialize(db.ChangeTracker.Entries<EvidenceLink>().Where(x => x.State is EntityState.Modified or EntityState.Added).Select(x => new { x.Entity.Id, x.Entity.VersionId, x.Entity.Status })) });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new StorageConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException s && s.Number is 2601 or 2627 or 1205)
        { db.ChangeTracker.Clear(); throw new StorageConflictException(); }
    }
    public async Task<EvidenceCountsDto> CountsAsync(int school, int year, int? owner, CancellationToken ct)
    {
        var links = Eligible(school, year, owner).Where(x => owner == null || x.StoredFile.OwnerTeacherId == owner);
        var approved = Approved(school, year, owner).Where(x => owner == null || x.StoredFile.OwnerTeacherId == owner);
        var grouped = approved.GroupBy(x => x.RequirementId).Select(g => new { Id = g.Key, Count = g.Count() });
        var requirements = db.EvidenceRequirements.Where(x => x.SchoolId == school && x.AcademicYearId == year && x.IsActive);
        var fulfilled = await (from r in requirements join g in grouped on r.Id equals g.Id
            where g.Count >= (r.FulfillmentPolicy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks) select r.Id).CountAsync(ct);
        var files = db.StoredFiles.Where(x => x.SchoolId == school && (owner == null || x.OwnerTeacherId == owner) &&
            x.SourceKind != StoredFileSourceKind.VisitArchive && x.SourceKind != StoredFileSourceKind.HistoricalImport &&
            db.StoredFileVersions.Any(v => v.Id == x.CurrentVersionId && v.Availability == StoredFileAvailability.Available &&
                db.SchoolGoogleDrives.Any(s => s.SchoolId == school && s.IsEnabled && (s.SharedDriveId ?? "") == v.DriveId)) &&
            (x.OwnerTeacherId == null || db.TeacherDriveFolders.Any(g => g.TeacherId == x.OwnerTeacherId && g.SchoolId == school && g.IsActive) &&
                db.InstructorProfiles.Any(t => t.Id == x.OwnerTeacherId && t.IsActive && t.User.IsActive &&
                    db.UserSchoolRoles.Any(m => m.UserId == t.UserId && m.SchoolId == school && m.IsActive &&
                        db.RolePermissions.Any(p => p.RoleId == m.RoleId && p.Permission.Name == PermissionNames.StorageViewOwn)))));
        return new(await files.CountAsync(ct), await links.CountAsync(ct), await approved.CountAsync(ct), fulfilled, await requirements.CountAsync(ct));
    }
    public async Task<EvidenceMatrixDto> MatrixAsync(int school, EvidenceMatrixFilterDto filter, CancellationToken ct)
    {
        var year = await db.AcademicYears.AsNoTracking().Where(x => filter.AcademicYearId == null ? x.IsActive : x.Id == filter.AcademicYearId)
            .Select(x => new AcademicYearDto(x.Id, x.Code, x.NameAr, x.IsActive)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        var tasks = await db.EvidenceTasks.AsNoTracking().Where(x => x.IsActive && (filter.Category == null || x.Category == filter.Category))
            .OrderBy(x => x.CategorySortOrder).ThenBy(x => x.SortOrder).Select(x => new EvidenceTaskDto(x.Id, x.Code, x.NameAr, x.Category, x.CategorySortOrder, x.SortOrder)).ToListAsync(ct);
        var teachers = await db.InstructorProfiles.AsNoTracking().Where(x => x.SchoolId == school && x.IsActive && x.User.IsActive && (filter.TeacherId == null || x.Id == filter.TeacherId))
            .OrderBy(x => x.User.FirstName).ThenBy(x => x.Id).Select(x => new { x.Id, Name = x.User.FirstName + " " + x.User.LastName, School = x.School.Name }).ToListAsync(ct);
        var links = await Eligible(school, year.Id, filter.TeacherId).Where(x => x.TeacherId != null && x.Requirement.OriginalTaskId != null)
            .Select(x => new { Teacher = x.TeacherId!.Value, Task = x.Requirement.OriginalTaskId!.Value, x.Status, x.Version.Availability,
                Approved = Approved(school, year.Id, filter.TeacherId).Any(a => a.Id == x.Id), x.StoredFileId,
                x.Requirement.FulfillmentPolicy, x.Requirement.MinimumApprovedLinks }).ToListAsync(ct);
        var lookup = links.ToLookup(x => (x.Teacher, x.Task));
        var rows = teachers.Select(t => {
            var cells = tasks.Select(task => {
                var group = lookup[(t.Id, task.Id)].ToArray();
                var approved = group.Length > 0 && group.Count(x => x.Approved) >=
                    (group[0].FulfillmentPolicy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : group[0].MinimumApprovedLinks);
                var status = approved ? EvidenceCellStatus.Approved : group.Length == 0 ? EvidenceCellStatus.NotUploaded :
                    group.All(x => x.Availability is StoredFileAvailability.Missing or StoredFileAvailability.Deleted) ? EvidenceCellStatus.MissingFromDrive :
                    group.Any(x => x.Status is EvidenceLinkStatus.PendingReview or EvidenceLinkStatus.Resubmitted) ? EvidenceCellStatus.PendingReview :
                    group.Any(x => x.Status == EvidenceLinkStatus.Rejected) ? EvidenceCellStatus.Rejected : EvidenceCellStatus.Uploaded;
                return new EvidenceMatrixCellDto(task.Id, status, approved, group.Select(x => x.StoredFileId).Distinct().Count());
            }).ToList();
            return new EvidenceMatrixTeacherRowDto(t.Id, t.Name.Trim(), school, t.School, cells.Count(x => x.IsChecked), cells);
        }).Where(x => filter.CompletionStatus == null || x.Cells.Any(c => c.Status == filter.CompletionStatus)).ToList();
        return new(year, tasks, rows, tasks.Count);
    }
    public async Task<EvidenceCellFilesDto> CellAsync(int school, int teacher, int task, int year, CancellationToken ct)
    {
        var links = await Eligible(school, year, teacher).Where(x => x.Requirement.OriginalTaskId == task)
            .Select(x => new { x.Id, x.StoredFileId, x.VersionId, x.RowVersion, x.StoredFile.DisplayName, x.Version.FileExtension, x.Version.SizeInBytes, x.Status, x.Version.Availability, x.Version.UploadedAtUtc,
                Note = db.EvidenceReviewDecisions.Where(d => d.EvidenceLinkId == x.Id).OrderByDescending(d => d.Id).Select(d => d.Note).FirstOrDefault() }).ToListAsync(ct);
        var files = links.Select(x => new EvidenceSubmissionFileDto(x.Id, x.DisplayName, x.FileExtension, x.SizeInBytes, null,
            x.Status == EvidenceLinkStatus.Approved ? EvidenceReviewStatus.Approved : x.Status == EvidenceLinkStatus.Rejected ? EvidenceReviewStatus.Rejected : EvidenceReviewStatus.PendingReview,
            false, x.Availability != StoredFileAvailability.Available, x.UploadedAtUtc, x.Note, x.Id, x.StoredFileId, x.VersionId, Convert.ToBase64String(x.RowVersion))).ToArray();
        var matrix = await MatrixAsync(school, new EvidenceMatrixFilterDto { AcademicYearId = year, TeacherId = teacher }, ct);
        return new(teacher, task, year, matrix.Rows.SingleOrDefault()?.Cells.SingleOrDefault(x => x.TaskId == task)?.Status ?? EvidenceCellStatus.NotUploaded, files);
    }
    public Task<int?> LegacyLinkAsync(int school, long submission, CancellationToken ct) => Links(school).Where(x =>
        x.StoredFile.LegacySubmissionId == submission && x.Requirement.OriginalTaskId == db.TeacherEvidenceSubmissions.Where(s => s.Id == submission).Select(s => s.TaskId).FirstOrDefault() && x.IsActive)
        .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
}
