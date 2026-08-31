using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Settings;

public sealed record CreateStudentAffairsSettingsRequestDto(
    int MorningDelayThresholdPerTerm,
    int BehaviorIncidentMultiplePerTerm,
    int AcademicConcernThresholdPerTerm,
    int ClassroomEntryPermitThresholdPerTerm,
    int AbsenceVisualAlertThresholdPerTerm,
    int AbsenceReferralThresholdPerTerm,
    int AbsenceChildRightsThresholdPerTerm,
    string BehaviorCountabilityPolicy,
    TimeOnly ArrivalCutoffLocalTime,
    int ArrivalGraceMinutes);

public sealed record UpdateStudentAffairsSettingsRequestDto(
    int MorningDelayThresholdPerTerm,
    int BehaviorIncidentMultiplePerTerm,
    int AcademicConcernThresholdPerTerm,
    int ClassroomEntryPermitThresholdPerTerm,
    int AbsenceVisualAlertThresholdPerTerm,
    int AbsenceReferralThresholdPerTerm,
    int AbsenceChildRightsThresholdPerTerm,
    string BehaviorCountabilityPolicy,
    TimeOnly ArrivalCutoffLocalTime,
    int ArrivalGraceMinutes,
    string AuditReason,
    string RowVersion);

public sealed record ResetStudentAffairsSettingsRequestDto(string Reason, string RowVersion);

public sealed record SchoolStudentAffairsSettingsDto(
    int? Id,
    int MorningDelayThresholdPerTerm,
    int BehaviorIncidentMultiplePerTerm,
    int AcademicConcernThresholdPerTerm,
    int ClassroomEntryPermitThresholdPerTerm,
    int AbsenceVisualAlertThresholdPerTerm,
    int AbsenceReferralThresholdPerTerm,
    int AbsenceChildRightsThresholdPerTerm,
    string BehaviorCountabilityPolicy,
    TimeOnly ArrivalCutoffLocalTime,
    int ArrivalGraceMinutes,
    int EffectiveVersion,
    DateTimeOffset EffectiveFrom,
    bool UsesLockedDefaults,
    string RowVersion);

public sealed record StudentAffairsSettingsHistoryDto(int Version, SchoolStudentAffairsSettingsDto Settings, ActorSummaryDto Actor, string Reason, DateTimeOffset EffectiveFrom);

public sealed record GetStudentAffairsSettingsQuery : IRequest<ApiResponse<SchoolStudentAffairsSettingsDto>>;
public sealed record CreateStudentAffairsSettingsCommand(CreateStudentAffairsSettingsRequestDto Request) : IRequest<ApiResponse<SchoolStudentAffairsSettingsDto>>;
public sealed record UpdateStudentAffairsSettingsCommand(UpdateStudentAffairsSettingsRequestDto Request) : IRequest<ApiResponse<SchoolStudentAffairsSettingsDto>>;
public sealed record ResetStudentAffairsSettingsCommand(ResetStudentAffairsSettingsRequestDto Request) : IRequest<ApiResponse<SchoolStudentAffairsSettingsDto>>;
public sealed record GetStudentAffairsSettingsHistoryQuery(StudentAffairsPageQuery Query) : IRequest<ApiResponse<PagedResult<StudentAffairsSettingsHistoryDto>>>;
