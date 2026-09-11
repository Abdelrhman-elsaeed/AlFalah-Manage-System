using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TeacherAvailabilityRepository(AlFalahDbContext context) : ITeacherAvailabilityRepository
{
    public Task<TimetableSetupProfile?> GetSetupAsync(int schoolId, int setupId, CancellationToken ct) =>
        context.TimetableSetupProfiles.AsTracking().SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == setupId, ct);
    private IQueryable<InstructorProfile> Teachers(int schoolId) => context.InstructorProfiles.AsNoTracking()
        .Where(x => x.SchoolId == schoolId && x.IsActive && !x.IsDeleted && x.User.IsActive && !x.User.IsDeleted);
    public async Task<IReadOnlyList<AvailabilityTeacherDto>> GetTeachersAsync(int schoolId, CancellationToken ct) =>
        await Teachers(schoolId).OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName)
            .Select(x => new AvailabilityTeacherDto(x.Id, (x.User.FirstName + " " + x.User.LastName).Trim())).ToListAsync(ct);
    public Task<AvailabilityTeacherDto?> GetTeacherAsync(int schoolId, int teacherId, CancellationToken ct) =>
        Teachers(schoolId).Where(x => x.Id == teacherId)
            .Select(x => new AvailabilityTeacherDto(x.Id, (x.User.FirstName + " " + x.User.LastName).Trim())).SingleOrDefaultAsync(ct);
    public Task<BellScheduleRevision?> GetScheduleAsync(int schoolId, int templateId, CancellationToken ct) =>
        context.Set<BellScheduleRevision>().AsNoTracking().Include(x => x.Days).ThenInclude(x => x.Periods)
            .SingleOrDefaultAsync(x => x.SchoolId == schoolId && x.BellScheduleTemplateId == templateId &&
                x.Template.IsActive && !x.Template.IsDeleted && x.Revision == x.Template.Revision, ct);
    private IQueryable<TeacherTimetableProfile> Profiles(int schoolId, int setupId) => context.Set<TeacherTimetableProfile>().AsTracking()
        .Where(x => x.SchoolId == schoolId && x.TimetableSetupProfileId == setupId).Include(x => x.Slots).ThenInclude(x => x.Period);
    public Task<TeacherTimetableProfile?> GetProfileAsync(int schoolId, int setupId, int teacherId, CancellationToken ct) =>
        Profiles(schoolId, setupId).SingleOrDefaultAsync(x => x.InstructorProfileId == teacherId, ct);
    public async Task<IReadOnlyList<TeacherTimetableProfile>> GetProfilesAsync(int schoolId, int setupId, CancellationToken ct) =>
        await Profiles(schoolId, setupId).ToListAsync(ct);
    public async Task<IReadOnlyList<SchoolTimetableEntry>> GetAssignmentsAsync(int schoolId, int setupId, int teacherId, CancellationToken ct) =>
        await context.SchoolTimetableEntries.AsNoTracking().Where(x => x.SchoolId == schoolId && x.InstructorProfileId == teacherId &&
            x.SchoolTimetable.TimetableSetupProfileId == setupId && !x.SchoolTimetable.IsDeleted).ToListAsync(ct);
    public async Task<int> GetTeachingLoadAsync(int schoolId, int setupId, int teacherId, CancellationToken ct) =>
        await context.Set<TeachingAssignmentMember>().Where(m => m.SchoolId == schoolId && m.TimetableSetupProfileId == setupId &&
            m.Teacher.InstructorProfileId == teacherId).SumAsync(m => (int?)m.AllocatedPeriodCount, ct) ?? 0;
    public async Task SaveAsync(TeacherTimetableProfile profile, IReadOnlyList<AvailabilitySlotRequest> slots, object? before, CancellationToken ct)
    {
        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (profile.Id == 0) context.Add(profile);
            var desired = slots.ToDictionary(x => (x.Day, x.BellPeriodId));
            foreach (var old in profile.Slots.ToArray())
            {
                if (desired.Remove((old.Day, old.BellPeriodId), out var value)) old.IsAvailable = value.IsAvailable;
                else { context.Remove(old); profile.Slots.Remove(old); }
            }
            foreach (var slot in desired.Values) profile.Slots.Add(new() { Day = slot.Day, BellPeriodId = slot.BellPeriodId, IsAvailable = slot.IsAvailable });
            await context.SaveChangesAsync(ct);
            context.AuditLogs.Add(new AuditLog { SchoolId = profile.SchoolId, UserId = profile.UpdatedByUserId,
                Action = "Timetable.TeacherAvailability.Updated", EntityName = nameof(TeacherTimetableProfile), EntityId = profile.Id.ToString(),
                OldValues = before is null ? null : JsonSerializer.Serialize(before),
                NewValues = JsonSerializer.Serialize(new { profile.ShortDisplayName, profile.MaximumWeeklyPeriods, profile.IsVisiting,
                    profile.HideFromPrint, profile.BellScheduleRevisionId, profile.Revision, Slots = slots }), CreatedAt = profile.UpdatedAt });
            await context.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex) { throw new BellScheduleConflictException("Teacher availability conflict", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BellScheduleConflictException("Teacher availability conflict", ex); }
    }
}
