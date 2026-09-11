using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TeachingAssignmentService(ITeachingAssignmentRepository repository, SubjectService subjects)
{
    public async Task<TeachingAssignmentsOverview> GetAsync(int school, int setupId, CancellationToken ct)
    {
        var setup = await repository.GetSetupAsync(school, setupId, ct) ?? throw new KeyNotFoundException();
        var overview = await subjects.GetAsync(school, setupId, ct);
        var activeIds = (await repository.GetRequirementsAsync(school, setupId, ct)).Select(r => r.Id).ToHashSet();
        var requirements = overview.Requirements.Where(r => activeIds.Contains(r.Id)).ToArray();
        var assignments = await repository.GetAssignmentsAsync(school, setupId, ct);
        var teachers = await repository.GetTeachersAsync(school, setupId, ct);
        var warnings = new List<string>();
        foreach (var assignment in assignments)
        {
            var r = requirements.SingleOrDefault(x => x.Id == assignment.ClassSubjectRequirementId);
            if (r is null) { warnings.Add("إسناد مرتبط بمادة أو فصل محذوف؛ أزل الإسناد وأعد المراجعة."); continue; }
            try { TeachingAllocationPolicy.Validate(new() { IndividualPeriodCount = r.Rules.IndividualPeriodCount, PairedBlockCount = r.Rules.PairedBlockCount }, ToRequest(assignment)); }
            catch (ArgumentException ex) { warnings.Add($"{r.ClassroomName}: {ex.Message}"); }
        }
        return new(setup.Revision, overview.Subjects, overview.Classrooms, requirements,
            assignments.Select(a => new TeachingAssignmentDto(a.Id, a.ClassSubjectRequirementId, a.Mode,
                a.Members.Select(m => { var t = teachers.SingleOrDefault(t => t.Id == m.TeacherTimetableProfileId);
                    return new TeachingMemberDto(m.TeacherTimetableProfileId, t?.InstructorProfileId ?? 0, t?.Name ?? "معلم غير متاح",
                        m.AllocatedPeriodCount, m.AllocatedPairedBlockCount); }).ToArray())).ToArray(), teachers, warnings);
    }
    public async Task<TeachingTeacherPage> SearchAsync(int school, int setup, TeachingTeacherSearch search, CancellationToken ct)
    {
        _ = await repository.GetSetupAsync(school, setup, ct) ?? throw new KeyNotFoundException();
        if (search.Page < 1 || search.Page > 100000 || search.PageSize < 1 || search.PageSize > 100 || search.Sort is not ("name" or "load" or "remaining"))
            throw new ArgumentException("معايير بحث المعلمين غير صالحة.");
        return await repository.SearchTeachersAsync(school, setup, search, ct);
    }
    public async Task<TeachingAssignmentsOverview> SaveAsync(int school, int setupId, SaveTeachingAssignmentsRequest request, string actor, CancellationToken ct)
    {
        if (request.Changes is null || request.Changes.Count == 0 || request.Changes.Any(c => c is null || c.Members is null) ||
            request.Changes.Select(c => c.ClassSubjectRequirementId).Distinct().Count() != request.Changes.Count)
            throw new ArgumentException("أرسل خلايا مختلفة وتوزيع المعلمين لكل خلية.");
        var setup = await repository.GetSetupAsync(school, setupId, ct) ?? throw new KeyNotFoundException();
        if (request.Revision != setup.Revision) throw new BellScheduleConflictException("Stale setup revision");
        var requirements = await repository.GetRequirementsAsync(school, setupId, ct);
        var existing = await repository.GetAssignmentsAsync(school, setupId, ct);
        var teachers = await repository.GetTeachersAsync(school, setupId, ct);
        foreach (var cell in request.Changes)
        {
            var requirement = requirements.SingleOrDefault(r => r.Id == cell.ClassSubjectRequirementId);
            if (requirement is null && cell.Members.Count == 0 && existing.Any(a => a.ClassSubjectRequirementId == cell.ClassSubjectRequirementId))
                requirement = new(); // Allow repairing an assignment after its classroom/subject becomes inactive.
            if (requirement is null) throw new ArgumentException("المادة أو الفصل غير نشط أو خارج المدرسة أو ملف الإعداد.");
            TeachingAllocationPolicy.Validate(requirement, cell);
            if (cell.Members.Any(m => teachers.All(t => t.Id != m.TeacherTimetableProfileId || !t.IsActive)))
                throw new ArgumentException("اختر معلماً نشطاً من المدرسة وله ملف في إعدادات المعلمين لهذا الجدول.");
        }
        // Validate the final batch state, so swaps and removals release capacity before additions.
        var changed = request.Changes.Select(c => c.ClassSubjectRequirementId).ToHashSet();
        var final = existing.Where(a => !changed.Contains(a.ClassSubjectRequirementId)).Select(ToRequest).Concat(request.Changes).ToArray();
        foreach (var load in final.SelectMany(c => c.Members).GroupBy(m => m.TeacherTimetableProfileId))
        {
            var teacher = teachers.SingleOrDefault(t => t.Id == load.Key);
            if (teacher is null || !teacher.IsActive) throw new ArgumentException("يوجد إسناد لمعلم غير نشط؛ أزله أو استبدله قبل الحفظ.");
            var total = load.Sum(m => (long)m.AllocatedPeriodCount);
            if (total > teacher.MaximumWeeklyPeriods)
                throw new ArgumentException($"{teacher.Name}: النصاب المسند {total} يتجاوز الحد الأسبوعي {teacher.MaximumWeeklyPeriods}. خفّض الإسناد بمقدار {total - teacher.MaximumWeeklyPeriods} حصة.");
        }
        var before = existing.Where(a => changed.Contains(a.ClassSubjectRequirementId)).Select(ToRequest).ToArray();
        foreach (var cell in request.Changes)
        {
            var assignment = existing.SingleOrDefault(a => a.ClassSubjectRequirementId == cell.ClassSubjectRequirementId);
            if (assignment is null && cell.Members.Count == 0) continue;
            if (assignment is null) { assignment = new() { SchoolId = school, TimetableSetupProfileId = setupId,
                ClassSubjectRequirementId = cell.ClassSubjectRequirementId }; repository.Add(assignment); }
            else assignment.Revision++;
            assignment.Mode = cell.Mode; assignment.IsDeleted = cell.Members.Count == 0;
            assignment.UpdatedAt = DateTimeOffset.UtcNow; assignment.UpdatedByUserId = actor;
            repository.SetMembers(assignment, cell.Members);
        }
        setup.Revision++; setup.Status = TimetableSetupStatus.Draft;
        setup.UpdatedAt = DateTimeOffset.UtcNow; setup.UpdatedByUserId = actor;
        await repository.SaveAsync(setup, actor, before, request.Changes, ct);
        return await GetAsync(school, setupId, ct);
    }
    private static TeachingCellRequest ToRequest(TeachingAssignment a) => new(a.ClassSubjectRequirementId, a.Mode,
        a.Members.Select(m => new TeachingMemberRequest(m.TeacherTimetableProfileId, m.AllocatedPeriodCount, m.AllocatedPairedBlockCount)).ToArray());
}
