using AlFalah.Application.DTOs.Timetables;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>
/// Deterministic constraint solver for a complete timetable. Hard constraints are
/// applied while searching; soft timing preferences only rank valid candidates.
/// </summary>
public sealed class TimetableGenerationEngine
{
    private const int SearchLimit = 2_000_000;

    public IReadOnlyList<TimetableEntryDto> Generate(TimetableValidationContext context, CancellationToken cancellationToken)
    {
        if (context.Setup is null || context.Schedule is null || context.Timetable.TimetableSetupProfileId != context.Setup.Id)
            throw new ArgumentException("اربط الجدول بملف إعداد وتوقيت صالح قبل التوليد.");

        var requirements = context.Requirements.Where(r => !r.IsDeleted && r.TotalWeeklyPeriods > 0).OrderBy(r => r.Id).ToArray();
        if (requirements.Length == 0) throw new ArgumentException("أضف متطلبات المواد قبل توليد الجدول.");
        var teachers = context.Teachers.ToDictionary(x => x.Id);
        var assignments = context.Assignments.Where(x => !x.IsDeleted).ToDictionary(x => x.ClassSubjectRequirementId);
        var blocks = new List<Block>();

        foreach (var requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!assignments.TryGetValue(requirement.Id, out var assignment) || assignment.Members.Count == 0)
                throw new ArgumentException($"أكمل إسناد مادة {requirement.Subject.Name} للفصل {requirement.Classroom.ClassLabel} قبل التوليد.");
            TeachingAllocationPolicy.Validate(requirement, new(requirement.Id, assignment.Mode,
                assignment.Members.Select(m => new DTOs.TeachingMemberRequest(m.TeacherTimetableProfileId,
                    m.AllocatedPeriodCount, m.AllocatedPairedBlockCount)).ToArray()));

            if (assignment.Members.Any(member => !teachers.ContainsKey(member.TeacherTimetableProfileId)))
                throw new ArgumentException("أحد إسنادات المواد مرتبط بمعلم غير موجود في ملف الإعداد.");

            var requirementBlocks = BuildBlocks(requirement, assignment, teachers);
            BindFixedSlots(requirement, requirementBlocks, context);
            AssignSymmetryGroups(requirementBlocks);
            blocks.AddRange(requirementBlocks);
        }

