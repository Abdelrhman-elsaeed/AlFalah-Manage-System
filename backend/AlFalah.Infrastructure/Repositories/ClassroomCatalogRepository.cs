using System.Data;
using AlFalah.Application.Classrooms;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class ClassroomCatalogRepository(AlFalahDbContext db) : IClassroomCatalogRepository
{
    public async Task<CatalogContext> ContextAsync(int school, string actor, CancellationToken ct)
    {
        var row = await db.Schools.AsNoTracking().Where(s => s.Id == school && s.IsActive)
            .Select(s => new { s.Stage, s.ManagerUserId }).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        var member = await db.UserSchoolRoles.AsNoTracking().AnyAsync(r => r.SchoolId == school && r.UserId == actor && r.IsActive && r.User.IsActive, ct);
        var years = await db.AcademicTerms.AsNoTracking().Where(t => t.SchoolId == school && !t.IsDeleted)
            .GroupBy(t => new { t.AcademicYearId, t.AcademicYear.NameAr, t.AcademicYear.StartsOn, t.AcademicYear.EndsOn })
            .OrderByDescending(g => g.Key.StartsOn)
            .Select(g => new CatalogYear(g.Key.AcademicYearId, g.Key.NameAr, g.Any(t => t.IsActive && t.AcademicYear.IsActive), g.Key.StartsOn, g.Key.EndsOn)).ToArrayAsync(ct);
        return new(row.Stage, row.ManagerUserId == actor, member, years);
    }
    public async Task<IReadOnlyList<Classroom>> ClassroomsAsync(int school, int year, CancellationToken ct) =>
        await db.Classrooms.AsNoTracking().Where(c => c.SchoolId == school && c.AcademicYearId == year && !c.IsDeleted).ToArrayAsync(ct);
    public async Task<IReadOnlyList<Classroom>> PageAsync(int school, int year, int page, bool includeInactive, CancellationToken ct) =>
        await db.Classrooms.AsNoTracking().Where(c => c.SchoolId == school && c.AcademicYearId == year && !c.IsDeleted && (includeInactive || c.IsActive))
            .OrderBy(c => c.Stage).ThenBy(c => c.GradeLevel).ThenBy(c => c.SectionNumber ?? int.MaxValue).ThenBy(c => c.Section).ThenBy(c => c.Id)
            .Skip((page - 1) * 100).Take(101).ToArrayAsync(ct);
    public async Task<IReadOnlyList<InstructorClass>> AssociationsAsync(int teacher, CancellationToken ct) =>
        await db.InstructorClasses.AsTracking().Where(c => c.InstructorProfileId == teacher && !c.IsDeleted).ToArrayAsync(ct);
    public void AddAssociation(InstructorClass row) => db.InstructorClasses.Add(row);
    public void Audit(AuditLog row) => db.AuditLogs.Add(row);
    public async Task<IReadOnlyList<ClassroomLabelHistory>> LabelsAsync(int school, int year, CancellationToken ct) =>
        await db.ClassroomLabelHistory.AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == year).ToArrayAsync(ct);
    public void AddLabel(ClassroomLabelHistory label) => db.ClassroomLabelHistory.Add(label);
    public Task<Classroom?> ForUpdateAsync(int school, int id, CancellationToken ct) => db.Classrooms.AsTracking().SingleOrDefaultAsync(c => c.Id == id && c.SchoolId == school && !c.IsDeleted, ct);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ClassroomCatalogConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { throw new ClassroomCatalogConflictException(); }
    }
    public async Task AddAsync(IReadOnlyList<Classroom> rows, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.Classrooms.AddRange(rows);
        foreach (var row in rows) db.ClassroomLabelHistory.Add(new() { SchoolId = row.SchoolId, AcademicYearId = row.AcademicYearId,
            Classroom = row, Label = row.ClassLabel });
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new ClassroomCatalogConflictException(); }
    }
}
