using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record SubjectDto(int Id, string Name, string Color, int Revision);
public sealed record RoomDto(int Id, string Name);
public sealed record SubjectClassroomDto(int Id, string Name, int Stage, int GradeLevel);
public sealed record FixedSubjectSlot(int Day, int Period);
public sealed record SubjectRulesRequest(int IndividualPeriodCount, int PairedBlockCount, string TimePreference,
    int? EarliestPeriodSequence, int? LatestPreferredPeriodSequence, IReadOnlyList<int> AllowedDays,
    IReadOnlyList<FixedSubjectSlot> FixedSlots, IReadOnlyList<int> RoomIds, int? PreferredRoomId);
public sealed record SubjectRequirementDto(int Id, int SubjectId, int ClassroomId, string ClassroomName, int Revision,
    int TotalWeeklyPeriods, SubjectRulesRequest Rules);
public sealed record SubjectOverviewDto(IReadOnlyList<SubjectDto> Subjects, IReadOnlyList<SubjectClassroomDto> Classrooms,
    IReadOnlyList<RoomDto> Rooms, IReadOnlyList<SubjectRequirementDto> Requirements, BellScheduleDto? Schedule);
public sealed record SaveSubjectRequest(string Name, string Color, int Revision = 0);
public sealed record CreateRoomRequest(string Name);
// Zero revision means the caller saw no existing row. Existing rows are never replaced implicitly.
public sealed record SubjectClassTarget(int ClassroomId, int Revision);
public sealed record AllocateSubjectRequest(int SubjectId, IReadOnlyList<SubjectClassTarget> Classes,
    SubjectRulesRequest Rules, bool OverwriteExisting = false);
public sealed record UpdateSubjectRequirementsRequest(int Revision, SubjectRulesRequest Rules);
public sealed record SubjectAllocationResult(int ClassroomId, string ClassroomName, string Status, string? Message);
public sealed record SubjectBulkResult(IReadOnlyList<SubjectAllocationResult> Results);
public sealed record GetSubjectsQuery(int SetupId) : IRequest<ApiResponse<SubjectOverviewDto>>;
public sealed record CreateSubjectCommand(int SetupId, SaveSubjectRequest Request) : IRequest<ApiResponse<SubjectDto>>;
public sealed record UpdateSubjectCommand(int SetupId, int SubjectId, SaveSubjectRequest Request) : IRequest<ApiResponse<SubjectDto>>;
public sealed record CreateSubjectRoomCommand(int SetupId, CreateRoomRequest Request) : IRequest<ApiResponse<RoomDto>>;
public sealed record AllocateSubjectToClassesCommand(int SetupId, AllocateSubjectRequest Request) : IRequest<ApiResponse<SubjectBulkResult>>;
public sealed record UpdateSubjectRequirementsCommand(int SetupId, int RequirementId, UpdateSubjectRequirementsRequest Request) : IRequest<ApiResponse<SubjectBulkResult>>;
public sealed record RemoveSubjectRequirementCommand(int SetupId, int RequirementId, int Revision) : IRequest<ApiResponse<int>>;
