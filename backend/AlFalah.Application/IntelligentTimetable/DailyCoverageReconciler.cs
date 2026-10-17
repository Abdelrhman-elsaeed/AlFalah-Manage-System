using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Events;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class DailyCoverageReconciler(IDailyCoverageRepository repository, TimeProvider clock)
{
    public Task ProcessSignalAsync(long id, string owner, CancellationToken ct) => repository.ExecuteAsync(async token => {
        var signal = await repository.SignalAsync(id, owner, token); if (signal == null) return false;
        await ReconcileAsync(signal.SchoolId, signal.LocalDate, token);
        signal.ProcessedAt = clock.GetUtcNow(); signal.LeaseOwner = null; signal.LeaseUntil = null; signal.LastError = null;
        await repository.SaveAsync(token); return true;
    }, ct);

    public async Task ReconcileAsync(int school, DateOnly date, CancellationToken ct)
    {
        var ids = await repository.PublishedTimetablesAsync(school, date, ct);
        if (ids.Count != 1) throw new ArgumentException("يلزم جدول منشور واحد داخل الفصل الدراسي لهذا التاريخ. راجع نشر الجدول والتقويم الدراسي.");
        var c = await repository.GetValidationContextAsync(school, ids[0], ct) ?? throw new KeyNotFoundException();
        var day = BellScheduleResolver.ToDay(date.DayOfWeek);
        var absent = (await repository.GetAbsentTeachersAsync(school, date, ct)).ToHashSet();
        var changes = (await repository.GetChangesAsync(school, c.Timetable.Id, date, ct)).Where(x => x.Kind == "Substitution").OrderBy(x => x.Id).ToArray();
        var needs = await repository.NeedsAsync(school, date, ct);
        var found = new HashSet<int>();
        foreach (var member in c.Timetable.Entries.Where(e => !e.IsDeleted && e.EntryType == TimetableEntryType.Lesson && e.Day == day)
            .Select(e => TimetableSwapEngine.Block(c, e).Where(x => x.InstructorProfileId == e.InstructorProfileId).OrderBy(x => x.Id).ToArray()).DistinctBy(b => b[0].Id))
        {
            var source = member[0]; found.Add(source.Id);
            var last = changes.LastOrDefault(x => x.Movements.Any(m => m.SchoolTimetableEntryId == source.Id));
            var teacher = last?.Movements.First(m => m.SchoolTimetableEntryId == source.Id).ToTeacherId;
            var active = teacher != null && teacher != source.InstructorProfileId ? last : null;
            var need = needs.SingleOrDefault(n => n.SchoolTimetableId == c.Timetable.Id && n.SourceEntryId == source.Id);
            if (need == null && (absent.Contains(source.InstructorProfileId) || active != null))
            {
                need = Create(c, date, source.Id); repository.AddNeed(need);
                need.ActiveSubstitutionId = active?.Id;
                var target = c.Teachers.FirstOrDefault(t => t.InstructorProfileId == teacher);
                need.AssignedUserId = target?.Instructor.UserId;
                need.AssignedTeacherName = target == null ? null : $"{target.Instructor.User.FirstName} {target.Instructor.User.LastName}".Trim();
                need.State = active == null ? "Needed" : "Assigned";
            }
            if (need == null) continue;
            if (!Matches(need, c))
            {
                if (need.ActiveSubstitutionId != null)
                {
                    if (need.State != "ReviewRequired" || need.ReviewReason != "تغيرت تفاصيل الحصة أو توقيتها؛ راجع التكليف قبل التنفيذ.")
                    {
                        need.State = "ReviewRequired"; need.ReviewReason = "تغيرت تفاصيل الحصة أو توقيتها؛ راجع التكليف قبل التنفيذ.";
                        Touch(need, clock.GetUtcNow());
                        need.AppendDomainEvent(new CoverageAttentionEvent(Guid.NewGuid(), school, need.Id, "ReviewRequired", need.AssignedUserId, need.ReviewReason, clock.GetUtcNow()));
                    }
                }
                else
                {
                    var current = Create(c, date, source.Id);
                    need.EntryIdsJson = current.EntryIdsJson; need.PeriodsJson = current.PeriodsJson; need.StartsAt = current.StartsAt; need.EndsAt = current.EndsAt;
                    need.Classroom = current.Classroom; need.Subject = current.Subject; need.TimeZoneId = current.TimeZoneId;
                    need.OriginalTeacherId = current.OriginalTeacherId; need.OriginalTeacherName = current.OriginalTeacherName; Touch(need, clock.GetUtcNow());
                }
            }
            var required = absent.Contains(source.InstructorProfileId);
            var substituteAbsent = teacher != null && active != null && absent.Contains(teacher.Value);
            if (need.IsRequired != required || need.SubstituteWasAbsent != substituteAbsent)
            {
                need.IsRequired = required; need.SubstituteWasAbsent = substituteAbsent;
                if (need.ActiveSubstitutionId != null)
                {
                    need.State = "ReviewRequired";
                    need.ReviewReason = substituteAbsent ? "المعلم البديل مسجل غائبًا؛ راجع التغطية." : "تغير غياب المعلم الأصلي؛ راجع التكليف المعتمد.";
                    need.AppendDomainEvent(new CoverageAttentionEvent(Guid.NewGuid(), school, need.Id, "ReviewRequired", need.AssignedUserId,
                        $"{need.ReviewReason} {need.Classroom} · {need.Subject} · {date:yyyy-MM-dd}", clock.GetUtcNow()));
                }
                else need.State = required ? "Needed" : "Withdrawn";
                Touch(need, clock.GetUtcNow());
            }
        }
        foreach (var old in needs.Where(n => n.State != "Withdrawn" && (n.SchoolTimetableId != c.Timetable.Id || !found.Contains(n.SourceEntryId))))
        {
            if (old.ReviewReason == "تغير نشر الجدول أو هوية الحصة؛ راجع التكليف السابق.") continue;
            old.State = old.ActiveSubstitutionId == null ? "Withdrawn" : "ReviewRequired";
            old.ReviewReason = "تغير نشر الجدول أو هوية الحصة؛ راجع التكليف السابق."; Touch(old, clock.GetUtcNow());
        }
        await repository.SaveAsync(ct);
    }

    public static DailyCoverageNeed Create(TimetableValidationContext c, DateOnly date, int sourceId)
    {
        var source = c.Timetable.Entries.Single(e => e.Id == sourceId);
        var block = TimetableSwapEngine.Block(c, source).Where(e => e.InstructorProfileId == source.InstructorProfileId).OrderBy(e => e.Id).ToArray();
        var periods = block.Select(e => e.Period).Distinct().Order().ToArray();
        var windows = TimetableValidationEngine.Periods(c.Schedule, (int)source.Day);
        var zoneId = c.Schedule?.SchoolTimeZoneId ?? "Africa/Cairo";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        DateTimeOffset At(TimeOnly time) { var local = date.ToDateTime(time, DateTimeKind.Unspecified); return new(local, zone.GetUtcOffset(local)); }
        var teacher = c.Teachers.First(t => t.InstructorProfileId == source.InstructorProfileId);
        return new() { SchoolId = c.Timetable.SchoolId, SchoolTimetableId = c.Timetable.Id, LocalDate = date,
            SourceEntryId = block[0].Id, OriginalTeacherId = source.InstructorProfileId,
            OriginalTeacherName = $"{teacher.Instructor.User.FirstName} {teacher.Instructor.User.LastName}".Trim(),
            Classroom = source.ClassLabel ?? "", Subject = source.Subject ?? "", EntryIdsJson = JsonSerializer.Serialize(block.Select(e => e.Id)),
            PeriodsJson = JsonSerializer.Serialize(periods), TimeZoneId = zoneId,
            StartsAt = At(windows.Single(p => p.Sequence == periods[0]).StartLocalTime), EndsAt = At(windows.Single(p => p.Sequence == periods[^1]).EndLocalTime) };
    }
    public static void Touch(DailyCoverageNeed need, DateTimeOffset now) { need.Revision++; need.UpdatedAt = now; }
    public static bool Matches(DailyCoverageNeed need, TimetableValidationContext c)
    {
        if (need.SchoolTimetableId != c.Timetable.Id || !c.Timetable.Entries.Any(e => e.Id == need.SourceEntryId && !e.IsDeleted && e.EntryType == TimetableEntryType.Lesson && BellScheduleResolver.ToDay(need.LocalDate.DayOfWeek) == e.Day)) return false;
        var current = Create(c, need.LocalDate, need.SourceEntryId);
        return need.OriginalTeacherId == current.OriginalTeacherId && need.EntryIdsJson == current.EntryIdsJson && need.PeriodsJson == current.PeriodsJson &&
            need.StartsAt == current.StartsAt && need.EndsAt == current.EndsAt && need.Classroom == current.Classroom && need.Subject == current.Subject && need.TimeZoneId == current.TimeZoneId;
    }
}
