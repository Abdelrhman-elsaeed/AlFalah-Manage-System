using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class SaveStudentAttendanceSheetCommandHandler
    : IRequestHandler<SubmitAbsentRosterCommand, ApiResponse<StudentAttendanceSheetDto>>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public SaveStudentAttendanceSheetCommandHandler(
        IAttendanceWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentAttendanceSheetDto>> Handle(
        SubmitAbsentRosterCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentAttendanceSheetDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Secretary)
            || !_currentUser.HasPermission(PermissionNames.AttendanceManageStudents))
            return ApiResponse<StudentAttendanceSheetDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.ClassroomId <= 0 || string.IsNullOrWhiteSpace(request.RosterRevision))
            return ApiResponse<StudentAttendanceSheetDto>.Fail("Classroom and roster revision are required");

        var absentIds = request.AbsentStudentIds ?? Array.Empty<int>();
        if (absentIds.Any(id => id <= 0) || absentIds.Count != absentIds.Distinct().Count())
            return ApiResponse<StudentAttendanceSheetDto>.Fail("Absent student IDs must be unique and valid");

        var roster = await _repository.GetActiveRosterAsync(
            schoolId.Value,
            request.ClassroomId,
            request.Date,
            cancellationToken).ConfigureAwait(false);
        if (roster.Count == 0)
            return ApiResponse<StudentAttendanceSheetDto>.Fail("No active enrollment roster was found");
        if (roster.Select(student => student.StudentId).Distinct().Count() != roster.Count
            || roster.Select(student => student.AcademicTermId).Distinct().Count() != 1)
            return ApiResponse<StudentAttendanceSheetDto>.Fail("The active classroom roster is ambiguous");

        var rosterIds = roster.Select(student => student.StudentId).ToHashSet();
        var nonRosterIds = absentIds.Where(id => !rosterIds.Contains(id)).ToArray();
        if (nonRosterIds.Length > 0)
            return ApiResponse<StudentAttendanceSheetDto>.Fail(
                $"Students are not actively enrolled in this classroom: {string.Join(", ", nonRosterIds)}");

        var existingRows = await _repository.GetAttendanceSheetForUpdateAsync(
            schoolId.Value,
            request.ClassroomId,
            request.Date,
            cancellationToken).ConfigureAwait(false);
        var existingByStudent = existingRows.ToDictionary(attendance => attendance.StudentId);
        var absentSet = absentIds.ToHashSet();
        var now = _timeProvider.GetUtcNow();

        foreach (var rosterStudent in roster)
        {
            var desiredStatus = absentSet.Contains(rosterStudent.StudentId)
                ? StudentAttendanceStatus.Absent
                : StudentAttendanceStatus.Present;
            var isNewAbsence = desiredStatus == StudentAttendanceStatus.Absent;

            if (!existingByStudent.TryGetValue(rosterStudent.StudentId, out var attendance))
            {
                attendance = new DailyStudentAttendance
                {
                    SchoolId = schoolId.Value,
                    StudentId = rosterStudent.StudentId,
                    AcademicTermId = rosterStudent.AcademicTermId,
                    ClassroomId = request.ClassroomId,
                    AttendanceDate = request.Date,
                    Status = desiredStatus,
                    RecordedByUserId = userId,
                    RecordedAt = now,
                    Source = StudentAttendanceSource.SecretaryRoster,
                    CreatedAt = now,
                    CreatedByUserId = userId,
                    UpdatedAt = now,
                    UpdatedByUserId = userId
                };
                _repository.AddAttendance(attendance);
            }
            else
            {
                isNewAbsence = desiredStatus == StudentAttendanceStatus.Absent
                    && attendance.Status != StudentAttendanceStatus.Absent;
                attendance.Status = desiredStatus;
                attendance.ExcuseStatus = desiredStatus == StudentAttendanceStatus.Present
                    ? null
                    : attendance.ExcuseStatus;
                attendance.RecordedByUserId = userId;
                attendance.RecordedAt = now;
                attendance.Source = StudentAttendanceSource.SecretaryRoster;
                attendance.UpdatedAt = now;
                attendance.UpdatedByUserId = userId;
            }

            if (isNewAbsence)
            {
                attendance.AppendDomainEvent(new StudentAbsentRecordedEvent(
                    Guid.NewGuid(),
                    attendance.Id,
                    attendance.StudentId,
                    attendance.SchoolId,
                    attendance.AcademicTermId,
                    attendance.ClassroomId,
                    attendance.AttendanceDate,
                    userId,
                    now,
                    now));
            }
        }

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is AttendanceConcurrencyException
            or AttendancePersistenceConflictException)
        {
            return ApiResponse<StudentAttendanceSheetDto>.Fail(
                "The attendance sheet was changed by another request");
        }

        var dto = await _repository.GetAttendanceSheetDtoAsync(
            schoolId.Value,
            request.ClassroomId,
            request.Date,
            request.RosterRevision,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved attendance sheet could not be loaded");
        return ApiResponse<StudentAttendanceSheetDto>.Success(dto, "Attendance sheet saved successfully");
    }
}
