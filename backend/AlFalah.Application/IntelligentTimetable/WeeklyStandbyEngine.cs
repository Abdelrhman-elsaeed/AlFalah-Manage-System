using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Pure weekly reserve preparation. Teaching is an immutable input, never solver output.</summary>
public sealed class WeeklyStandbyEngine(TimetableValidationEngine validator)
{
    public static string Key(int teacher, int day, int period) => $"{teacher}:{day}:{period}";
    public static StandbyPlacement Placement(SchoolTimetableEntry e) => new(e.InstructorProfileId,
        (int)e.Day, e.Period, e.StandbySource is null || e.IsStandbyLocked, e.StandbySource ?? "Manual");

    public WeeklyStandbyWorkbenchDto Inspect(TimetableValidationContext c, WeeklyStandbySettings settings,
        IReadOnlyList<StandbyPlacement>? placements = null, IReadOnlyCollection<string>? protectedCells = null)
    {
        var profiles = c.Teachers.Where(t => t.SchoolId == c.Timetable.SchoolId &&
            t.TimetableSetupProfileId == c.Timetable.TimetableSetupProfileId &&
            t.Instructor.SchoolId == c.Timetable.SchoolId && t.Instructor.IsActive && !t.Instructor.IsDeleted && t.Instructor.User.IsActive && !t.Instructor.User.IsDeleted)
            .OrderBy(t => t.InstructorProfileId).ToArray();
        settings = Normalize(settings, profiles.Select(x => x.InstructorProfileId).ToHashSet());
        var selected = placements ?? c.Timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Standby)
            .Select(Placement).OrderBy(x => x.InstructorProfileId).ThenBy(x => x.Day).ThenBy(x => x.Period).ToArray();
        var protectedSet = (protectedCells ?? []).ToHashSet(StringComparer.Ordinal);
        selected = selected.Select(p => protectedSet.Contains(Key(p.InstructorProfileId, p.Day, p.Period)) ? p with { IsLocked = true } : p).ToArray();
        var lessons = c.Timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Lesson).ToArray();
        var lessonContext = TimetableRepairEngine.Simulate(c, []);
        lessonContext.Timetable.Entries = lessonContext.Timetable.Entries.Where(x => x.EntryType == TimetableEntryType.Lesson).ToList();
        var issues = validator.Evaluate(lessonContext, includeSoft: false).Select(x => x.MessageAr).Distinct().ToList();
        if (lessons.Length == 0 || c.Requirements.All(x => x.IsDeleted))
            issues.Insert(0, "أكمل توليد حصص التدريس وإسنادات المواد واحفظ الجدول أولًا.");
        if (c.Timetable.TimingsRequireRevalidation)
            issues.Add("تغيرت إعدادات الجدول؛ راجع حصص التدريس واحفظها قبل توزيع الانتظار.");
        var periods = (c.Schedule?.Days ?? []).Where(x => x.Day > 0 && x.IsStudyDay).OrderBy(x => x.Day)
            .SelectMany(d => TimetableValidationEngine.Periods(c.Schedule, d.Day).Select(p => (Day: d.Day, Bell: p))).ToArray();
        foreach (var group in periods.GroupBy(x => x.Day))
        {
            var rows = group.ToArray();
            if (rows.Any(x => x.Bell.EndLocalTime <= x.Bell.StartLocalTime) || rows.Where((x, i) =>
                    rows.Skip(i + 1).Any(y => Overlap(x.Bell, y.Bell))).Any())
                issues.Add("فترات قالب التوقيت متداخلة أو غير صالحة؛ راجع توقيت الجدول.");
        }
        var excluded = settings.ExcludedTeacherIds!.ToHashSet();
        var teachers = profiles.Select(t => {
            var owned = lessons.Where(e => e.InstructorProfileId == t.InstructorProfileId).ToArray();
            var available = 0;
            var cells = periods.Select(slot => {
                var p = slot.Bell;
                var teaching = owned.FirstOrDefault(e => (int)e.Day == slot.Day &&
                    (e.Period == p.Sequence || TimetableValidationEngine.Periods(c.Schedule, slot.Day)
                        .Any(q => q.Sequence == e.Period && Overlap(p, q))));
                var breaks = TimetableValidationEngine.Breaks(c.Schedule, slot.Day).Any(b =>
                    b.Window.StartLocalTime < p.EndLocalTime && p.StartLocalTime < b.Window.EndLocalTime);
                var unavailable = t.BellScheduleRevisionId != c.Schedule?.Id || t.Slots.Any(s =>
                    s.Day == slot.Day && !s.IsAvailable && (s.BellPeriodId == p.Id ||
                    s.Period != null && Overlap(p, s.Period)));
                if (!breaks && !unavailable) available++;
                var reason = teaching != null ? "حصة تدريس محفوظة" : breaks ? "فترة استراحة" : unavailable ? "المعلم غير متاح في هذه الفترة" :
                    excluded.Contains(t.InstructorProfileId) ? "مستبعد من توزيع الانتظار" : null;
                return new StandbyCellDto(slot.Day, p.Sequence, reason is null,
                    teaching != null ? "Lesson" : reason != null ? "Unavailable" : "Free", reason,
                    teaching?.ClassLabel, teaching?.Subject, protectedSet.Contains(Key(t.InstructorProfileId, slot.Day, p.Sequence)),
                    teaching is null && !breaks && !unavailable);
            }).ToArray();
            return new StandbyTeacherDto(t.InstructorProfileId,
                $"{t.Instructor.User.FirstName} {t.Instructor.User.LastName}".Trim(),
                owned.Select(e => (e.Day, e.Period)).Distinct().Count(), available,
                selected.Count(x => x.InstructorProfileId == t.InstructorProfileId), cells.Count(x => x.Eligible), cells);
        }).ToArray();
        var overrides = settings.Overrides!.ToDictionary(x => (x.Day, x.Period), x => x.Required);
        if (overrides.Keys.Any(k => !periods.Any(p => p.Day == k.Day && p.Bell.Sequence == k.Period)))
            throw new ArgumentException("احتياج الانتظار مرتبط بفترة غير موجودة في التوقيت الحالي.");
        var projected = periods.Select(slot => new StandbyPeriodDto(slot.Day, slot.Bell.Sequence,
            slot.Bell.StartLocalTime.ToString("HH:mm"), slot.Bell.EndLocalTime.ToString("HH:mm"),
            overrides.GetValueOrDefault((slot.Day, slot.Bell.Sequence),
                lessons.Any(e => (int)e.Day == slot.Day && e.Period == slot.Bell.Sequence) ? settings.RequiredPerPeriod : 0),
            teachers.Count(t => t.Cells.Any(x => x.Day == slot.Day && x.Period == slot.Bell.Sequence && x.Eligible)),
            selected.Count(x => x.Day == slot.Day && x.Period == slot.Bell.Sequence))).ToArray();
        return new(c.Timetable.Id, c.Timetable.Title, c.Timetable.Revision, c.Setup?.Revision,
            c.Schedule?.Id, c.Timetable.IsPublished, issues.Count == 0, issues.Distinct().ToArray(),
            settings, projected, teachers, selected);
    }

    public WeeklyStandbyWorkbenchDto Preview(TimetableValidationContext c, WeeklyStandbyRequest request,
        IReadOnlyCollection<string> protectedCells, CancellationToken ct = default)
    {
        if (request.Revision != c.Timetable.Revision) throw new BellScheduleConflictException("تغيرت مراجعة الجدول؛ أعد المعاينة.");
        if (request.Mode is not ("Manual" or "Fair")) throw new ArgumentException("طريقة توزيع الانتظار غير صالحة.");
        if (request.Settings is null || request.Placements is null || request.Placements.Any(p => p is null)) throw new ArgumentException("بيانات توزيع الانتظار مطلوبة.");
        var view = Inspect(c, request.Settings, request.Placements, protectedCells);
        if (!view.Ready) throw new ArgumentException(string.Join(" ", view.Issues));
        var cells = view.Teachers.SelectMany(t => t.Cells.Select(x => (Key: Key(t.Id, x.Day, x.Period), Cell: x)))
            .ToDictionary(x => x.Key, x => x.Cell);
        if (request.Placements.Count > cells.Count || request.Placements.GroupBy(x => Key(x.InstructorProfileId, x.Day, x.Period)).Any(g => g.Count() > 1))
            throw new ArgumentException("خانات الانتظار مكررة أو أكثر من الفترات الفعلية.");
        foreach (var item in request.Placements)
        {
            ct.ThrowIfCancellationRequested();
            if (!cells.TryGetValue(Key(item.InstructorProfileId, item.Day, item.Period), out var cell) || !cell.Eligible)
                throw new ArgumentException("إحدى خانات الانتظار تتعارض مع التدريس أو الإتاحة أو المدرسة.");
        }
        foreach (var key in protectedCells)
            if (!request.Placements.Any(p => Key(p.InstructorProfileId, p.Day, p.Period) == key && p.IsLocked))
                throw new ArgumentException("لا يمكن إزالة أو فك تثبيت انتظار مرتبط بتغطية يومية مؤكدة. راجع التكليف أولًا.");

        var existing = c.Timetable.Entries.Where(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Standby)
            .ToDictionary(x => Key(x.InstructorProfileId, (int)x.Day, x.Period));
        StandbyPlacement Canonical(StandbyPlacement p) => p with { Source = existing.TryGetValue(Key(p.InstructorProfileId, p.Day, p.Period), out var e)
            ? e.StandbySource ?? "Manual" : "Manual" };
        var placements = request.Mode == "Manual" ? request.Placements.Select(Canonical).ToList() :
            Allocate(view, request.Placements.Where(p => p.IsLocked).Select(Canonical).ToArray(), existing.Keys.ToHashSet(), ct);
        placements = placements.OrderBy(x => x.InstructorProfileId).ThenBy(x => x.Day).ThenBy(x => x.Period).ToList();
        var simulated = TimetableRepairEngine.Simulate(c, []);
        simulated.Timetable.Entries = simulated.Timetable.Entries.Where(x => x.EntryType == TimetableEntryType.Lesson).ToList();
        var nextId = simulated.Timetable.Entries.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
        foreach (var p in placements) simulated.Timetable.Entries.Add(new() { Id = nextId++, SchoolId = c.Timetable.SchoolId,
            SchoolTimetableId = c.Timetable.Id, InstructorProfileId = p.InstructorProfileId, Day = (TimetableDay)p.Day,
            Period = p.Period, EntryType = TimetableEntryType.Standby });
        var errors = validator.Evaluate(simulated, includeSoft: false);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors.Select(x => x.MessageAr).Distinct()));
        return Inspect(c, view.Settings, placements, protectedCells);
    }

    private static WeeklyStandbySettings Normalize(WeeklyStandbySettings s, HashSet<int> teachers)
    {
        if (s is null || s.RequiredPerPeriod < 0 || s.RequiredPerPeriod > Math.Max(1, teachers.Count) ||
            (s.Overrides?.Count ?? 0) > 1000 || (s.ExcludedTeacherIds?.Count ?? 0) > teachers.Count)
            throw new ArgumentException("احتياج الانتظار أكبر من عدد معلمي المدرسة أو غير صالح.");
        var overrides = s.Overrides ?? [];
        if (overrides.Any(x => x is null || x.Required < 0 || x.Required > Math.Max(1, teachers.Count)) ||
            overrides.GroupBy(x => (x.Day, x.Period)).Any(x => x.Count() > 1) ||
            (s.ExcludedTeacherIds ?? []).Any(x => !teachers.Contains(x)))
            throw new ArgumentException("راجع احتياج الفترات والمعلمين المشاركين في الانتظار.");
        return s with { Overrides = overrides.OrderBy(x => x.Day).ThenBy(x => x.Period).ToArray(),
            ExcludedTeacherIds = (s.ExcludedTeacherIds ?? []).Distinct().Order().ToArray() };
    }

    private static bool Overlap(BellPeriod a, BellPeriod b) => a.StartLocalTime < b.EndLocalTime && b.StartLocalTime < a.EndLocalTime;

    private static List<StandbyPlacement> Allocate(WeeklyStandbyWorkbenchDto view, IReadOnlyList<StandbyPlacement> locked,
        HashSet<string> previous, CancellationToken ct)
    {
        var chosen = locked.ToList();
        var fixedKeys = locked.Select(x => Key(x.InstructorProfileId, x.Day, x.Period)).ToHashSet();
        var graph = new FlowNetwork(); var source = graph.Node(); var sink = graph.Node();
        var periods = view.Periods.ToDictionary(x => (x.Day, x.Period), p => {
            var node = graph.Node();
            graph.Edge(node, sink, Math.Max(0, p.Required - locked.Count(x => x.Day == p.Day && x.Period == p.Period)), 0);
            return node;
        });
        var choices = new List<(StandbyPlacement Placement, FlowNetwork.Arc Arc)>();
        foreach (var teacher in view.Teachers)
        {
            ct.ThrowIfCancellationRequested();
            var node = graph.Node();
            var retained = locked.Count(x => x.InstructorProfileId == teacher.Id);
            var free = teacher.Cells.Where(x => x.Eligible && !fixedKeys.Contains(Key(teacher.Id, x.Day, x.Period))).ToArray();
            for (var n = 1; n <= free.Length; n++)
                graph.Edge(source, node, 1, (teacher.TeachingCount + retained + n) * 10000 / Math.Max(1, teacher.AvailablePeriods));
            foreach (var day in free.GroupBy(x => x.Day).OrderBy(x => x.Key))
            {
                var d = graph.Node();
                var busy = teacher.Cells.Count(x => x.Day == day.Key && x.Kind == "Lesson") +
                    locked.Count(x => x.InstructorProfileId == teacher.Id && x.Day == day.Key);
                for (var n = 1; n <= day.Count(); n++) graph.Edge(node, d, 1, (busy + n) * 100);
                foreach (var cell in day.OrderBy(x => x.Period))
                    choices.Add((new(teacher.Id, cell.Day, cell.Period, false, "Fair"),
                        graph.Edge(d, periods[(cell.Day, cell.Period)], 1, previous.Contains(Key(teacher.Id, cell.Day, cell.Period)) ? 0 : 1)));
            }
        }
        graph.Fill(source, sink, ct);
        chosen.AddRange(choices.Where(x => x.Arc.Capacity == 0).Select(x => x.Placement));
        return chosen;
    }

    /// <summary>Integral min-cost maximum flow. Reverse arcs allow reallocating a constrained period's last candidate.</summary>
    private sealed class FlowNetwork
    {
        internal sealed class Arc(int to, int reverse, int capacity, int cost)
        { public int To = to, Reverse = reverse, Capacity = capacity, Cost = cost; }
        private readonly List<List<Arc>> nodes = [];
        public int Node() { nodes.Add([]); return nodes.Count - 1; }
        public Arc Edge(int from, int to, int capacity, int cost)
        {
            var arc = new Arc(to, nodes[to].Count, capacity, cost);
            nodes[from].Add(arc); nodes[to].Add(new(from, nodes[from].Count - 1, 0, -cost)); return arc;
        }
        public void Fill(int source, int sink, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var distance = Enumerable.Repeat(int.MaxValue, nodes.Count).ToArray();
                var previous = new (int Node, int Arc)[nodes.Count]; var queued = new bool[nodes.Count];
                var queue = new Queue<int>(); distance[source] = 0; queue.Enqueue(source); queued[source] = true;
                while (queue.TryDequeue(out var v))
                {
                    ct.ThrowIfCancellationRequested(); queued[v] = false;
                    for (var i = 0; i < nodes[v].Count; i++)
                    {
                        var a = nodes[v][i];
                        if (a.Capacity <= 0 || distance[a.To] <= distance[v] + a.Cost) continue;
                        distance[a.To] = distance[v] + a.Cost; previous[a.To] = (v, i);
                        if (!queued[a.To]) { queued[a.To] = true; queue.Enqueue(a.To); }
                    }
                }
                if (distance[sink] == int.MaxValue) return;
                for (var v = sink; v != source;)
                {
                    var p = previous[v]; var a = nodes[p.Node][p.Arc]; a.Capacity--;
                    nodes[v][a.Reverse].Capacity++; v = p.Node;
                }
            }
        }
    }
}
