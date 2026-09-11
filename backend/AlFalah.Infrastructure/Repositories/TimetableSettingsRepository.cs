using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class TimetableSettingsRepository : ITimetableSettingsRepository
{
    private readonly AlFalahDbContext _context;

    public TimetableSettingsRepository(AlFalahDbContext context) => _context = context;

    public Task<string?> GetSchoolNameAsync(int schoolId, CancellationToken cancellationToken) =>
        _context.Schools
            .AsNoTracking()
            .Where(x => x.Id == schoolId && x.IsActive && !x.IsDeleted)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TimetableSetupAcademicYearDto>> GetAcademicYearsAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        await _context.AcademicTerms
            .AsNoTracking()
            .Where(term => term.SchoolId == schoolId && !term.IsDeleted)
            .Select(term => term.AcademicYear)
            .Distinct()
            .OrderByDescending(year => year.StartsOn)
            .Select(year => new TimetableSetupAcademicYearDto(year.Id, year.Code, year.NameAr, year.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TimetableSetupProfileDto>> GetProfilesAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        CancellationToken cancellationToken) =>
        await BuildProfilesQuery(
                _context.TimetableSetupProfiles.AsNoTracking(),
                schoolId,
                academicYearId,
                semester)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<TimetableSetupProfileDto?> GetProfileDtoAsync(
        int schoolId,
        int profileId,
        CancellationToken cancellationToken) =>
        ProjectProfiles(_context.TimetableSetupProfiles.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.Id == profileId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<TimetableSetupProfile?> GetProfileForUpdateAsync(
        int schoolId,
        int profileId,
        CancellationToken cancellationToken) =>
        _context.TimetableSetupProfiles
            .AsTracking()
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == profileId, cancellationToken);

    public async Task<TimetableReadinessData> GetReadinessDataAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        CancellationToken cancellationToken)
    {
        var activeClassrooms = _context.Classrooms
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId
                && x.AcademicYearId == academicYearId
                && x.IsActive
                && !x.IsDeleted);

        var classroomCount = await activeClassrooms.CountAsync(cancellationToken).ConfigureAwait(false);
        var missingLocationCount = await activeClassrooms
            .CountAsync(x => x.PhysicalLocation == null || x.PhysicalLocation == "", cancellationToken)
            .ConfigureAwait(false);
        var teacherCount = await _context.InstructorProfiles
            .AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && x.IsActive && !x.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        var termId = await _context.AcademicTerms
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester
                && !x.IsDeleted)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var studentCount = termId.HasValue
            ? await _context.StudentEnrollments
                .AsNoTracking()
                .CountAsync(x => x.SchoolId == schoolId
                    && x.AcademicTermId == termId.Value
                    && x.Status == StudentEnrollmentStatus.Active
                    && !x.IsDeleted,
                    cancellationToken)
                .ConfigureAwait(false)
            : 0;

        return new TimetableReadinessData(classroomCount, studentCount, missingLocationCount, teacherCount);
    }

    public Task<bool> AcademicScopeExistsAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        CancellationToken cancellationToken) =>
        _context.AcademicTerms
            .AsNoTracking()
            .AnyAsync(x => x.SchoolId == schoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester
                && !x.IsDeleted,
                cancellationToken);

    public Task<bool> ProfileNameExistsAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        string normalizedName,
        int? excludingProfileId,
        CancellationToken cancellationToken) =>
        _context.TimetableSetupProfiles
            .AsNoTracking()
            .AnyAsync(x => x.SchoolId == schoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester
                && x.Name == normalizedName
                && (!excludingProfileId.HasValue || x.Id != excludingProfileId.Value),
                cancellationToken);

    public Task<bool> HasEditorGrantAsync(int schoolId, string userId, CancellationToken cancellationToken) =>
        _context.TimetableEditorGrants
            .AsNoTracking()
            .AnyAsync(x => x.SchoolId == schoolId
                && x.ModeratorUserId == userId
                && !x.IsDeleted,
                cancellationToken);

    public void Add(TimetableSetupProfile profile) => _context.TimetableSetupProfiles.Add(profile);

    public void WriteAudit(
        int schoolId,
        string userId,
        string action,
        TimetableSetupProfile profile,
        object? oldValues,
        object? newValues) =>
        _context.AuditLogs.Add(new AuditLog
        {
            SchoolId = schoolId,
            UserId = userId,
            Action = action,
            EntityName = nameof(TimetableSetupProfile),
            EntityId = profile.Id.ToString(),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Reason = action.EndsWith("Created", StringComparison.Ordinal) ? "إنشاء ملف إعداد الجدول" : "تحديث إعدادات الجدول",
            CreatedAt = DateTimeOffset.UtcNow
        });

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
        {
            await action(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await action(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable<TimetableSetupProfileDto> ProjectProfiles(IQueryable<TimetableSetupProfile> query) =>
        query.Select(x => new TimetableSetupProfileDto(
            x.Id,
            x.SchoolId,
            x.AcademicYearId,
            x.AcademicYear.NameAr,
            (int)x.Semester,
            x.Semester == TimetableSemester.First ? "الفصل الدراسي الأول" : "الفصل الدراسي الثاني",
            x.Name,
            x.BellScheduleTemplateId,
            x.Status,
            x.Status == TimetableSetupStatus.Draft
                ? "مسودة"
                : x.Status == TimetableSetupStatus.ReadyForGeneration
                    ? "جاهز للتوليد"
                    : x.Status == TimetableSetupStatus.Generated
                        ? "تم توليد الجدول"
                        : "مؤرشف",
            x.Revision,
            x.UpdatedAt));

    internal static IQueryable<TimetableSetupProfileDto> BuildProfilesQuery(
        IQueryable<TimetableSetupProfile> query,
        int schoolId,
        int academicYearId,
        TimetableSemester semester) =>
        ProjectProfiles(query
            .Where(x => x.SchoolId == schoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester)
            .OrderByDescending(x => x.UpdatedAt));
}
