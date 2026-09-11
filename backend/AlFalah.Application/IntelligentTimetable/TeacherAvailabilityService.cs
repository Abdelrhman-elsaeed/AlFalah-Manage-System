using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TeacherAvailabilityService(ITeacherAvailabilityRepository repository)
{
    public static IReadOnlyList<AvailabilityCellDto> EffectiveCells(BellScheduleRevision schedule) =>
        schedule.Days.Where(x => x.Day != 0 && x.IsStudyDay).OrderBy(x => x.Day).SelectMany(day =>
            (day.UsesDefaultSchedule ? schedule.Days.Single(x => x.Day == 0).Periods : day.Periods)
                .OrderBy(x => x.Sequence).Select(p => new AvailabilityCellDto(day.Day, p.Id, p.Sequence, p.StartLocalTime, p.EndLocalTime, true))).ToArray();

    public async Task<TeacherAvailabilityDto> GetAsync(int schoolId, int setupId, int teacherId, CancellationToken ct)
    {
        var setup = await repository.GetSetupAsync(schoolId, setupId, ct) ?? throw new KeyNotFoundException();
        var teacher = await repository.GetTeacherAsync(schoolId, teacherId, ct) ?? throw new KeyNotFoundException();
        var schedule = setup.BellScheduleTemplateId is int templateId ? await repository.GetScheduleAsync(schoolId, templateId, ct) : null;
        if (schedule is null) throw new ArgumentException("اختر قالب توقيت صالحاً لملف الإعداد أولاً.");
        var profile = await repository.GetProfileAsync(schoolId, setupId, teacherId, ct);
        var cells = EffectiveCells(schedule);
        var review = profile != null && profile.BellScheduleRevisionId != schedule.Id;
        var old = profile?.Slots.Select(s => new AvailabilityCellDto(s.Day, s.BellPeriodId, s.Period.Sequence,
            s.Period.StartLocalTime, s.Period.EndLocalTime, s.IsAvailable)).ToArray() ?? [];
        // Revisions are append-only, so period IDs change. Propose exact day/time matches only;
        // every revision change still requires explicit review before it can be saved or scheduled.
        AvailabilityCellDto? Match(AvailabilityCellDto cell) => old.SingleOrDefault(s => s.Day == cell.Day &&
            (review ? s.StartLocalTime == cell.StartLocalTime && s.EndLocalTime == cell.EndLocalTime : s.BellPeriodId == cell.BellPeriodId));
        var mapped = cells.Select(c => c with { IsAvailable = Match(c)?.IsAvailable ?? true }).ToArray();
        var orphans = review ? old.Where(s => !cells.Any(c => c.Day == s.Day && c.StartLocalTime == s.StartLocalTime && c.EndLocalTime == s.EndLocalTime)).ToArray() : [];
        var assigned = await repository.GetAssignmentsAsync(schoolId, setupId, teacherId, ct);
        // Planned teaching load and placed entries represent the same lessons; never add them together.
        var load = Math.Max(assigned.Count(x => x.EntryType == TimetableEntryType.Lesson),
            await repository.GetTeachingLoadAsync(schoolId, setupId, teacherId, ct));
        var maximum = profile?.MaximumWeeklyPeriods ?? cells.Count;
        var violations = assigned.Where(a => !mapped.Any(c => c.Day == (int)a.Day && c.Sequence == a.Period && c.IsAvailable))
            .Select(a => $"الحصة {a.Period} في اليوم {(int)a.Day} مسندة بالفعل وغير متاحة؛ عدّل الجدول قبل نشره.").ToList();
        if (load > maximum) violations.Add("النصاب المسند يتجاوز الحد الأسبوعي؛ يجب تصحيح الجدول.");
        if (review) violations.Add("تغيّر قالب التوقيت. راجع الإتاحة واعتمد توزيع الحصص الجديد قبل الجدولة.");
        return new(teacherId, teacher.Name, setupId, profile?.Revision ?? 0, schedule.Id, schedule.Name,
            profile?.ShortDisplayName ?? string.Empty, maximum, profile?.IsVisiting ?? false, profile?.HideFromPrint ?? false,
            load, maximum - load, review, mapped, orphans, violations);
    }

    public async Task<TeacherAvailabilityDto> UpdateAsync(int schoolId, int setupId, int teacherId,
        UpdateTeacherProfileRequest request, string userId, DateTimeOffset now, CancellationToken ct)
    {
        var current = await GetAsync(schoolId, setupId, teacherId, ct);
        if (current.Revision != request.Revision || current.BellScheduleRevisionId != request.BellScheduleRevisionId)
            throw new BellScheduleConflictException("Stale teacher or timing revision");
        if (current.RequiresScheduleReview && !request.ConfirmScheduleReview)
            throw new ArgumentException("يجب مراجعة تغييرات التوقيت واعتماد الإتاحة الجديدة صراحةً.");
        if (request.Slots.Count != current.Slots.Count || !request.Slots.Select(s => (s.Day, s.BellPeriodId)).ToHashSet()
                .SetEquals(current.Slots.Select(s => (s.Day, s.BellPeriodId))))
            throw new ArgumentException("يجب إرسال جميع حصص التوقيت الفعلي فقط، دون أيام العطلات أو الاستراحات.");
        if (request.MaximumWeeklyPeriods < current.AllocatedPeriods)
            throw new ArgumentException("لا يمكن خفض الحد الأسبوعي عن النصاب المسند. خفّض إسنادات المعلم أولاً.");
        var setup = (await repository.GetSetupAsync(schoolId, setupId, ct))!;
        var profile = await repository.GetProfileAsync(schoolId, setupId, teacherId, ct);
        var before = profile is null ? null : new {
            profile.ShortDisplayName, profile.MaximumWeeklyPeriods, profile.IsVisiting, profile.HideFromPrint,
            profile.BellScheduleRevisionId, profile.Revision,
            Slots = profile.Slots.Select(s => new AvailabilitySlotRequest(s.Day, s.BellPeriodId, s.IsAvailable)).ToArray()
        };
        profile ??= new TeacherTimetableProfile {
            SchoolId = schoolId, TimetableSetupProfileId = setupId, InstructorProfileId = teacherId,
            CreatedAt = now, CreatedByUserId = userId, Revision = 0 };
        profile.ShortDisplayName = request.ShortDisplayName.Trim();
        profile.MaximumWeeklyPeriods = request.MaximumWeeklyPeriods;
        profile.IsVisiting = request.IsVisiting;
        profile.HideFromPrint = request.HideFromPrint;
        profile.BellScheduleRevisionId = request.BellScheduleRevisionId;
        profile.UpdatedAt = now;
        profile.UpdatedByUserId = userId;
        profile.Revision++;
        // Shared concurrency fence with timing selection and timetable writes, including first-time profiles.
        setup.Revision++;
        setup.Status = TimetableSetupStatus.Draft;
        setup.UpdatedAt = now;
        setup.UpdatedByUserId = userId;
        await repository.SaveAsync(profile, request.Slots, before, ct);
        return await GetAsync(schoolId, setupId, teacherId, ct);
    }

    /// <summary>Shared hard constraints for manual saves, imports, restores, publishing and future generation/swaps.</summary>
    public async Task ValidateAssignmentsAsync(SchoolTimetable timetable, IReadOnlyList<TimetableEntryDto> entries, CancellationToken ct)
    {
        if (timetable.TimetableSetupProfileId is not int setupId)
            throw new ArgumentException("اربط الجدول بملف إعداد قبل إسناد الحصص.");
        var setup = await repository.GetSetupAsync(timetable.SchoolId, setupId, ct) ?? throw new KeyNotFoundException();
        var schedule = setup.BellScheduleTemplateId is int templateId
            ? await repository.GetScheduleAsync(timetable.SchoolId, templateId, ct) : null;
        if (schedule is null || schedule.Id != timetable.BellScheduleRevisionId)
            throw new ArgumentException("تغيّر التوقيت. راجع التوقيت الحالي قبل إسناد الحصص أو نشر الجدول.");
        var cells = EffectiveCells(schedule);
        var profiles = await repository.GetProfilesAsync(timetable.SchoolId, setupId, ct);
        var activeTeachers = (await repository.GetTeachersAsync(timetable.SchoolId, ct)).Select(x => x.Id).ToHashSet();
        foreach (var teacher in entries.GroupBy(x => x.InstructorProfileId))
        {
            if (!activeTeachers.Contains(teacher.Key))
                throw new ArgumentException("إحدى الحصص مسندة إلى معلم غير نشط أو خارج المدرسة.");
            var profile = profiles.SingleOrDefault(x => x.InstructorProfileId == teacher.Key);
            if (profile != null && profile.BellScheduleRevisionId != schedule.Id)
                throw new ArgumentException($"راجع إتاحة المعلم {teacher.Key} بعد تغيير التوقيت.");
            if (teacher.Count(x => x.EntryType == TimetableEntryType.Lesson) > (profile?.MaximumWeeklyPeriods ?? cells.Count))
                throw new ArgumentException($"تجاوز الحد الأسبوعي الصارم للمعلم {teacher.Key}.");
            foreach (var entry in teacher)
            {
                var cell = cells.SingleOrDefault(x => x.Day == (int)entry.Day && x.Sequence == entry.Period);
                if (cell is null || profile?.Slots.Any(x => x.Day == cell.Day && x.BellPeriodId == cell.BellPeriodId && !x.IsAvailable) == true)
                    throw new ArgumentException($"المعلم {teacher.Key} غير متاح في اليوم {(int)entry.Day}، الحصة {entry.Period}.");
            }
        }
        // Persisted in the caller's transaction; a concurrent availability save makes that transaction fail.
        setup.Revision++;
    }
}
