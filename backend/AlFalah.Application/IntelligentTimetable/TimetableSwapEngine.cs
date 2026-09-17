using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Server-generated structural cycles and date-specific teacher cover, simulated through Phase 7 rules.</summary>
public sealed class TimetableSwapEngine(TimetableValidationEngine validator)
{
    private const int MaxThreeWayAttemptsPerTarget = 12;

    public static SchoolTimetableEntry[] Block(TimetableValidationContext c, SchoolTimetableEntry source)
    {
        var related = c.Timetable.Entries.Where(e => !e.IsDeleted && e.EntryType == TimetableEntryType.Lesson && e.Day == source.Day &&
            (source.ClassSubjectRequirementId.HasValue ? e.ClassSubjectRequirementId == source.ClassSubjectRequirementId : e.Id == source.Id)).ToArray();
        var periods = new HashSet<int> { source.Period };
        if (c.Requirements.Any(r => r.Id == source.ClassSubjectRequirementId && r.PairedBlockCount > 0))
        {
            // Legacy entries have no occurrence id. Preserve the entire adjacent run conservatively.
            bool added;
            do { added = false; foreach (var e in related) if (periods.Contains(e.Period - 1) || periods.Contains(e.Period + 1)) added |= periods.Add(e.Period); } while (added);
        }
        return related.Where(e => periods.Contains(e.Period)).OrderBy(e => e.Id).ToArray();
    }
    public IReadOnlyList<SwapCandidateDto> Candidates(TimetableValidationContext c, int sourceId, string mode,
        DateOnly date, DateTimeOffset expires, IReadOnlyDictionary<int, int>? assigned, CancellationToken ct,
        SwapSearchScope scope = SwapSearchScope.SameDay)
    {
        if (mode is not ("Substitution" or "Swap")) throw new ArgumentException("نوع العملية غير صالح.");
        if (!Enum.IsDefined(scope) || mode == "Substitution" && scope != SwapSearchScope.SameDay)
            throw new ArgumentException("نطاق البحث غير صالح لهذه العملية.");
        var source = c.Timetable.Entries.SingleOrDefault(e => !e.IsDeleted && e.Id == sourceId && e.EntryType == TimetableEntryType.Lesson)
            ?? throw new KeyNotFoundException();
        if (source.Day != BellScheduleResolver.ToDay(date.DayOfWeek)) throw new ArgumentException("الحصة لا تقع في اليوم المختار.");
        var block = Block(c, source);
        var validation = validator.PrepareSwap(c, mode, assigned, ct);
        var candidates = new List<SwapCandidateDto>();
        string Name(int id) => c.Teachers.Where(t => t.InstructorProfileId == id).Select(t => (t.Instructor.User.FirstName + " " + t.Instructor.User.LastName).Trim()).FirstOrDefault() ?? id.ToString();
        TimetableSwapEvaluation Validate(RepairMovementDto[] moves, string[]? extraErrors = null)
        {
            ct.ThrowIfCancellationRequested();
            var result = validation.Evaluate(moves);
            var errors = result.Errors.Concat(extraErrors ?? []).Distinct().ToArray();
            return new(errors, result.Warnings);
        }
        SwapCandidateDto Create(string kind, string label, RepairMovementDto[] moves, TimetableSwapEvaluation result)
        {
            var errors = result.Errors;
            var warnings = result.Warnings;
            var color = errors.Length > 0 ? "Red" : warnings.Length > 0 ? "Yellow" : "Green";
            var preview = moves.Select(m => {
                var e = c.Timetable.Entries.Single(e => e.Id == m.EntryId);
                string Time(TimetableDay day, int p) => TimetableValidationEngine.Periods(c.Schedule, (int)day).FirstOrDefault(x => x.Sequence == p) is { } period
                    ? $"{period.StartLocalTime:HH:mm}–{period.EndLocalTime:HH:mm}" : "—";
                return new SwapPreviewDto(e.Id, Name(m.FromTeacherId), e.ClassLabel ?? "", e.Subject ?? "", m.FromPeriod, m.ToPeriod,
                    Time(m.FromDay, m.FromPeriod), Time(m.ToDay, m.ToPeriod), Name(m.ToTeacherId), e.RoomId, m.FromDay, m.ToDay);
            }).ToArray();
            // Recomputed on confirmation: any change to movements, warnings, setup or date invalidates the id.
            var evidence = JsonSerializer.Serialize(new { c.Timetable.Id, c.Timetable.Revision, SetupRevision = c.Setup?.Revision,
                ScheduleId = c.Schedule?.Id, date, expires, mode, scope, kind, sourceId, moves, errors, warnings, preview });
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
            return new(id, kind, color, label, errors, warnings, moves, preview);
        }
        if (mode == "Substitution")
        {
            foreach (var t in c.Teachers.Where(t => t.InstructorProfileId != source.InstructorProfileId &&
                t.Instructor.IsActive && !t.Instructor.IsDeleted && t.Instructor.User.IsActive && !t.Instructor.User.IsDeleted))
            {
                // Cover every period belonging to the absent member; retain the other co-teachers.
                var moves = block.Where(e => e.InstructorProfileId == source.InstructorProfileId)
                    .Select(e => Move(e, e.Day, e.Period, t.InstructorProfileId)).ToArray();
                candidates.Add(Create("Substitution", Name(t.InstructorProfileId), moves, Validate(moves)));
            }
        }
        else
        {
            var blocks = c.Timetable.Entries.Where(e => !e.IsDeleted && e.EntryType == TimetableEntryType.Lesson &&
                    (scope == SwapSearchScope.WholeTimetable || e.Day == source.Day))
                .Select(e => Block(c, e)).DistinctBy(b => b[0].Id).Where(b =>
                    (b[0].Day != block[0].Day || b.Min(e => e.Period) != block.Min(e => e.Period)) &&
                    !b.Any(e => block.Any(s => s.Id == e.Id))).ToArray();
            foreach (var target in blocks)
            {
                var targetPlace = scope == SwapSearchScope.WholeTimetable
                    ? $"{DayLabel(target[0].Day)} · الحصة {target.Min(e => e.Period)}"
                    : $"الحصة {target.Min(e => e.Period)}";
                var directMoves = Cycle(block, target);
                var direct = Create("DirectSwap", $"تبادل مع {Name(target[0].InstructorProfileId)} — {targetPlace}",
                    directMoves, Validate(directMoves));
                candidates.Add(direct);
                if (direct.Color != "Red") continue;
                // Rank feasible third participants separately; red direct proposals remain non-executable.
                var alternatives = new List<SwapCandidateDto>();
                foreach (var third in blocks.Where(b => b[0].Id != target[0].Id)
                             .OrderBy(b => BlockDistance(target, b)).ThenBy(b => BlockDistance(block, b))
                             .ThenBy(b => b[0].Id).Take(MaxThreeWayAttemptsPerTarget))
                {
                    var moves = Cycle(block, target, third);
                    var result = Validate(moves);
                    // Rejected three-way attempts are internal search states. Avoid the comparatively
                    // expensive preview, JSON evidence and SHA-256 work until a proposal is viable.
                    if (result.Errors.Length == 0)
                        alternatives.Add(Create("ThreeWaySwap", $"تبديل ثلاثي: {Name(target[0].InstructorProfileId)} ({DayLabel(target[0].Day)}) ثم {Name(third[0].InstructorProfileId)} ({DayLabel(third[0].Day)})", moves, result));
                    if (alternatives.Count >= 3) break;
                }
                candidates.AddRange(alternatives.OrderBy(x => x.Warnings.Length).Take(3));
            }
        }
        return candidates.OrderBy(x => x.Color == "Green" ? 0 : x.Color == "Yellow" ? 1 : 2)
            .ThenBy(x => x.Warnings.Length).ThenBy(x => Distance(block, x)).ToArray();
    }
    private static RepairMovementDto Move(SchoolTimetableEntry e, TimetableDay day, int period, int teacher) =>
        new(e.Id, e.Day, e.Period, e.InstructorProfileId, day, period, teacher);
    public static void SuppressCoveredStandby(TimetableValidationContext c)
    {
        var occupied = c.Timetable.Entries.Where(e => e.EntryType == TimetableEntryType.Lesson)
            .Select(e => (e.InstructorProfileId, e.Day, e.Period)).ToHashSet();
        c.Timetable.Entries = c.Timetable.Entries.Where(e => e.EntryType != TimetableEntryType.Standby ||
            !occupied.Contains((e.InstructorProfileId, e.Day, e.Period))).ToList();
    }
    private static RepairMovementDto[] Cycle(params SchoolTimetableEntry[][] blocks) => blocks.SelectMany((block, i) =>
        block.Select(e => Move(e, blocks[(i + 1) % blocks.Length][0].Day,
            blocks[(i + 1) % blocks.Length].Min(x => x.Period) + e.Period - block.Min(x => x.Period), e.InstructorProfileId)))
        .OrderBy(x => x.EntryId).ToArray();
    private static int Distance(IReadOnlyCollection<SchoolTimetableEntry> source, SwapCandidateDto candidate)
    {
        var sourceIds = source.Select(x => x.Id).ToHashSet();
        return candidate.Movements.Where(x => sourceIds.Contains(x.EntryId))
            .Select(x => Math.Abs((int)x.ToDay - (int)x.FromDay) * 100 + Math.Abs(x.ToPeriod - x.FromPeriod))
            .DefaultIfEmpty(int.MaxValue).Min();
    }
    private static int BlockDistance(IReadOnlyCollection<SchoolTimetableEntry> left,
        IReadOnlyCollection<SchoolTimetableEntry> right)
    {
        var leftDay = left.First().Day;
        var rightDay = right.First().Day;
        return Math.Abs((int)leftDay - (int)rightDay) * 100 +
               Math.Abs(left.Min(x => x.Period) - right.Min(x => x.Period));
    }
    private static string DayLabel(TimetableDay day) => day switch
    {
        TimetableDay.Saturday => "السبت", TimetableDay.Sunday => "الأحد", TimetableDay.Monday => "الاثنين",
        TimetableDay.Tuesday => "الثلاثاء", TimetableDay.Wednesday => "الأربعاء", TimetableDay.Thursday => "الخميس",
        TimetableDay.Friday => "الجمعة", _ => day.ToString()
    };
}
