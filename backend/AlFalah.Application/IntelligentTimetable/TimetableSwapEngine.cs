using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Server-generated same-day cycles and date-specific teacher cover, simulated through Phase 7 rules.</summary>
public sealed class TimetableSwapEngine(TimetableValidationEngine validator)
{
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
        DateOnly date, DateTimeOffset expires, IReadOnlyDictionary<int, int>? assigned, CancellationToken ct)
    {
        if (mode is not ("Substitution" or "Swap")) throw new ArgumentException("نوع العملية غير صالح.");
        var source = c.Timetable.Entries.SingleOrDefault(e => !e.IsDeleted && e.Id == sourceId && e.EntryType == TimetableEntryType.Lesson)
            ?? throw new KeyNotFoundException();
        if (source.Day != BellScheduleResolver.ToDay(date.DayOfWeek)) throw new ArgumentException("الحصة لا تقع في اليوم المختار.");
        var block = Block(c, source);
        var candidates = new List<SwapCandidateDto>();
        string Name(int id) => c.Teachers.Where(t => t.InstructorProfileId == id).Select(t => (t.Instructor.User.FirstName + " " + t.Instructor.User.LastName).Trim()).FirstOrDefault() ?? id.ToString();
        SwapCandidateDto Evaluate(string kind, string label, RepairMovementDto[] moves, string[]? extraErrors = null)
        {
            ct.ThrowIfCancellationRequested();
            var simulated = TimetableRepairEngine.Simulate(c, moves);
            if (mode == "Substitution") SuppressCoveredStandby(simulated);
            var violations = validator.Evaluate(simulated, scheduledTeachers: assigned);
            var errors = violations.Where(v => v.Severity == ViolationSeverity.Error).Select(v => v.MessageAr).Concat(extraErrors ?? []).Distinct().ToArray();
            var affected = moves.Select(m => m.EntryId).ToHashSet();
            var teachers = moves.SelectMany(m => new[] { m.FromTeacherId, m.ToTeacherId }).ToHashSet();
            var warnings = violations.Where(v => v.Severity == ViolationSeverity.Warning &&
                (v.EntryIds.Any(affected.Contains) || v.InstructorProfileId.HasValue && teachers.Contains(v.InstructorProfileId.Value))).Select(v => v.MessageAr).Distinct().ToArray();
            var color = errors.Length > 0 ? "Red" : warnings.Length > 0 ? "Yellow" : "Green";
            var preview = moves.Select(m => {
                var e = c.Timetable.Entries.Single(e => e.Id == m.EntryId);
                string Time(int p) => TimetableValidationEngine.Periods(c.Schedule, (int)e.Day).FirstOrDefault(x => x.Sequence == p) is { } period
                    ? $"{period.StartLocalTime:HH:mm}–{period.EndLocalTime:HH:mm}" : "—";
                return new SwapPreviewDto(e.Id, Name(m.FromTeacherId), e.ClassLabel ?? "", e.Subject ?? "", m.FromPeriod, m.ToPeriod,
                    Time(m.FromPeriod), Time(m.ToPeriod), Name(m.ToTeacherId), e.RoomId);
            }).ToArray();
            // Recomputed on confirmation: any change to movements, warnings, setup or date invalidates the id.
            var evidence = JsonSerializer.Serialize(new { c.Timetable.Id, c.Timetable.Revision, SetupRevision = c.Setup?.Revision,
                ScheduleId = c.Schedule?.Id, date, expires, mode, kind, sourceId, moves, errors, warnings, preview });
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
                    .Select(e => Move(e, e.Period, t.InstructorProfileId)).ToArray();
                candidates.Add(Evaluate("Substitution", Name(t.InstructorProfileId), moves));
            }
        }
        else
        {
            var blocks = c.Timetable.Entries.Where(e => !e.IsDeleted && e.Day == source.Day && e.EntryType == TimetableEntryType.Lesson)
                .Select(e => Block(c, e)).DistinctBy(b => b[0].Id).Where(b => b.Min(e => e.Period) != block.Min(e => e.Period) &&
                    !b.Any(e => block.Any(s => s.Id == e.Id))).ToArray();
            foreach (var target in blocks)
            {
                var direct = Evaluate("DirectSwap", $"تبادل مع {Name(target[0].InstructorProfileId)} — الحصة {target.Min(e => e.Period)}", Cycle(block, target));
                candidates.Add(direct);
                if (direct.Color != "Red") continue;
                // Rank feasible third participants separately; red direct proposals remain non-executable.
                var alternatives = new List<SwapCandidateDto>();
                foreach (var third in blocks.Where(b => b[0].Id != target[0].Id))
                {
                    var proposal = Evaluate("ThreeWaySwap", $"تبديل ثلاثي: {Name(target[0].InstructorProfileId)} ثم {Name(third[0].InstructorProfileId)}", Cycle(block, target, third));
                    if (proposal.Color != "Red") alternatives.Add(proposal);
                    if (alternatives.Count >= 3) break;
                }
                candidates.AddRange(alternatives.OrderBy(x => x.Warnings.Length).Take(3));
            }
        }
        return candidates.OrderBy(x => x.Color == "Green" ? 0 : x.Color == "Yellow" ? 1 : 2).ThenBy(x => x.Warnings.Length).ToArray();
    }
    private static RepairMovementDto Move(SchoolTimetableEntry e, int period, int teacher) => new(e.Id, e.Day, e.Period, e.InstructorProfileId, e.Day, period, teacher);
    public static void SuppressCoveredStandby(TimetableValidationContext c)
    {
        var occupied = c.Timetable.Entries.Where(e => e.EntryType == TimetableEntryType.Lesson)
            .Select(e => (e.InstructorProfileId, e.Day, e.Period)).ToHashSet();
        c.Timetable.Entries = c.Timetable.Entries.Where(e => e.EntryType != TimetableEntryType.Standby ||
            !occupied.Contains((e.InstructorProfileId, e.Day, e.Period))).ToList();
    }
    private static RepairMovementDto[] Cycle(params SchoolTimetableEntry[][] blocks) => blocks.SelectMany((block, i) =>
        block.Select(e => Move(e, blocks[(i + 1) % blocks.Length].Min(x => x.Period) + e.Period - block.Min(x => x.Period), e.InstructorProfileId)))
        .OrderBy(x => x.EntryId).ToArray();
}
