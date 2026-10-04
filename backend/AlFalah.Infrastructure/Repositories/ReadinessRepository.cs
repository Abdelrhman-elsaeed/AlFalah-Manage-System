using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class ReadinessRepository(AlFalahDbContext db) : IReadinessRepository
{
    private IQueryable<EvidenceRequirement> Requirements(int school, ReadinessFilter f) => db.EvidenceRequirements.AsNoTracking().Where(r =>
        r.SchoolId == school && r.AcademicYearId == f.AcademicYearId && r.TemplateVersion == f.TemplateVersion && r.IsActive &&
        (f.DomainCode == null || r.DomainCode == f.DomainCode) && (f.StandardCode == null || r.StandardCode == f.StandardCode) &&
        (f.ResponsibleUserId == null || r.ResponsibleUserId == f.ResponsibleUserId) && (f.Importance == null || r.Importance == f.Importance) &&
        (!f.TrackerOnly || r.SourceKey != null) && (f.Search == null || r.Code.Contains(f.Search) || r.DisplayName.Contains(f.Search)));
    private IQueryable<RequirementFacts> Facts(int school, ReadinessFilter f, IReadOnlyList<int> denied, int[]? pageIds = null)
    {
        var requirements = Requirements(school, f);
        if (pageIds != null) requirements = requirements.Where(r => pageIds.Contains(r.Id));
        var ids = requirements.Select(r => r.Id);
        var raw = db.EvidenceLinks.IgnoreQueryFilters().AsNoTracking().Where(l => l.SchoolId == school && l.AcademicYearId == f.AcademicYearId && l.IsActive &&
            ids.Contains(l.RequirementId) && l.StoredFile.SourceKind != StoredFileSourceKind.VisitArchive && l.StoredFile.SourceKind != StoredFileSourceKind.HistoricalImport);
        var available = EvidenceLinkQueries.Eligible(db, school, f.AcademicYearId).Where(l => ids.Contains(l.RequirementId) && l.Version.Availability == StoredFileAvailability.Available && !denied.Contains(l.VersionId));
        var approved = EvidenceLinkQueries.Approved(db, school, f.AcademicYearId).Where(l => ids.Contains(l.RequirementId) && !denied.Contains(l.VersionId));
        var rawCounts = raw.GroupBy(l => l.RequirementId).Select(g => new { Id = g.Key, Count = (int?)g.Count() });
        var availableCounts = available.GroupBy(l => l.RequirementId).Select(g => new { Id = g.Key, Count = (int?)g.Count(), File = (int?)g.Min(l => l.StoredFileId),
            Pending = (int?)g.Count(l => l.Status == EvidenceLinkStatus.PendingReview || l.Status == EvidenceLinkStatus.Resubmitted || l.Status == EvidenceLinkStatus.Draft),
            Rejected = (int?)g.Count(l => l.Status == EvidenceLinkStatus.Rejected) });
        var approvedCounts = approved.GroupBy(l => l.RequirementId).Select(g => new { Id = g.Key, Count = (int?)g.Count() });
        return from r in requirements
            join rawCount in rawCounts on r.Id equals rawCount.Id into rawJoin from rawCount in rawJoin.DefaultIfEmpty()
            join availableCount in availableCounts on r.Id equals availableCount.Id into availableJoin from availableCount in availableJoin.DefaultIfEmpty()
            join approvedCount in approvedCounts on r.Id equals approvedCount.Id into approvedJoin from approvedCount in approvedJoin.DefaultIfEmpty()
            select new RequirementFacts { Id = r.Id, AcademicYearId = r.AcademicYearId!.Value, Code = r.Code, Name = r.DisplayName, DomainCode = r.DomainCode, StandardCode = r.StandardCode,
            OriginalTaskId = r.OriginalTaskId, ResponsibleUserId = r.ResponsibleUserId,
            ResponsibleName = db.Users.Where(u => u.Id == r.ResponsibleUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "",
            ResponsibleRole = r.ResponsibleRole, Importance = r.Importance, IsMandatory = r.IsMandatory, Policy = r.FulfillmentPolicy, MinimumApprovedLinks = r.MinimumApprovedLinks,
            SourceKey = r.SourceKey, SourceSHA256 = r.SourceSHA256, ReferencePath = r.ReferencePath, CompletionAction = r.CompletionAction, ImportanceReason = r.ImportanceReason,
            CandidateTaskCodesJson = r.CandidateTaskCodesJson, DueDate = r.DueDate, FollowUpStatus = r.FollowUpStatus, FollowUpNote = r.FollowUpNote, RowVersion = r.RowVersion,
            Links = rawCount.Count ?? 0, AvailableLinks = availableCount.Count ?? 0, ApprovedLinks = approvedCount.Count ?? 0,
            PendingLinks = availableCount.Pending ?? 0, RejectedLinks = availableCount.Rejected ?? 0,
            AvailableFileId = availableCount.File, SortOrder = r.SortOrder };
    }
    private static IQueryable<RequirementFacts> Display(IQueryable<RequirementFacts> q, ReadinessFilter f, bool gaps)
    {
        gaps |= f.GapsOnly;
        if (gaps) q = q.Where(r => r.IsMandatory);
        if (gaps || f.HideCompleted || f.Status == "Unfulfilled") q = q.Where(r => r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks));
        if (f.CriticalOnly) q = q.Where(r => r.Importance == EvidenceImportance.Critical);
        return f.Status switch {
            "Fulfilled" => q.Where(r => r.ApprovedLinks >= (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks)),
            "NoFile" => q.Where(r => r.Links == 0),
            "Unavailable" => q.Where(r => r.Links > r.AvailableLinks && r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks)),
            "AwaitingReview" => q.Where(r => r.PendingLinks > 0 && r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks)),
            "Rejected" => q.Where(r => r.RejectedLinks > 0 && r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks)),
            "InsufficientApprovedLinks" => q.Where(r => r.ApprovedLinks > 0 && r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks)),
            _ => q
        };
    }
    public Task<SelfEvaluationTemplate?> TemplateAsync(int version, CancellationToken ct) => db.Set<SelfEvaluationTemplate>().AsNoTracking().SingleOrDefaultAsync(x => x.Version == version, ct);
    public async Task<IReadOnlyList<EvaluationMemberDto>> MembersAsync(int school, CancellationToken ct) => await db.Users.AsNoTracking().Where(u => u.IsActive &&
        db.UserSchoolRoles.Any(m => m.UserId == u.Id && m.SchoolId == school && m.IsActive)).OrderBy(u => u.FirstName).ThenBy(u => u.Id).Take(1000)
        .Select(u => new EvaluationMemberDto(u.Id, u.FirstName + " " + u.LastName)).ToListAsync(ct);
    public async Task<IReadOnlyList<EvaluationVersionDto>> VersionsAsync(CancellationToken ct) => await db.Set<SelfEvaluationTemplate>().AsNoTracking().OrderBy(x => x.Version)
        .Select(x => new EvaluationVersionDto(x.Version, x.Name, x.SHA256)).ToListAsync(ct);
    public async Task<StoragePage<DigitalIndexFileDto>> IndexAsync(int school, ReadinessFilter f, IReadOnlyList<int> denied, CancellationToken ct)
    {
        var displayed = Display(Facts(school, f, denied), f, false).Select(x => x.Id);
        var eligible = EvidenceLinkQueries.Eligible(db, school, f.AcademicYearId).Where(l => displayed.Contains(l.RequirementId) &&
            l.Version.Availability == StoredFileAvailability.Available && !denied.Contains(l.VersionId));
        var approved = EvidenceLinkQueries.Approved(db, school, f.AcademicYearId).Where(l => !denied.Contains(l.VersionId));
        var counts = eligible.GroupBy(l => l.StoredFileId).Select(g => new { FileId = g.Key, Links = g.Count() });
        var approvals = approved.Where(l => displayed.Contains(l.RequirementId)).GroupBy(l => l.StoredFileId).Select(g => new { FileId = g.Key, Count = (int?)g.Count() });
        var q = from g in counts join file in db.StoredFiles on g.FileId equals file.Id
                join version in db.StoredFileVersions on file.CurrentVersionId equals version.Id
                join a in approvals on g.FileId equals a.FileId into approvedCounts from a in approvedCounts.DefaultIfEmpty()
                select new { FileId = file.Id, Name = file.DisplayName, version.MimeType, Size = version.SizeInBytes, g.Links, ApprovedLinks = a.Count ?? 0 };
        return new(await q.OrderBy(x => x.Name).ThenBy(x => x.FileId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .Select(x => new DigitalIndexFileDto(x.FileId, x.Name, x.MimeType, x.Size, x.Links, x.ApprovedLinks)).ToListAsync(ct), await q.CountAsync(ct), f.Page, f.PageSize);
    }
    public async Task AddTemplateAsync(SelfEvaluationTemplate template, CancellationToken ct) { db.Add(template); await db.SaveChangesAsync(ct); }
    public Task<bool> ScopeExistsAsync(int school, int year, int version, CancellationToken ct) => db.Set<SchoolEvaluationScope>().AnyAsync(x => x.SchoolId == school && x.AcademicYearId == year && x.TemplateVersion == version, ct);
    public async Task InitializeAsync(SchoolEvaluationScope scope, IReadOnlyList<EvidenceRequirement> requirements, string actor, CancellationToken ct)
    {
        db.Add(scope); db.AddRange(requirements);
        db.AuditLogs.Add(Audit(scope.SchoolId, actor, "Storage.EvaluationInitialized", scope.AcademicYearId.ToString(), null, JsonSerializer.Serialize(new { scope.AcademicYearId, scope.TemplateVersion, Requirements = requirements.Count })));
        await SaveAsync(ct);
    }
    public Task<EvaluationIdentity> IdentityAsync(int school, int year, CancellationToken ct) => db.Schools.AsNoTracking().Where(s => s.Id == school).Select(s => new EvaluationIdentity(s.Name,
        db.AcademicYears.Where(y => y.Id == year).Select(y => y.NameAr).Single(),
        db.SchoolReportSettings.Where(x => x.SchoolId == school).Select(x => x.ReportHeaderText).FirstOrDefault())).SingleAsync(ct);
    public async Task<StoragePage<RequirementFacts>> PageAsync(int school, ReadinessFilter f, IReadOnlyList<int> denied, bool gaps, CancellationToken ct)
    {
        if (!gaps && !f.GapsOnly && !f.HideCompleted && !f.CriticalOnly && f.Status == null)
        {
            var ids = await Requirements(school, f).OrderBy(r => r.SortOrder).ThenBy(r => r.Code).ThenBy(r => r.Id)
                .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).Select(r => r.Id).ToArrayAsync(ct);
            var window = await Facts(school, f, denied, ids).OrderBy(r => r.SortOrder).ThenBy(r => r.Code).ThenBy(r => r.Id).ToListAsync(ct);
            return new(window, await Requirements(school, f).CountAsync(ct), f.Page, f.PageSize);
        }
        var q = Display(Facts(school, f, denied), f, gaps);
        return new(await q.OrderBy(r => r.SortOrder).ThenBy(r => r.Code).ThenBy(r => r.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct),
            await q.CountAsync(ct), f.Page, f.PageSize);
    }
    public async Task<IReadOnlyList<ReadinessAggregate>> AggregatesAsync(int school, ReadinessFilter f, IReadOnlyList<int> denied, CancellationToken ct) =>
        await Facts(school, f, denied).GroupBy(r => new { r.DomainCode, r.StandardCode }).Select(g => new ReadinessAggregate(g.Key.DomainCode, g.Key.StandardCode,
            g.Count(), g.Sum(r => r.IsMandatory ? 1 : 0), g.Sum(r => r.IsMandatory && r.ApprovedLinks >= (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks) ? 1 : 0),
            g.Sum(r => r.Links), g.Sum(r => r.ApprovedLinks),
            g.Sum(r => r.IsMandatory && r.Importance == EvidenceImportance.Critical && r.ApprovedLinks < (r.Policy == EvidenceFulfillmentPolicy.AnyApprovedLink ? 1 : r.MinimumApprovedLinks) ? 1 : 0))).ToListAsync(ct);
    public async Task<IReadOnlyList<ReadinessFileCount>> FileCountsAsync(int school, ReadinessFilter f, IReadOnlyList<int> denied, CancellationToken ct)
    {
        var q = from r in Requirements(school, f)
                join l in EvidenceLinkQueries.Eligible(db, school, f.AcademicYearId) on r.Id equals l.RequirementId
                where l.Version.Availability == StoredFileAvailability.Available && !denied.Contains(l.VersionId)
                select new { r.DomainCode, r.StandardCode, l.StoredFileId };
        // Three bounded SQL aggregates. A file linked to two standards counts once overall.
        var overall = await q.Select(x => x.StoredFileId).Distinct().CountAsync(ct);
        var domains = await q.GroupBy(x => x.DomainCode).Select(g => new ReadinessFileCount(g.Key, null, g.Select(x => x.StoredFileId).Distinct().Count())).ToListAsync(ct);
        var standards = await q.GroupBy(x => new { x.DomainCode, x.StandardCode }).Select(g => new ReadinessFileCount(g.Key.DomainCode, g.Key.StandardCode, g.Select(x => x.StoredFileId).Distinct().Count())).ToListAsync(ct);
        return new[] { new ReadinessFileCount(null, null, overall) }.Concat(domains).Concat(standards.Where(x => x.StandardCode != null)).ToArray();
    }
    public async Task<IReadOnlyList<ReadinessLiveFile>> LiveFilesAsync(int school, ReadinessFilter f, int afterFileId, int limit, CancellationToken ct) =>
        await (from l in EvidenceLinkQueries.Eligible(db, school, f.AcademicYearId)
               join r in Requirements(school, f) on l.RequirementId equals r.Id
               join s in db.SchoolGoogleDrives on l.SchoolId equals s.SchoolId
               where l.StoredFileId > afterFileId
               select new { FileId = l.StoredFileId, l.VersionId, l.Version.DriveItemId, l.Version.DriveId, SchoolRoot = s.RootFolderId,
                   AuthorizedRoot = l.StoredFile.OwnerTeacherId == null ? s.RootFolderId : db.TeacherDriveFolders.Where(g => g.SchoolId == school && g.TeacherId == l.StoredFile.OwnerTeacherId && g.IsActive).Select(g => g.RootItemId).Single(),
                   AuthorizedDriveId = l.StoredFile.OwnerTeacherId == null ? (s.SharedDriveId ?? "") : db.TeacherDriveFolders.Where(g => g.SchoolId == school && g.TeacherId == l.StoredFile.OwnerTeacherId && g.IsActive).Select(g => g.DriveId).Single() })
            .Distinct().OrderBy(x => x.FileId).Take(limit).Select(x => new ReadinessLiveFile(x.FileId, x.VersionId, x.DriveItemId, x.DriveId, x.SchoolRoot, x.AuthorizedRoot, x.AuthorizedDriveId)).ToListAsync(ct);
    public async Task ObserveAsync(int school, IReadOnlyList<AvailabilityObservation> observations, string actor, CancellationToken ct)
    {
        var ids = observations.Select(x => x.VersionId).ToArray(); var map = observations.ToDictionary(x => x.VersionId);
        var versions = await db.StoredFileVersions.AsTracking().Where(x => x.SchoolId == school && ids.Contains(x.Id)).ToListAsync(ct);
        foreach (var version in versions)
        {
            var availability = map[version.Id].Missing ? StoredFileAvailability.Missing : StoredFileAvailability.Available;
            if (version.Availability == availability) continue;
            version.Availability = availability; version.MissingFromDriveAtUtc = map[version.Id].Missing ? DateTimeOffset.UtcNow : null;
            db.AuditLogs.Add(Audit(school, actor, "Storage.VersionAvailability", version.Id.ToString(), null, availability.ToString()));
        }
        await SaveAsync(ct);
    }
    public Task<EvidenceRequirement?> RequirementAsync(int school, int id, CancellationToken ct) => db.EvidenceRequirements.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == id, ct);
    public async Task SaveFollowUpAsync(EvidenceRequirement r, byte[] expected, RequirementFollowUpRevision revision, CancellationToken ct)
    {
        db.Entry(r).Property(x => x.RowVersion).OriginalValue = expected; db.Add(revision);
        db.AuditLogs.Add(Audit(r.SchoolId!.Value, revision.ActorUserId, "Storage.FollowUpConfigured", r.Id.ToString(), revision.OldValuesJson, revision.NewValuesJson));
        await SaveAsync(ct);
    }
    public async Task<IReadOnlyList<FollowUpHistoryDto>> FollowUpHistoryAsync(int school, int id, CancellationToken ct) =>
        await db.Set<RequirementFollowUpRevision>().AsNoTracking().Where(x => x.SchoolId == school && x.RequirementId == id).OrderByDescending(x => x.Id).Take(100)
            .Select(x => new FollowUpHistoryDto(db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "", x.Reason, x.OldValuesJson, x.NewValuesJson, x.CreatedAtUtc)).ToListAsync(ct);
    public Task<ManualEvaluation?> ManualAsync(int school, int year, int version, string scopeCode, CancellationToken ct) => db.Set<ManualEvaluation>().AsTracking()
        .SingleOrDefaultAsync(x => x.SchoolId == school && x.AcademicYearId == year && x.TemplateVersion == version && x.ScopeCode == scopeCode, ct);
    public async Task SaveManualAsync(ManualEvaluation e, byte[]? expected, string actor, CancellationToken ct)
    {
        if (e.Id == 0) db.Add(e); else db.Entry(e).Property(x => x.RowVersion).OriginalValue = expected!;
        await SaveAsync(ct);
        var snapshot = JsonSerializer.Serialize(ManualDto(e));
        db.Add(new ManualEvaluationRevision { SchoolId = e.SchoolId, ManualEvaluationId = e.Id, Revision = e.Revision, SnapshotJson = snapshot });
        db.AuditLogs.Add(Audit(e.SchoolId, actor, "Storage.ManualEvaluationSaved", e.Id.ToString(), null, snapshot));
        await SaveAsync(ct);
    }
    public async Task<IReadOnlyList<ManualEvaluationDto>> ManualsAsync(int school, int year, int version, CancellationToken ct) =>
        (await db.Set<ManualEvaluation>().AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == year && x.TemplateVersion == version).OrderBy(x => x.ScopeCode).ToListAsync(ct)).Select(ManualDto).ToArray();
    public async Task<IReadOnlyList<ManualEvaluationHistoryDto>> ManualHistoryAsync(int school, int id, CancellationToken ct) =>
        await db.Set<ManualEvaluationRevision>().AsNoTracking().Where(x => x.SchoolId == school && x.ManualEvaluationId == id).OrderByDescending(x => x.Revision).Take(100)
            .Select(x => new ManualEvaluationHistoryDto(x.Revision, x.SnapshotJson, x.CreatedAtUtc)).ToListAsync(ct);
    private static ManualEvaluationDto ManualDto(ManualEvaluation e) => new(e.Id, e.ScopeCode, e.Judgment, e.Value, e.Reason, e.EvaluatorName, e.EvaluatedAtUtc, e.Revision, Convert.ToBase64String(e.RowVersion));
    private static AuditLog Audit(int school, string actor, string action, string id, string? old, string? values) =>
        new() { SchoolId = school, UserId = actor, Action = action, EntityName = "SchoolSelfEvaluation", EntityId = id, OldValues = old, NewValues = values };
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new StorageConflictException(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException s && s.Number is 2601 or 2627 or 1205) { db.ChangeTracker.Clear(); throw new StorageConflictException(); }
    }
}
