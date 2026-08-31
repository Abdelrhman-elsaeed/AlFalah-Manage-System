using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/notifications")]
public sealed class NotificationsController : StudentAffairsControllerBase
{
    public NotificationsController(IMediator mediator, ICurrentUserService currentUser) : base(mediator, currentUser) { }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] NotificationListQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetNotificationsQuery(query), cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetUnreadNotificationCountQuery(), cancellationToken));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new MarkNotificationReadCommand(id), cancellationToken));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationViewOwn)) return PermissionDenied();
        return Ok(await Mediator.Send(new MarkAllNotificationsReadCommand(), cancellationToken));
    }

    [HttpGet("pending-dispatch")]
    public async Task<IActionResult> PendingDispatch([FromQuery] StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationApproveDispatch, PermissionNames.NotificationSuppressDispatch)) return PermissionDenied();
        return Ok(await Mediator.Send(new GetPendingDispatchNotificationsQuery(query), cancellationToken));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveNotificationRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationApproveDispatch)) return PermissionDenied();
        return Ok(await Mediator.Send(new ApproveNotificationDispatchCommand(id, request), cancellationToken));
    }

    [HttpPost("{id:int}/suppress")]
    public async Task<IActionResult> Suppress(int id, [FromBody] SuppressNotificationRequestDto request, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.NotificationSuppressDispatch)) return PermissionDenied();
        return Ok(await Mediator.Send(new SuppressNotificationDispatchCommand(id, request), cancellationToken));
    }
}
