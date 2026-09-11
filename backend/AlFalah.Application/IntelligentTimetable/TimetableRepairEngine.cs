using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Enumerates explicit, simulated alternatives. It never changes the supplied timetable.</summary>
public sealed class TimetableRepairEngine(TimetableValidationEngine validator)
{
    public List<RepairProposalDto> Propose(TimetableValidationContext c, TimetableAnalysisRun run,
        TimetableAnalysisFinding finding, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<ValidationViolation>(finding.EvidenceJson ?? "null");
        if (target is null || finding.Severity != ViolationSeverity.Error) return [];
        var before = validator.Evaluate(c); var hard = before.Where(x => x.Severity == ViolationSeverity.Error).Select(x => x.Key).ToHashSet();
        var entries = c.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToArray();
        var proposals = new Dictionary<string, RepairProposalDto>(); var attempts = 0;
        void Try(RepairProposalKind kind, string description, IReadOnlyList<RepairMovementDto> moves)
        {
            ct.ThrowIfCancellationRequested(); if (++attempts > 3000) return;
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(moves))));
            if (proposals.ContainsKey(id)) return;
            var after = validator.Evaluate(Simulate(c, moves));
            var errors = after.Where(x => x.Severity == ViolationSeverity.Error).ToArray();
            if (errors.Length >= hard.Count || errors.Any(x => !hard.Contains(x.Key)) || errors.Any(x => x.Key == target.Key)) return;
            proposals[id] = new(id, run.Id, finding.Id, c.Timetable.Revision, kind, 0, description,
                "تم التحقق من القيود الصارمة", errors.Length, after.Count(x => x.Severity == ViolationSeverity.Warning), moves);
        }
        foreach (var e in entries.Where(x => target.EntryIds.Contains(x.Id)))
        {
            var block = Block(c, e, entries); var first = block.Min(x => x.Period);
            foreach (var day in (c.Schedule?.Days ?? []).Where(x => x.Day > 0 && x.IsStudyDay).OrderBy(x => x.Day))
            foreach (var period in TimetableValidationEngine.Periods(c.Schedule, day.Day))
            {
                if (attempts > 3000) break;
                if ((int)e.Day == day.Day && period.Sequence == first) continue;
                Try(RepairProposalKind.MoveEntry, $"نقل الحصة ومجموعتها إلى اليوم {day.Day}، الحصة {period.Sequence}.",
                    block.Select(x => Movement(x, (TimetableDay)day.Day, period.Sequence + x.Period - first, x.InstructorProfileId)).ToArray());
            }
            foreach (var other in entries.Where(x => x.Day != e.Day || x.Period != e.Period))
            {
                if (attempts > 3000) break;
                var otherBlock = Block(c, other, entries); var otherFirst = otherBlock.Min(x => x.Period);
                if (block.Any(x => otherBlock.Any(y => y.Id == x.Id))) continue;
                Try(RepairProposalKind.SwapEntries, "تبادل موضعي حصتين مع الحفاظ على المشاركة والحصص المزدوجة.",
                    block.Select(x => Movement(x, other.Day, otherFirst + x.Period - first, x.InstructorProfileId)).Concat(
                        otherBlock.Select(x => Movement(x, e.Day, first + x.Period - otherFirst, x.InstructorProfileId))).OrderBy(x => x.EntryId).ToArray());
                // A split-quota teacher exchange preserves each qualified teacher's allocated load.
                if (block.Length == 1 && otherBlock.Length == 1 && e.ClassSubjectRequirementId.HasValue &&
                    e.ClassSubjectRequirementId == other.ClassSubjectRequirementId && e.InstructorProfileId != other.InstructorProfileId)
                    Try(RepairProposalKind.ReassignTeacher, "تبادل المعلمين المسندين للمادة مع الحفاظ على نصيب كل معلم.",
                        new[] { Movement(e, e.Day, e.Period, other.InstructorProfileId), Movement(other, other.Day, other.Period, e.InstructorProfileId) }.OrderBy(x => x.EntryId).ToArray());
            }
        }
        var ordered = proposals.Values.OrderBy(x => x.PredictedErrors).ThenBy(x => x.PredictedWarnings).ThenBy(x => x.Movements.Count)
            .ThenBy(x => x.Kind).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        // Offer distinct strategies when safe alternatives exist, then rank by severity counts and disruption.
        var selected = ordered.GroupBy(x => x.Kind).Select(x => x.First()).ToList();
        selected.AddRange(ordered.Where(x => selected.All(s => s.Id != x.Id)).Take(Math.Max(0, 3 - selected.Count)).ToArray());
        return selected.OrderBy(x => x.PredictedErrors).ThenBy(x => x.PredictedWarnings).ThenBy(x => x.Movements.Count)
            .ThenBy(x => x.Id, StringComparer.Ordinal).Take(3).Select((x, i) => x with { Rank = i + 1 }).ToList();
    }
    private static SchoolTimetableEntry[] Block(TimetableValidationContext c, SchoolTimetableEntry e, SchoolTimetableEntry[] entries)
    {
        if (!e.ClassSubjectRequirementId.HasValue) return [e];
        var related = entries.Where(x => x.ClassSubjectRequirementId == e.ClassSubjectRequirementId && x.Day == e.Day).ToArray();
        var periods = new HashSet<int> { e.Period };
        if (c.Requirements.Any(x => x.Id == e.ClassSubjectRequirementId && x.PairedBlockCount > 0))
        {
            // Without an occurrence id in the legacy model, moving the entire adjacent run is conservative.
            bool added;
            do { added = false; foreach (var x in related) if (periods.Contains(x.Period - 1) || periods.Contains(x.Period + 1)) added |= periods.Add(x.Period); } while (added);
        }
        return related.Where(x => periods.Contains(x.Period)).OrderBy(x => x.Id).ToArray();
    }
    private static RepairMovementDto Movement(SchoolTimetableEntry e, TimetableDay day, int period, int teacher) =>
        new(e.Id, e.Day, e.Period, e.InstructorProfileId, day, period, teacher);
    public static TimetableValidationContext Simulate(TimetableValidationContext c, IReadOnlyList<RepairMovementDto> moves)
    {
        var t = c.Timetable;
        var copy = new SchoolTimetable { Id = t.Id, SchoolId = t.SchoolId, AcademicYearId = t.AcademicYearId, Semester = t.Semester,
            Revision = t.Revision, TimetableSetupProfileId = t.TimetableSetupProfileId, BellScheduleRevisionId = t.BellScheduleRevisionId };
        copy.Entries = t.Entries.Where(x => !x.IsDeleted).Select(e => {
            var m = moves.SingleOrDefault(x => x.EntryId == e.Id);
            return new SchoolTimetableEntry { Id = e.Id, SchoolId = e.SchoolId, SchoolTimetableId = e.SchoolTimetableId,
                ClassroomId = e.ClassroomId, SubjectId = e.SubjectId, ClassSubjectRequirementId = e.ClassSubjectRequirementId,
                RoomId = e.RoomId, InstructorProfileId = m?.ToTeacherId ?? e.InstructorProfileId, Day = m?.ToDay ?? e.Day,
                Period = m?.ToPeriod ?? e.Period, EntryType = e.EntryType, ClassLabel = e.ClassLabel, Subject = e.Subject };
        }).ToList();
        return c with { Timetable = copy };
    }
}
