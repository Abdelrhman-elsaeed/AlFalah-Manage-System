using AlFalah.Application.StudentAffairs.DTOs.Notifications;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Notifications;

public interface INotificationWorkflowRepository
{
    Task<PagedResult<StudentAffairsNotificationDto>> GetOwnAsync(
        int schoolId,
        string userId,
        NotificationListQuery query,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(int schoolId, string userId, CancellationToken cancellationToken);
    Task<Notification?> GetOwnForUpdateAsync(int schoolId, string userId, int notificationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> GetAllOwnUnreadForUpdateAsync(int schoolId, string userId, CancellationToken cancellationToken);
    Task<PagedResult<PendingDispatchDto>> GetPendingAsync(int schoolId, StudentAffairsPageQuery query, CancellationToken cancellationToken);
    Task<Notification?> GetPendingForUpdateAsync(int schoolId, int notificationId, CancellationToken cancellationToken);
    void SetExpectedRowVersion(Notification notification, byte[] rowVersion);
    Task SetSourceDecisionAsync(int schoolId, Notification notification, GuardianDispatchDecision decision, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
