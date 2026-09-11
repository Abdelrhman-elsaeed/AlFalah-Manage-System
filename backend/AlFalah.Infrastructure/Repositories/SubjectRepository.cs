using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SubjectRepository(AlFalahDbContext db) : ISubjectRepository
{
    public Task<TimetableSetupProfile?> GetSetupAsync(int schoolId, int setupId, CancellationToken ct) =>
        db.TimetableSetupProfiles.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == setupId && !x.IsDeleted, ct);
    public async Task<IReadOnlyList<SubjectDefinition>> GetSubjectsAsync(int schoolId, CancellationToken ct) =>
        await db.Set<SubjectDefinition>().AsTracking().Where(x => x.SchoolId == schoolId && x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(int schoolId, CancellationToken ct) =>
        await db.Set<TimetableRoom>().AsNoTracking().Where(x => x.SchoolId == schoolId && x.IsActive).OrderBy(x => x.Name).Select(x => new RoomDto(x.Id, x.Name)).ToListAsync(ct);
    public async Task<IReadOnlyList<SubjectClassroomDto>> GetClassroomsAsync(int schoolId, int yearId, CancellationToken ct) =>
        await db.Classrooms.AsNoTracking().Where(x => x.SchoolId == schoolId && x.AcademicYearId == yearId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Stage).ThenBy(x => x.GradeLevel).ThenBy(x => x.ClassLabel)
            .Select(x => new SubjectClassroomDto(x.Id, x.ClassLabel, (int)x.Stage, x.GradeLevel)).ToListAsync(ct);
    public async Task<IReadOnlyList<ClassSubjectRequirement>> GetRequirementsAsync(int schoolId, int setupId, CancellationToken ct) =>
        await db.Set<ClassSubjectRequirement>().AsTracking().Where(x => x.SchoolId == schoolId && x.TimetableSetupProfileId == setupId)
            .Include(x => x.AllowedDays).Include(x => x.FixedSlots).Include(x => x.Rooms).ToListAsync(ct);
    public async Task<bool> IsUsedAsync(int schoolId, int requirementId, CancellationToken ct) =>
        await db.SchoolTimetableEntries.IgnoreQueryFilters().AnyAsync(x => x.SchoolId == schoolId && x.ClassSubjectRequirementId == requirementId, ct) ||
        await db.Set<TeachingAssignment>().AnyAsync(x => x.SchoolId == schoolId && x.ClassSubjectRequirementId == requirementId, ct);
    public async Task<IReadOnlyList<TeachingAssignment>> GetTeachingAssignmentsAsync(int schoolId, int setupId, CancellationToken ct) =>
        await db.Set<TeachingAssignment>().IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == schoolId && x.TimetableSetupProfileId == setupId)
            .Include(x => x.Members).ThenInclude(x => x.Teacher).ToListAsync(ct);
    public void AddSubject(SubjectDefinition subject) => db.Add(subject);
    public void AddRoom(TimetableRoom room) => db.Add(room);
    public void AddRequirement(ClassSubjectRequirement requirement) => db.Add(requirement);
    public void SetRules(ClassSubjectRequirement r, SubjectRulesRequest rules)
    {
        // Reconcile children in place so unique indexes never see duplicate replacement rows.
        foreach (var old in r.AllowedDays.Where(x => !rules.AllowedDays.Contains(x.Day)).ToArray()) { db.Remove(old); r.AllowedDays.Remove(old); }
        foreach (var day in rules.AllowedDays.Where(d => r.AllowedDays.All(x => x.Day != d))) r.AllowedDays.Add(new() { Day = day });
        foreach (var old in r.FixedSlots.Where(x => !rules.FixedSlots.Contains(new(x.Day, x.Period))).ToArray()) { db.Remove(old); r.FixedSlots.Remove(old); }
        foreach (var slot in rules.FixedSlots.Where(s => r.FixedSlots.All(x => x.Day != s.Day || x.Period != s.Period))) r.FixedSlots.Add(new() { Day = slot.Day, Period = slot.Period });
        foreach (var old in r.Rooms.Where(x => !rules.RoomIds.Contains(x.RoomId)).ToArray()) { db.Remove(old); r.Rooms.Remove(old); }
        foreach (var id in rules.RoomIds.Where(id => r.Rooms.All(x => x.RoomId != id))) r.Rooms.Add(new() { SchoolId = r.SchoolId, RoomId = id });
        foreach (var room in r.Rooms) room.IsPreferred = room.RoomId == rules.PreferredRoomId;
    }
    public async Task SaveAsync(int schoolId, int setupId, string userId, string action, object details, CancellationToken ct)
    {
        db.AuditLogs.Add(new() { SchoolId = schoolId, UserId = userId, Action = action, EntityName = "TimetableSubjects",
            EntityId = setupId.ToString(), NewValues = JsonSerializer.Serialize(details), CreatedAt = DateTimeOffset.UtcNow });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new BellScheduleConflictException("Subject rules changed", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BellScheduleConflictException("Subject already exists or changed", ex); }
    }
}
