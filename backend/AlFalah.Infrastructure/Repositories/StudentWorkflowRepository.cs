using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StudentWorkflowRepository : IStudentWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public StudentWorkflowRepository(AlFalahDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StudentGuardianLinkDto>> GetStudentGuardiansAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var rawLinks = await _context.StudentGuardians
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId && g.StudentId == studentId && !g.IsDeleted)
            .Include(g => g.GuardianProfile)
                .ThenInclude(gp => gp.ApplicationUser)
            .Include(g => g.Student)
            .OrderByDescending(g => g.IsPrimary)
            .ThenBy(g => g.GuardianProfile.ApplicationUser.FirstName)
            .Select(g => new
            {
                g.Id,
                g.GuardianProfileId,
                FirstName = g.GuardianProfile.ApplicationUser.FirstName,
                LastName = g.GuardianProfile.ApplicationUser.LastName,
                g.RelationshipType,
                g.IsPrimary,
                g.ReceivesNotifications,
                g.CanSubmitExcuses,
                g.CanRequestGatePass,
                g.ValidFrom,
                g.ValidTo,
                GuardianActive = g.GuardianProfile.IsActive && !g.GuardianProfile.IsDeleted,
                StudentActive = g.Student.IsActive && !g.Student.IsDeleted
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rawLinks.Select(g => new StudentGuardianLinkDto(
            g.Id,
            new GuardianSummaryDto(
                g.GuardianProfileId,
                $"{g.FirstName} {g.LastName}".Trim(),
                g.RelationshipType,
                g.IsPrimary,
                g.ReceivesNotifications
            ),
            g.CanSubmitExcuses,
            g.CanRequestGatePass,
            g.ValidFrom,
            g.ValidTo,
            g.GuardianActive && g.StudentActive && g.ValidFrom <= onDate && (g.ValidTo == null || g.ValidTo >= onDate),
            string.Empty
        )).ToList();
    }

    public async Task<PagedResult<StudentListItemDto>> GetStudentsAsync(
        int schoolId,
        StudentListQuery query,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && !s.IsDeleted);

        if (query.IsActive.HasValue)
            dbQuery = dbQuery.Where(s => s.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(s =>
                s.FirstName.Contains(search)
                || (s.MiddleName != null && s.MiddleName.Contains(search))
                || s.LastName.Contains(search)
                || s.StudentNumber.Contains(search)
                || (s.NationalId != null && s.NationalId.Contains(search)));
        }

        if (query.ClassroomId.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.Enrollments.Any(e =>
                e.SchoolId == schoolId
                && !e.IsDeleted
                && e.ClassroomId == query.ClassroomId.Value
                && e.Status == StudentEnrollmentStatus.Active
                && e.EnrolledOn <= onDate
                && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)));
        }

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var students = await dbQuery
            .OrderBy(s => s.FirstName)
            .ThenBy(s => s.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.StudentNumber,
                s.IdentityNumber,
                FullName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
                s.IsActive,
                s.ProfilePhotoStorageKey,
                Enrollment = s.Enrollments
                    .Where(e => e.SchoolId == schoolId && !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active && e.EnrolledOn <= onDate && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate))
                    .Select(e => new { e.ClassroomId, e.Classroom.ClassLabel })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = students.Select(s => new StudentListItemDto(
            new StudentSummaryDto(
                s.Id,
                s.StudentNumber,
                s.IdentityNumber,
                s.FullName,
                s.Enrollment?.ClassroomId,
                s.Enrollment?.ClassLabel,
                s.IsActive,
                s.ProfilePhotoStorageKey
            ),
            Array.Empty<MetricBadgeDto>()
        )).ToList();

        return new PagedResult<StudentListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StudentDetailsDto?> GetStudentDetailsAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var s = await _context.Students
            .AsNoTracking()
            .Where(st => st.SchoolId == schoolId && st.Id == studentId && !st.IsDeleted)
            .Include(st => st.Enrollments)
                .ThenInclude(e => e.Classroom)
            .Include(st => st.Enrollments)
                .ThenInclude(e => e.AcademicTerm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (s is null) return null;

        var activeEnrollment = s.Enrollments
            .Where(e => !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active && e.EnrolledOn <= onDate && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate))
            .FirstOrDefault();

        StudentEnrollmentDto? enrollmentDto = null;
        if (activeEnrollment != null)
        {
            enrollmentDto = new StudentEnrollmentDto(
                activeEnrollment.Id,
                new AcademicTermSummaryDto(
                    activeEnrollment.AcademicTermId,
                    $"{activeEnrollment.AcademicTerm.Semester} ({activeEnrollment.AcademicTerm.StartsOn:yyyy-MM-dd})",
                    activeEnrollment.AcademicTerm.StartsOn,
                    activeEnrollment.AcademicTerm.EndsOn,
                    activeEnrollment.AcademicTerm.IsActive
                ),
                new ClassroomSummaryDto(
                    activeEnrollment.ClassroomId,
                    activeEnrollment.Classroom.ClassLabel,
                    activeEnrollment.Classroom.Stage.ToString(),
                    activeEnrollment.Classroom.GradeLevel,
                    activeEnrollment.Classroom.Section
                ),
                activeEnrollment.RollNumber,
                activeEnrollment.EnrolledOn,
                activeEnrollment.WithdrawnOn,
                activeEnrollment.Status,
                string.Empty
            );
        }

        var guardians = await GetStudentGuardiansAsync(schoolId, studentId, onDate, cancellationToken).ConfigureAwait(false);

        var fullName = $"{s.FirstName} {s.MiddleName} {s.LastName}".Trim();
        var summary = new StudentSummaryDto(
            s.Id,
            s.StudentNumber,
            s.IdentityNumber,
            fullName,
            activeEnrollment?.ClassroomId,
            activeEnrollment?.Classroom?.ClassLabel,
            s.IsActive,
            s.ProfilePhotoStorageKey
        );

        var audit = new AuditSummaryDto(
            new ActorSummaryDto(s.CreatedByUserId, s.CreatedByUserId, "User"),
            s.CreatedAt,
            string.IsNullOrWhiteSpace(s.UpdatedByUserId) ? null : new ActorSummaryDto(s.UpdatedByUserId, s.UpdatedByUserId, "User"),
            s.UpdatedAt
        );

        return new StudentDetailsDto(
            summary,
            s.IdentityNumber,
            s.FirstName,
            s.MiddleName,
            s.LastName,
            s.NationalId,
            s.DateOfBirth,
            s.Gender,
            enrollmentDto,
            guardians,
            Array.Empty<MetricBadgeDto>(),
            Array.Empty<StudentTimelineItemDto>(),
            audit,
            string.Empty
        );
    }

    public Task<Student?> GetStudentForUpdateAsync(
        int schoolId,
        int studentId,
        CancellationToken cancellationToken) =>
        _context.Students
            .AsTracking()
            .Where(s => s.SchoolId == schoolId && s.Id == studentId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StudentEnrollment?> GetActiveStudentEnrollmentForUpdateAsync(
        int schoolId,
        int studentId,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsTracking()
            .Where(enrollment =>
                enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.AcademicTerm.IsActive
                && !enrollment.IsDeleted
                && !enrollment.AcademicTerm.IsDeleted)
            .OrderByDescending(enrollment => enrollment.EnrolledOn)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StudentEnrollmentTarget?> GetStudentEnrollmentTargetAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken) =>
        _context.Classrooms
            .AsNoTracking()
            .Where(classroom =>
                classroom.SchoolId == schoolId
                && classroom.Id == classroomId
                && classroom.IsActive
                && !classroom.IsDeleted)
            .SelectMany(
                classroom => _context.AcademicTerms
                    .AsNoTracking()
                    .Where(term =>
                        term.SchoolId == schoolId
                        && term.AcademicYearId == classroom.AcademicYearId
                        && term.IsActive
                        && !term.IsDeleted),
                (classroom, term) => new StudentEnrollmentTarget(classroom.Id, term.Id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> StudentNumberExistsAsync(
        int schoolId,
        string studentNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken) =>
        _context.Students
            .AsNoTracking()
            .AnyAsync(student =>
                student.SchoolId == schoolId
                && student.StudentNumber == studentNumber
                && !student.IsDeleted
                && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value),
                cancellationToken);

    public Task<bool> StudentIdentityNumberExistsAsync(
        int schoolId,
        string identityNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken) =>
        _context.Students
            .AsNoTracking()
            .AnyAsync(student =>
                student.SchoolId == schoolId
                && student.IdentityNumber == identityNumber
                && !student.IsDeleted
                && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value),
                cancellationToken);

    public Task<bool> StudentNationalIdExistsAsync(
        int schoolId,
        string nationalId,
        int? excludingStudentId,
        CancellationToken cancellationToken) =>
        _context.Students
            .AsNoTracking()
            .AnyAsync(student =>
                student.SchoolId == schoolId
                && student.NationalId == nationalId
                && !student.IsDeleted
                && (!excludingStudentId.HasValue || student.Id != excludingStudentId.Value),
                cancellationToken);

    public Task<StudentGuardian?> GetGuardianLinkForUpdateAsync(
        int schoolId,
        int studentId,
        int linkId,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .AsTracking()
            .Where(g => g.SchoolId == schoolId && g.StudentId == studentId && g.Id == linkId && !g.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StudentEnrollment?> GetEnrollmentForUpdateAsync(
        int schoolId,
        int studentId,
        int enrollmentId,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsTracking()
            .Where(e => e.SchoolId == schoolId && e.StudentId == studentId && e.Id == enrollmentId && !e.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedResult<StudentTimelineItemDto>> GetStudentTimelineAsync(
        int schoolId,
        int studentId,
        StudentTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var items = new List<StudentTimelineItemDto>();
        return Task.FromResult(new PagedResult<StudentTimelineItemDto>
        {
            Items = items,
            TotalCount = 0,
            Page = query.PageNumber <= 0 ? 1 : query.PageNumber,
            PageSize = query.PageSize <= 0 ? 20 : query.PageSize
        });
    }

    public async Task<StudentEnrollmentDto?> GetEnrollmentDtoAsync(
        int schoolId,
        int enrollmentId,
        CancellationToken cancellationToken)
    {
        var e = await _context.StudentEnrollments
            .AsNoTracking()
            .Where(en => en.SchoolId == schoolId && en.Id == enrollmentId && !en.IsDeleted)
            .Include(en => en.Classroom)
            .Include(en => en.AcademicTerm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (e is null) return null;

        return new StudentEnrollmentDto(
            e.Id,
            new AcademicTermSummaryDto(
                e.AcademicTermId,
                $"{e.AcademicTerm.Semester}",
                e.AcademicTerm.StartsOn,
                e.AcademicTerm.EndsOn,
                e.AcademicTerm.IsActive
            ),
            new ClassroomSummaryDto(
                e.ClassroomId,
                e.Classroom.ClassLabel,
                e.Classroom.Stage.ToString(),
                e.Classroom.GradeLevel,
                e.Classroom.Section
            ),
            e.RollNumber,
            e.EnrolledOn,
            e.WithdrawnOn,
            e.Status,
            string.Empty
        );
    }

    public async Task<StudentGuardianLinkDto?> GetGuardianLinkDtoAsync(
        int schoolId,
        int linkId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var g = await _context.StudentGuardians
            .AsNoTracking()
            .Where(link => link.SchoolId == schoolId && link.Id == linkId && !link.IsDeleted)
            .Include(link => link.GuardianProfile)
                .ThenInclude(gp => gp.ApplicationUser)
            .Include(link => link.Student)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (g is null) return null;

        var guardianSummary = new GuardianSummaryDto(
            g.GuardianProfileId,
            $"{g.GuardianProfile.ApplicationUser.FirstName} {g.GuardianProfile.ApplicationUser.LastName}".Trim(),
            g.RelationshipType,
            g.IsPrimary,
            g.ReceivesNotifications
        );

        var isActive = g.GuardianProfile.IsActive && g.Student.IsActive && g.ValidFrom <= onDate && (g.ValidTo == null || g.ValidTo >= onDate);

        return new StudentGuardianLinkDto(
            g.Id,
            guardianSummary,
            g.CanSubmitExcuses,
            g.CanRequestGatePass,
            g.ValidFrom,
            g.ValidTo,
            isActive,
            string.Empty
        );
    }

    public async Task<IReadOnlyList<GuardianStudentDto>> GetGuardianStudentsAsync(
        int schoolId,
        string guardianUserId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var links = await _context.StudentGuardians
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId
                && !g.IsDeleted
                && g.GuardianProfile.ApplicationUserId == guardianUserId
                && !g.GuardianProfile.IsDeleted
                && g.GuardianProfile.IsActive)
            .Include(g => g.Student)
                .ThenInclude(s => s.Enrollments)
                    .ThenInclude(e => e.Classroom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return links.Select(g =>
        {
            var enrollment = g.Student.Enrollments
                .FirstOrDefault(e => e.SchoolId == schoolId && !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active);
            var fullName = $"{g.Student.FirstName} {g.Student.MiddleName} {g.Student.LastName}".Trim();
            var summary = new StudentSummaryDto(
                g.Student.Id,
                g.Student.StudentNumber,
                g.Student.IdentityNumber,
                fullName,
                enrollment?.ClassroomId,
                enrollment?.Classroom?.ClassLabel,
                g.Student.IsActive,
                g.Student.ProfilePhotoStorageKey
            );

            return new GuardianStudentDto(summary, g.CanSubmitExcuses, g.CanRequestGatePass, g.ReceivesNotifications);
        }).ToList();
    }

    public async Task<GuardianStudentSummaryDto?> GetGuardianStudentSummaryAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var link = await _context.StudentGuardians
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId
                && g.StudentId == studentId
                && !g.IsDeleted
                && g.GuardianProfile.ApplicationUserId == guardianUserId
                && !g.GuardianProfile.IsDeleted)
            .Include(g => g.Student)
                .ThenInclude(s => s.Enrollments)
                    .ThenInclude(e => e.Classroom)
            .Include(g => g.Student)
                .ThenInclude(s => s.Enrollments)
                    .ThenInclude(e => e.AcademicTerm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (link is null) return null;

        var enrollment = link.Student.Enrollments
            .FirstOrDefault(e => e.SchoolId == schoolId && !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active);

        var fullName = $"{link.Student.FirstName} {link.Student.MiddleName} {link.Student.LastName}".Trim();
        var studentSummary = new StudentSummaryDto(
            link.Student.Id,
            link.Student.StudentNumber,
            link.Student.IdentityNumber,
            fullName,
            enrollment?.ClassroomId,
            enrollment?.Classroom?.ClassLabel,
            link.Student.IsActive,
            link.Student.ProfilePhotoStorageKey
        );

        var contextDto = new StudentContextDto(
            studentSummary,
            enrollment?.AcademicTerm == null ? null : new AcademicTermSummaryDto(enrollment.AcademicTerm.Id, $"{enrollment.AcademicTerm.Semester}", enrollment.AcademicTerm.StartsOn, enrollment.AcademicTerm.EndsOn, enrollment.AcademicTerm.IsActive),
            enrollment?.Classroom == null ? null : new ClassroomSummaryDto(enrollment.Classroom.Id, enrollment.Classroom.ClassLabel, enrollment.Classroom.Stage.ToString(), enrollment.Classroom.GradeLevel, enrollment.Classroom.Section),
            null,
            Array.Empty<MetricBadgeDto>()
        );

        var pendingSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && s.StudentId == studentId && !s.IsDeleted && s.Status == GuardianSummonStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var activeGatePasses = await _context.GatePasses
            .AsNoTracking()
            .CountAsync(gp => gp.SchoolId == schoolId && gp.StudentId == studentId && !gp.IsDeleted && (gp.Status == GatePassStatus.Requested || gp.Status == GatePassStatus.Approved), cancellationToken)
            .ConfigureAwait(false);

        var recentRecognitions = await _context.StudentRecognitions
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId && r.StudentId == studentId && !r.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return new GuardianStudentSummaryDto(contextDto, pendingSummons, activeGatePasses, recentRecognitions);
    }

    public Task<PagedResult<GuardianNotificationDto>> GetGuardianStudentNotificationsAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        StudentAffairsPageQuery query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new PagedResult<GuardianNotificationDto>
        {
            Items = new List<GuardianNotificationDto>(),
            TotalCount = 0,
            Page = query.PageNumber <= 0 ? 1 : query.PageNumber,
            PageSize = query.PageSize <= 0 ? 20 : query.PageSize
        });
    }

    public async Task<PagedResult<ClassroomDto>> GetClassroomsAsync(
        int schoolId,
        ClassroomListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.Classrooms
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId && !c.IsDeleted);

        if (query.AcademicYearId.HasValue)
            dbQuery = dbQuery.Where(c => c.AcademicYearId == query.AcademicYearId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(c => c.ClassLabel.Contains(search) || c.Section.Contains(search));
        }

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var classrooms = await dbQuery
            .OrderBy(c => c.Stage)
            .ThenBy(c => c.GradeLevel)
            .ThenBy(c => c.Section)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.AcademicYear)
            .Include(c => c.Enrollments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = classrooms.Select(c => new ClassroomDto(
            c.Id,
            c.ClassLabel,
            c.Stage,
            c.GradeLevel,
            c.Section,
            c.AcademicYearId,
            c.AcademicYear.NameAr,
            c.IsActive,
            c.Enrollments.Count(e => !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active),
            string.Empty
        )).ToList();

        return new PagedResult<ClassroomDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<ClassroomAcademicYearDto>> GetClassroomAcademicYearsAsync(
        CancellationToken cancellationToken) =>
        await _context.AcademicYears
            .AsNoTracking()
            .OrderByDescending(year => year.StartsOn)
            .Select(year => new ClassroomAcademicYearDto(year.Id, year.Code, year.NameAr, year.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ClassroomDto?> GetClassroomDtoAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken)
    {
        var c = await _context.Classrooms
            .AsNoTracking()
            .Where(cl => cl.SchoolId == schoolId && cl.Id == classroomId && !cl.IsDeleted)
            .Include(cl => cl.AcademicYear)
            .Include(cl => cl.Enrollments)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (c is null) return null;

        return new ClassroomDto(
            c.Id,
            c.ClassLabel,
            c.Stage,
            c.GradeLevel,
            c.Section,
            c.AcademicYearId,
            c.AcademicYear.NameAr,
            c.IsActive,
            c.Enrollments.Count(e => !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active),
            string.Empty
        );
    }

    public Task<Classroom?> GetClassroomForUpdateAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken) =>
        _context.Classrooms
            .AsTracking()
            .Where(c => c.SchoolId == schoolId && c.Id == classroomId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken) =>
        _context.AcademicYears
            .AsNoTracking()
            .AnyAsync(year => year.Id == academicYearId, cancellationToken);

    public Task<bool> ClassroomLabelExistsAsync(
        int schoolId,
        int academicYearId,
        string classLabel,
        int? excludingClassroomId,
        CancellationToken cancellationToken) =>
        _context.Classrooms
            .AsNoTracking()
            .AnyAsync(classroom =>
                classroom.SchoolId == schoolId
                && classroom.AcademicYearId == academicYearId
                && classroom.ClassLabel == classLabel
                && !classroom.IsDeleted
                && (!excludingClassroomId.HasValue || classroom.Id != excludingClassroomId.Value),
                cancellationToken);

    public Task<bool> HasActiveClassroomEnrollmentsAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsNoTracking()
            .AnyAsync(enrollment =>
                enrollment.SchoolId == schoolId
                && enrollment.ClassroomId == classroomId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && !enrollment.IsDeleted,
                cancellationToken);

    public async Task<int> UnassignActiveClassroomEnrollmentsAsync(
        int schoolId,
        int classroomId,
        DateOnly effectiveOn,
        DateTimeOffset changedAt,
        string changedByUserId,
        CancellationToken cancellationToken)
    {
        var enrollments = await _context.StudentEnrollments
            .AsTracking()
            .Where(enrollment =>
                enrollment.SchoolId == schoolId
                && enrollment.ClassroomId == classroomId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && !enrollment.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var enrollment in enrollments)
        {
            enrollment.Status = StudentEnrollmentStatus.Withdrawn;
            enrollment.WithdrawnOn = effectiveOn < enrollment.EnrolledOn
                ? enrollment.EnrolledOn
                : effectiveOn;
            enrollment.UpdatedAt = changedAt;
            enrollment.UpdatedByUserId = changedByUserId;
        }

        return enrollments.Count;
    }

    public async Task<int> UnassignActiveStudentEnrollmentsAsync(
        int schoolId,
        int studentId,
        DateOnly effectiveOn,
        DateTimeOffset changedAt,
        string changedByUserId,
        CancellationToken cancellationToken)
    {
        var enrollments = await _context.StudentEnrollments
            .AsTracking()
            .Where(enrollment =>
                enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && !enrollment.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var enrollment in enrollments)
        {
            enrollment.Status = StudentEnrollmentStatus.Withdrawn;
            enrollment.WithdrawnOn = effectiveOn < enrollment.EnrolledOn
                ? enrollment.EnrolledOn
                : effectiveOn;
            enrollment.UpdatedAt = changedAt;
            enrollment.UpdatedByUserId = changedByUserId;
        }

        return enrollments.Count;
    }

    public async Task<IReadOnlyList<StudentSummaryDto>> GetClassroomStudentsAsync(
        int schoolId,
        int classroomId,
        int? academicTermId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var enrollmentsQuery = _context.StudentEnrollments
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId
                && e.ClassroomId == classroomId
                && !e.IsDeleted
                && e.Status == StudentEnrollmentStatus.Active
                && e.EnrolledOn <= onDate
                && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)
                && e.Student.IsActive
                && !e.Student.IsDeleted);

        if (academicTermId.HasValue && academicTermId.Value > 0)
            enrollmentsQuery = enrollmentsQuery.Where(e => e.AcademicTermId == academicTermId.Value);

        var enrollments = await enrollmentsQuery
            .Include(e => e.Student)
            .Include(e => e.Classroom)
            .OrderBy(e => e.RollNumber ?? int.MaxValue)
            .ThenBy(e => e.Student.FirstName)
            .ThenBy(e => e.Student.LastName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return enrollments.Select(e =>
        {
            var fullName = $"{e.Student.FirstName} {e.Student.MiddleName} {e.Student.LastName}".Trim();
            return new StudentSummaryDto(
                e.Student.Id,
                e.Student.StudentNumber,
                e.Student.IdentityNumber,
                fullName,
                e.ClassroomId,
                e.Classroom.ClassLabel,
                e.Student.IsActive,
                e.Student.ProfilePhotoStorageKey
            );
        }).ToList();
    }

    public Task<TeacherStudentAffairsDashboardDto> GetTeacherDashboardAsync(
        int schoolId,
        string teacherUserId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var currentContext = new TeacherCurrentContextDto(
            new ActorSummaryDto(teacherUserId, teacherUserId, RoleNames.Instructor),
            DateTimeOffset.UtcNow,
            "Asia/Riyadh",
            1,
            null,
            Array.Empty<StudentSummaryDto>(),
            Array.Empty<string>()
        );

        var topPriority = new TeacherTopPriorityDto(
            currentContext,
            0,
            0,
            Array.Empty<string>()
        );

        return Task.FromResult(new TeacherStudentAffairsDashboardDto(
            topPriority,
            Array.Empty<DashboardCountDto>()
        ));
    }

    public Task<OfficerStudentAffairsDashboardDto> GetOfficerDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new OfficerStudentAffairsDashboardDto(
            Array.Empty<DashboardCountDto>(),
            Array.Empty<DashboardCountDto>()
        ));
    }

    public async Task<SocialWorkerStudentAffairsDashboardDto> GetSocialWorkerDashboardAsync(
        int schoolId,
        string socialWorkerUserId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var openCases = await _context.StudentReferrals
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId && !r.IsDeleted && (r.Status == StudentReferralStatus.Open || r.Status == StudentReferralStatus.Assigned || r.Status == StudentReferralStatus.InProgress), cancellationToken)
            .ConfigureAwait(false);

        var pendingSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && !s.IsDeleted && s.Status == GuardianSummonStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var attendedSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && !s.IsDeleted && s.Status == GuardianSummonStatus.Attended, cancellationToken)
            .ConfigureAwait(false);

        var underObservationSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && !s.IsDeleted && s.Status == GuardianSummonStatus.UnderObservation, cancellationToken)
            .ConfigureAwait(false);

        var improvedSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && !s.IsDeleted && s.Status == GuardianSummonStatus.Improved, cancellationToken)
            .ConfigureAwait(false);

        var casesList = new List<DashboardCountDto>
        {
            new("ActiveCases", "حالات المتابعة النشطة", openCases, openCases > 0 ? "warning" : "info")
        };

        var summonsList = new List<DashboardCountDto>
        {
            new("Pending", "بانتظار الموعد/الحضور", pendingSummons, pendingSummons > 0 ? "warning" : "info"),
            new("Attended", "تم الحضور", attendedSummons, "info"),
            new("UnderObservation", "تحت الملاحظة", underObservationSummons, "info"),
            new("Improved", "تحسّن", improvedSummons, "success")
        };

        return new SocialWorkerStudentAffairsDashboardDto(casesList, summonsList);
    }

    public Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SecurityStudentAffairsDashboardDto(
            Array.Empty<SecurityGatePassQueueItemDto>(),
            Array.Empty<DashboardCountDto>()
        ));
    }

    public Task<GuardianStudentAffairsDashboardDto> GetGuardianDashboardAsync(
        int schoolId,
        string guardianUserId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new GuardianStudentAffairsDashboardDto(
            Array.Empty<StudentContextDto>(),
            Array.Empty<DashboardCountDto>()
        ));
    }

    public Task<SchoolOversightDashboardDto> GetSchoolOversightDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SchoolOversightDashboardDto(
            0,
            0,
            0,
            Array.Empty<ClassroomAttendanceAggregateDto>(),
            Array.Empty<DashboardCountDto>(),
            Array.Empty<DashboardCountDto>(),
            DateTimeOffset.UtcNow
        ));
    }

    public async Task<StudentStatsPageResult> GetStudentsStatsAsync(
        int schoolId,
        StudentStatsQuery query,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 500);

        var totalClassrooms = await _context.Classrooms
            .AsNoTracking()
            .CountAsync(c => c.SchoolId == schoolId && !c.IsDeleted && c.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var dbQuery = _context.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && !s.IsDeleted);

        if (query.IsActive.HasValue)
            dbQuery = dbQuery.Where(s => s.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(s =>
                s.FirstName.Contains(search)
                || (s.MiddleName != null && s.MiddleName.Contains(search))
                || s.LastName.Contains(search)
                || s.StudentNumber.Contains(search)
                || (s.IdentityNumber != null && s.IdentityNumber.Contains(search))
                || (s.NationalId != null && s.NationalId.Contains(search)));
        }

        if (query.ClassroomId.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.Enrollments.Any(e =>
                e.SchoolId == schoolId
                && !e.IsDeleted
                && e.ClassroomId == query.ClassroomId.Value
                && e.Status == StudentEnrollmentStatus.Active
                && e.EnrolledOn <= onDate
                && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)));
        }

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projected = await dbQuery
            .OrderBy(s => s.FirstName)
            .ThenBy(s => s.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.StudentNumber,
                FullName = (s.FirstName + " " + (s.MiddleName ?? string.Empty) + " " + s.LastName).Trim(),
                s.IdentityNumber,
                s.NationalId,
                s.IsActive,
                Enrollment = s.Enrollments
                    .Where(e => e.SchoolId == schoolId && !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active && e.EnrolledOn <= onDate && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate))
                    .Select(e => new { e.ClassroomId, e.Classroom.ClassLabel })
                    .FirstOrDefault(),
                TotalAbsences = _context.DailyStudentAttendances.Count(a => a.SchoolId == schoolId && a.StudentId == s.Id && !a.IsDeleted && (a.Status == StudentAttendanceStatus.Absent || a.Status == StudentAttendanceStatus.AbsentExcused)),
                TotalDelays = _context.MorningArrivalDelays.Count(d => d.SchoolId == schoolId && d.StudentId == s.Id && !d.IsDeleted) + _context.SessionDelays.Count(sd => sd.SchoolId == schoolId && sd.StudentId == s.Id && !sd.IsDeleted),
                TotalExcuses = _context.DailyStudentAttendances.Count(a => a.SchoolId == schoolId && a.StudentId == s.Id && !a.IsDeleted && (a.ExcuseStatus != null || a.Status == StudentAttendanceStatus.AbsentExcused)),
                TotalReferrals = _context.StudentReferrals.Count(r => r.SchoolId == schoolId && r.StudentId == s.Id && !r.IsDeleted)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projected.Select(s => new StudentStatsDto(
            s.Id,
            s.StudentNumber,
            s.FullName,
            s.IdentityNumber,
            s.NationalId,
            s.Enrollment?.ClassLabel ?? "—",
            s.Enrollment?.ClassroomId,
            s.IsActive,
            s.TotalAbsences,
            s.TotalDelays,
            s.TotalExcuses,
            s.TotalReferrals
        )).ToList();

        return new StudentStatsPageResult
        {
            Items = items,
            TotalCount = total,
            TotalClassrooms = totalClassrooms,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StudentAnalyticsProfileDto?> GetStudentAnalyticsProfileAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var s = await _context.Students
            .AsNoTracking()
            .Where(st => st.SchoolId == schoolId && st.Id == studentId && !st.IsDeleted)
            .Include(st => st.Enrollments)
                .ThenInclude(e => e.Classroom)
            .Include(st => st.Enrollments)
                .ThenInclude(e => e.AcademicTerm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (s is null) return null;

        var activeEnrollment = s.Enrollments
            .Where(e => !e.IsDeleted && e.Status == StudentEnrollmentStatus.Active && e.EnrolledOn <= onDate && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate))
            .OrderByDescending(e => e.EnrolledOn)
            .FirstOrDefault()
            ?? s.Enrollments
                .Where(e => !e.IsDeleted)
                .OrderByDescending(e => e.EnrolledOn)
                .FirstOrDefault();

        var guardians = await GetStudentGuardiansAsync(schoolId, studentId, onDate, cancellationToken).ConfigureAwait(false);

        var attendances = await _context.DailyStudentAttendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId && a.StudentId == studentId && !a.IsDeleted)
            .Include(a => a.RecordedByUser)
            .OrderByDescending(a => a.AttendanceDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var morningDelays = await _context.MorningArrivalDelays
            .AsNoTracking()
            .Where(d => d.SchoolId == schoolId && d.StudentId == studentId && !d.IsDeleted)
            .OrderByDescending(d => d.ArrivalAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessionDelays = await _context.SessionDelays
            .AsNoTracking()
            .Where(sd => sd.SchoolId == schoolId && sd.StudentId == studentId && !sd.IsDeleted)
            .Include(sd => sd.ReportedByInstructorProfile)
            .OrderByDescending(sd => sd.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentAttendanceIds = attendances.Select(a => a.Id).ToList();
        var excuses = await _context.AbsenceExcuses
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId && !e.IsDeleted && studentAttendanceIds.Contains(e.DailyStudentAttendanceId))
            .Include(e => e.DailyStudentAttendance)
            .OrderByDescending(e => e.SubmittedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var referrals = await _context.StudentReferrals
            .AsNoTracking()
            .Where(r => r.SchoolId == schoolId && r.StudentId == studentId && !r.IsDeleted)
            .Include(r => r.AssignedSocialWorkerUser)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var behaviors = await _context.BehaviorIncidents
            .AsNoTracking()
            .Where(b => b.SchoolId == schoolId && b.StudentId == studentId && !b.IsDeleted)
            .OrderByDescending(b => b.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recognitions = await _context.StudentRecognitions
            .AsNoTracking()
            .Where(rec => rec.SchoolId == schoolId && rec.StudentId == studentId && !rec.IsDeleted)
            .OrderByDescending(rec => rec.RecognizedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gatePasses = await _context.GatePasses
            .AsNoTracking()
            .Where(gp => gp.SchoolId == schoolId && gp.StudentId == studentId && !gp.IsDeleted)
            .OrderByDescending(gp => gp.RequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalAbsences = attendances.Count(a => a.Status == StudentAttendanceStatus.Absent || a.Status == StudentAttendanceStatus.AbsentExcused);
        var totalDelays = morningDelays.Count + sessionDelays.Count;
        var totalExcuses = excuses.Count + attendances.Count(a => a.ExcuseStatus != null && !excuses.Any(e => e.DailyStudentAttendanceId == a.Id));
        var totalReferrals = referrals.Count;
        var totalBehaviors = behaviors.Count;
        var totalRecognitions = recognitions.Count;
        var totalGatePasses = gatePasses.Count;

        var monthsMap = new Dictionary<string, (string Label, int Absences, int Delays, int Excuses)>();
        var current = onDate;
        for (int i = 5; i >= 0; i--)
        {
            var targetDate = current.AddMonths(-i);
            var key = $"{targetDate.Year:D4}-{targetDate.Month:D2}";
            var label = GetArabicMonthName(targetDate.Month) + " " + targetDate.Year;
            monthsMap[key] = (label, 0, 0, 0);
        }

        foreach (var att in attendances.Where(a => a.Status == StudentAttendanceStatus.Absent || a.Status == StudentAttendanceStatus.AbsentExcused))
        {
            var key = $"{att.AttendanceDate.Year:D4}-{att.AttendanceDate.Month:D2}";
            if (monthsMap.TryGetValue(key, out var val))
            {
                monthsMap[key] = (val.Label, val.Absences + 1, val.Delays, val.Excuses);
            }
            else
            {
                var label = GetArabicMonthName(att.AttendanceDate.Month) + " " + att.AttendanceDate.Year;
                monthsMap[key] = (label, 1, 0, 0);
            }
        }

        foreach (var md in morningDelays)
        {
            var key = $"{md.SchoolLocalDate.Year:D4}-{md.SchoolLocalDate.Month:D2}";
            if (monthsMap.TryGetValue(key, out var val))
            {
                monthsMap[key] = (val.Label, val.Absences, val.Delays + 1, val.Excuses);
            }
            else
            {
                var label = GetArabicMonthName(md.SchoolLocalDate.Month) + " " + md.SchoolLocalDate.Year;
                monthsMap[key] = (label, 0, 1, 0);
            }
        }

        foreach (var sd in sessionDelays)
        {
            var date = DateOnly.FromDateTime(sd.OccurredAt.DateTime);
            var key = $"{date.Year:D4}-{date.Month:D2}";
            if (monthsMap.TryGetValue(key, out var val))
            {
                monthsMap[key] = (val.Label, val.Absences, val.Delays + 1, val.Excuses);
            }
            else
            {
                var label = GetArabicMonthName(date.Month) + " " + date.Year;
                monthsMap[key] = (label, 0, 1, 0);
            }
        }

        foreach (var ex in excuses)
        {
            var date = DateOnly.FromDateTime(ex.SubmittedAt.DateTime);
            var key = $"{date.Year:D4}-{date.Month:D2}";
            if (monthsMap.TryGetValue(key, out var val))
            {
                monthsMap[key] = (val.Label, val.Absences, val.Delays, val.Excuses + 1);
            }
            else
            {
                var label = GetArabicMonthName(date.Month) + " " + date.Year;
                monthsMap[key] = (label, 0, 0, 1);
            }
        }

        var monthlyTrends = monthsMap
            .OrderBy(kv => kv.Key)
            .Select(kv => new MonthlyAttendanceTrendDto(kv.Key, kv.Value.Label, kv.Value.Absences, kv.Value.Delays, kv.Value.Excuses))
            .ToList();

        var events = new List<StudentAnalyticsEventDto>();

        foreach (var att in attendances.Where(a => a.Status != StudentAttendanceStatus.Present))
        {
            var isExcused = att.Status == StudentAttendanceStatus.AbsentExcused || att.ExcuseStatus == AbsenceExcuseStatus.Accepted;
            var attTime = new DateTimeOffset(att.AttendanceDate.Year, att.AttendanceDate.Month, att.AttendanceDate.Day, 0, 0, 0, TimeSpan.Zero);
            var recordedBy = att.RecordedByUser != null
                ? string.Join(" ", new[] { att.RecordedByUser.FirstName, att.RecordedByUser.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim()
                : null;
            events.Add(new StudentAnalyticsEventDto(
                $"att-{att.Id}",
                "Absence",
                isExcused ? "غياب (بعذر مقبول)" : "غياب (بدون عذر)",
                $"تاريخ الغياب: {att.AttendanceDate:yyyy-MM-dd}" + (att.CorrectionReason != null ? $" - {att.CorrectionReason}" : ""),
                attTime,
                isExcused ? "info" : "danger",
                isExcused ? "pi pi-file-check" : "pi pi-calendar-times",
                isExcused ? "بعذر" : "بدون عذر",
                string.IsNullOrWhiteSpace(recordedBy) ? null : recordedBy
            ));
        }

        foreach (var md in morningDelays)
        {
            events.Add(new StudentAnalyticsEventDto(
                $"md-{md.Id}",
                "Delay",
                $"تأخر صباحي ({md.DelayMinutes} دقيقة)",
                string.IsNullOrWhiteSpace(md.Reason) ? "تسجيل تأخر في طابور الصباح" : $"السبب: {md.Reason}",
                md.ArrivalAt,
                "warning",
                "pi pi-clock",
                "تأخر صباحي",
                null
            ));
        }

        foreach (var sd in sessionDelays)
        {
            events.Add(new StudentAnalyticsEventDto(
                $"sd-{sd.Id}",
                "Delay",
                $"تأخر عن الحصة (الحصة {sd.Period})",
                $"تأخر {sd.DelayMinutes ?? 5} دقائق - {sd.Reason ?? "بدون سبب مدون"}",
                sd.OccurredAt,
                "warning",
                "pi pi-hourglass",
                $"حصة {sd.Period}",
                null
            ));
        }

        foreach (var ex in excuses)
        {
            var statusLabel = ex.Status switch
            {
                AbsenceExcuseStatus.Accepted => "مقبول",
                AbsenceExcuseStatus.Rejected => "مرفوض",
                _ => "قيد المراجعة"
            };
            var severity = ex.Status switch
            {
                AbsenceExcuseStatus.Accepted => "success",
                AbsenceExcuseStatus.Rejected => "danger",
                _ => "info"
            };
            events.Add(new StudentAnalyticsEventDto(
                $"exc-{ex.Id}",
                "Excuse",
                $"عذر غياب ({ex.ExcuseType}) - {statusLabel}",
                ex.GuardianNotes ?? ex.ReviewReason ?? "تم تقديم طلب العذر من ولي الأمر",
                ex.SubmittedAt,
                severity,
                "pi pi-paperclip",
                statusLabel,
                null
            ));
        }

        foreach (var refItem in referrals)
        {
            var workerName = refItem.AssignedSocialWorkerUser != null
                ? string.Join(" ", new[] { refItem.AssignedSocialWorkerUser.FirstName, refItem.AssignedSocialWorkerUser.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim()
                : null;
            events.Add(new StudentAnalyticsEventDto(
                $"ref-{refItem.Id}",
                "Referral",
                $"إحالة للموجه الطلابي ({refItem.SourceType})",
                $"الأولوية: {refItem.Priority} | الحالة: {refItem.Status}" + (refItem.RecommendedActions != null ? $" | التوصيات: {refItem.RecommendedActions}" : ""),
                refItem.CreatedAt,
                "danger",
                "pi pi-briefcase",
                refItem.Status.ToString(),
                string.IsNullOrWhiteSpace(workerName) ? null : workerName
            ));
        }

        foreach (var beh in behaviors)
        {
            events.Add(new StudentAnalyticsEventDto(
                $"beh-{beh.Id}",
                "Behavior",
                $"مخالفة سلوكية ({beh.CategoryCode})",
                $"الدرجة: {beh.Severity} - {beh.Description}" + (beh.ImmediateActionTaken != null ? $" - الإجراء: {beh.ImmediateActionTaken}" : ""),
                beh.OccurredAt,
                beh.Severity == BehaviorSeverity.Critical || beh.Severity == BehaviorSeverity.High ? "danger" : "warning",
                "pi pi-exclamation-triangle",
                beh.Severity.ToString(),
                null
            ));
        }

        foreach (var rec in recognitions)
        {
            events.Add(new StudentAnalyticsEventDto(
                $"rec-{rec.Id}",
                "Recognition",
                $"تكريم وتميز: {rec.Title}",
                rec.Description,
                rec.RecognizedAt,
                "success",
                "pi pi-star-fill",
                rec.RecognitionType,
                null
            ));
        }

        foreach (var gp in gatePasses)
        {
            events.Add(new StudentAnalyticsEventDto(
                $"gp-{gp.Id}",
                "GatePass",
                $"استئذان خروج ({gp.Status})",
                $"السبب: {gp.Reason} - المستلم: {gp.PickupPersonName}",
                gp.RequestedAt,
                gp.Status == GatePassStatus.Exited ? "success" : "info",
                "pi pi-sign-out",
                gp.Status.ToString(),
                null
            ));
        }

        var sortedEvents = events.OrderByDescending(e => e.OccurredAt).Take(50).ToList();

        var fullName = string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        return new StudentAnalyticsProfileDto(
            s.Id,
            s.StudentNumber,
            fullName,
            s.IdentityNumber,
            s.NationalId,
            s.DateOfBirth,
            s.Gender,
            s.IsActive,
            s.ProfilePhotoStorageKey,
            activeEnrollment?.ClassroomId,
            activeEnrollment?.Classroom?.ClassLabel ?? "—",
            activeEnrollment?.Classroom?.Stage.ToString() ?? "—",
            activeEnrollment?.Classroom?.GradeLevel,
            activeEnrollment?.Classroom?.Section ?? "—",
            activeEnrollment?.RollNumber,
            activeEnrollment?.Status,
            totalAbsences,
            totalDelays,
            totalExcuses,
            totalReferrals,
            totalBehaviors,
            totalRecognitions,
            totalGatePasses,
            monthlyTrends,
            sortedEvents,
            guardians
        );
    }

    private static string GetArabicMonthName(int month) => month switch
    {
        1 => "يناير",
        2 => "فبراير",
        3 => "مارس",
        4 => "أبريل",
        5 => "مايو",
        6 => "يونيو",
        7 => "يوليو",
        8 => "أغسطس",
        9 => "سبتمبر",
        10 => "أكتوبر",
        11 => "نوفمبر",
        12 => "ديسمبر",
        _ => $"شهر {month}"
    };

    public void AddStudent(Student student) => _context.Students.Add(student);
    public void AddEnrollment(StudentEnrollment enrollment) => _context.StudentEnrollments.Add(enrollment);
    public void AddGuardianLink(StudentGuardian link) => _context.StudentGuardians.Add(link);
    public void AddClassroom(Classroom classroom) => _context.Classrooms.Add(classroom);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}

