using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class WeeklyStandbyService(IWeeklyStandbyRepository repository, WeeklyStandbyEngine engine,
    TimetableReviewService review, ICurrentUserService user, TimeProvider clock)
{
    private async Task<TimetableValidationContext> Context(int id, CancellationToken ct)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.UserId) || user.ActiveSchoolId is not int school ||
            user.IsInRole(RoleNames.Instructor) || user.IsInRole(RoleNames.Guardian)) throw new UnauthorizedAccessException();
        var allowed = user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager) || user.HasPermission(PermissionNames.TimetableManage) ||
            user.IsInRole(RoleNames.Moderator) && await repository.HasEditorGrantAsync(school, user.UserId, ct);
        if (!allowed) throw new UnauthorizedAccessException();
        return await repository.GetValidationContextAsync(school, id, ct) ?? throw new KeyNotFoundException("الجدول غير موجود في المدرسة النشطة.");
    }
    private async Task<IReadOnlyList<string>> Protected(TimetableValidationContext c, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(c.Schedule?.SchoolTimeZoneId ?? "Africa/Cairo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var occupied = (await repository.GetProtectedCellsAsync(c.Timetable.SchoolId, c.Timetable.Id, today, ct)).ToHashSet();
        // Legacy cover from a free period has no preexisting reserve to protect or manufacture.
        return c.Timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Standby)
            .Select(x => WeeklyStandbyEngine.Key(x.InstructorProfileId, (int)x.Day, x.Period)).Where(occupied.Contains).ToArray();
    }
    public Task<WeeklyStandbyWorkbenchDto> GetAsync(int id, CancellationToken ct) => repository.ExecuteAsync(async token => {
        var c = await Context(id, token);
        WeeklyStandbySettings settings;
        try { settings = JsonSerializer.Deserialize<WeeklyStandbySettings>(c.Timetable.StandbySettingsJson) ?? new(); }
        catch (JsonException) { settings = new(); }
        // Saved configuration may outlive a removed teacher or shortened school day.
        // Recover the editor without weakening validation of new requests.
        var ids = c.Teachers.Where(t => t.Instructor.SchoolId == c.Timetable.SchoolId && t.Instructor.IsActive && !t.Instructor.IsDeleted &&
            t.Instructor.User.IsActive && !t.Instructor.User.IsDeleted).Select(t => t.InstructorProfileId).ToHashSet();
        var periods = (c.Schedule?.Days ?? []).Where(d => d.Day > 0 && d.IsStudyDay)
            .SelectMany(d => TimetableValidationEngine.Periods(c.Schedule, d.Day).Select(p => (d.Day, p.Sequence))).ToHashSet();
        var cleaned = new WeeklyStandbySettings(Math.Clamp(settings.RequiredPerPeriod, 0, Math.Max(1, ids.Count)),
            (settings.Overrides ?? []).Where(p => periods.Contains((p.Day, p.Period))).DistinctBy(p => (p.Day, p.Period))
                .Select(p => p with { Required = Math.Clamp(p.Required, 0, Math.Max(1, ids.Count)) }).ToArray(),
            (settings.ExcludedTeacherIds ?? []).Where(ids.Contains).Distinct().Order().ToArray());
        var adjusted = JsonSerializer.Serialize(settings) != JsonSerializer.Serialize(cleaned);
        return engine.Inspect(c, cleaned, protectedCells: await Protected(c, token)) with {
            Notices = adjusted && c.Timetable.StandbySettingsJson != "{}"
                ? ["تمت مواءمة احتياج الانتظار مع المعلمين والفترات الحالية. راجع الإعدادات ثم احفظها."] : [] };
    }, ct);
    public Task<WeeklyStandbyPreviewDto> PreviewAsync(int id, WeeklyStandbyRequest request, CancellationToken ct) =>
        repository.ExecuteAsync(async token => {
            var c = await Context(id, token);
            return Preview(c, request, await Protected(c, token), token);
        }, ct);
    private WeeklyStandbyPreviewDto Preview(TimetableValidationContext c, WeeklyStandbyRequest request,
        IReadOnlyList<string> protectedCells, CancellationToken ct)
    {
        var view = engine.Preview(c, request, protectedCells, ct);
        var stamp = JsonSerializer.Serialize(new { request.Mode, Workbench = view, Protected = protectedCells.Order().ToArray(),
            TemplateRevision = c.Schedule?.Template.Revision,
            TeacherRevisions = c.Teachers.OrderBy(t => t.Id).Select(t => new { t.Id, t.Revision }),
            RequirementRevisions = c.Requirements.OrderBy(r => r.Id).Select(r => new { r.Id, r.Revision }),
            AssignmentRevisions = c.Assignments.OrderBy(a => a.Id).Select(a => new { a.Id, a.Revision }) });
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp))), request.Mode, view);
    }
    public Task<WeeklyStandbyWorkbenchDto> ApplyAsync(int id, WeeklyStandbyRequest request, CancellationToken ct) =>
        repository.ExecuteAsync(async token => {
            var c = await Context(id, token);
            var accepted = Preview(c, request, await Protected(c, token), token);
            if (string.IsNullOrEmpty(request.ProposalId) || request.ProposalId != accepted.ProposalId)
                throw new BellScheduleConflictException("تغيرت بيانات الانتظار أو المعلمين؛ أعد المعاينة قبل الحفظ.");
            var before = c.Timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Standby)
                .Select(WeeklyStandbyEngine.Placement).ToArray();
            var now = clock.GetUtcNow();
            repository.StageStandby(c.Timetable, accepted.Workbench.Placements, now);
            c.Timetable.StandbySettingsJson = JsonSerializer.Serialize(accepted.Workbench.Settings);
            c.Timetable.Revision++; c.Timetable.UpdatedAt = now; c.Timetable.UpdatedByUserId = user.UserId!;
            c.Timetable.IsPublished = false; c.Timetable.PublishedAt = null; c.Timetable.PublishedByUserId = null;
            c.Timetable.SetupRevision = c.Setup?.Revision;
            c.Timetable.TimingsRequireRevalidation = false;
            await repository.SaveAsync(token);
            var version = await review.Snapshot(c, TimetableChangeKind.Saved, token);
            version.ChangeSummary = "تم حفظ توزيع الانتظار الأسبوعي";
            version.ChangeDetailsJson = JsonSerializer.Serialize(new[] { new { Code = "standby", Label = "حصص الانتظار", Count = accepted.Workbench.Placements.Count } });
            repository.AddAudit(new AuditLog { SchoolId = c.Timetable.SchoolId, UserId = user.UserId!,
                Action = "Timetable.WeeklyStandby.Saved", EntityName = nameof(SchoolTimetable), EntityId = id.ToString(),
                OldValues = JsonSerializer.Serialize(before), NewValues = JsonSerializer.Serialize(new { request.Mode, accepted.Workbench.Settings, accepted.Workbench.Placements }) });
            await repository.SaveAsync(token);
            return engine.Inspect(c, accepted.Workbench.Settings, protectedCells: await Protected(c, token));
        }, ct);
}
