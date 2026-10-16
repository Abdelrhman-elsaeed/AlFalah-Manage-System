using System.Data;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TeachingStatementRepository(AlFalahDbContext db) : ITeachingStatementRepository
{
    public Task<TeachingAssignmentStatement?> FindAsync(int school, int setup, int revision, string scope, string renderer, CancellationToken ct) =>
        db.Set<TeachingAssignmentStatement>().AsNoTracking().SingleOrDefaultAsync(x => x.SchoolId == school &&
            x.TimetableSetupProfileId == setup && x.Revision == revision && x.Scope == scope && x.RendererVersion == renderer &&
            !x.Setup.IsDeleted && !x.Setup.School.IsDeleted, ct);

    public async Task<StatementSource> CaptureAsync(int school, int setup, CancellationToken ct)
    {
        // Freeze a consistent multi-query read. Rendering runs after the read transaction releases its locks.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var context = await db.TimetableSetupProfiles.AsNoTracking().Where(x => x.SchoolId == school && x.Id == setup && !x.School.IsDeleted)
            .Select(x => new {x.Revision, Header = new StatementHeader(x.School.Name, x.AcademicYear.NameAr, (int)x.Semester,
                x.Name, x.School.ReportSettings == null ? null : x.School.ReportSettings.ReportHeaderText,
                x.School.ReportSettings == null ? null : x.School.ReportSettings.ReportFooterText,
                x.School.Manager == null ? null : (x.School.Manager.FirstName + " " + x.School.Manager.LastName).Trim()),x.AcademicYearId})
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        var classes = await db.Classrooms.AsNoTracking().Where(x => x.SchoolId == school && x.AcademicYearId == context.AcademicYearId && x.IsActive)
            .OrderBy(x => x.Stage).ThenBy(x => x.GradeLevel).ThenBy(x => x.SectionNumber).ThenBy(x => x.Id)
            .Select(x => new StatementClass(x.Id, (int)x.Stage, x.GradeLevel, x.ClassLabel,
                x.SectionNumber.HasValue ? x.GradeLevel + "/" + x.SectionNumber.Value : x.ClassLabel)).ToArrayAsync(ct);
        var subjects = await db.Set<SubjectDefinition>().AsNoTracking().Where(x => x.SchoolId == school && x.IsActive)
            .Select(x => new SubjectDto(x.Id,x.Name,x.Color,x.Revision)).ToArrayAsync(ct);
        var requirements = await db.Set<ClassSubjectRequirement>().AsNoTracking().Where(x => x.SchoolId == school && x.TimetableSetupProfileId == setup)
            .Select(x => new StatementRequirement(x.Id,x.SubjectId,x.ClassroomId,x.IndividualPeriodCount,x.PairedBlockCount)).ToArrayAsync(ct);
        var teachers = await db.TeacherTimetableProfiles.IgnoreQueryFilters().AsNoTracking().Where(x => x.SchoolId == school && x.TimetableSetupProfileId == setup)
            .Select(x => new TeachingTeacherDto(x.Id,x.InstructorProfileId,(x.Instructor.User.FirstName + " " + x.Instructor.User.LastName).Trim(),
                x.Instructor.SubjectSpecialization,x.Instructor.IsActive && !x.Instructor.IsDeleted && x.Instructor.User.IsActive && !x.Instructor.User.IsDeleted,
                0,x.MaximumWeeklyPeriods,0,0,0,x.IsVisiting,Array.Empty<string>())).ToArrayAsync(ct);
        var assignments = await db.Set<TeachingAssignment>().AsNoTracking().Where(x => x.SchoolId == school && x.TimetableSetupProfileId == setup)
            .Select(x => new TeachingAssignmentDto(x.Id,x.ClassSubjectRequirementId,x.Mode,x.Members.Select(m => new TeachingMemberDto(
                m.TeacherTimetableProfileId,0,"",m.AllocatedPeriodCount,m.AllocatedPairedBlockCount,m.StatementCategory)).ToArray())).ToArrayAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(context.Revision,context.Header,classes,subjects,requirements,assignments,teachers);
    }
    public async Task<TeachingAssignmentStatement> StoreAsync(TeachingAssignmentStatement statement, CancellationToken ct)
    {
        db.Add(statement);
        try { await db.SaveChangesAsync(ct); return statement; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(statement).State = EntityState.Detached;
            return await FindAsync(statement.SchoolId,statement.TimetableSetupProfileId,statement.Revision,statement.Scope,statement.RendererVersion,ct)
                ?? throw new BellScheduleConflictException("Statement changed",ex);
        }
    }
}
