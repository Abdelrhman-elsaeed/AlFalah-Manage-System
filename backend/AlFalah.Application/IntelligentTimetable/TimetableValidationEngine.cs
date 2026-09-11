using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed record ValidationViolation(ViolationRuleCode RuleCode, ViolationSeverity Severity, string MessageAr,
    int? ClassroomId, int? InstructorProfileId, int? SubjectId, TimetableDay? Day, int? Period, int[] EntryIds,
    string Detail = "")
{
    // Identity deliberately includes evidence, so fixing one conflict cannot conceal a newly introduced one.
    public string Key => JsonSerializer.Serialize(new { RuleCode, ClassroomId, InstructorProfileId, SubjectId, Day, Period, EntryIds, Detail });
}

/// <summary>Pure rule evaluation; no persistence or mutations. Counts lessons by logical occurrence.</summary>
public sealed class TimetableValidationEngine
{
    public static IReadOnlyList<BellPeriod> Periods(BellScheduleRevision? schedule, int day)
    {
        var d = schedule?.Days.SingleOrDefault(x => x.Day == day);
        if (d is null || !d.IsStudyDay) return [];
        return (d.UsesDefaultSchedule ? schedule!.Days.SingleOrDefault(x => x.Day == 0)?.Periods : d.Periods)?
            .OrderBy(x => x.Sequence).ToArray() ?? [];
    }
    public static IReadOnlyList<ScheduleBreakDefinition> Breaks(BellScheduleRevision? schedule, int day)
    {
        var d = schedule?.Days.SingleOrDefault(x => x.Day == day);
        if (d is null || !d.IsStudyDay) return [];
        return (d.UsesDefaultBreaks ? schedule!.Days.SingleOrDefault(x => x.Day == 0)?.Breaks : d.Breaks)?
            .Where(x => !x.IsDeleted).ToArray() ?? [];
    }
    public static bool IsPair(TimetableValidationContext c, SchoolTimetableEntry a, SchoolTimetableEntry b) =>
        a.Day == b.Day && b.Period == a.Period + 1 && a.InstructorProfileId == b.InstructorProfileId &&
        a.RoomId == b.RoomId && Periods(c.Schedule, (int)a.Day).SingleOrDefault(x => x.Sequence == a.Period) is { } p &&
        Periods(c.Schedule, (int)b.Day).SingleOrDefault(x => x.Sequence == b.Period) is { } q && p.EndLocalTime == q.StartLocalTime;

