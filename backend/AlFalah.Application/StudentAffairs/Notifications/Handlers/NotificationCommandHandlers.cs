using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Notifications.Handlers;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<StudentAffairsNotificationDto>>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    public GetNotificationsQueryHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser)
        => (_repository, _currentUser) = (repository, currentUser);

    public async Task<ApiResponse<PagedResult<StudentAffairsNotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<StudentAffairsNotificationDto>>.Fail("An active school is required");
        return ApiResponse<PagedResult<StudentAffairsNotificationDto>>.Success(
            await _repository.GetOwnAsync(schoolId, _currentUser.UserId, request.Query, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, ApiResponse<int>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    public GetUnreadNotificationCountQueryHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser)
        => (_repository, _currentUser) = (repository, currentUser);
    public async Task<ApiResponse<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<int>.Fail("An active school is required");
        return ApiResponse<int>.Success(await _repository.GetUnreadCountAsync(
            schoolId, _currentUser.UserId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, ApiResponse<bool>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    public MarkNotificationReadCommandHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
        => (_repository, _currentUser, _timeProvider) = (repository, currentUser, timeProvider);
    public async Task<ApiResponse<bool>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<bool>.Fail("An active school is required");
        var notification = await _repository.GetOwnForUpdateAsync(
            schoolId, _currentUser.UserId, request.NotificationId, cancellationToken).ConfigureAwait(false);
        if (notification is null) return ApiResponse<bool>.Fail("Notification was not found");
        notification.IsRead = true;
        notification.ReadAt ??= _timeProvider.GetUtcNow();
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<bool>.Success(true);
    }
}

public sealed class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, ApiResponse<bool>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    public MarkAllNotificationsReadCommandHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
        => (_repository, _currentUser, _timeProvider) = (repository, currentUser, timeProvider);
    public async Task<ApiResponse<bool>> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<bool>.Fail("An active school is required");
        var notifications = await _repository.GetAllOwnUnreadForUpdateAsync(
            schoolId, _currentUser.UserId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        foreach (var notification in notifications) { notification.IsRead = true; notification.ReadAt = now; }
        if (notifications.Count > 0) await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<bool>.Success(true);
    }
}

public sealed class GetPendingDispatchNotificationsQueryHandler
    : IRequestHandler<GetPendingDispatchNotificationsQuery, ApiResponse<PagedResult<PendingDispatchDto>>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    public GetPendingDispatchNotificationsQueryHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser)
        => (_repository, _currentUser) = (repository, currentUser);
    public async Task<ApiResponse<PagedResult<PendingDispatchDto>>> Handle(
        GetPendingDispatchNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId)
            return ApiResponse<PagedResult<PendingDispatchDto>>.Fail("An active school is required");
        return ApiResponse<PagedResult<PendingDispatchDto>>.Success(
            await _repository.GetPendingAsync(schoolId, request.Query, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class ApproveNotificationDispatchCommandHandler
    : IRequestHandler<ApproveNotificationDispatchCommand, ApiResponse<PendingDispatchDto>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    public ApproveNotificationDispatchCommandHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
        => (_repository, _currentUser, _timeProvider) = (repository, currentUser, timeProvider);
    public async Task<ApiResponse<PendingDispatchDto>> Handle(
        ApproveNotificationDispatchCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PendingDispatchDto>.Fail("An active school is required");
        var notification = await _repository.GetPendingForUpdateAsync(schoolId, request.NotificationId, cancellationToken).ConfigureAwait(false);
        if (notification is null) return ApiResponse<PendingDispatchDto>.Fail("Pending notification was not found");
        if (!TryDecode(request.Request.RowVersion, out var rowVersion))
            return ApiResponse<PendingDispatchDto>.Fail("Invalid row version");
        _repository.SetExpectedRowVersion(notification, rowVersion);
        var now = _timeProvider.GetUtcNow();
        notification.RequiresApproval = false;
        notification.ApprovedByUserId = _currentUser.UserId;
        notification.ApprovedAt = now;
        notification.DeliveryStatus = NotificationDeliveryStatus.Delivered;
        notification.DeliveredAt = now;
        notification.UpdatedAt = now;
        notification.UpdatedByUserId = _currentUser.UserId;
        await _repository.SetSourceDecisionAsync(schoolId, notification, GuardianDispatchDecision.Approved, cancellationToken)
            .ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<PendingDispatchDto>.Success(ToDto(notification));
    }

    internal static bool TryDecode(string value, out byte[] rowVersion)
    {
        try { rowVersion = Convert.FromBase64String(value); return rowVersion.Length > 0; }
        catch (FormatException) { rowVersion = Array.Empty<byte>(); return false; }
    }

    internal static PendingDispatchDto ToDto(AlFalah.Domain.Entities.Notification notification) => new(
        notification.Id,
        notification.StudentId ?? 0,
        notification.RelatedEntityType ?? string.Empty,
        int.TryParse(notification.RelatedEntityId, out var factId) ? factId : 0,
        notification.Message,
        notification.CreatedAt,
        Convert.ToBase64String(notification.RowVersion));
}

public sealed class SuppressNotificationDispatchCommandHandler
    : IRequestHandler<SuppressNotificationDispatchCommand, ApiResponse<PendingDispatchDto>>
{
    private readonly INotificationWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    public SuppressNotificationDispatchCommandHandler(INotificationWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
        => (_repository, _currentUser, _timeProvider) = (repository, currentUser, timeProvider);
    public async Task<ApiResponse<PendingDispatchDto>> Handle(
        SuppressNotificationDispatchCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PendingDispatchDto>.Fail("An active school is required");
        if (string.IsNullOrWhiteSpace(request.Request.Reason))
            return ApiResponse<PendingDispatchDto>.Fail("A suppression reason is required");
        var notification = await _repository.GetPendingForUpdateAsync(schoolId, request.NotificationId, cancellationToken).ConfigureAwait(false);
        if (notification is null) return ApiResponse<PendingDispatchDto>.Fail("Pending notification was not found");
        if (!ApproveNotificationDispatchCommandHandler.TryDecode(request.Request.RowVersion, out var rowVersion))
            return ApiResponse<PendingDispatchDto>.Fail("Invalid row version");
        _repository.SetExpectedRowVersion(notification, rowVersion);
        var now = _timeProvider.GetUtcNow();
        notification.RequiresApproval = false;
        notification.IsSuppressed = true;
        notification.SuppressedByUserId = _currentUser.UserId;
        notification.SuppressedAt = now;
        notification.SuppressionReason = request.Request.Reason.Trim();
        notification.DeliveryStatus = NotificationDeliveryStatus.Suppressed;
        notification.UpdatedAt = now;
        notification.UpdatedByUserId = _currentUser.UserId;
        await _repository.SetSourceDecisionAsync(schoolId, notification, GuardianDispatchDecision.Suppressed, cancellationToken)
            .ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiResponse<PendingDispatchDto>.Success(ApproveNotificationDispatchCommandHandler.ToDto(notification));
    }
}
