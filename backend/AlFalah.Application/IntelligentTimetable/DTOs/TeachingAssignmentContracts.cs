using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record TeachingMemberRequest(int TeacherTimetableProfileId, int AllocatedPeriodCount, int AllocatedPairedBlockCount);
// Empty members explicitly unassign this cell. Other cells remain unchanged.
public sealed record TeachingCellRequest(int ClassSubjectRequirementId, string Mode, IReadOnlyList<TeachingMemberRequest> Members);
public sealed record SaveTeachingAssignmentsRequest(int Revision, IReadOnlyList<TeachingCellRequest> Changes);
public sealed record TeachingMemberDto(int TeacherTimetableProfileId, int InstructorProfileId, string Name,
    int AllocatedPeriodCount, int AllocatedPairedBlockCount);
public sealed record TeachingAssignmentDto(int Id, int ClassSubjectRequirementId, string Mode, IReadOnlyList<TeachingMemberDto> Members);
public sealed record TeachingTeacherDto(int Id, int InstructorProfileId, string Name, string? Specialization, bool IsActive,
    int AllocatedPeriods, int MaximumWeeklyPeriods, int RemainingPeriods, int SubjectCount, int ClassroomCount,
    bool IsVisiting, IReadOnlyList<string> Warnings);
public sealed record TeachingTeacherSearch(string? Search = null, string? Specialization = null, bool? IsActive = true,
    bool? IsVisiting = null, bool HasCapacity = false, string Sort = "name", bool Descending = false, int Page = 1, int PageSize = 10);
public sealed record TeachingTeacherPage(IReadOnlyList<TeachingTeacherDto> Items, int TotalCount);
public sealed record TeachingAssignmentsOverview(int Revision, IReadOnlyList<SubjectDto> Subjects,
    IReadOnlyList<SubjectClassroomDto> Classrooms, IReadOnlyList<SubjectRequirementDto> Requirements,
    IReadOnlyList<TeachingAssignmentDto> Assignments, IReadOnlyList<TeachingTeacherDto> Teachers,
    IReadOnlyList<string> Warnings);
public sealed record GetTeachingAssignmentsQuery(int SetupId) : IRequest<ApiResponse<TeachingAssignmentsOverview>>;
public sealed record SearchTeachingTeachersQuery(int SetupId, TeachingTeacherSearch Search) : IRequest<ApiResponse<TeachingTeacherPage>>;
public sealed record SaveTeachingAssignmentsCommand(int SetupId, SaveTeachingAssignmentsRequest Request) : IRequest<ApiResponse<TeachingAssignmentsOverview>>;
public sealed record UnassignTeachingSubjectCommand(int SetupId, int RequirementId, int Revision) : IRequest<ApiResponse<TeachingAssignmentsOverview>>;
