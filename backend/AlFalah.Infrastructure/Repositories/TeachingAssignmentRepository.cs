using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TeachingAssignmentRepository(AlFalahDbContext db) : ITeachingAssignmentRepository
{
    public Task<TimetableSetupProfile?> GetSetupAsync(int school, int setup, CancellationToken ct) =>
        db.TimetableSetupProfiles.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.Id == setup, ct);
    public async Task<IReadOnlyList<ClassSubjectRequirement>> GetRequirementsAsync(int school, int setup, CancellationToken ct) =>
        await db.Set<ClassSubjectRequirement>().AsNoTracking().Where(x => x.SchoolId == school && x.TimetableSetupProfileId == setup &&
            x.Subject.IsActive && !x.Subject.IsDeleted && x.Classroom.IsActive && !x.Classroom.IsDeleted &&
            x.Classroom.AcademicYearId == x.Setup.AcademicYearId).ToListAsync(ct);
    public async Task<IReadOnlyList<TeachingAssignment>> GetAssignmentsAsync(int school, int setup, CancellationToken ct) =>
        await db.Set<TeachingAssignment>().AsTracking().Where(x => x.SchoolId == school && x.TimetableSetupProfileId == setup)
            .Include(x => x.Members).ToListAsync(ct);

    private IQueryable<TeacherRow> TeacherQuery(int school, int setup)
    {
        // Explicit deletion predicates preserve inactive/deleted teachers for repairing old assignments.
        var members = db.Set<TeachingAssignmentMember>().Where(m => m.SchoolId == school && m.TimetableSetupProfileId == setup && !m.Assignment.IsDeleted);
        return db.TeacherTimetableProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.SchoolId == school && p.TimetableSetupProfileId == setup)
            .Select(p => new TeacherRow {
                Id = p.Id, InstructorProfileId = p.InstructorProfileId, Name = (p.Instructor.User.FirstName + " " + p.Instructor.User.LastName).Trim(),
                Specialization = p.Instructor.SubjectSpecialization, IsActive = p.Instructor.IsActive && !p.Instructor.IsDeleted && p.Instructor.User.IsActive && !p.Instructor.User.IsDeleted,
                AllocatedPeriods = members.Where(m => m.TeacherTimetableProfileId == p.Id).Sum(m => (int?)m.AllocatedPeriodCount) ?? 0,
                MaximumWeeklyPeriods = p.MaximumWeeklyPeriods,
                RemainingPeriods = p.MaximumWeeklyPeriods - (members.Where(m => m.TeacherTimetableProfileId == p.Id).Sum(m => (int?)m.AllocatedPeriodCount) ?? 0),
                SubjectCount = members.Where(m => m.TeacherTimetableProfileId == p.Id).Select(m => m.Assignment.Requirement.SubjectId).Distinct().Count(),
                ClassroomCount = members.Where(m => m.TeacherTimetableProfileId == p.Id).Select(m => m.Assignment.Requirement.ClassroomId).Distinct().Count(),
                IsVisiting = p.IsVisiting });
    }
    private async Task<IReadOnlyList<TeachingTeacherDto>> Warnings(IReadOnlyList<TeacherRow> teachers, int school, int setup, CancellationToken ct)
    {
        var ids = teachers.Select(x => x.Id).ToArray();
        var constraints = await db.TeacherTimetableProfiles.AsNoTracking().Where(p => p.SchoolId == school && p.TimetableSetupProfileId == setup && ids.Contains(p.Id))
            .Select(p => new { p.Id, Restricted = p.Slots.Count(s => !s.IsAvailable),
                Review = p.Setup.BellScheduleTemplateId != p.BellScheduleRevision.BellScheduleTemplateId || p.BellScheduleRevision.Revision != p.BellScheduleRevision.Template.Revision }).ToListAsync(ct);
        return teachers.Select(t => {
            var warnings = new List<string>(); var c = constraints.Single(x => x.Id == t.Id);
            if (!t.IsActive) warnings.Add("المعلم غير نشط ولا يمكن إسناد حصص إليه.");
            if (t.RemainingPeriods <= 0) warnings.Add(t.RemainingPeriods < 0 ? "تجاوز الحد الأسبوعي" : "النصاب مكتمل");
            if (c.Restricted > 0) warnings.Add($"توجد {c.Restricted} حصة غير متاحة؛ تُراجع عند توزيع الجدول.");
            if (c.Review) warnings.Add("تغيّر التوقيت؛ راجع إتاحة المعلم قبل توزيع الجدول.");
            return new TeachingTeacherDto(t.Id, t.InstructorProfileId, t.Name, t.Specialization, t.IsActive,
                t.AllocatedPeriods, t.MaximumWeeklyPeriods, t.RemainingPeriods, t.SubjectCount, t.ClassroomCount, t.IsVisiting, warnings);
        }).ToArray();
    }
    public async Task<IReadOnlyList<TeachingTeacherDto>> GetTeachersAsync(int school, int setup, CancellationToken ct) =>
        await Warnings(await TeacherQuery(school, setup).ToListAsync(ct), school, setup, ct);
    public async Task<TeachingTeacherPage> SearchTeachersAsync(int school, int setup, TeachingTeacherSearch search, CancellationToken ct)
    {
        var q = TeacherQuery(school, setup);
        if (!string.IsNullOrWhiteSpace(search.Search)) q = q.Where(t => t.Name.Contains(search.Search) || (t.Specialization != null && t.Specialization.Contains(search.Search)));
        if (!string.IsNullOrWhiteSpace(search.Specialization)) q = q.Where(t => t.Specialization != null && t.Specialization.Contains(search.Specialization));
        if (search.IsActive.HasValue) q = q.Where(t => t.IsActive == search.IsActive);
        if (search.IsVisiting.HasValue) q = q.Where(t => t.IsVisiting == search.IsVisiting);
        if (search.HasCapacity) q = q.Where(t => t.RemainingPeriods > 0);
        var count = await q.CountAsync(ct);
        var ordered = search.Sort switch {
            "load" => search.Descending ? q.OrderByDescending(t => t.AllocatedPeriods) : q.OrderBy(t => t.AllocatedPeriods),
            "remaining" => search.Descending ? q.OrderByDescending(t => t.RemainingPeriods) : q.OrderBy(t => t.RemainingPeriods),
            _ => search.Descending ? q.OrderByDescending(t => t.Name) : q.OrderBy(t => t.Name)
        };
        var items = await ordered.ThenBy(t => t.Id).Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToListAsync(ct);
        return new(await Warnings(items, school, setup, ct), count);
    }
    public void Add(TeachingAssignment assignment) => db.Add(assignment);
    public void SetMembers(TeachingAssignment assignment, IReadOnlyList<TeachingMemberRequest> members)
    {
        foreach (var old in assignment.Members.Where(m => members.All(n => n.TeacherTimetableProfileId != m.TeacherTimetableProfileId)).ToArray())
        { db.Remove(old); assignment.Members.Remove(old); }
        foreach (var member in members)
        {
            var row = assignment.Members.SingleOrDefault(m => m.TeacherTimetableProfileId == member.TeacherTimetableProfileId);
            if (row is null) { row = new() { SchoolId = assignment.SchoolId, TimetableSetupProfileId = assignment.TimetableSetupProfileId,
                TeacherTimetableProfileId = member.TeacherTimetableProfileId }; assignment.Members.Add(row); }
            row.AllocatedPeriodCount = member.AllocatedPeriodCount;
            row.AllocatedPairedBlockCount = member.AllocatedPairedBlockCount;
        }
    }
    public async Task SaveAsync(TimetableSetupProfile setup, string actor, object before, object after, CancellationToken ct)
    {
        var timetables = await db.SchoolTimetables.AsTracking().Where(x => x.SchoolId == setup.SchoolId &&
            x.TimetableSetupProfileId == setup.Id && !x.IsPublished).ToListAsync(ct);
        foreach (var timetable in timetables) { timetable.TimingsRequireRevalidation = true; timetable.Revision++; }
        db.AuditLogs.Add(new() { SchoolId = setup.SchoolId, UserId = actor, Action = "Timetable.TeachingAssignments.Saved",
            EntityName = nameof(TeachingAssignment), EntityId = setup.Id.ToString(), OldValues = JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(new { setup.Revision, Changes = after }), CreatedAt = DateTimeOffset.UtcNow });
        // One SaveChanges transaction includes the shared setup concurrency fence, every cell and its audit.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new BellScheduleConflictException("Teaching assignments changed", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BellScheduleConflictException("Teaching assignment already exists", ex); }
    }
    // Member initialization keeps subsequent filters/orderings translatable by SQL Server.
    private sealed class TeacherRow
    {
        public int Id { get; init; }
        public int InstructorProfileId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Specialization { get; init; }
        public bool IsActive { get; init; }
        public int AllocatedPeriods { get; init; }
        public int MaximumWeeklyPeriods { get; init; }
        public int RemainingPeriods { get; init; }
        public int SubjectCount { get; init; }
        public int ClassroomCount { get; init; }
        public bool IsVisiting { get; init; }
    }
}
