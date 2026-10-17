using AlFalah.Application.StudentAffairs.DTOs.Shared;
namespace AlFalah.Application.StudentAffairs.DTOs.Notifications;
public sealed class PendingDispatchQuery : StudentAffairsPageQuery
{
    public int? NotificationId { get; set; }
}
