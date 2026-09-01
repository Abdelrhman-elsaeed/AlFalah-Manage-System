using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Students;

public sealed class StudentListQuery : StudentAffairsPageQuery
{
    public bool? IsActive { get; set; }
    public int? AcademicTermId { get; set; }
    public int? ClassroomId { get; set; }
    public byte? GradeLevel { get; set; }
    public StudentTermMetricCode? RiskMetric { get; set; }
    public string? RiskSeverity { get; set; }
}

public sealed class StudentTimelineQuery : StudentAffairsPageQuery
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public IReadOnlyList<string> EventTypes { get; set; } = Array.Empty<string>();
}

public sealed record CreateStudentRequestDto(
    string StudentNumber,
    string IdentityNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? NationalId,
    DateOnly? DateOfBirth,
    StudentGender? Gender,
    int? ClassroomId,
    int? RollNumber);

public sealed record UpdateStudentRequestDto(
    string StudentNumber,
    string IdentityNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? NationalId,
    DateOnly? DateOfBirth,
    StudentGender? Gender,
    bool IsActive,
    int? ClassroomId,
    int? RollNumber,
    string RowVersion);

public sealed record ArchiveStudentRequestDto(string Reason, string RowVersion);
public sealed record DeleteStudentRequestDto(string Reason, string RowVersion);

public sealed record CreateStudentEnrollmentRequestDto(
    int AcademicTermId,
    int ClassroomId,
    DateOnly EnrolledOn,
    int? RollNumber);

public sealed record UpdateStudentEnrollmentRequestDto(
    StudentEnrollmentStatus Status,
    int? ClassroomId,
    DateOnly EffectiveOn,
    string Reason,
    string RowVersion);

public sealed record LinkStudentGuardianRequestDto(
    int GuardianProfileId,
    GuardianRelationshipType Relationship,
    bool IsPrimary,
    bool ReceivesNotifications,
    bool CanSubmitExcuses,
    bool CanRequestGatePass,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record RevokeStudentGuardianRequestDto(string Reason, string RowVersion);

public sealed record StudentEnrollmentDto(
    int Id,
    AcademicTermSummaryDto Term,
    ClassroomSummaryDto Classroom,
    int? RollNumber,
    DateOnly EnrolledOn,
    DateOnly? WithdrawnOn,
    StudentEnrollmentStatus Status,
    string RowVersion);

public sealed record StudentGuardianLinkDto(
    int Id,
    GuardianSummaryDto Guardian,
    bool CanSubmitExcuses,
    bool CanRequestGatePass,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive,
    string RowVersion);

public sealed record StudentTimelineItemDto(
    string EventType,
    DateTimeOffset OccurredAt,
    string Title,
    string? Description,
    string Severity,
    ActorSummaryDto? Actor);

public sealed record StudentListItemDto(StudentSummaryDto Student, IReadOnlyList<MetricBadgeDto> RiskBadges);

public sealed record StudentDetailsDto(
    StudentSummaryDto Student,
    string IdentityNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? NationalId,
    DateOnly? DateOfBirth,
    StudentGender? Gender,
    StudentEnrollmentDto? CurrentEnrollment,
    IReadOnlyList<StudentGuardianLinkDto> Guardians,
    IReadOnlyList<MetricBadgeDto> TermMetrics,
    IReadOnlyList<StudentTimelineItemDto> RecentEvents,
    AuditSummaryDto Audit,
    string RowVersion);

public sealed record GetStudentsQuery(StudentListQuery Query) : IRequest<ApiResponse<PagedResult<StudentListItemDto>>>;
public sealed record GetStudentByIdQuery(int StudentId) : IRequest<ApiResponse<StudentDetailsDto>>;
public sealed record CreateStudentCommand(CreateStudentRequestDto Request) : IRequest<ApiResponse<StudentDetailsDto>>;
public sealed record UpdateStudentCommand(int StudentId, UpdateStudentRequestDto Request) : IRequest<ApiResponse<StudentDetailsDto>>;
public sealed record ArchiveStudentCommand(int StudentId, ArchiveStudentRequestDto Request) : IRequest<ApiResponse<bool>>;
public sealed record DeleteStudentCommand(int StudentId, DeleteStudentRequestDto Request) : IRequest<ApiResponse<bool>>;
public sealed record GetStudentTimelineQuery(int StudentId, StudentTimelineQuery Query) : IRequest<ApiResponse<PagedResult<StudentTimelineItemDto>>>;
public sealed record CreateStudentEnrollmentCommand(int StudentId, CreateStudentEnrollmentRequestDto Request) : IRequest<ApiResponse<StudentEnrollmentDto>>;
public sealed record UpdateStudentEnrollmentCommand(int StudentId, int EnrollmentId, UpdateStudentEnrollmentRequestDto Request) : IRequest<ApiResponse<StudentEnrollmentDto>>;
public sealed record GetStudentGuardiansQuery(int StudentId) : IRequest<ApiResponse<IReadOnlyList<StudentGuardianLinkDto>>>;
public sealed record LinkStudentGuardianCommand(int StudentId, LinkStudentGuardianRequestDto Request) : IRequest<ApiResponse<StudentGuardianLinkDto>>;
public sealed record RevokeStudentGuardianCommand(int StudentId, int LinkId, RevokeStudentGuardianRequestDto Request) : IRequest<ApiResponse<bool>>;

public sealed class StudentStatsQuery : StudentAffairsPageQuery
{
    public int? ClassroomId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record StudentStatsDto(
    int StudentId,
    string StudentNumber,
    string Name,
    string IdentityNumber,
    string? NationalId,
    string ClassroomName,
    int? ClassroomId,
    bool IsActive,
    int TotalAbsences,
    int TotalDelays,
    int TotalExcuses,
    int TotalReferrals);

public sealed class StudentStatsPageResult : PagedResult<StudentStatsDto>
{
    public int TotalClassrooms { get; set; }
}

public sealed record GetStudentsStatsQuery(StudentStatsQuery Query) : IRequest<ApiResponse<StudentStatsPageResult>>;

public sealed record MonthlyAttendanceTrendDto(
    string MonthKey,
    string MonthLabel,
    int Absences,
    int Delays,
    int Excuses);

public sealed record StudentAnalyticsEventDto(
    string Id,
    string EventType,
    string Title,
    string? Description,
    DateTimeOffset OccurredAt,
    string Severity,
    string Icon,
    string? Status,
    string? ActorName);

public sealed record StudentAnalyticsProfileDto(
    int StudentId,
    string StudentNumber,
    string FullName,
    string IdentityNumber,
    string? NationalId,
    DateOnly? DateOfBirth,
    StudentGender? Gender,
    bool IsActive,
    string? ProfilePhotoStorageKey,
    int? ClassroomId,
    string ClassroomName,
    string Stage,
    byte? GradeLevel,
    string Section,
    int? RollNumber,
    StudentEnrollmentStatus? EnrollmentStatus,
    int TotalAbsences,
    int TotalDelays,
    int TotalExcuses,
    int TotalReferrals,
    int TotalBehaviors,
    int TotalRecognitions,
    int TotalGatePasses,
    IReadOnlyList<MonthlyAttendanceTrendDto> MonthlyTrends,
    IReadOnlyList<StudentAnalyticsEventDto> RecentEvents,
    IReadOnlyList<StudentGuardianLinkDto> Guardians);

public sealed record GetStudentAnalyticsProfileQuery(int StudentId) : IRequest<ApiResponse<StudentAnalyticsProfileDto>>;

