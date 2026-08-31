using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.DTOs.Notifications;

public sealed class NotificationListQuery : StudentAffairsPageQuery
{
    public bool? IsRead { get; set; }
    public int? StudentId { get; set; }
}

public sealed record SuppressNotificationRequestDto(string Reason, string RowVersion);
public sealed record ApproveNotificationRequestDto(string RowVersion);
public sealed record StudentAffairsNotificationDto(int Id, int? StudentId, string Type, string Title, string Body, NotificationPriority Priority, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string RowVersion);
public sealed record PendingDispatchDto(int Id, int StudentId, string FactType, int FactId, string Summary, DateTimeOffset QueuedAt, string RowVersion);

public sealed record GetNotificationsQuery(NotificationListQuery Query) : IRequest<ApiResponse<PagedResult<StudentAffairsNotificationDto>>>;
public sealed record GetUnreadNotificationCountQuery : IRequest<ApiResponse<int>>;
public sealed record MarkNotificationReadCommand(int NotificationId) : IRequest<ApiResponse<bool>>;
public sealed record MarkAllNotificationsReadCommand : IRequest<ApiResponse<bool>>;
public sealed record GetPendingDispatchNotificationsQuery(StudentAffairsPageQuery Query) : IRequest<ApiResponse<PagedResult<PendingDispatchDto>>>;
public sealed record ApproveNotificationDispatchCommand(int NotificationId, ApproveNotificationRequestDto Request) : IRequest<ApiResponse<PendingDispatchDto>>;
public sealed record SuppressNotificationDispatchCommand(int NotificationId, SuppressNotificationRequestDto Request) : IRequest<ApiResponse<PendingDispatchDto>>;
