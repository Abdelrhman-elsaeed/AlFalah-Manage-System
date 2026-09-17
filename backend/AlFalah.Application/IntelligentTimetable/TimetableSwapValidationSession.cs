using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

internal sealed record TimetableSwapEvaluation(string[] Errors, string[] Warnings);

/// <summary>
/// Prepared, indexed validation for the hot swap-search loop. It preserves the
/// Phase 7 hard/soft rules without recalculating unrelated soft alternatives for
/// every possible move.
/// </summary>
internal sealed class TimetableSwapValidationSession
{
    private const string InvalidSetup = "إعدادات الجدول أو نسخة التوقيت غير صالحة؛ حدّث توقيت الجدول.";
    private const string TeacherCollision = "المعلم مكلّف بحصتين في توقيت متداخل.";
    private const string ClassroomCollision = "الفصل محجوز لحصتين في توقيت متداخل.";
    private const string RoomCollision = "القاعة محجوزة لحصتين في توقيت متداخل.";
    private const string InvalidPeriod = "الحصة خارج يوم الدراسة أو في فترة غير صالحة أو استراحة.";
    private const string UnavailableTeacher = "المعلم غير متاح أو يلزم تحديث إتاحته بعد تعديل التوقيت.";
    private const string InvalidTeacher = "مرجع المعلم غير صالح أو غير نشط أو خارج المدرسة.";
    private const string InvalidLesson = "يلزم ربط الحصة بفصل ومادة ومتطلب وقاعة صالحة في المدرسة.";
    private const string MissingAssignment = "المعلم غير مسند إلى متطلب هذه المادة.";
    private const string DisallowedDay = "المادة موضوعة في يوم غير مسموح.";
    private const string RequiredRoom = "الحصة تحتاج إلى قاعة متخصصة من القاعات المحددة.";
    private const string MissingOccurrence = "توجد حصص مطلوبة غير مجدولة.";
    private const string UnassignedRequirement = "لم يتم إسناد المادة إلى معلم.";
    private const string AllocationMismatch = "الحصص المجدولة لا تطابق نصيب المعلم أو مشاركة التدريس.";
    private const string BrokenPair = "الحصص المزدوجة غير متصلة أو يفصلها وقت استراحة.";
    private const string ExcessiveConsecutive = "أكثر من ثلاث حصص متتالية للمعلم.";
    private const string UnfairLastPeriods = "أكثر من حصتين أخيرتين أسبوعياً للمعلم.";
    private const string ExcessiveGaps = "أكثر من حصتي انتظار في اليوم للمعلم.";
    private const string SubjectConcentration = "تكرار المادة في اليوم؛ الحد حصة فردية واحدة باستثناء الحصص المزدوجة.";
    private const string EarlyPreference = "لم تتحقق أفضلية الحصة المبكرة.";
    private const string ClusterImbalance = "حصص المادة متمركزة في أول اليوم أو آخره؛ راجع التوزيع.";

    private readonly TimetableValidationContext context;
    private readonly string mode;
    private readonly IReadOnlyDictionary<int, int>? scheduledTeachers;
    private readonly CancellationToken cancellationToken;
    private readonly IReadOnlyDictionary<int, TeacherTimetableProfile> teachers;
    private readonly IReadOnlyDictionary<int, ClassSubjectRequirement> requirements;
    private readonly IReadOnlyDictionary<int, TeachingAssignment> assignments;
    private readonly IReadOnlyDictionary<int, Classroom> classrooms;
    private readonly IReadOnlyDictionary<int, SubjectDefinition> subjects;
    private readonly IReadOnlyDictionary<int, TimetableRoom> rooms;
    private readonly IReadOnlyDictionary<TimetableDay, IReadOnlyDictionary<int, BellPeriod>> periods;
    private readonly IReadOnlyDictionary<TimetableDay, IReadOnlyList<ScheduleBreakDefinition>> breaks;
    private readonly string[] baselineHardErrors;

