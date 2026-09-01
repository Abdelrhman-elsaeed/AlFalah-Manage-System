using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.DTOs.Guardian;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Students;

public sealed record StudentEnrollmentTarget(int ClassroomId, int AcademicTermId);

public interface IStudentWorkflowRepository
{
    Task<IReadOnlyList<StudentGuardianLinkDto>> GetStudentGuardiansAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<PagedResult<StudentListItemDto>> GetStudentsAsync(
        int schoolId,
        StudentListQuery query,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<StudentDetailsDto?> GetStudentDetailsAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<Student?> GetStudentForUpdateAsync(
        int schoolId,
        int studentId,
        CancellationToken cancellationToken);

    Task<StudentEnrollment?> GetActiveStudentEnrollmentForUpdateAsync(
        int schoolId,
        int studentId,
        CancellationToken cancellationToken);

    Task<StudentEnrollmentTarget?> GetStudentEnrollmentTargetAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken);

    Task<bool> StudentNumberExistsAsync(
        int schoolId,
        string studentNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken);

    Task<bool> StudentIdentityNumberExistsAsync(
        int schoolId,
        string identityNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken);

    Task<bool> StudentNationalIdExistsAsync(
        int schoolId,
        string nationalId,
        int? excludingStudentId,
        CancellationToken cancellationToken);

    Task<StudentGuardian?> GetGuardianLinkForUpdateAsync(
        int schoolId,
        int studentId,
        int linkId,
        CancellationToken cancellationToken);

    Task<StudentEnrollment?> GetEnrollmentForUpdateAsync(
        int schoolId,
        int studentId,
        int enrollmentId,
        CancellationToken cancellationToken);

    Task<PagedResult<StudentTimelineItemDto>> GetStudentTimelineAsync(
        int schoolId,
        int studentId,
        StudentTimelineQuery query,
        CancellationToken cancellationToken);

    Task<StudentEnrollmentDto?> GetEnrollmentDtoAsync(
        int schoolId,
        int enrollmentId,
        CancellationToken cancellationToken);

    Task<StudentGuardianLinkDto?> GetGuardianLinkDtoAsync(
        int schoolId,
        int linkId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuardianStudentDto>> GetGuardianStudentsAsync(
        int schoolId,
        string guardianUserId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<GuardianStudentSummaryDto?> GetGuardianStudentSummaryAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<PagedResult<GuardianNotificationDto>> GetGuardianStudentNotificationsAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        StudentAffairsPageQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<ClassroomDto>> GetClassroomsAsync(
        int schoolId,
        ClassroomListQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassroomAcademicYearDto>> GetClassroomAcademicYearsAsync(
        CancellationToken cancellationToken);

    Task<ClassroomDto?> GetClassroomDtoAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken);

    Task<Classroom?> GetClassroomForUpdateAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken);

    Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken);

    Task<bool> ClassroomLabelExistsAsync(
        int schoolId,
        int academicYearId,
        string classLabel,
        int? excludingClassroomId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveClassroomEnrollmentsAsync(
        int schoolId,
        int classroomId,
        CancellationToken cancellationToken);

    Task<int> UnassignActiveClassroomEnrollmentsAsync(
        int schoolId,
        int classroomId,
        DateOnly effectiveOn,
        DateTimeOffset changedAt,
        string changedByUserId,
        CancellationToken cancellationToken);

    Task<int> UnassignActiveStudentEnrollmentsAsync(
        int schoolId,
        int studentId,
        DateOnly effectiveOn,
        DateTimeOffset changedAt,
        string changedByUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSummaryDto>> GetClassroomStudentsAsync(
        int schoolId,
        int classroomId,
        int? academicTermId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<TeacherStudentAffairsDashboardDto> GetTeacherDashboardAsync(
        int schoolId,
        string teacherUserId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<OfficerStudentAffairsDashboardDto> GetOfficerDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<SocialWorkerStudentAffairsDashboardDto> GetSocialWorkerDashboardAsync(
        int schoolId,
        string socialWorkerUserId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<SecurityStudentAffairsDashboardDto> GetSecurityDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<GuardianStudentAffairsDashboardDto> GetGuardianDashboardAsync(
        int schoolId,
        string guardianUserId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<SchoolOversightDashboardDto> GetSchoolOversightDashboardAsync(
        int schoolId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<StudentStatsPageResult> GetStudentsStatsAsync(
        int schoolId,
        StudentStatsQuery query,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<StudentAnalyticsProfileDto?> GetStudentAnalyticsProfileAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    void AddStudent(Student student);
    void AddEnrollment(StudentEnrollment enrollment);
    void AddGuardianLink(StudentGuardian link);
    void AddClassroom(Classroom classroom);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
