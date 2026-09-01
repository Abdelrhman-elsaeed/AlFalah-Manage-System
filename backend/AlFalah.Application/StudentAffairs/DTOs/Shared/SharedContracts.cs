using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.DTOs.Shared;

public class StudentAffairsPageQuery : PagedQuery
{
    public int PageNumber
    {
        get => Page;
        set => Page = value;
    }

    public string? Search { get; set; }
    public string SortDirection { get; set; } = "asc";
    public int? SchoolId { get; set; }
}

public sealed record StudentSummaryDto(
    int Id,
    string StudentNumber,
    string IdentityNumber,
    string DisplayName,
    int? ClassroomId,
    string? ClassLabel,
    bool IsActive,
    string? PhotoUrl)
{
    public StudentSummaryDto(
        int id,
        string studentNumber,
        string displayName,
        int? classroomId,
        string? classLabel,
        bool isActive,
        string? photoUrl)
        : this(id, studentNumber, string.Empty, displayName, classroomId, classLabel, isActive, photoUrl)
    {
    }
}

public sealed record AcademicTermSummaryDto(
    int Id,
    string Label,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsActive);

public sealed record ClassroomSummaryDto(int Id, string Label, string Stage, byte GradeLevel, string Section);

public sealed record GuardianSummaryDto(
    int Id,
    string DisplayName,
    GuardianRelationshipType Relationship,
    bool IsPrimary,
    bool ReceivesNotifications);

public sealed record StudentContextDto(
    StudentSummaryDto Student,
    AcademicTermSummaryDto? ActiveTerm,
    ClassroomSummaryDto? Classroom,
    GuardianSummaryDto? PrimaryGuardian,
    IReadOnlyList<MetricBadgeDto> Metrics);

public sealed record ActorSummaryDto(string UserId, string DisplayName, string RoleSnapshot);

public sealed record AttachmentDto(
    int Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    ActorSummaryDto UploadedBy,
    string DownloadUrl);

public sealed record TransitionDto(
    string? FromState,
    string ToState,
    ActorSummaryDto Actor,
    DateTimeOffset OccurredAt,
    string? Reason);

public sealed record MetricBadgeDto(
    StudentTermMetricCode MetricCode,
    int EligibleTermCount,
    int EffectiveSettingsVersion,
    int? NextThreshold,
    string Severity,
    DateTimeOffset? LastOccurrenceAt,
    DateTimeOffset RecalculatedAt);

public sealed record NotificationDeliveryDto(
    string RecipientLabel,
    string RecipientRole,
    NotificationDeliveryStatus Status,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt);

public sealed record AuditSummaryDto(
    ActorSummaryDto CreatedBy,
    DateTimeOffset CreatedAt,
    ActorSummaryDto? UpdatedBy,
    DateTimeOffset? UpdatedAt);

public sealed record AuthorizedFileDto(byte[] Content, string ContentType, string FileName);

public sealed record OperationAcceptedDto(Guid OperationId, string Status, DateTimeOffset AcceptedAt);
