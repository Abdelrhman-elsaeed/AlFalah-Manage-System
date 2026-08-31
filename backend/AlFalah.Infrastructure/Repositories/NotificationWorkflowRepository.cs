using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Notifications;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class NotificationWorkflowRepository : INotificationWorkflowRepository
{
    private readonly AlFalahDbContext _context;
    public NotificationWorkflowRepository(AlFalahDbContext context) => _context = context;

    public async Task<PagedResult<StudentAffairsNotificationDto>> GetOwnAsync(
        int schoolId, string userId, NotificationListQuery query, CancellationToken cancellationToken)
    {
        var source = _context.Notifications.AsNoTracking().Where(notification =>
            notification.SchoolId == schoolId
            && notification.UserId == userId
            && !notification.RequiresApproval
            && !notification.IsSuppressed
            && notification.DeliveryStatus == NotificationDeliveryStatus.Delivered);
        if (query.IsRead is { } isRead) source = source.Where(notification => notification.IsRead == isRead);
        if (query.StudentId is { } studentId) source = source.Where(notification => notification.StudentId == studentId);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await source.OrderByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(notification => new StudentAffairsNotificationDto(
                notification.Id,
                notification.StudentId,
                notification.Type ?? string.Empty,
                notification.Title,
                notification.Message,
                notification.Priority,
                notification.CreatedAt,
                notification.ReadAt,
                Convert.ToBase64String(notification.RowVersion)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<StudentAffairsNotificationDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<int> GetUnreadCountAsync(int schoolId, string userId, CancellationToken cancellationToken) =>
        _context.Notifications.AsNoTracking().CountAsync(notification =>
            notification.SchoolId == schoolId && notification.UserId == userId
            && !notification.RequiresApproval && !notification.IsSuppressed
            && notification.DeliveryStatus == NotificationDeliveryStatus.Delivered && !notification.IsRead,
            cancellationToken);

    public Task<Notification?> GetOwnForUpdateAsync(
        int schoolId, string userId, int notificationId, CancellationToken cancellationToken) =>
        _context.Notifications.SingleOrDefaultAsync(notification =>
            notification.Id == notificationId && notification.SchoolId == schoolId
            && notification.UserId == userId && !notification.RequiresApproval && !notification.IsSuppressed,
            cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetAllOwnUnreadForUpdateAsync(
        int schoolId, string userId, CancellationToken cancellationToken) =>
        await _context.Notifications.Where(notification => notification.SchoolId == schoolId
            && notification.UserId == userId && !notification.IsRead
            && !notification.RequiresApproval && !notification.IsSuppressed
            && notification.DeliveryStatus == NotificationDeliveryStatus.Delivered)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PagedResult<PendingDispatchDto>> GetPendingAsync(
        int schoolId, StudentAffairsPageQuery query, CancellationToken cancellationToken)
    {
        var source = _context.Notifications.AsNoTracking().Where(notification =>
            notification.SchoolId == schoolId && notification.RequiresApproval && !notification.IsSuppressed);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        var projections = await source.OrderBy(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(notification => new
            {
                notification.Id, notification.StudentId, notification.RelatedEntityType,
                notification.RelatedEntityId, notification.Message, notification.CreatedAt, notification.RowVersion
            }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var items = projections.Select(notification => new PendingDispatchDto(
            notification.Id,
            notification.StudentId ?? 0,
            notification.RelatedEntityType ?? string.Empty,
            int.TryParse(notification.RelatedEntityId, out var factId) ? factId : 0,
            notification.Message,
            notification.CreatedAt,
            Convert.ToBase64String(notification.RowVersion))).ToList();
        return new PagedResult<PendingDispatchDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Task<Notification?> GetPendingForUpdateAsync(
        int schoolId, int notificationId, CancellationToken cancellationToken) =>
        _context.Notifications.SingleOrDefaultAsync(notification =>
            notification.Id == notificationId && notification.SchoolId == schoolId
            && notification.RequiresApproval && !notification.IsSuppressed,
            cancellationToken);

    public void SetExpectedRowVersion(Notification notification, byte[] rowVersion) =>
        _context.Entry(notification).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public async Task SetSourceDecisionAsync(
        int schoolId,
        Notification notification,
        GuardianDispatchDecision decision,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(notification.RelatedEntityId, out var entityId)) return;
        if (notification.RelatedEntityType == nameof(BehaviorIncident))
        {
            var incident = await _context.BehaviorIncidents.SingleOrDefaultAsync(item =>
                item.Id == entityId && item.SchoolId == schoolId, cancellationToken).ConfigureAwait(false);
            if (incident is not null) incident.GuardianDispatchDecision = decision;
        }
        else if (notification.RelatedEntityType == nameof(AcademicConcern))
        {
            var concern = await _context.AcademicConcerns.SingleOrDefaultAsync(item =>
                item.Id == entityId && item.SchoolId == schoolId, cancellationToken).ConfigureAwait(false);
            if (concern is not null) concern.GuardianDispatchDecision = decision;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException exception) { throw new InvalidOperationException("Notification was changed by another officer", exception); }
    }
}
