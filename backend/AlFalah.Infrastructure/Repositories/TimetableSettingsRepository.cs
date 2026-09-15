using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
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
            .GroupBy(term => new
            {
                term.AcademicYear.Id,
                term.AcademicYear.Code,
                term.AcademicYear.NameAr,
                term.AcademicYear.StartsOn
            })
            .OrderByDescending(group => group.Key.StartsOn)
            .Select(group => new TimetableSetupAcademicYearDto(
                group.Key.Id,
                group.Key.Code,
                group.Key.NameAr,
                group.Any(term => term.IsActive)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<AcademicYear?> GetAcademicYearByCodeForUpdateAsync(
        string normalizedCode,
        CancellationToken cancellationToken) =>
        _context.AcademicYears
            .AsTracking()
            .SingleOrDefaultAsync(year => year.Code == normalizedCode, cancellationToken);

    public async Task<IReadOnlyList<AcademicTerm>> GetAcademicTermsForUpdateAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        await _context.AcademicTerms
            .AsTracking()
            .Where(term => term.SchoolId == schoolId && !term.IsDeleted)
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

    public void Add(AcademicYear academicYear) => _context.AcademicYears.Add(academicYear);

    public void Add(AcademicTerm academicTerm) => _context.AcademicTerms.Add(academicTerm);

    public void Add(TimetableSetupProfile profile) => _context.TimetableSetupProfiles.Add(profile);

    public void WriteAcademicScopeAudit(
        int schoolId,
        string userId,
        AcademicYear academicYear,
        TimetableSemester activeSemester) =>
        _context.AuditLogs.Add(new AuditLog
        {
            SchoolId = schoolId,
            UserId = userId,
            Action = "Timetable.AcademicYear.Created",
            EntityName = nameof(AcademicYear),
            EntityId = academicYear.Id.ToString(),
            NewValues = JsonSerializer.Serialize(new
            {
                academicYear.Code,
                academicYear.NameAr,
                academicYear.StartsOn,
                academicYear.EndsOn,
                ActiveSemester = activeSemester
            }),
            Reason = "إضافة عام دراسي وربطه بإعدادات الجدول",
            CreatedAt = DateTimeOffset.UtcNow
        });

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
