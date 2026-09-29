using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Teacher;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Dashboards;

public sealed record DashboardCountDto(string Code, string Label, int Count, string Severity);
public sealed record TeacherStudentAffairsDashboardDto(TeacherTopPriorityDto TopPriority, IReadOnlyList<DashboardCountDto> Counts);
public sealed record OfficerStudentAffairsDashboardDto(IReadOnlyList<DashboardCountDto> Queues, IReadOnlyList<DashboardCountDto> ThresholdAlerts);
public sealed record SocialWorkerStudentAffairsDashboardDto(IReadOnlyList<DashboardCountDto> Cases, IReadOnlyList<DashboardCountDto> Summons);
public sealed record SecurityStudentAffairsDashboardDto(IReadOnlyList<SecurityGatePassQueueItemDto> ApprovedGatePasses, IReadOnlyList<DashboardCountDto> Counts);
public sealed record GuardianAbsenceSummaryDto(
    int OfficialAbsences,
    int ExcusedAbsences,
    int PendingExcuses,
    int AcceptedExcuses,
    int RejectedExcuses);
public sealed record GuardianDashboardStudentDto(
    StudentContextDto Context,
    bool CanSubmitExcuses,
    bool CanRequestGatePass,
    bool ReceivesNotifications,
    GuardianAbsenceSummaryDto Attendance,
    int ActiveGatePasses,
    int ActiveEntryPermits,
    int PendingOrUpcomingSummons,
    int RecentRecognitions,
    int UnreadNotifications,
    int UnreadThreads);
public sealed record GuardianStudentAffairsDashboardDto(
    IReadOnlyList<GuardianDashboardStudentDto> Students,
    IReadOnlyList<DashboardCountDto> Actions,
    int UnreadNotifications,
    int UnreadThreads,
    DateTimeOffset GeneratedAt);
public sealed record ClassroomAttendanceAggregateDto(int ClassroomId, string ClassLabel, int Present, int Absent, int AbsentExcused);
public sealed record SchoolOversightDashboardDto(
    int Present,
    int Absent,
    int AbsentExcused,
    IReadOnlyList<ClassroomAttendanceAggregateDto> ByClassroom,
    IReadOnlyList<DashboardCountDto> ThresholdCounts,
    IReadOnlyList<DashboardCountDto> CaseCounts,
    DateTimeOffset GeneratedAt);

public sealed record GetTeacherStudentAffairsDashboardQuery : IRequest<ApiResponse<TeacherStudentAffairsDashboardDto>>;
public sealed record GetOfficerStudentAffairsDashboardQuery : IRequest<ApiResponse<OfficerStudentAffairsDashboardDto>>;
public sealed record GetSocialWorkerStudentAffairsDashboardQuery : IRequest<ApiResponse<SocialWorkerStudentAffairsDashboardDto>>;
public sealed record GetSecurityStudentAffairsDashboardQuery : IRequest<ApiResponse<SecurityStudentAffairsDashboardDto>>;
public sealed record GetGuardianStudentAffairsDashboardQuery : IRequest<ApiResponse<GuardianStudentAffairsDashboardDto>>;
public sealed record GetSchoolOversightDashboardQuery : IRequest<ApiResponse<SchoolOversightDashboardDto>>;
