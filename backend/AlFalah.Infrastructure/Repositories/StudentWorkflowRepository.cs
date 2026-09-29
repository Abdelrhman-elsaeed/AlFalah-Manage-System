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

    public Task<bool> IsActiveGuardianProfileAsync(
        int schoolId,
        string guardianUserId,
        CancellationToken cancellationToken) =>
        _context.GuardianProfiles.AsNoTracking().AnyAsync(
            profile => profile.SchoolId == schoolId
                && profile.ApplicationUserId == guardianUserId
                && profile.IsActive
                && !profile.IsDeleted
                && profile.ApplicationUser.IsActive,
            cancellationToken);

    public Task<bool> IsGuardianLinkedToStudentAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians.AsNoTracking().AnyAsync(
            link => link.SchoolId == schoolId
                && link.StudentId == studentId
                && link.GuardianProfile.ApplicationUserId == guardianUserId
                && link.GuardianProfile.IsActive
                && !link.GuardianProfile.IsDeleted
                && link.Student.IsActive
                && !link.Student.IsDeleted
                && !link.IsDeleted
                && link.ValidFrom <= onDate
                && (link.ValidTo == null || link.ValidTo >= onDate),
            cancellationToken);

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

        if (query.AcademicTermId.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.Enrollments.Any(e =>
                e.SchoolId == schoolId
                && !e.IsDeleted
                && e.AcademicTermId == query.AcademicTermId.Value
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
        DateOnly effectiveOn,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsTracking()
            .Where(enrollment =>
                enrollment.SchoolId == schoolId
                && enrollment.StudentId == studentId
                && enrollment.Status == StudentEnrollmentStatus.Active
                && enrollment.AcademicTerm.IsActive
                && !enrollment.IsDeleted
                && !enrollment.AcademicTerm.IsDeleted
                && enrollment.EnrolledOn <= effectiveOn
                && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= effectiveOn)
                && enrollment.AcademicTerm.StartsOn <= effectiveOn
                && enrollment.AcademicTerm.EndsOn >= effectiveOn)
            .OrderByDescending(enrollment => enrollment.EnrolledOn)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<StudentEnrollmentTarget?> GetStudentEnrollmentTargetAsync(
        int schoolId,
        int classroomId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var targets = await _context.Classrooms
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
                        && !term.IsDeleted
                        && term.StartsOn <= effectiveOn
                        && term.EndsOn >= effectiveOn),
                (classroom, term) => new StudentEnrollmentTarget(classroom.Id, term.Id))
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return targets.Count == 1 ? targets[0] : null;
    }

    public Task<StudentEnrollmentTarget?> GetStudentEnrollmentTargetAsync(
        int schoolId,
        int classroomId,
        int academicTermId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken) =>
        _context.Classrooms
            .AsNoTracking()
            .Where(classroom => classroom.SchoolId == schoolId
                && classroom.Id == classroomId
                && classroom.IsActive
                && !classroom.IsDeleted)
            .SelectMany(
                classroom => _context.AcademicTerms.AsNoTracking().Where(term =>
                    term.SchoolId == schoolId
                    && term.Id == academicTermId
                    && term.AcademicYearId == classroom.AcademicYearId
                    && term.IsActive
                    && !term.IsDeleted
                    && term.StartsOn <= effectiveOn
                    && term.EndsOn >= effectiveOn),
                (classroom, term) => new StudentEnrollmentTarget(classroom.Id, term.Id))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasOverlappingStudentEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly startsOn,
        DateOnly? endsOn,
        int? excludingEnrollmentId,
        CancellationToken cancellationToken)
    {
        var effectiveEnd = endsOn ?? DateOnly.MaxValue;
        return _context.StudentEnrollments.AsNoTracking().AnyAsync(enrollment =>
            enrollment.SchoolId == schoolId
            && enrollment.StudentId == studentId
            && !enrollment.IsDeleted
            && (!excludingEnrollmentId.HasValue || enrollment.Id != excludingEnrollmentId.Value)
            && enrollment.EnrolledOn <= effectiveEnd
            && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= startsOn),
            cancellationToken);
    }

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
                && g.GuardianProfile.IsActive
                && g.GuardianProfile.ApplicationUser.IsActive
                && g.Student.IsActive
                && !g.Student.IsDeleted
                && g.ValidFrom <= onDate
                && (g.ValidTo == null || g.ValidTo >= onDate))
            .OrderBy(g => g.Student.FirstName)
            .ThenBy(g => g.Student.LastName)
            .ThenBy(g => g.Student.StudentNumber)
            .Select(g => new
            {
                g.StudentId,
                g.Student.StudentNumber,
                FullName = (g.Student.FirstName + " "
                    + (g.Student.MiddleName ?? string.Empty) + " "
                    + g.Student.LastName).Trim(),
                g.Student.IsActive,
                Enrollment = g.Student.Enrollments
                    .Where(e => e.SchoolId == schoolId
                        && !e.IsDeleted
                        && e.Status == StudentEnrollmentStatus.Active
                        && e.EnrolledOn <= onDate
                        && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)
                        && e.AcademicTerm.IsActive
                        && !e.AcademicTerm.IsDeleted
                        && e.AcademicTerm.StartsOn <= onDate
                        && e.AcademicTerm.EndsOn >= onDate
                        && e.Classroom.IsActive
                        && !e.Classroom.IsDeleted)
                    .OrderByDescending(e => e.EnrolledOn)
                    .Select(e => new { e.ClassroomId, e.Classroom.ClassLabel })
                    .FirstOrDefault(),
                g.CanSubmitExcuses,
                g.CanRequestGatePass,
                g.ReceivesNotifications
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return links.Select(g => new GuardianStudentDto(
            new StudentSummaryDto(
                g.StudentId,
                g.StudentNumber,
                g.FullName,
                g.Enrollment?.ClassroomId,
                g.Enrollment?.ClassLabel,
                g.IsActive,
                null),
            g.CanSubmitExcuses,
            g.CanRequestGatePass,
            g.ReceivesNotifications)).ToList();
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
                && !g.GuardianProfile.IsDeleted
                && g.GuardianProfile.IsActive
                && g.GuardianProfile.ApplicationUser.IsActive
                && g.Student.IsActive
                && !g.Student.IsDeleted
                && g.ValidFrom <= onDate
                && (g.ValidTo == null || g.ValidTo >= onDate))
            .Select(g => new
            {
                g.GuardianProfileId,
                g.StudentId,
                g.Student.StudentNumber,
                StudentName = (g.Student.FirstName + " "
                    + (g.Student.MiddleName ?? string.Empty) + " "
                    + g.Student.LastName).Trim(),
                g.Student.IsActive,
                Enrollment = g.Student.Enrollments
                    .Where(e => e.SchoolId == schoolId
                        && !e.IsDeleted
                        && e.Status == StudentEnrollmentStatus.Active
                        && e.EnrolledOn <= onDate
                        && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)
                        && e.AcademicTerm.IsActive
                        && !e.AcademicTerm.IsDeleted
                        && e.AcademicTerm.StartsOn <= onDate
                        && e.AcademicTerm.EndsOn >= onDate
                        && e.Classroom.IsActive
                        && !e.Classroom.IsDeleted)
                    .OrderByDescending(e => e.EnrolledOn)
                    .Select(e => new
                    {
                        e.ClassroomId,
                        e.Classroom.ClassLabel,
                        Stage = e.Classroom.Stage.ToString(),
                        e.Classroom.GradeLevel,
                        e.Classroom.Section,
                        e.AcademicTermId,
                        Semester = e.AcademicTerm.Semester.ToString(),
                        e.AcademicTerm.StartsOn,
                        e.AcademicTerm.EndsOn,
                        e.AcademicTerm.IsActive
                    })
                    .FirstOrDefault(),
                PendingSummons = _context.GuardianSummons.Count(s => s.SchoolId == schoolId
                    && s.StudentId == studentId
                    && s.GuardianProfileId == g.GuardianProfileId
                    && !s.IsDeleted
                    && s.Status == GuardianSummonStatus.Pending),
                ActiveGatePasses = _context.GatePasses.Count(gp => gp.SchoolId == schoolId
                    && gp.StudentId == studentId
                    && gp.RequestedByGuardianProfileId == g.GuardianProfileId
                    && !gp.IsDeleted
                    && (gp.Status == GatePassStatus.Requested
                        || gp.Status == GatePassStatus.Approved
                        || gp.Status == GatePassStatus.SecurityAcknowledged)),
                RecentRecognitions = _context.StudentRecognitions.Count(r => r.SchoolId == schoolId
                    && r.StudentId == studentId
                    && !r.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (link is null) return null;
        var studentSummary = new StudentSummaryDto(
            link.StudentId,
            link.StudentNumber,
            link.StudentName,
            link.Enrollment?.ClassroomId,
            link.Enrollment?.ClassLabel,
            link.IsActive,
            null);

        var contextDto = new StudentContextDto(
            studentSummary,
            link.Enrollment == null ? null : new AcademicTermSummaryDto(
                link.Enrollment.AcademicTermId,
                link.Enrollment.Semester,
                link.Enrollment.StartsOn,
                link.Enrollment.EndsOn,
                link.Enrollment.IsActive),
            link.Enrollment == null ? null : new ClassroomSummaryDto(
                link.Enrollment.ClassroomId,
                link.Enrollment.ClassLabel,
                link.Enrollment.Stage,
                link.Enrollment.GradeLevel,
                link.Enrollment.Section),
            null,
            Array.Empty<MetricBadgeDto>());

        return new GuardianStudentSummaryDto(
            contextDto,
            link.PendingSummons,
            link.ActiveGatePasses,
            link.RecentRecognitions);
    }

    public async Task<PagedResult<GuardianNotificationDto>> GetGuardianStudentNotificationsAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        StudentAffairsPageQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var source = _context.Notifications.AsNoTracking().Where(notification =>
            notification.SchoolId == schoolId
            && notification.UserId == guardianUserId
            && notification.StudentId == studentId
            && !notification.IsDeleted
            && !notification.RequiresApproval
            && !notification.IsSuppressed
            && notification.DeliveryStatus == NotificationDeliveryStatus.Delivered
            && _context.StudentGuardians.Any(link => link.SchoolId == schoolId
                && link.StudentId == studentId
                && !link.IsDeleted
                && link.GuardianProfile.ApplicationUserId == guardianUserId
                && link.GuardianProfile.IsActive
                && !link.GuardianProfile.IsDeleted
                && link.GuardianProfile.ApplicationUser.IsActive
                && link.Student.IsActive
                && !link.Student.IsDeleted
                && link.ValidFrom <= onDate
                && (link.ValidTo == null || link.ValidTo >= onDate)));

        var total = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await source
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new GuardianNotificationDto(
                notification.Id,
                studentId,
                notification.Type ?? string.Empty,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<GuardianNotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
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

        if (query.AcademicTermId.HasValue)
            dbQuery = dbQuery.Where(c => c.Enrollments.Any(e => e.SchoolId == schoolId
                && !e.IsDeleted
                && e.AcademicTermId == query.AcademicTermId.Value
                && e.Status == StudentEnrollmentStatus.Active));

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
            string.Empty,
            c.PhysicalLocation
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
            .Select(group => new ClassroomAcademicYearDto(
                group.Key.Id,
                group.Key.Code,
                group.Key.NameAr,
                group.Any(term => term.IsActive)))
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
            string.Empty,
            c.PhysicalLocation
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

    public Task<bool> AcademicYearExistsAsync(int schoolId, int academicYearId, CancellationToken cancellationToken) =>
        _context.AcademicTerms
            .AsNoTracking()
            .AnyAsync(term => term.SchoolId == schoolId
                && term.AcademicYearId == academicYearId
                && !term.IsDeleted,
                cancellationToken);

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
            "NoPublishedSchedule",
            "Teacher dashboard projection does not resolve the live lesson context",
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
            Array.Empty<TeacherGatePassAcknowledgementDto>(),
            Array.Empty<TeacherEntryPermitAcknowledgementDto>(),
            Array.Empty<string>()
        );

        return Task.FromResult(new TeacherStudentAffairsDashboardDto(
            topPriority,
            Array.Empty<DashboardCountDto>()
        ));
    }

    public async Task<OfficerStudentAffairsDashboardDto> GetOfficerDashboardAsync(
        int schoolId,
        string officerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pendingExcuses = await _context.AbsenceExcuses.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.Status == AbsenceExcuseStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var requestedGatePasses = await _context.GatePasses.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.Status == GatePassStatus.Requested, cancellationToken)
            .ConfigureAwait(false);

        var activeEntryPermits = await _context.ClassroomEntryPermits.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.ValidFrom <= now && now < x.ValidUntil
                && (x.Status == ClassroomEntryPermitStatus.Issued
                    || x.Status == ClassroomEntryPermitStatus.AcknowledgedByTeacher), cancellationToken)
            .ConfigureAwait(false);

        var pendingBehaviorNotices = await _context.BehaviorIncidents.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.GuardianDispatchDecision == GuardianDispatchDecision.PendingOfficerDecision,
                cancellationToken).ConfigureAwait(false);

        var pendingAcademicNotices = await _context.AcademicConcerns.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.GuardianDispatchDecision == GuardianDispatchDecision.PendingOfficerDecision,
                cancellationToken).ConfigureAwait(false);

        var openReferrals = await _context.StudentReferrals.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.Status != StudentReferralStatus.Resolved
                && x.Status != StudentReferralStatus.Closed, cancellationToken)
            .ConfigureAwait(false);

        var unassignedReferrals = await _context.StudentReferrals.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted
                && x.Status == StudentReferralStatus.Open
                && x.AssignedSocialWorkerUserId == null, cancellationToken)
            .ConfigureAwait(false);

        var automationReviews = await _context.GuardianSummons.AsNoTracking()
            .CountAsync(x => x.SchoolId == schoolId && !x.IsDeleted && x.RequiresOfficerReview,
                cancellationToken).ConfigureAwait(false);

        var unreadOfficerThreads = await _context.MessageReceipts.AsNoTracking()
            .Where(x => x.SchoolId == schoolId
                && x.RecipientUserId == officerUserId
                && x.ReadAt == null
                && x.DeliveryState == MessageDeliveryState.Delivered
                && x.ConversationMessage.ConversationThread.SchoolId == schoolId
                && !x.ConversationMessage.ConversationThread.IsDeleted
                && x.ConversationMessage.ConversationThread.ThreadType == ConversationThreadType.GuardianStudentAffairs)
            .Select(x => x.ConversationMessage.ConversationThreadId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var queues = new List<DashboardCountDto>
        {
            Queue("PendingExcuses", "أعذار غياب قيد المراجعة", pendingExcuses),
            Queue("RequestedGatePasses", "طلبات خروج قيد المراجعة", requestedGatePasses),
            Queue("ActiveEntryPermits", "تصاريح دخول صفية نشطة", activeEntryPermits),
            Queue("PendingBehaviorNotices", "إشعارات سلوكية قيد الاعتماد", pendingBehaviorNotices),
            Queue("PendingAcademicNotices", "إشعارات أكاديمية قيد الاعتماد", pendingAcademicNotices),
            Queue("OpenReferrals", "إحالات مفتوحة", openReferrals),
            Queue("UnassignedReferrals", "إحالات غير مسندة", unassignedReferrals),
            Queue("AutomationReviews", "مراجعات أثر الأتمتة", automationReviews),
            Queue("UnreadOfficerThreads", "محادثات غير مقروءة", unreadOfficerThreads)
        };

        return new OfficerStudentAffairsDashboardDto(queues, Array.Empty<DashboardCountDto>());

        static DashboardCountDto Queue(string code, string label, int count) =>
            new(code, label, count, count > 0 ? "warning" : "info");
    }

    public async Task<SocialWorkerStudentAffairsDashboardDto> GetSocialWorkerDashboardAsync(
        int schoolId,
        string socialWorkerUserId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var openCases = await _context.StudentReferrals
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId
                && r.AssignedSocialWorkerUserId == socialWorkerUserId
                && !r.IsDeleted
                && (r.Status == StudentReferralStatus.Assigned || r.Status == StudentReferralStatus.InProgress), cancellationToken)
            .ConfigureAwait(false);

        var pendingSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && s.ScheduledBySocialWorkerUserId == socialWorkerUserId && !s.IsDeleted && s.Status == GuardianSummonStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var attendedSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && s.ScheduledBySocialWorkerUserId == socialWorkerUserId && !s.IsDeleted && s.Status == GuardianSummonStatus.Attended, cancellationToken)
            .ConfigureAwait(false);

        var underObservationSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && s.ScheduledBySocialWorkerUserId == socialWorkerUserId && !s.IsDeleted && s.Status == GuardianSummonStatus.UnderObservation, cancellationToken)
            .ConfigureAwait(false);

        var improvedSummons = await _context.GuardianSummons
            .AsNoTracking()
            .CountAsync(s => s.SchoolId == schoolId && s.ScheduledBySocialWorkerUserId == socialWorkerUserId && !s.IsDeleted && s.Status == GuardianSummonStatus.Improved, cancellationToken)
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

    public async Task<GuardianStudentAffairsDashboardDto> GetGuardianDashboardAsync(
        int schoolId,
        string guardianUserId,
        DateOnly onDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recognitionSince = now.AddDays(-30);
        var rows = await _context.StudentGuardians.AsNoTracking()
            .Where(link => link.SchoolId == schoolId
                && !link.IsDeleted
                && link.GuardianProfile.ApplicationUserId == guardianUserId
                && link.GuardianProfile.IsActive
                && !link.GuardianProfile.IsDeleted
                && link.GuardianProfile.ApplicationUser.IsActive
                && link.Student.IsActive
                && !link.Student.IsDeleted
                && link.ValidFrom <= onDate
                && (link.ValidTo == null || link.ValidTo >= onDate))
            .OrderBy(link => link.Student.FirstName)
            .ThenBy(link => link.Student.LastName)
            .ThenBy(link => link.Student.StudentNumber)
            .Select(link => new
            {
                link.GuardianProfileId,
                link.StudentId,
                link.Student.StudentNumber,
                StudentName = (link.Student.FirstName + " "
                    + (link.Student.MiddleName ?? string.Empty) + " "
                    + link.Student.LastName).Trim(),
                link.Student.IsActive,
                link.CanSubmitExcuses,
                link.CanRequestGatePass,
                link.ReceivesNotifications,
                Enrollment = link.Student.Enrollments
                    .Where(enrollment => enrollment.SchoolId == schoolId
                        && !enrollment.IsDeleted
                        && enrollment.Status == StudentEnrollmentStatus.Active
                        && enrollment.EnrolledOn <= onDate
                        && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= onDate)
                        && enrollment.AcademicTerm.IsActive
                        && !enrollment.AcademicTerm.IsDeleted
                        && enrollment.AcademicTerm.StartsOn <= onDate
                        && enrollment.AcademicTerm.EndsOn >= onDate
                        && enrollment.Classroom.IsActive
                        && !enrollment.Classroom.IsDeleted)
                    .OrderByDescending(enrollment => enrollment.EnrolledOn)
                    .Select(enrollment => new
                    {
                        enrollment.ClassroomId,
                        enrollment.Classroom.ClassLabel,
                        Stage = enrollment.Classroom.Stage.ToString(),
                        enrollment.Classroom.GradeLevel,
                        enrollment.Classroom.Section,
                        enrollment.AcademicTermId,
                        Semester = enrollment.AcademicTerm.Semester.ToString(),
                        enrollment.AcademicTerm.StartsOn,
                        enrollment.AcademicTerm.EndsOn,
                        enrollment.AcademicTerm.IsActive
                    })
                    .FirstOrDefault(),
                OfficialAbsences = _context.DailyStudentAttendances.Count(attendance =>
                    attendance.SchoolId == schoolId
                    && attendance.StudentId == link.StudentId
                    && !attendance.IsDeleted
                    && (attendance.Status == StudentAttendanceStatus.Absent
                        || attendance.Status == StudentAttendanceStatus.AbsentExcused)),
                ExcusedAbsences = _context.DailyStudentAttendances.Count(attendance =>
                    attendance.SchoolId == schoolId
                    && attendance.StudentId == link.StudentId
                    && !attendance.IsDeleted
                    && attendance.Status == StudentAttendanceStatus.AbsentExcused),
                PendingExcuses = _context.AbsenceExcuses.Count(excuse =>
                    excuse.SchoolId == schoolId
                    && excuse.DailyStudentAttendance.StudentId == link.StudentId
                    && excuse.GuardianProfileId == link.GuardianProfileId
                    && !excuse.IsDeleted
                    && excuse.Status == AbsenceExcuseStatus.Pending),
                AcceptedExcuses = _context.AbsenceExcuses.Count(excuse =>
                    excuse.SchoolId == schoolId
                    && excuse.DailyStudentAttendance.StudentId == link.StudentId
                    && excuse.GuardianProfileId == link.GuardianProfileId
                    && !excuse.IsDeleted
                    && excuse.Status == AbsenceExcuseStatus.Accepted),
                RejectedExcuses = _context.AbsenceExcuses.Count(excuse =>
                    excuse.SchoolId == schoolId
                    && excuse.DailyStudentAttendance.StudentId == link.StudentId
                    && excuse.GuardianProfileId == link.GuardianProfileId
                    && !excuse.IsDeleted
                    && excuse.Status == AbsenceExcuseStatus.Rejected),
                ActiveGatePasses = _context.GatePasses.Count(gatePass =>
                    gatePass.SchoolId == schoolId
                    && gatePass.StudentId == link.StudentId
                    && gatePass.RequestedByGuardianProfileId == link.GuardianProfileId
                    && !gatePass.IsDeleted
                    && (gatePass.Status == GatePassStatus.Requested
                        || gatePass.Status == GatePassStatus.Approved
                        || gatePass.Status == GatePassStatus.SecurityAcknowledged)),
                ActiveEntryPermits = _context.ClassroomEntryPermits.Count(permit =>
                    permit.SchoolId == schoolId
                    && permit.StudentId == link.StudentId
                    && !permit.IsDeleted
                    && permit.ValidFrom <= now
                    && now < permit.ValidUntil
                    && (permit.Status == ClassroomEntryPermitStatus.Issued
                        || permit.Status == ClassroomEntryPermitStatus.AcknowledgedByTeacher)),
                PendingOrUpcomingSummons = _context.GuardianSummons.Count(summon =>
                    summon.SchoolId == schoolId
                    && summon.StudentId == link.StudentId
                    && summon.GuardianProfileId == link.GuardianProfileId
                    && !summon.IsDeleted
                    && summon.Status == GuardianSummonStatus.Pending),
                RecentRecognitions = _context.StudentRecognitions.Count(recognition =>
                    recognition.SchoolId == schoolId
                    && recognition.StudentId == link.StudentId
                    && !recognition.IsDeleted
                    && recognition.RecognizedAt >= recognitionSince),
                UnreadNotifications = _context.Notifications.Count(notification =>
                    notification.SchoolId == schoolId
                    && notification.UserId == guardianUserId
                    && notification.StudentId == link.StudentId
                    && !notification.IsDeleted
                    && !notification.RequiresApproval
                    && !notification.IsSuppressed
                    && notification.DeliveryStatus == NotificationDeliveryStatus.Delivered
                    && !notification.IsRead),
                UnreadThreads = _context.MessageReceipts
                    .Where(receipt => receipt.SchoolId == schoolId
                        && receipt.RecipientUserId == guardianUserId
                        && receipt.DeliveryState == MessageDeliveryState.Delivered
                        && receipt.ReadAt == null
                        && receipt.ConversationMessage.ConversationThread.StudentId == link.StudentId
                        && !receipt.ConversationMessage.ConversationThread.IsDeleted)
                    .Select(receipt => receipt.ConversationMessage.ConversationThreadId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var students = rows.Select(row =>
        {
            var student = new StudentSummaryDto(
                row.StudentId,
                row.StudentNumber,
                row.StudentName,
                row.Enrollment?.ClassroomId,
                row.Enrollment?.ClassLabel,
                row.IsActive,
                null);
            var term = row.Enrollment == null ? null : new AcademicTermSummaryDto(
                row.Enrollment.AcademicTermId,
                row.Enrollment.Semester,
                row.Enrollment.StartsOn,
                row.Enrollment.EndsOn,
                row.Enrollment.IsActive);
            var classroom = row.Enrollment == null ? null : new ClassroomSummaryDto(
                row.Enrollment.ClassroomId,
                row.Enrollment.ClassLabel,
                row.Enrollment.Stage,
                row.Enrollment.GradeLevel,
                row.Enrollment.Section);
            return new GuardianDashboardStudentDto(
                new StudentContextDto(student, term, classroom, null, Array.Empty<MetricBadgeDto>()),
                row.CanSubmitExcuses,
                row.CanRequestGatePass,
                row.ReceivesNotifications,
                new GuardianAbsenceSummaryDto(
                    row.OfficialAbsences,
                    row.ExcusedAbsences,
                    row.PendingExcuses,
                    row.AcceptedExcuses,
                    row.RejectedExcuses),
                row.ActiveGatePasses,
                row.ActiveEntryPermits,
                row.PendingOrUpcomingSummons,
                row.RecentRecognitions,
                row.UnreadNotifications,
                row.UnreadThreads);
        }).ToList();

        var unreadNotifications = students.Sum(student => student.UnreadNotifications);
        var unreadThreads = students.Sum(student => student.UnreadThreads);
        var actions = new List<DashboardCountDto>
        {
            new("PendingExcuses", "أعذار قيد المراجعة", students.Sum(student => student.Attendance.PendingExcuses), "warning"),
            new("ActiveGatePasses", "طلبات خروج نشطة", students.Sum(student => student.ActiveGatePasses), "info"),
            new("ActiveEntryPermits", "تصاريح دخول نشطة", students.Sum(student => student.ActiveEntryPermits), "info"),
            new("UpcomingSummons", "استدعاءات قادمة", students.Sum(student => student.PendingOrUpcomingSummons), "warning"),
            new("UnreadNotifications", "إشعارات غير مقروءة", unreadNotifications, "warning"),
            new("UnreadThreads", "محادثات غير مقروءة", unreadThreads, "warning")
        };

        return new GuardianStudentAffairsDashboardDto(
            students,
            actions,
            unreadNotifications,
            unreadThreads,
            now);
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
