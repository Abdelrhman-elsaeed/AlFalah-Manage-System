using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Notifications;

public sealed class StudentAffairsNotificationDispatcher
{
    private readonly AlFalahDbContext _context;
    private readonly TimeProvider _timeProvider;

    public StudentAffairsNotificationDispatcher(AlFalahDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task ProcessAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        StudentAbsentRecordedEvent absence => CreateGuardianNotificationsAsync(
            absence, absence.StudentId, absence.AttendanceDate,
            nameof(DailyStudentAttendance), absence.DailyStudentAttendanceId,
            "غياب الطالب", "تم تسجيل غياب الطالب اليوم.",
            "student-affairs.absence.immediate", NotificationPriority.High, false, cancellationToken),
        MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3 delay => CreateGuardianNotificationsAsync(
            delay, delay.StudentId, delay.SchoolLocalDate,
            nameof(MorningArrivalDelay), delay.MorningArrivalDelayId,
            "تأخر صباحي", $"تم تسجيل تأخر صباحي لمدة {delay.DelayMinutes} دقيقة.",
            "student-affairs.morning-delay.immediate", NotificationPriority.High, false, cancellationToken),
        SessionDelayLoggedEvent delay => ProcessSessionDelayAsync(delay, cancellationToken),
        BehaviorIncidentLoggedEvent behavior => CreateGuardianNotificationsAsync(
            behavior, behavior.StudentId, DateOnly.FromDateTime(behavior.IncidentOccurredAt.Date),
            nameof(BehaviorIncident), behavior.BehaviorIncidentId,
            "ملاحظة سلوكية", "توجد ملاحظة سلوكية بانتظار اعتماد مسؤول شؤون الطلاب.",
            "student-affairs.behavior.approval", NotificationPriority.High, true, cancellationToken),
        AcademicConcernLoggedEvent concern => CreateGuardianNotificationsAsync(
            concern, concern.StudentId, DateOnly.FromDateTime(concern.ConcernOccurredAt.Date),
            nameof(AcademicConcern), concern.AcademicConcernId,
            "ملاحظة أكاديمية", "توجد ملاحظة أكاديمية بانتظار اعتماد مسؤول شؤون الطلاب.",
            "student-affairs.academic-concern.approval", NotificationPriority.High, true, cancellationToken),
        _ => Task.CompletedTask
    };

    private async Task ProcessSessionDelayAsync(
        SessionDelayLoggedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await CreateGuardianNotificationsAsync(
            domainEvent,
            domainEvent.StudentId,
            DateOnly.FromDateTime(domainEvent.DelayOccurredAt.Date),
            nameof(SessionDelay),
            domainEvent.SessionDelayId,
            "تأخر عن الحصة",
            domainEvent.DelayMinutes is null
                ? "تم تسجيل تأخر الطالب عن الحصة."
                : $"تم تسجيل تأخر الطالب عن الحصة لمدة {domainEvent.DelayMinutes} دقيقة.",
            "student-affairs.session-delay.immediate",
            NotificationPriority.Normal,
            false,
            cancellationToken).ConfigureAwait(false);

        var delay = await _context.SessionDelays.SingleOrDefaultAsync(item =>
            item.Id == domainEvent.SessionDelayId && item.SchoolId == domainEvent.SchoolId,
            cancellationToken).ConfigureAwait(false);
        if (delay is not null) delay.GuardianNotificationStatus = GuardianNotificationStatus.Delivered;
    }

    private async Task CreateGuardianNotificationsAsync(
        IDomainEvent domainEvent,
        int studentId,
        DateOnly onDate,
        string entityType,
        int entityId,
        string title,
        string message,
        string templateKey,
        NotificationPriority priority,
        bool requiresApproval,
        CancellationToken cancellationToken)
    {
        var guardians = await _context.StudentGuardians.AsNoTracking()
            .Where(link => link.SchoolId == domainEvent.SchoolId
                && link.StudentId == studentId
                && link.ReceivesNotifications
                && link.GuardianProfile.IsActive
                && link.GuardianProfile.ApplicationUser.IsActive
                && link.ValidFrom <= onDate
                && (link.ValidTo == null || link.ValidTo >= onDate))
            .OrderByDescending(link => link.IsPrimary)
            .Select(link => link.GuardianProfile.ApplicationUserId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        foreach (var guardianUserId in guardians)
        {
            var deduplicationKey = $"{templateKey}:{domainEvent.EventId:N}:{guardianUserId}";
            if (await _context.Notifications.AsNoTracking().AnyAsync(notification =>
                    notification.SchoolId == domainEvent.SchoolId
                    && notification.UserId == guardianUserId
                    && notification.DeduplicationKey == deduplicationKey,
                    cancellationToken).ConfigureAwait(false)) continue;

            _context.Notifications.Add(new Notification
            {
                SchoolId = domainEvent.SchoolId,
                UserId = guardianUserId,
                StudentId = studentId,
                Title = title,
                Message = message,
                Type = requiresApproval ? "GuardianApprovalRequired" : "GuardianImmediate",
                RelatedEntityType = entityType,
                RelatedEntityId = entityId.ToString(),
                Priority = priority,
                TemplateKey = templateKey,
                CorrelationId = domainEvent.EventId,
                DeduplicationKey = deduplicationKey,
                DeliveryStatus = requiresApproval
                    ? NotificationDeliveryStatus.Pending
                    : NotificationDeliveryStatus.Delivered,
                DeliveredAt = requiresApproval ? null : now,
                RequiresApproval = requiresApproval,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
}