    internal TimetableSwapValidationSession(TimetableValidationContext context, string mode,
        IReadOnlyDictionary<int, int>? scheduledTeachers, CancellationToken cancellationToken)
    {
        this.context = context;
        this.mode = mode;
        this.scheduledTeachers = scheduledTeachers;
        this.cancellationToken = cancellationToken;
        teachers = context.Teachers.ToDictionary(x => x.InstructorProfileId);
        requirements = context.Requirements.Where(x => !x.IsDeleted).ToDictionary(x => x.Id);
        assignments = context.Assignments.Where(x => !x.IsDeleted).ToDictionary(x => x.ClassSubjectRequirementId);
        classrooms = context.Classrooms.ToDictionary(x => x.Id);
        subjects = context.Subjects.ToDictionary(x => x.Id);
        rooms = context.Rooms.ToDictionary(x => x.Id);
        periods = Enum.GetValues<TimetableDay>().ToDictionary(day => day,
            day => (IReadOnlyDictionary<int, BellPeriod>)TimetableValidationEngine.Periods(context.Schedule, (int)day)
                .ToDictionary(x => x.Sequence));
        breaks = Enum.GetValues<TimetableDay>().ToDictionary(day => day,
            day => TimetableValidationEngine.Breaks(context.Schedule, (int)day));
        // Published timetables are normally hard-valid. Calculating this once lets every proposed
        // move validate only the constraints it can change. Invalid legacy data keeps the full path.
        baselineHardErrors = EvaluateHard(context);
    }

    internal TimetableSwapEvaluation Evaluate(IReadOnlyList<RepairMovementDto> moves)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var simulated = TimetableRepairEngine.Simulate(context, moves);
        if (mode == "Substitution") TimetableSwapEngine.SuppressCoveredStandby(simulated);

