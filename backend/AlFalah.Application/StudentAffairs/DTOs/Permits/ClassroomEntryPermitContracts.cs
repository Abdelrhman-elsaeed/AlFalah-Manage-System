using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Permits;

public sealed class ClassroomEntryPermitListQuery : StudentAffairsPageQuery
{
    public ClassroomEntryPermitStatus? Status { get; set; }
    public DateOnly? Date { get; set; }
    public int? StudentId { get; set; }
    public int? ClassroomId { get; set; }
    public int? InstructorProfileId { get; set; }
    public string? Severity { get; set; }
}

public sealed record CreateClassroomEntryPermitRequestDto(int StudentId, string Reason, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record AcknowledgeClassroomEntryPermitRequestDto(string RowVersion);
public sealed record RevokeClassroomEntryPermitRequestDto(string Reason, string RowVersion);

public sealed record ClassroomEntryPermitDto(
    int Id,
    StudentSummaryDto Student,
    string Reason,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int? TimetableEntryId,
    ClassroomSummaryDto Classroom,
    ActorSummaryDto? TargetTeacher,
    ClassroomEntryPermitStatus Status,
    ActorSummaryDto? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    NotificationDeliveryDto? GuardianDelivery,
    MetricBadgeDto RepetitionMetric,
    string RowVersion);

public sealed record CreateClassroomEntryPermitCommand(CreateClassroomEntryPermitRequestDto Request) : IRequest<ApiResponse<ClassroomEntryPermitDto>>;
public sealed record GetClassroomEntryPermitsQuery(ClassroomEntryPermitListQuery Query) : IRequest<ApiResponse<PagedResult<ClassroomEntryPermitDto>>>;
public sealed record GetClassroomEntryPermitByIdQuery(int PermitId) : IRequest<ApiResponse<ClassroomEntryPermitDto>>;
public sealed record AcknowledgeClassroomEntryPermitCommand(int PermitId, AcknowledgeClassroomEntryPermitRequestDto Request) : IRequest<ApiResponse<ClassroomEntryPermitDto>>;
public sealed record RevokeClassroomEntryPermitCommand(int PermitId, RevokeClassroomEntryPermitRequestDto Request) : IRequest<ApiResponse<ClassroomEntryPermitDto>>;
