using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

/// <summary>Applies configured subject rules to manual, imported and published entries.</summary>
public sealed class SubjectAssignmentService(ISubjectRepository repository)
{
    public async Task<List<TimetableEntryDto>> ValidateAsync(SchoolTimetable timetable, IReadOnlyList<TimetableEntryDto> entries,
        BellScheduleDto schedule, bool publishing, CancellationToken ct)
    {
        if (!timetable.TimetableSetupProfileId.HasValue)
        {
            if (entries.Any(e => e.SubjectId.HasValue || e.ClassSubjectRequirementId.HasValue || e.RoomId.HasValue))
                throw new ArgumentException("ربط المادة يتطلب ملف إعداد للجدول.");
            return entries.ToList();
        }
        var setup = await repository.GetSetupAsync(timetable.SchoolId, timetable.TimetableSetupProfileId.Value, ct) ?? throw new KeyNotFoundException();
        var requirements = await repository.GetRequirementsAsync(timetable.SchoolId, setup.Id, ct);
        var subjects = await repository.GetSubjectsAsync(timetable.SchoolId, ct);
        var classes = await repository.GetClassroomsAsync(timetable.SchoolId, setup.AcademicYearId, ct);
        var rooms = await repository.GetRoomsAsync(timetable.SchoolId, ct);
        var assignmentHistory = await repository.GetTeachingAssignmentsAsync(timetable.SchoolId, setup.Id, ct);
        var assignments = assignmentHistory.Where(a => !a.IsDeleted).ToArray();
        var normalized = new List<TimetableEntryDto>();
        foreach (var e in entries)
        {
            if (e.EntryType != TimetableEntryType.Lesson)
            {
                if (e.SubjectId.HasValue || e.ClassroomId.HasValue || e.ClassSubjectRequirementId.HasValue || e.RoomId.HasValue)
                    throw new ArgumentException("خانة الانتظار لا ترتبط بمادة أو غرفة.");
                normalized.Add(e); continue;
            }
            var classroom = e.ClassroomId.HasValue ? classes.SingleOrDefault(c => c.Id == e.ClassroomId)
                : classes.SingleOrDefault(c => string.Equals(c.Name, e.ClassLabel, StringComparison.OrdinalIgnoreCase));
            var subject = e.SubjectId.HasValue ? subjects.SingleOrDefault(s => s.Id == e.SubjectId)
                : subjects.SingleOrDefault(s => string.Equals(s.Name, e.Subject, StringComparison.OrdinalIgnoreCase));
            var r = requirements.SingleOrDefault(r => r.ClassroomId == classroom?.Id && r.SubjectId == subject?.Id);
            if (r is null)
            {
                if (e.ClassSubjectRequirementId.HasValue || e.SubjectId.HasValue || e.RoomId.HasValue || e.ClassroomId.HasValue)
                    throw new ArgumentException("إعداد المادة للفصل غير موجود في هذا الجدول.");
                normalized.Add(e); continue; // Existing legacy text subjects remain readable/editable.
            }
            if (e.ClassSubjectRequirementId.HasValue && e.ClassSubjectRequirementId != r.Id)
                throw new ArgumentException("مصدر إعداد المادة لا يطابق الفصل والمادة.");
            if (r.AllowedDays.Count > 0 && !r.AllowedDays.Any(d => d.Day == (int)e.Day))
                throw new ArgumentException("المادة غير مسموحة في هذا اليوم.");
            var room = e.RoomId ?? r.Rooms.Where(x => x.IsPreferred).Select(x => (int?)x.RoomId).FirstOrDefault()
                ?? (r.Rooms.Count == 1 ? r.Rooms.Single().RoomId : (int?)null);
            if (r.Rooms.Count > 0 && (!room.HasValue || !r.Rooms.Any(x => x.RoomId == room) || rooms.All(x => x.Id != room)))
                throw new ArgumentException("اختر إحدى الغرف المتاحة للمادة.");
            if (r.Rooms.Count == 0 && room.HasValue && rooms.All(x => x.Id != room)) throw new ArgumentException("الغرفة غير متاحة في المدرسة.");
            normalized.Add(e with { ClassroomId = classroom!.Id, ClassLabel = classroom.Name, SubjectId = subject!.Id,
                Subject = subject.Name, ClassSubjectRequirementId = r.Id, RoomId = room, SubjectColor = subject.Color });
        }
        // Logical occurrence identity is (requirement, day, period) within this timetable.
        // Co-teachers share this identity and room; unrelated lessons never share a classroom/room slot.
        bool SharedOccurrence(IEnumerable<TimetableEntryDto> group)
        {
            var rows = group.ToArray(); var id = rows[0].ClassSubjectRequirementId;
            var assignment = assignments.SingleOrDefault(a => a.ClassSubjectRequirementId == id);
            return id.HasValue && rows.All(e => e.ClassSubjectRequirementId == id && e.RoomId == rows[0].RoomId) &&
                assignment?.Mode == "CoTeaching" && rows.Select(e => e.InstructorProfileId).Distinct().Count() == rows.Length &&
                rows.All(e => assignment.Members.Any(m => m.Teacher.InstructorProfileId == e.InstructorProfileId));
        }
        if (normalized.Where(x => x.EntryType == TimetableEntryType.Lesson).GroupBy(x => new { x.Day, x.Period, Class = x.ClassLabel?.Trim().ToUpperInvariant() })
            .Any(g => g.Count() > 1 && !SharedOccurrence(g)))
            throw new ArgumentException("الفصل مسند لأكثر من معلم في الوقت نفسه؛ التزامن مسموح فقط لمعلمي إسناد التدريس المشترك نفسه.");
        if (normalized.Where(x => x.RoomId.HasValue).GroupBy(x => new { x.RoomId, x.Day, x.Period }).Any(g => g.Count() > 1 && !SharedOccurrence(g)))
            throw new ArgumentException("تعارض في الغرفة: لا يمكن استخدامها لحصتين في الوقت نفسه.");
        foreach (var r in requirements)
        {
            var placed = normalized.Where(e => e.ClassSubjectRequirementId == r.Id).ToArray();
            var assignment = assignments.SingleOrDefault(a => a.ClassSubjectRequirementId == r.Id);
            if (assignmentHistory.Count > 0 && assignment is null && (publishing || placed.Length > 0))
                throw new ArgumentException("أكمل إسناد معلمي جميع المواد؛ توجد مادة بلا إسناد أو أُلغي إسنادها.");
            if (assignment is not null)
            {
                TeachingAllocationPolicy.Validate(r, new(r.Id, assignment.Mode, assignment.Members.Select(m =>
                    new TeachingMemberRequest(m.TeacherTimetableProfileId, m.AllocatedPeriodCount, m.AllocatedPairedBlockCount)).ToArray()));
                if (placed.Any(e => assignment.Members.All(m => m.Teacher.InstructorProfileId != e.InstructorProfileId)))
                    throw new ArgumentException("معلم الحصة لا يطابق إسناد المادة الحالي؛ راجع الحصص المتأثرة.");
                foreach (var member in assignment.Members)
                {
                    var teacherEntries = placed.Where(e => e.InstructorProfileId == member.Teacher.InstructorProfileId).ToArray();
                    if (teacherEntries.Length > member.AllocatedPeriodCount || publishing && teacherEntries.Length != member.AllocatedPeriodCount)
                        throw new ArgumentException("عدد حصص المعلم لا يطابق حصته من إسناد المادة.");
                    if (publishing && CountPairs(teacherEntries, schedule) < member.AllocatedPairedBlockCount)
                        throw new ArgumentException("كل حصة مزدوجة يجب أن تكون كاملة لدى المعلم المالك لها، متجاورة وبنفس الغرفة.");
                }
                if (assignment.Mode == "CoTeaching" && placed.GroupBy(e => new { e.Day, e.Period }).Any(g =>
                    !g.Select(e => e.InstructorProfileId).ToHashSet().SetEquals(assignment.Members.Select(m => m.Teacher.InstructorProfileId))))
                    throw new ArgumentException("يجب وضع جميع معلمي التدريس المشترك معاً في كل حصة.");
            }
            var occurrences = placed.GroupBy(e => new { e.Day, e.Period }).Select(g => g.First()).ToArray();
            if (occurrences.Length > r.TotalWeeklyPeriods) throw new ArgumentException("الحصص المسندة تتجاوز نصاب المادة للفصل.");
            if (!publishing) continue;
            SubjectSchedulingPolicy.Validate(SubjectService.Rules(r), schedule);
            if (occurrences.Length != r.TotalWeeklyPeriods) throw new ArgumentException("أكمل نصاب كل مادة قبل نشر الجدول.");
            if (r.FixedSlots.Any(f => !placed.Any(e => (int)e.Day == f.Day && e.Period == f.Period)))
                throw new ArgumentException("توجد حصة مثبتة لم يتم الالتزام بها.");
            if (assignment is null && CountPairs(occurrences, schedule) < r.PairedBlockCount)
                throw new ArgumentException("أكمل الحصص الزوجية المتجاورة دون استراحة وبنفس المعلم والغرفة قبل النشر.");
        }
        // Participate in setup concurrency so newly changed rules cannot race a validated save.
        setup.Revision++;
        return normalized;
    }
    private static int CountPairs(IReadOnlyList<TimetableEntryDto> placed, BellScheduleDto schedule)
    {
        var pairs = 0;
        foreach (var day in placed.GroupBy(e => e.Day))
            {
                var ordered = day.OrderBy(e => e.Period).ToArray();
                var periods = BellScheduleResolver.EffectivePeriods(schedule, day.Key);
                for (var i = 0; i + 1 < ordered.Length; i++)
                {
                    var a = ordered[i]; var b = ordered[i + 1];
                    var first = periods.SingleOrDefault(p => p.Sequence == a.Period); var second = periods.SingleOrDefault(p => p.Sequence == b.Period);
                    if (first is not null && second is not null && SubjectSchedulingPolicy.Adjacent(first, second) &&
                        a.InstructorProfileId == b.InstructorProfileId && a.RoomId == b.RoomId) { pairs++; i++; }
                }
            }
        return pairs;
    }
}