    public IReadOnlyList<ValidationViolation> Evaluate(TimetableValidationContext c, bool includeSoft = true,
        IReadOnlyDictionary<int, int>? scheduledTeachers = null)
    {
        // Daily cover changes operational ownership, not the permanent assignment or its allocated quota.
        int AssignedTeacher(SchoolTimetableEntry e) => scheduledTeachers?.GetValueOrDefault(e.Id, e.InstructorProfileId) ?? e.InstructorProfileId;
        var found = new List<ValidationViolation>();
        var entries = c.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToArray();
        void Add(ViolationRuleCode code, string message, SchoolTimetableEntry? e = null,
            IEnumerable<int>? ids = null, ClassSubjectRequirement? r = null, string detail = "")
        {
            if (!includeSoft && code >= ViolationRuleCode.ExcessiveConsecutive) return;
            found.Add(new(code, code >= ViolationRuleCode.ExcessiveConsecutive ? ViolationSeverity.Warning : ViolationSeverity.Error,
                message, e?.ClassroomId ?? r?.ClassroomId, e?.InstructorProfileId, e?.SubjectId ?? r?.SubjectId,
                e?.Day, e?.Period, (ids ?? (e is null ? [] : new[] { e.Id })).Distinct().Order().ToArray(), detail));
        }

        if (c.Setup is null || c.Setup.IsDeleted || c.Setup.SchoolId != c.Timetable.SchoolId ||
            c.Setup.AcademicYearId != c.Timetable.AcademicYearId || c.Setup.Semester != c.Timetable.Semester ||
            c.Schedule is null || c.Schedule.SchoolId != c.Timetable.SchoolId ||
            c.Schedule.BellScheduleTemplateId != c.Setup.BellScheduleTemplateId ||
            c.Schedule.Template.IsDeleted || !c.Schedule.Template.IsActive || c.Schedule.Revision != c.Schedule.Template.Revision)
            Add(ViolationRuleCode.NonStudyDayPlacement, "إعدادات الجدول أو نسخة التوقيت غير صالحة؛ حدّث توقيت الجدول.");

        bool CoOccurrence(SchoolTimetableEntry a, SchoolTimetableEntry b) => a.ClassSubjectRequirementId.HasValue &&
            a.ClassSubjectRequirementId == b.ClassSubjectRequirementId && a.Day == b.Day && a.Period == b.Period &&
            a.RoomId == b.RoomId && a.InstructorProfileId != b.InstructorProfileId &&
            c.Assignments.Any(x => !x.IsDeleted && x.ClassSubjectRequirementId == a.ClassSubjectRequirementId && x.Mode == "CoTeaching" &&
                x.Members.Any(m => c.Teachers.Any(t => t.Id == m.TeacherTimetableProfileId && t.InstructorProfileId == AssignedTeacher(a))) &&
                x.Members.Any(m => c.Teachers.Any(t => t.Id == m.TeacherTimetableProfileId && t.InstructorProfileId == AssignedTeacher(b))));
        bool Overlaps(SchoolTimetableEntry a, SchoolTimetableEntry b)
        {
            if (a.Day != b.Day) return false;
            var p = Periods(c.Schedule, (int)a.Day).SingleOrDefault(x => x.Sequence == a.Period);
            var q = Periods(c.Schedule, (int)b.Day).SingleOrDefault(x => x.Sequence == b.Period);
            return a.Period == b.Period || (p is not null && q is not null && p.StartLocalTime < q.EndLocalTime && q.StartLocalTime < p.EndLocalTime);
        }
        for (var i = 0; i < entries.Length; i++)
        for (var j = i + 1; j < entries.Length; j++)
        {
            var a = entries[i]; var b = entries[j]; if (!Overlaps(a, b)) continue;
            if (a.InstructorProfileId > 0 && a.InstructorProfileId == b.InstructorProfileId)
                Add(ViolationRuleCode.TeacherDoubleBooking, "المعلم مكلّف بحصتين في توقيت متداخل.", a, [a.Id, b.Id]);
            if (CoOccurrence(a, b)) continue;
            if (a.ClassroomId.HasValue && a.ClassroomId == b.ClassroomId)
                Add(ViolationRuleCode.ClassroomDoubleBooking, "الفصل محجوز لحصتين في توقيت متداخل.", a, [a.Id, b.Id]);
            if (a.RoomId.HasValue && a.RoomId == b.RoomId)
                Add(ViolationRuleCode.RoomDoubleBooking, "القاعة محجوزة لحصتين في توقيت متداخل.", a, [a.Id, b.Id]);
        }
        foreach (var e in entries)
        {
            var teacher = c.Teachers.SingleOrDefault(x => x.InstructorProfileId == e.InstructorProfileId);
            var period = Periods(c.Schedule, (int)e.Day).SingleOrDefault(x => x.Sequence == e.Period);
            if (period is null || period.EndLocalTime <= period.StartLocalTime || Breaks(c.Schedule, (int)e.Day).Any(b =>
                    b.Window.StartLocalTime < period.EndLocalTime && period.StartLocalTime < b.Window.EndLocalTime))
                Add(ViolationRuleCode.NonStudyDayPlacement, "الحصة خارج يوم الدراسة أو في فترة غير صالحة أو استراحة.", e);
            if (teacher is not null && (teacher.BellScheduleRevisionId != c.Schedule?.Id ||
                teacher.Slots.Any(s => s.Day == (int)e.Day && !s.IsAvailable && (s.BellPeriodId == period?.Id ||
                    s.Period.StartLocalTime < period?.EndLocalTime && period?.StartLocalTime < s.Period.EndLocalTime))))
                Add(ViolationRuleCode.UnavailableSlot, "المعلم غير متاح أو يلزم تحديث إتاحته بعد تعديل التوقيت.", e);
            if (e.SchoolId != c.Timetable.SchoolId || teacher is null || !teacher.Instructor.IsActive || teacher.Instructor.IsDeleted ||
                !teacher.Instructor.User.IsActive || teacher.Instructor.User.IsDeleted || teacher.Instructor.SchoolId != c.Timetable.SchoolId)
                Add(ViolationRuleCode.MissingAssignment, "مرجع المعلم غير صالح أو غير نشط أو خارج المدرسة.", e, detail: "teacher-reference");
            if (e.EntryType != TimetableEntryType.Lesson) continue;
            var r = c.Requirements.SingleOrDefault(x => x.Id == e.ClassSubjectRequirementId && !x.IsDeleted);
            if (r is null || r.ClassroomId != e.ClassroomId || r.SubjectId != e.SubjectId ||
                !c.Classrooms.Any(x => x.Id == e.ClassroomId && x.IsActive && !x.IsDeleted && x.AcademicYearId == c.Timetable.AcademicYearId) ||
                !c.Subjects.Any(x => x.Id == e.SubjectId && x.IsActive && !x.IsDeleted) ||
                e.RoomId.HasValue && !c.Rooms.Any(x => x.Id == e.RoomId && x.IsActive && !x.IsDeleted))
                Add(ViolationRuleCode.MissingAssignment, "يلزم ربط الحصة بفصل ومادة ومتطلب وقاعة صالحة في المدرسة.", e, detail: "lesson-reference");
            if (r is null) continue;
            var assignment = c.Assignments.SingleOrDefault(x => !x.IsDeleted && x.ClassSubjectRequirementId == r.Id);
            var assignedProfile = c.Teachers.SingleOrDefault(x => x.InstructorProfileId == AssignedTeacher(e));
            if (assignment is null || !assignment.Members.Any(x => x.TeacherTimetableProfileId == assignedProfile?.Id))
                Add(ViolationRuleCode.MissingAssignment, "المعلم غير مسند إلى متطلب هذه المادة.", e, detail: "assignment");
            if (r.AllowedDays.Count > 0 && r.AllowedDays.All(x => x.Day != (int)e.Day))
                Add(ViolationRuleCode.DisallowedDay, "المادة موضوعة في يوم غير مسموح.", e);
            if (r.Rooms.Any(x => !x.IsPreferred) && !r.Rooms.Any(x => !x.IsPreferred && x.RoomId == e.RoomId))
                Add(ViolationRuleCode.MissingAssignment, "الحصة تحتاج إلى قاعة متخصصة من القاعات المحددة.", e, detail: "required-room");
            if (r.TimePreference == "Early" && e.Period > (r.LatestPreferredPeriodSequence ?? 3))
                Add(ViolationRuleCode.EarlyPreferenceMissed, "لم تتحقق أفضلية الحصة المبكرة.", e);
        }
        foreach (var teacher in c.Teachers)
        {
            var lessons = entries.Where(x => x.InstructorProfileId == teacher.InstructorProfileId && x.EntryType == TimetableEntryType.Lesson).ToArray();
            if (lessons.Length > teacher.MaximumWeeklyPeriods)
                Add(ViolationRuleCode.ScheduleCountMismatch, $"نصاب المعلم {lessons.Length} يتجاوز الحد {teacher.MaximumWeeklyPeriods}.", lessons[0], lessons.Select(x => x.Id), detail: "maximum-load");
            var lasts = lessons.Where(e => e.Period == Periods(c.Schedule, (int)e.Day).LastOrDefault()?.Sequence).ToArray();
            if (lasts.Length > 2) Add(ViolationRuleCode.UnfairLastPeriods, "أكثر من حصتين أخيرتين أسبوعياً للمعلم.", lasts[0], lasts.Select(x => x.Id));
            foreach (var day in lessons.GroupBy(x => x.Day))
            {
                var sorted = day.OrderBy(x => x.Period).ToArray(); var consecutive = 1; var maximum = 1;
                for (var i = 1; i < sorted.Length; i++)
                { consecutive = Consecutive(c, sorted[i - 1], sorted[i]) ? consecutive + 1 : 1; maximum = Math.Max(maximum, consecutive); }
                if (maximum > 3) Add(ViolationRuleCode.ExcessiveConsecutive, "أكثر من ثلاث حصص متتالية للمعلم.", sorted[0], sorted.Select(x => x.Id));
                var gaps = Periods(c.Schedule, (int)day.Key).Count(p => p.Sequence > sorted[0].Period && p.Sequence < sorted[^1].Period &&
                    !entries.Any(x => x.InstructorProfileId == teacher.InstructorProfileId && x.Day == day.Key && x.Period == p.Sequence));
                if (gaps > 2) Add(ViolationRuleCode.ExcessiveGaps, "أكثر من حصتي انتظار في اليوم للمعلم.", sorted[0], sorted.Select(x => x.Id));
            }
        }
        foreach (var r in c.Requirements.Where(x => !x.IsDeleted && x.Classroom.IsActive && !x.Classroom.IsDeleted && x.Subject.IsActive && !x.Subject.IsDeleted))
        {
            var all = entries.Where(x => x.ClassSubjectRequirementId == r.Id && x.EntryType == TimetableEntryType.Lesson).ToArray();
            var logical = all.GroupBy(x => (x.Day, x.Period)).Select(x => x.OrderBy(e => e.InstructorProfileId).First()).OrderBy(x => x.Day).ThenBy(x => x.Period).ToArray();
            if (logical.Length != r.TotalWeeklyPeriods)
                Add(ViolationRuleCode.ScheduleCountMismatch, $"المطلوب {r.TotalWeeklyPeriods} حصة والمجدول {logical.Length}.", ids: all.Select(x => x.Id), r: r);
            if (logical.Length < r.TotalWeeklyPeriods)
                Add(ViolationRuleCode.MissingAssignment, "توجد حصص مطلوبة غير مجدولة.", ids: all.Select(x => x.Id), r: r, detail: "missing-occurrence");
            var assignment = c.Assignments.SingleOrDefault(x => !x.IsDeleted && x.ClassSubjectRequirementId == r.Id);
            if (assignment is null || assignment.Members.Count == 0)
                Add(ViolationRuleCode.MissingAssignment, "لم يتم إسناد المادة إلى معلم.", ids: all.Select(x => x.Id), r: r, detail: "unassigned-requirement");
            else foreach (var member in assignment.Members)
            {
                var teacher = c.Teachers.SingleOrDefault(x => x.Id == member.TeacherTimetableProfileId);
                var owned = all.Where(x => AssignedTeacher(x) == teacher?.InstructorProfileId).OrderBy(x => x.Day).ThenBy(x => x.Period).ToArray();
                if (owned.Length != member.AllocatedPeriodCount || assignment.Mode == "CoTeaching" &&
                    logical.Any(x => !owned.Any(o => o.Day == x.Day && o.Period == x.Period)))
                    Add(ViolationRuleCode.MissingAssignment, "الحصص المجدولة لا تطابق نصيب المعلم أو مشاركة التدريس.", owned.FirstOrDefault(), all.Select(x => x.Id), r, $"member:{member.Id}");
                if (PairCount(c, owned) < member.AllocatedPairedBlockCount)
                    Add(ViolationRuleCode.BrokenPairedBlock, "حصة مزدوجة مسندة للمعلم غير متصلة أو مقسمة.", owned.FirstOrDefault(), all.Select(x => x.Id), r, $"member-pair:{member.Id}");
            }
            if (PairCount(c, logical) < r.PairedBlockCount)
                Add(ViolationRuleCode.BrokenPairedBlock, "الحصص المزدوجة غير متصلة أو يفصلها وقت استراحة.", ids: all.Select(x => x.Id), r: r);
            foreach (var slot in r.FixedSlots.Where(s => !logical.Any(x => (int)x.Day == s.Day && x.Period == s.Period)))
                Add(ViolationRuleCode.ViolatedFixedSlot, $"الموعد الثابت غير محقق: اليوم {slot.Day}، الحصة {slot.Period}.", ids: all.Select(x => x.Id), r: r, detail: $"fixed:{slot.Day}:{slot.Period}");
            var remainingPairs = r.PairedBlockCount;
            foreach (var day in logical.GroupBy(x => x.Day))
            {
                var rows = day.ToArray(); var paired = Math.Min(remainingPairs, PairCount(c, rows)); remainingPairs -= paired;
                if (rows.Length > 1 && (paired == 0 || rows.Length != 2 * paired))
                    Add(ViolationRuleCode.SubjectConcentration, "تكرار المادة في اليوم؛ الحد حصة فردية واحدة باستثناء الحصص المزدوجة.", rows[0], rows.Select(x => x.Id));
            }
            if (includeSoft && logical.Length > 1 && r.TimePreference == "None" &&
                (logical.All(e => e.Period <= Math.Max(1, Periods(c.Schedule, (int)e.Day).Count / 2)) ||
                 logical.All(e => e.Period > Math.Max(1, Periods(c.Schedule, (int)e.Day).Count / 2))) &&
                r.FixedSlots.Count < logical.Length && HasBalancedAlternative(c, logical))
                Add(ViolationRuleCode.SubjectClusterImbalance, "حصص المادة متركزة في أول اليوم أو آخره؛ راجع التوزيع.", logical[0], logical.Select(x => x.Id));
        }
        return found;
    }
    private static int PairCount(TimetableValidationContext c, SchoolTimetableEntry[] rows)
    {
        var pairs = 0;
        for (var i = 0; i < rows.Length - 1; i++) if (IsPair(c, rows[i], rows[i + 1])) { pairs++; i++; }
        return pairs;
    }
    private bool HasBalancedAlternative(TimetableValidationContext c, SchoolTimetableEntry[] lessons)
    {
        var existingErrors = Evaluate(c, false).Select(x => x.Key).ToHashSet();
        var early = lessons.All(e => e.Period <= Math.Max(1, Periods(c.Schedule, (int)e.Day).Count / 2));
        foreach (var lesson in lessons)
        foreach (var period in Periods(c.Schedule, (int)lesson.Day).Where(p =>
            (p.Sequence <= Math.Max(1, Periods(c.Schedule, (int)lesson.Day).Count / 2)) != early))
        {
            // Co-teachers are one occurrence and must move together. Pair/fixed-slot rules remain hard checks.
            var moves = c.Timetable.Entries.Where(e => !e.IsDeleted && e.ClassSubjectRequirementId == lesson.ClassSubjectRequirementId &&
                e.Day == lesson.Day && e.Period == lesson.Period).Select(e => new DTOs.RepairMovementDto(e.Id, e.Day, e.Period,
                    e.InstructorProfileId, e.Day, period.Sequence, e.InstructorProfileId)).ToArray();
            if (Evaluate(TimetableRepairEngine.Simulate(c, moves), false).All(x => existingErrors.Contains(x.Key))) return true;
        }
        return false;
    }
    private static bool Consecutive(TimetableValidationContext c, SchoolTimetableEntry a, SchoolTimetableEntry b) =>
        a.Day == b.Day && b.Period == a.Period + 1 &&
        !Breaks(c.Schedule, (int)a.Day).Any(br =>
            Periods(c.Schedule, (int)a.Day).SingleOrDefault(p => p.Sequence == a.Period) is { } p &&
            Periods(c.Schedule, (int)b.Day).SingleOrDefault(p => p.Sequence == b.Period) is { } q &&
            br.Window.StartLocalTime < q.StartLocalTime && br.Window.EndLocalTime > p.EndLocalTime);
}