        var expectedLoads = blocks.SelectMany(block => block.TeacherIds.Select(id => (id, block.Length)))
            .GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.Sum(item => item.Length));
        foreach (var profile in context.Teachers)
        {
            if (profile.BellScheduleRevisionId != context.Schedule.Id)
                throw new ArgumentException($"راجع إتاحة المعلم {TeacherName(profile)} بعد تعديل توقيت الجدول.");
            if (expectedLoads.GetValueOrDefault(profile.InstructorProfileId) > profile.MaximumWeeklyPeriods)
                throw new ArgumentException($"نصاب المعلم {TeacherName(profile)} يتجاوز الحد الأسبوعي المسموح.");
        }

        foreach (var block in blocks)
        {
            block.Candidates.AddRange(BuildCandidates(block, context));
            if (block.Candidates.Count == 0)
                throw new ArgumentException($"لا يوجد موعد صالح لمادة {block.Requirement.Subject.Name} في الفصل {block.Requirement.Classroom.ClassLabel}.");
        }

        var state = new SearchState(context, blocks);
        if (!Search(state, cancellationToken))
            throw new ArgumentException("تعذر إنشاء جدول يحقق جميع القيود الإلزامية. راجع التثبيتات، إتاحة المعلمين، الغرف وأنصبة المواد.");

        return blocks.OrderBy(x => x.Requirement.ClassroomId).ThenBy(x => x.Requirement.Id).ThenBy(x => x.Id)
            .SelectMany(block => state.Placements[block.Id].Periods.SelectMany(period => block.TeacherIds.Select(teacherId =>
                new TimetableEntryDto(teacherId, (TimetableDay)state.Placements[block.Id].Day, period,
                    TimetableEntryType.Lesson, block.Requirement.Classroom.ClassLabel, block.Requirement.Subject.Name,
                    block.Requirement.ClassroomId, block.Requirement.SubjectId, block.Requirement.Id,
                    state.Placements[block.Id].RoomId, block.Requirement.Subject.Color))))
            .ToArray();
    }

    private static List<Block> BuildBlocks(ClassSubjectRequirement requirement, TeachingAssignment assignment,
        IReadOnlyDictionary<int, TeacherTimetableProfile> teachers)
    {
        var result = new List<Block>();
        var nextId = requirement.Id * 1000;
        void Add(int length, IEnumerable<TeachingAssignmentMember> members)
        {
            var ids = members.Select(member => teachers[member.TeacherTimetableProfileId].InstructorProfileId).Order().ToArray();
            result.Add(new(++nextId, requirement, ids, length));
        }

        if (assignment.Mode == "SplitQuota")
        {
            foreach (var member in assignment.Members.OrderBy(x => x.Id))
            {
                for (var i = 0; i < member.AllocatedPairedBlockCount; i++) Add(2, [member]);
                for (var i = 0; i < member.AllocatedPeriodCount - 2 * member.AllocatedPairedBlockCount; i++) Add(1, [member]);
            }
        }
        else
        {
            for (var i = 0; i < requirement.PairedBlockCount; i++) Add(2, assignment.Members);
            for (var i = 0; i < requirement.IndividualPeriodCount; i++) Add(1, assignment.Members);
        }
        return result;
    }

    private static void BindFixedSlots(ClassSubjectRequirement requirement, List<Block> blocks, TimetableValidationContext context)
    {
        var remaining = requirement.FixedSlots.OrderBy(x => x.Day).ThenBy(x => x.Period)
            .Select(x => new Slot(x.Day, x.Period)).ToList();
        var pairs = blocks.Where(x => x.Length == 2).ToList();
        for (var i = 0; i + 1 < remaining.Count && pairs.Count > 0; i++)
        {
            var first = remaining[i]; var second = remaining[i + 1];
            if (first.Day != second.Day || !Adjacent(context, first.Day, first.Period, second.Period)) continue;
            pairs[0].Forced.Add(first); pairs[0].Forced.Add(second); pairs.RemoveAt(0);
            remaining.RemoveAt(i + 1); remaining.RemoveAt(i); i--;
        }
        foreach (var slot in remaining)
        {
            var block = blocks.FirstOrDefault(x => x.Forced.Count == 0 && x.Length == 1)
                ?? blocks.FirstOrDefault(x => x.Forced.Count == 0 && x.Length == 2)
                ?? throw new ArgumentException("الحصص المثبتة تتجاوز النصاب الأسبوعي للمادة.");
            block.Forced.Add(slot);
        }
    }

    private static void AssignSymmetryGroups(IEnumerable<Block> blocks)
    {
        foreach (var group in blocks.Where(x => x.Forced.Count == 0)
            .GroupBy(x => $"{x.Requirement.Id}:{x.Length}:{string.Join('-', x.TeacherIds)}"))
        {
            Block? previous = null;
            foreach (var block in group.OrderBy(x => x.Id))
            {
                block.PreviousEquivalentId = previous?.Id;
                previous = block;
            }
        }
    }

    private static IEnumerable<Candidate> BuildCandidates(Block block, TimetableValidationContext context)
    {
        var allowedDays = block.Requirement.AllowedDays.Count == 0
            ? null : block.Requirement.AllowedDays.Select(x => x.Day).ToHashSet();
        var allFixed = block.Requirement.FixedSlots.Select(x => new Slot(x.Day, x.Period)).ToHashSet();
        var rooms = block.Requirement.Rooms.Where(x => !x.IsPreferred).Select(x => (int?)x.RoomId).Order().ToArray();
        if (rooms.Length == 0) rooms = block.Requirement.Rooms.OrderByDescending(x => x.IsPreferred).ThenBy(x => x.RoomId)
            .Select(x => (int?)x.RoomId).ToArray();
        if (rooms.Length == 0) rooms = [null];

        foreach (var day in context.Schedule!.Days.Where(x => x.Day > 0 && x.IsStudyDay && (allowedDays is null || allowedDays.Contains(x.Day))).OrderBy(x => x.Day))
        {
            var periods = TimetableValidationEngine.Periods(context.Schedule, day.Day).OrderBy(x => x.Sequence).ToArray();
            for (var index = 0; index < periods.Length; index++)
            {
                BellPeriod[] selected = block.Length == 1 ? [periods[index]] : index + 1 < periods.Length &&
                    periods[index + 1].Sequence == periods[index].Sequence + 1 && periods[index].EndLocalTime == periods[index + 1].StartLocalTime
                    ? [periods[index], periods[index + 1]] : [];
                if (selected.Length != block.Length) continue;
                var slots = selected.Select(x => new Slot(day.Day, x.Sequence)).ToArray();
                if (block.Forced.Any(force => !slots.Contains(force))) continue;
                if (slots.Any(slot => allFixed.Contains(slot) && !block.Forced.Contains(slot))) continue;
                if (block.TeacherIds.Any(id => !TeacherAvailable(context, id, day.Day, selected))) continue;
                foreach (var room in rooms)
                    yield return new(day.Day, selected.Select(x => x.Sequence).ToArray(), room,
                        CandidateScore(context, block, day.Day, selected.Select(x => x.Sequence)));
            }
        }
    }

    private static bool Search(SearchState state, CancellationToken cancellationToken)
    {
        if (state.Placements.Count == state.Blocks.Count) return true;
        if (++state.Visited > SearchLimit) return false;
        if ((state.Visited & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();

        Block? selected = null; List<Candidate>? candidates = null;
        foreach (var block in state.Blocks)
        {
            if (state.Placements.ContainsKey(block.Id) || block.PreviousEquivalentId is int previous && !state.Placements.ContainsKey(previous)) continue;
            var feasible = block.Candidates.Where(candidate => state.CanPlace(block, candidate)).
                OrderBy(candidate => state.Score(block, candidate)).ThenBy(x => x.Day).ThenBy(x => x.Periods[0]).ThenBy(x => x.RoomId).ToList();
            if (feasible.Count == 0) return false;
            if (candidates is null || feasible.Count < candidates.Count || feasible.Count == candidates.Count && block.Forced.Count > selected!.Forced.Count)
            { selected = block; candidates = feasible; }
        }
        if (selected is null || candidates is null) return false;
        foreach (var candidate in candidates)
        {
            state.Place(selected, candidate);
            if (Search(state, cancellationToken)) return true;
            state.Remove(selected, candidate);
        }
        return false;
    }

    private static bool TeacherAvailable(TimetableValidationContext context, int instructorId, int day, IReadOnlyList<BellPeriod> periods)
    {
        var teacher = context.Teachers.SingleOrDefault(x => x.InstructorProfileId == instructorId);
        return teacher is not null && teacher.Instructor.IsActive && !teacher.Instructor.IsDeleted && teacher.Instructor.User.IsActive &&
            !teacher.Instructor.User.IsDeleted && teacher.Slots.Where(x => x.Day == day && !x.IsAvailable).All(slot =>
                periods.All(period => slot.Period.EndLocalTime <= period.StartLocalTime || period.EndLocalTime <= slot.Period.StartLocalTime));
    }

    private static int CandidateScore(TimetableValidationContext context, Block block, int day, IEnumerable<int> periods)
    {
        var list = periods.ToArray();
        var old = context.Timetable.Entries.Where(x => !x.IsDeleted && x.ClassSubjectRequirementId == block.Requirement.Id)
            .Select(x => new Slot((int)x.Day, x.Period)).ToHashSet();
        var retained = list.Count(period => old.Contains(new(day, period)));
        var preference = list.Sum(period => block.Requirement.TimePreference switch
        {
            "Early" => Math.Max(0, period - (block.Requirement.LatestPreferredPeriodSequence ?? period)),
            "Late" => Math.Max(0, (block.Requirement.EarliestPeriodSequence ?? period) - period),
            _ => 0
        });
        return preference * 10 - retained * 100;
    }

    private static bool Adjacent(TimetableValidationContext context, int day, int first, int second)
    {
        var periods = TimetableValidationEngine.Periods(context.Schedule, day);
        var a = periods.SingleOrDefault(x => x.Sequence == first); var b = periods.SingleOrDefault(x => x.Sequence == second);
        return a is not null && b is not null && b.Sequence == a.Sequence + 1 && a.EndLocalTime == b.StartLocalTime;
    }

    private static string TeacherName(TeacherTimetableProfile profile) =>
        $"{profile.Instructor.User.FirstName} {profile.Instructor.User.LastName}".Trim();

    private sealed class SearchState
    {
        private readonly HashSet<(int Resource, int Day, int Period)> _classes = [];
        private readonly HashSet<(int Resource, int Day, int Period)> _teachers = [];
        private readonly HashSet<(int Resource, int Day, int Period)> _rooms = [];
        private readonly Dictionary<(int Requirement, int Day), int> _requirementDays = [];
        public SearchState(TimetableValidationContext context, IReadOnlyList<Block> blocks) { Context = context; Blocks = blocks; }
        public TimetableValidationContext Context { get; }
        public IReadOnlyList<Block> Blocks { get; }
        public Dictionary<int, Candidate> Placements { get; } = [];
        public int Visited { get; set; }

        public bool CanPlace(Block block, Candidate candidate)
        {
            if (block.PreviousEquivalentId is int previous && Placements.TryGetValue(previous, out var prior) &&
                (candidate.Day < prior.Day || candidate.Day == prior.Day && candidate.Periods[0] <= prior.Periods[0])) return false;
            foreach (var period in candidate.Periods)
            {
                if (_classes.Contains((block.Requirement.ClassroomId, candidate.Day, period))) return false;
                if (block.TeacherIds.Any(id => _teachers.Contains((id, candidate.Day, period)))) return false;
                if (candidate.RoomId is int room && _rooms.Contains((room, candidate.Day, period))) return false;
            }
            return true;
        }

        public int Score(Block block, Candidate candidate) => candidate.BaseScore +
            _requirementDays.GetValueOrDefault((block.Requirement.Id, candidate.Day)) * 25 +
            block.TeacherIds.Sum(id => candidate.Periods.Any(period => IsLast(candidate.Day, period)) ?
                _teachers.Count(x => x.Resource == id && IsLast(x.Day, x.Period)) * 5 : 0);

        public void Place(Block block, Candidate candidate)
        {
            Placements.Add(block.Id, candidate);
            foreach (var period in candidate.Periods)
            {
                _classes.Add((block.Requirement.ClassroomId, candidate.Day, period));
                foreach (var teacher in block.TeacherIds) _teachers.Add((teacher, candidate.Day, period));
                if (candidate.RoomId is int room) _rooms.Add((room, candidate.Day, period));
            }
            _requirementDays[(block.Requirement.Id, candidate.Day)] = _requirementDays.GetValueOrDefault((block.Requirement.Id, candidate.Day)) + 1;
        }

        public void Remove(Block block, Candidate candidate)
        {
            Placements.Remove(block.Id);
            foreach (var period in candidate.Periods)
            {
                _classes.Remove((block.Requirement.ClassroomId, candidate.Day, period));
                foreach (var teacher in block.TeacherIds) _teachers.Remove((teacher, candidate.Day, period));
                if (candidate.RoomId is int room) _rooms.Remove((room, candidate.Day, period));
            }
            var key = (block.Requirement.Id, candidate.Day); var count = _requirementDays[key] - 1;
            if (count == 0) _requirementDays.Remove(key); else _requirementDays[key] = count;
        }

        private bool IsLast(int day, int period) => TimetableValidationEngine.Periods(Context.Schedule, day).LastOrDefault()?.Sequence == period;
    }

    private sealed class Block(int id, ClassSubjectRequirement requirement, int[] teacherIds, int length)
    {
        public int Id { get; } = id;
        public ClassSubjectRequirement Requirement { get; } = requirement;
        public int[] TeacherIds { get; } = teacherIds;
        public int Length { get; } = length;
        public List<Slot> Forced { get; } = [];
        public List<Candidate> Candidates { get; } = [];
        public int? PreviousEquivalentId { get; set; }
    }
    private sealed record Candidate(int Day, int[] Periods, int? RoomId, int BaseScore);
    private readonly record struct Slot(int Day, int Period);
}
