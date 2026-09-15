using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class BellScheduleRepository(AlFalahDbContext context) : IBellScheduleRepository
{
    public Task<BellScheduleTemplate?> FindAsync(int schoolId, int id, CancellationToken ct) =>
        context.Set<BellScheduleTemplate>().AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == id, ct);

    public Task<bool> NameExistsAsync(int schoolId, SaveBellScheduleRequest request, int? exceptId, CancellationToken ct) =>
        context.Set<BellScheduleTemplate>().AnyAsync(x => x.SchoolId == schoolId && x.AcademicYearId == request.AcademicYearId &&
            x.Semester == request.Semester && x.Name == request.Name.Trim() && (!exceptId.HasValue || x.Id != exceptId), ct);

    public async Task<IReadOnlyList<BellScheduleDto>> ListAsync(int schoolId, int yearId, TimetableSemester semester, CancellationToken ct)
    {
        var revisions = await RevisionQuery(schoolId).Where(x => x.Template.AcademicYearId == yearId && x.Template.Semester == semester
            && x.Template.IsActive && !x.Template.IsDeleted && x.Revision == x.Template.Revision).OrderBy(x => x.Name).ToListAsync(ct);
        var selections = await context.TimetableSetupProfiles.AsNoTracking().Where(x => x.SchoolId == schoolId &&
            x.AcademicYearId == yearId && x.Semester == semester && x.BellScheduleTemplateId != null)
            .Select(x => new { x.Id, x.BellScheduleTemplateId }).ToListAsync(ct);
        return revisions.Select(x => Map(x, selections.Where(p => p.BellScheduleTemplateId == x.BellScheduleTemplateId).Select(p => p.Id).ToArray())).ToArray();
    }

    public async Task<BellScheduleDto?> GetRevisionAsync(int schoolId, int revisionId, CancellationToken ct)
    {
        var revision = await RevisionQuery(schoolId).SingleOrDefaultAsync(x => x.Id == revisionId, ct);
        if (revision is null) return null;
        var selections = await context.TimetableSetupProfiles.AsNoTracking().Where(x => x.SchoolId == schoolId && x.BellScheduleTemplateId == revision.BellScheduleTemplateId)
            .Select(x => x.Id).ToArrayAsync(ct);
        return Map(revision, selections);
    }

    public async Task<BellScheduleDto?> GetSelectedAsync(int schoolId, int yearId, TimetableSemester semester, int? profileId, CancellationToken ct)
    {
        var profiles = context.TimetableSetupProfiles.AsNoTracking().Where(x => x.SchoolId == schoolId && x.AcademicYearId == yearId &&
            x.Semester == semester && x.BellScheduleTemplateId != null && (!profileId.HasValue || x.Id == profileId));
        var ids = await profiles.Select(x => x.BellScheduleTemplateId!.Value).Distinct().Take(2).ToArrayAsync(ct);
        if (ids.Length != 1) return null; // Multiple setups require an explicit profile, never guess.
        var revision = await RevisionQuery(schoolId).SingleOrDefaultAsync(x => x.BellScheduleTemplateId == ids[0] &&
            x.Revision == x.Template.Revision && x.Template.IsActive && !x.Template.IsDeleted, ct);
        return revision is null ? null : Map(revision, await profiles.Select(x => x.Id).ToArrayAsync(ct));
    }

    public async Task<BellScheduleDto?> GetPublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct)
    {
        var candidates = await context.SchoolTimetables.AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsPublished &&
            x.BellScheduleRevisionId != null).Select(x => new { x.BellScheduleRevisionId, x.AcademicYearId, x.Semester }).Distinct().ToListAsync(ct);
        var ids = candidates.Select(x => x.BellScheduleRevisionId!.Value).ToArray();
        var revisions = await RevisionQuery(schoolId).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var terms = await context.AcademicTerms.AsNoTracking().Where(x => x.SchoolId == schoolId && !x.IsDeleted && x.IsActive)
            .Select(x => new { x.AcademicYearId, x.Semester, x.StartsOn, x.EndsOn }).ToListAsync(ct);
        var matches = revisions.Where(r => {
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(r.SchoolTimeZoneId)).DateTime);
            return candidates.Any(c => c.BellScheduleRevisionId == r.Id && terms.Any(t => t.AcademicYearId == c.AcademicYearId &&
                t.Semester == c.Semester && t.StartsOn <= date && date <= t.EndsOn));
        }).ToArray();
        return matches.Length == 1 ? Map(matches[0], Array.Empty<int>()) : null;
    }

    public async Task<BellScheduleDependencies> GetDependenciesAsync(int schoolId, int? templateId, int? profileId, CancellationToken ct)
    {
        var profiles = await context.TimetableSetupProfiles.AsTracking().Where(x => x.SchoolId == schoolId &&
            ((templateId != null && x.BellScheduleTemplateId == templateId) || (profileId != null && x.Id == profileId))).ToListAsync(ct);
        var ids = profiles.Select(x => x.Id).ToArray();
        var timetables = await context.SchoolTimetables.AsTracking().Where(x => x.SchoolId == schoolId && !x.IsPublished &&
            x.TimetableSetupProfileId != null && ids.Contains(x.TimetableSetupProfileId.Value)).ToListAsync(ct);
        return new(profiles, timetables);
    }

    public async Task SaveRevisionAsync(BellScheduleTemplate template, BellScheduleRevision revision, bool isNew, CancellationToken ct)
    {
        await PersistAsync(async () => {
            if (isNew) context.Add(template);
            context.Add(revision);
            await context.SaveChangesAsync(ct); // Allocate IDs before recording the audit, within the same transaction.
            context.AuditLogs.Add(new AuditLog { SchoolId = template.SchoolId, UserId = template.UpdatedByUserId,
                Action = "Timetable.Timings.Revised", EntityName = nameof(BellScheduleTemplate), EntityId = template.Id.ToString(),
                NewValues = JsonSerializer.Serialize(new { template.Name, template.Revision }), CreatedAt = template.UpdatedAt });
            await context.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task SelectAsync(TimetableSetupProfile profile, BellScheduleTemplate template, CancellationToken ct)
    {
        await PersistAsync(async () => {
            context.AuditLogs.Add(new AuditLog { SchoolId = profile.SchoolId, UserId = profile.UpdatedByUserId,
                Action = "Timetable.Timings.Selected", EntityName = nameof(TimetableSetupProfile), EntityId = profile.Id.ToString(),
                NewValues = JsonSerializer.Serialize(new { template.Id, template.Revision }), CreatedAt = profile.UpdatedAt });
            await context.SaveChangesAsync(ct);
        }, ct);
    }

    private async Task PersistAsync(Func<Task> action, CancellationToken ct)
    {
        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(ct) : null;
        try { await action(); if (transaction is not null) await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new BellScheduleConflictException("Timing revision conflict", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BellScheduleConflictException("Timing name or revision conflict", ex); }
    }

    private IQueryable<BellScheduleRevision> RevisionQuery(int schoolId) => context.Set<BellScheduleRevision>().IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.SchoolId == schoolId).Include(x => x.Template).Include(x => x.Days).ThenInclude(x => x.Periods)
        .Include(x => x.Days).ThenInclude(x => x.Breaks).ThenInclude(x => x.Window).AsSplitQuery();

    private static BellScheduleDto Map(BellScheduleRevision revision, IReadOnlyList<int> selections) => new(
        revision.BellScheduleTemplateId, revision.Id, revision.SchoolId, revision.Template.AcademicYearId, revision.Template.Semester,
        revision.Name, revision.Revision, revision.SchoolTimeZoneId,
        Periods(revision.Days.Single(x => x.Day == 0)),
        revision.Days.Where(x => x.Day != 0).OrderBy(x => x.Day).Select(x => new BellScheduleDayDto(x.Day, x.IsStudyDay, x.UsesDefaultSchedule, Periods(x), x.UsesDefaultBreaks, Breaks(x))).ToArray(), selections,
        Breaks(revision.Days.Single(x => x.Day == 0)));
    private static IReadOnlyList<ScheduleBreakDto> Breaks(BellScheduleDay day) => day.Breaks.Where(x => !x.IsDeleted).OrderBy(x => x.Window.StartLocalTime)
        .Select(x => new ScheduleBreakDto(x.Name, x.Category, x.Window.StartLocalTime, x.Window.EndLocalTime)).ToArray();
    private static IReadOnlyList<BellPeriodDto> Periods(BellScheduleDay day) => day.Periods.OrderBy(x => x.Sequence)
        .Select(x => new BellPeriodDto(x.Sequence, x.DisplayLabel, x.StartLocalTime, x.EndLocalTime)).ToArray();
}