        return EvaluateCurrent(simulated, moves, baselineHardErrors.Length == 0);
    }

    private TimetableSwapEvaluation EvaluateCurrent(TimetableValidationContext candidate,
        IReadOnlyList<RepairMovementDto> affectedMoves, bool useDelta)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = useDelta ? EvaluateHardDelta(candidate, affectedMoves) : EvaluateHard(candidate);
        if (errors.Length > 0) return new(errors, []);

        var affectedEntries = affectedMoves.Select(x => x.EntryId).ToHashSet();
        var affectedTeachers = affectedMoves.SelectMany(x => new[] { x.FromTeacherId, x.ToTeacherId }).ToHashSet();
        var affectedRequirements = candidate.Timetable.Entries
            .Where(x => affectedEntries.Contains(x.Id) && x.ClassSubjectRequirementId.HasValue)
            .Select(x => x.ClassSubjectRequirementId!.Value).ToHashSet();
        return new(errors, EvaluateSoft(candidate, affectedEntries, affectedTeachers, affectedRequirements));
    }

    private string[] EvaluateHardDelta(TimetableValidationContext candidate,
        IReadOnlyList<RepairMovementDto> moves)
    {
        var found = new OrderedMessages();
        var entries = candidate.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToArray();
        var movedIds = moves.Select(x => x.EntryId).ToHashSet();
        var movedEntries = entries.Where(x => movedIds.Contains(x.Id)).ToArray();

        // A valid baseline means a newly introduced collision must involve at least one moved row.
        var checkedPairs = new HashSet<(int Left, int Right)>();
        foreach (var moved in movedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var other in entries)
            {
                if (moved.Id == other.Id) continue;
                var pair = moved.Id < other.Id ? (moved.Id, other.Id) : (other.Id, moved.Id);
                if (!checkedPairs.Add(pair) || !Overlaps(moved, other)) continue;
                if (moved.InstructorProfileId > 0 && moved.InstructorProfileId == other.InstructorProfileId)
                    found.Add(TeacherCollision);
                if (CoOccurrence(moved, other)) continue;
                if (moved.ClassroomId.HasValue && moved.ClassroomId == other.ClassroomId)
                    found.Add(ClassroomCollision);
                if (moved.RoomId.HasValue && moved.RoomId == other.RoomId)
                    found.Add(RoomCollision);
            }
            ValidateEntry(moved, found);
        }

        var affectedTeachers = moves.SelectMany(x => new[] { x.FromTeacherId, x.ToTeacherId }).ToHashSet();
        foreach (var teacherId in affectedTeachers)
        {
            if (!teachers.TryGetValue(teacherId, out var teacher)) continue;
            var count = entries.Count(x => x.InstructorProfileId == teacherId && x.EntryType == TimetableEntryType.Lesson);
            if (count > teacher.MaximumWeeklyPeriods)
                found.Add($"نصاب المعلم {count} يتجاوز الحد {teacher.MaximumWeeklyPeriods}.");
        }

        var affectedRequirements = movedEntries.Where(x => x.ClassSubjectRequirementId.HasValue)
            .Select(x => x.ClassSubjectRequirementId!.Value).Distinct();
        foreach (var requirementId in affectedRequirements)
            if (requirements.TryGetValue(requirementId, out var requirement) && requirement.Classroom.IsActive &&
                !requirement.Classroom.IsDeleted && requirement.Subject.IsActive && !requirement.Subject.IsDeleted)
                ValidateRequirement(requirement, entries, found);

        return found.ToArray();
    }

    private string[] EvaluateHard(TimetableValidationContext candidate)
    {
        var found = new OrderedMessages();
        var entries = candidate.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToArray();
        if (context.Setup is null || context.Setup.IsDeleted || context.Setup.SchoolId != context.Timetable.SchoolId ||
            context.Setup.AcademicYearId != context.Timetable.AcademicYearId || context.Setup.Semester != context.Timetable.Semester ||
            context.Schedule is null || context.Schedule.SchoolId != context.Timetable.SchoolId ||
            context.Schedule.BellScheduleTemplateId != context.Setup.BellScheduleTemplateId ||
            context.Schedule.Template.IsDeleted || !context.Schedule.Template.IsActive ||
            context.Schedule.Revision != context.Schedule.Template.Revision)
            found.Add(InvalidSetup);

        CheckResourceCollisions(entries, found);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateEntry(entry, found);
        }

        foreach (var teacher in context.Teachers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lessons = entries.Where(x => x.InstructorProfileId == teacher.InstructorProfileId &&
                x.EntryType == TimetableEntryType.Lesson).ToArray();
            if (lessons.Length > teacher.MaximumWeeklyPeriods)
                found.Add($"نصاب المعلم {lessons.Length} يتجاوز الحد {teacher.MaximumWeeklyPeriods}.");
        }

        foreach (var requirement in context.Requirements.Where(x => !x.IsDeleted && x.Classroom.IsActive &&
                     !x.Classroom.IsDeleted && x.Subject.IsActive && !x.Subject.IsDeleted))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequirement(requirement, entries, found);
        }
        return found.ToArray();
    }

    private void ValidateEntry(SchoolTimetableEntry entry, OrderedMessages found)
    {
        var period = Period(entry);
        if (period is null || period.EndLocalTime <= period.StartLocalTime || breaks[entry.Day].Any(x =>
                x.Window.StartLocalTime < period.EndLocalTime && period.StartLocalTime < x.Window.EndLocalTime))
            found.Add(InvalidPeriod);

        teachers.TryGetValue(entry.InstructorProfileId, out var teacher);
        if (teacher is not null && (teacher.BellScheduleRevisionId != context.Schedule?.Id || period is not null && teacher.Slots.Any(slot =>
                slot.Day == (int)entry.Day && !slot.IsAvailable &&
                (slot.BellPeriodId == period.Id || slot.Period.StartLocalTime < period.EndLocalTime &&
                 period.StartLocalTime < slot.Period.EndLocalTime))))
            found.Add(UnavailableTeacher);
        if (entry.SchoolId != context.Timetable.SchoolId || teacher is null || !teacher.Instructor.IsActive ||
            teacher.Instructor.IsDeleted || !teacher.Instructor.User.IsActive || teacher.Instructor.User.IsDeleted ||
            teacher.Instructor.SchoolId != context.Timetable.SchoolId)
            found.Add(InvalidTeacher);

        if (entry.EntryType != TimetableEntryType.Lesson) return;
        requirements.TryGetValue(entry.ClassSubjectRequirementId ?? 0, out var requirement);
        if (requirement is null || requirement.ClassroomId != entry.ClassroomId || requirement.SubjectId != entry.SubjectId ||
            !classrooms.TryGetValue(entry.ClassroomId ?? 0, out var classroom) || !classroom.IsActive || classroom.IsDeleted ||
            classroom.AcademicYearId != context.Timetable.AcademicYearId ||
            !subjects.TryGetValue(entry.SubjectId ?? 0, out var subject) || !subject.IsActive || subject.IsDeleted ||
            entry.RoomId.HasValue && (!rooms.TryGetValue(entry.RoomId.Value, out var room) || !room.IsActive || room.IsDeleted))
            found.Add(InvalidLesson);
        if (requirement is null) return;

        assignments.TryGetValue(requirement.Id, out var assignment);
        teachers.TryGetValue(AssignedTeacher(entry), out var assignedProfile);
        if (assignment is null || !assignment.Members.Any(x => x.TeacherTimetableProfileId == assignedProfile?.Id))
            found.Add(MissingAssignment);
        if (requirement.AllowedDays.Count > 0 && requirement.AllowedDays.All(x => x.Day != (int)entry.Day))
            found.Add(DisallowedDay);
        if (requirement.Rooms.Any(x => !x.IsPreferred) &&
            !requirement.Rooms.Any(x => !x.IsPreferred && x.RoomId == entry.RoomId))
            found.Add(RequiredRoom);
    }

    private void ValidateRequirement(ClassSubjectRequirement requirement, SchoolTimetableEntry[] entries,
        OrderedMessages found)
    {
        var all = entries.Where(x => x.ClassSubjectRequirementId == requirement.Id &&
            x.EntryType == TimetableEntryType.Lesson).ToArray();
        var logical = Logical(all);
        if (logical.Length != requirement.TotalWeeklyPeriods)
            found.Add($"المطلوب {requirement.TotalWeeklyPeriods} حصة والمجدول {logical.Length}.");
        if (logical.Length < requirement.TotalWeeklyPeriods) found.Add(MissingOccurrence);
        assignments.TryGetValue(requirement.Id, out var assignment);
        if (assignment is null || assignment.Members.Count == 0)
        {
            found.Add(UnassignedRequirement);
        }
        else
        {
            foreach (var member in assignment.Members)
            {
                var teacher = context.Teachers.SingleOrDefault(x => x.Id == member.TeacherTimetableProfileId);
                var owned = all.Where(x => AssignedTeacher(x) == teacher?.InstructorProfileId)
                    .OrderBy(x => x.Day).ThenBy(x => x.Period).ToArray();
                if (owned.Length != member.AllocatedPeriodCount || assignment.Mode == "CoTeaching" &&
                    logical.Any(x => !owned.Any(o => o.Day == x.Day && o.Period == x.Period)))
                    found.Add(AllocationMismatch);
                if (PairCount(owned) < member.AllocatedPairedBlockCount) found.Add(BrokenPair);
            }
        }
        if (PairCount(logical) < requirement.PairedBlockCount) found.Add(BrokenPair);
        foreach (var slot in requirement.FixedSlots.Where(slot =>
                     !logical.Any(x => (int)x.Day == slot.Day && x.Period == slot.Period)))
            found.Add($"الموعد الثابت غير محقق: اليوم {slot.Day}، الحصة {slot.Period}.");
    }

    private string[] EvaluateSoft(TimetableValidationContext candidate, IReadOnlySet<int> affectedEntries,
        IReadOnlySet<int> affectedTeachers, IReadOnlySet<int> affectedRequirements)
    {
        var found = new OrderedMessages();
        var entries = candidate.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToArray();

        foreach (var entry in entries.Where(x => x.EntryType == TimetableEntryType.Lesson &&
                     (affectedEntries.Contains(x.Id) || affectedTeachers.Contains(x.InstructorProfileId))))
        {
            if (requirements.TryGetValue(entry.ClassSubjectRequirementId ?? 0, out var requirement) &&
                requirement.TimePreference == "Early" && entry.Period > (requirement.LatestPreferredPeriodSequence ?? 3))
                found.Add(EarlyPreference);
        }

        foreach (var teacherId in affectedTeachers.Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lessons = entries.Where(x => x.InstructorProfileId == teacherId && x.EntryType == TimetableEntryType.Lesson).ToArray();
            var lastPeriods = lessons.Where(x => x.Period == periods[x.Day].Values.OrderBy(p => p.Sequence).LastOrDefault()?.Sequence).ToArray();
            if (lastPeriods.Length > 2) found.Add(UnfairLastPeriods);
            foreach (var day in lessons.GroupBy(x => x.Day))
            {
                var sorted = day.OrderBy(x => x.Period).ToArray();
                if (sorted.Length == 0) continue;
                var consecutive = 1;
                var maximum = 1;
                for (var index = 1; index < sorted.Length; index++)
                {
                    consecutive = Consecutive(sorted[index - 1], sorted[index]) ? consecutive + 1 : 1;
                    maximum = Math.Max(maximum, consecutive);
                }
                if (maximum > 3) found.Add(ExcessiveConsecutive);
                var gaps = periods[day.Key].Values.Count(period => period.Sequence > sorted[0].Period &&
                    period.Sequence < sorted[^1].Period && !entries.Any(x => x.InstructorProfileId == teacherId &&
                        x.Day == day.Key && x.Period == period.Sequence));
                if (gaps > 2) found.Add(ExcessiveGaps);
            }
        }

        foreach (var requirementId in affectedRequirements.Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!requirements.TryGetValue(requirementId, out var requirement)) continue;
            var all = entries.Where(x => x.ClassSubjectRequirementId == requirement.Id &&
                x.EntryType == TimetableEntryType.Lesson).ToArray();
            var logical = Logical(all);
            var remainingPairs = requirement.PairedBlockCount;
            foreach (var day in logical.GroupBy(x => x.Day))
            {
                var rows = day.ToArray();
                var paired = Math.Min(remainingPairs, PairCount(rows));
                remainingPairs -= paired;
                if (rows.Length > 1 && (paired == 0 || rows.Length != 2 * paired)) found.Add(SubjectConcentration);
            }
            if (logical.Length > 1 && requirement.TimePreference == "None" &&
                (logical.All(IsEarly) || logical.All(x => !IsEarly(x))) &&
                requirement.FixedSlots.Count < logical.Length && HasBalancedAlternative(candidate, logical))
                found.Add(ClusterImbalance);
        }
        return found.ToArray();
    }

    private bool HasBalancedAlternative(TimetableValidationContext candidate, SchoolTimetableEntry[] lessons)
    {
        var early = lessons.All(IsEarly);
        foreach (var lesson in lessons)
        foreach (var period in periods[lesson.Day].Values.Where(period => IsEarly(lesson.Day, period.Sequence) != early))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var moves = candidate.Timetable.Entries.Where(entry => !entry.IsDeleted &&
                    entry.ClassSubjectRequirementId == lesson.ClassSubjectRequirementId && entry.Day == lesson.Day &&
                    entry.Period == lesson.Period)
                .Select(entry => new RepairMovementDto(entry.Id, entry.Day, entry.Period, entry.InstructorProfileId,
                    entry.Day, period.Sequence, entry.InstructorProfileId)).ToArray();
            var alternative = TimetableRepairEngine.Simulate(candidate, moves);
            if (EvaluateHardDelta(alternative, moves).Length == 0) return true;
        }
        return false;
    }

    private void CheckResourceCollisions(SchoolTimetableEntry[] entries, OrderedMessages found)
    {
        if (HasCollision(entries.Where(x => x.InstructorProfileId > 0), x => x.InstructorProfileId, false))
            found.Add(TeacherCollision);
        if (HasCollision(entries.Where(x => x.ClassroomId.HasValue), x => x.ClassroomId!.Value, true))
            found.Add(ClassroomCollision);
        if (HasCollision(entries.Where(x => x.RoomId.HasValue), x => x.RoomId!.Value, true))
            found.Add(RoomCollision);
    }

    private bool HasCollision(IEnumerable<SchoolTimetableEntry> source, Func<SchoolTimetableEntry, int> resource,
        bool allowCoOccurrence)
    {
        foreach (var group in source.GroupBy(x => (x.Day, Resource: resource(x))))
        {
            var rows = group.OrderBy(x => x.Period).ThenBy(x => x.Id).ToArray();
            for (var left = 0; left < rows.Length; left++)
            for (var right = left + 1; right < rows.Length; right++)
            {
                if (!Overlaps(rows[left], rows[right])) continue;
                if (!allowCoOccurrence || !CoOccurrence(rows[left], rows[right])) return true;
            }
        }
        return false;
    }

    private bool CoOccurrence(SchoolTimetableEntry left, SchoolTimetableEntry right)
    {
        if (!left.ClassSubjectRequirementId.HasValue || left.ClassSubjectRequirementId != right.ClassSubjectRequirementId ||
            left.Day != right.Day || left.Period != right.Period || left.RoomId != right.RoomId ||
            left.InstructorProfileId == right.InstructorProfileId ||
            !assignments.TryGetValue(left.ClassSubjectRequirementId.Value, out var assignment) || assignment.Mode != "CoTeaching")
            return false;
        teachers.TryGetValue(AssignedTeacher(left), out var leftTeacher);
        teachers.TryGetValue(AssignedTeacher(right), out var rightTeacher);
        return assignment.Members.Any(x => x.TeacherTimetableProfileId == leftTeacher?.Id) &&
               assignment.Members.Any(x => x.TeacherTimetableProfileId == rightTeacher?.Id);
    }

    private bool Overlaps(SchoolTimetableEntry left, SchoolTimetableEntry right)
    {
        if (left.Day != right.Day) return false;
        if (left.Period == right.Period) return true;
        var leftPeriod = Period(left);
        var rightPeriod = Period(right);
        return leftPeriod is not null && rightPeriod is not null && leftPeriod.StartLocalTime < rightPeriod.EndLocalTime &&
               rightPeriod.StartLocalTime < leftPeriod.EndLocalTime;
    }

    private BellPeriod? Period(SchoolTimetableEntry entry) => periods[entry.Day].GetValueOrDefault(entry.Period);
    private int AssignedTeacher(SchoolTimetableEntry entry) =>
        scheduledTeachers?.GetValueOrDefault(entry.Id, entry.InstructorProfileId) ?? entry.InstructorProfileId;
    private static SchoolTimetableEntry[] Logical(IEnumerable<SchoolTimetableEntry> entries) => entries
        .GroupBy(x => (x.Day, x.Period)).Select(x => x.OrderBy(e => e.InstructorProfileId).First())
        .OrderBy(x => x.Day).ThenBy(x => x.Period).ToArray();

    private int PairCount(SchoolTimetableEntry[] rows)
    {
        var pairs = 0;
        for (var index = 0; index < rows.Length - 1; index++)
            if (IsPair(rows[index], rows[index + 1])) { pairs++; index++; }
        return pairs;
    }

    private bool IsPair(SchoolTimetableEntry left, SchoolTimetableEntry right) =>
        left.Day == right.Day && right.Period == left.Period + 1 &&
        left.InstructorProfileId == right.InstructorProfileId && left.RoomId == right.RoomId &&
        Period(left) is { } leftPeriod && Period(right) is { } rightPeriod &&
        leftPeriod.EndLocalTime == rightPeriod.StartLocalTime;

    private bool Consecutive(SchoolTimetableEntry left, SchoolTimetableEntry right) =>
        left.Day == right.Day && right.Period == left.Period + 1 && !breaks[left.Day].Any(item =>
            Period(left) is { } leftPeriod && Period(right) is { } rightPeriod &&
            item.Window.StartLocalTime < rightPeriod.StartLocalTime && item.Window.EndLocalTime > leftPeriod.EndLocalTime);

    private bool IsEarly(SchoolTimetableEntry entry) => IsEarly(entry.Day, entry.Period);
    private bool IsEarly(TimetableDay day, int period) =>
        period <= Math.Max(1, periods[day].Count / 2);

    private sealed class OrderedMessages
    {
        private readonly List<string> values = [];
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);
        internal void Add(string value) { if (seen.Add(value)) values.Add(value); }
        internal string[] ToArray() => values.ToArray();
    }
}
