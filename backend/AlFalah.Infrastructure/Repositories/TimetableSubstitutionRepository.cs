using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed partial class TimetableSubstitutionRepository : TimetableReviewRepository, ITimetableSubstitutionRepository
{
    private readonly AlFalahDbContext db;
    public TimetableSubstitutionRepository(AlFalahDbContext context) : base(context) => db = context;
    public async Task<IReadOnlyList<TimetableSubstitution>> GetChangesAsync(int school, int timetable, DateOnly? date, CancellationToken ct) =>
        await db.Set<TimetableSubstitution>().AsNoTracking().Where(x => x.SchoolId == school && x.SchoolTimetableId == timetable &&
            (date == null || x.LocalDate == date)).Include(x => x.Movements).OrderBy(x => x.Id).ToListAsync(ct);
    public Task<TimetableSubstitution?> FindRequestAsync(int school, Guid requestId, CancellationToken ct) =>
        db.Set<TimetableSubstitution>().AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == school && x.RequestId == requestId, ct);
    public void AddChange(TimetableSubstitution change) => db.Add(change);
    public Task<bool> HasEditorGrantAsync(int school, string userId, CancellationToken ct) =>
        db.TimetableEditorGrants.AnyAsync(x => x.SchoolId == school && x.ModeratorUserId == userId, ct);
    public Task<DailyCoverageNeed?> CoverageNeedAsync(int school, int timetable, DateOnly date, int entry, CancellationToken ct) =>
        db.Set<DailyCoverageNeed>().AsTracking().Include(x => x.ActiveSubstitution).ThenInclude(x => x!.Movements)
            .Include(x => x.Records).SingleOrDefaultAsync(x => x.SchoolId == school && x.SchoolTimetableId == timetable && x.LocalDate == date && x.SourceEntryId == entry, ct);
    public void AddNeed(DailyCoverageNeed need) => db.Add(need);
    public async Task<IReadOnlyList<int>> GetAbsentTeachersAsync(int school, DateOnly date, CancellationToken ct) =>
        await db.InstructorProfiles.AsNoTracking().Where(t => t.SchoolId == school && db.AttendanceRecords.Any(a =>
            a.SchoolId == school && a.UserId == t.UserId && a.AttendanceDate == date && a.Status != AlFalah.Domain.Enums.AttendanceStatus.Present))
            .Select(t => t.Id).ToListAsync(ct);
}
