using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.TeacherActions;

public sealed record TeacherActionScopeSnapshot(
    int InstructorProfileId,
    int AcademicTermId,
    int ClassroomId,
    int SchoolTimetableId,
    int SchoolTimetableEntryId,
    byte Period);

public interface ITeacherActionWorkflowRepository
{
    Task<TeacherActionScopeSnapshot?> ResolveScopeAsync(
        int schoolId,
        string teacherUserId,
        int studentId,
        int timetableEntryId,
        bool allowOverride,
        TimetableDay day,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken);

    Task<TeacherActionScopeSnapshot?> ResolveStudentEnrollmentScopeAsync(
        int schoolId,
        string teacherUserId,
        int studentId,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken);

    void Add(BehaviorIncident incident);
    void Add(AcademicConcern concern);
    void Add(SessionDelay delay);
    void Add(StudentRecognition recognition);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<BehaviorIncidentDto?> GetBehaviorDtoAsync(
        int schoolId,
        int incidentId,
        CancellationToken cancellationToken);
    Task<AcademicConcernDto?> GetAcademicConcernDtoAsync(
        int schoolId,
        int concernId,
        CancellationToken cancellationToken);
    Task<SessionDelayDto?> GetSessionDelayDtoAsync(
        int schoolId,
        int delayId,
        CancellationToken cancellationToken);
    Task<RecognitionDto?> GetRecognitionDtoAsync(
        int schoolId,
        int recognitionId,
        CancellationToken cancellationToken);
}
