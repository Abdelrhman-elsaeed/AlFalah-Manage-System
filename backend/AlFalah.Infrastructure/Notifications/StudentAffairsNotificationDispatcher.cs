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
        TeacherTimetableChangedEvent changed => TimetableChangedAsync(changed, cancellationToken),
        MessageReleaseDueEvent released => MessageReleasedAsync(released, cancellationToken),
        GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7 summon
            when summon.Action is "Scheduled" or "Rescheduled" => GuardianSummonScheduledAsync(summon, cancellationToken),
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
        ClassroomEntryPermitIssuedEvent permit => ProcessClassroomEntryPermitAsync(permit, cancellationToken),
        _ => Task.CompletedTask
    };

    private async Task GuardianSummonScheduledAsync(
        GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7 domainEvent,
        CancellationToken cancellationToken)
    {
        var summon = await _context.GuardianSummons
            .Include(item => item.GuardianProfile)
            .SingleOrDefaultAsync(item => item.Id == domainEvent.GuardianSummonId
                && item.SchoolId == domainEvent.SchoolId
                && !item.IsDeleted,
                cancellationToken).ConfigureAwait(false);
        if (summon is null || !summon.ScheduledAt.HasValue || !summon.GuardianProfile.IsActive)
            return;

        var guardianUserId = summon.GuardianProfile.ApplicationUserId;
        var deduplicationKey = $"student-affairs.summon.{domainEvent.Action.ToLowerInvariant()}:{domainEvent.EventId:N}:{guardianUserId}";
        var exists = await _context.Notifications.AsNoTracking().AnyAsync(notification =>
            notification.SchoolId == domainEvent.SchoolId
            && notification.UserId == guardianUserId
            && notification.DeduplicationKey == deduplicationKey,
            cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (!exists)
        {
            _context.Notifications.Add(new Notification
            {
                SchoolId = domainEvent.SchoolId,
                UserId = guardianUserId,
                StudentId = domainEvent.StudentId,
                Title = domainEvent.Action == "Rescheduled" ? "تم تعديل موعد الاستدعاء" : "تم تحديد موعد استدعاء",
                Message = $"موعد الحضور: {summon.ScheduledAt:yyyy-MM-dd HH:mm}. المكان: {summon.Location}.",
                Type = "GuardianSummonAppointment",
                RelatedEntityType = nameof(GuardianSummon),
                RelatedEntityId = summon.Id.ToString(),
                Priority = NotificationPriority.High,
                TemplateKey = domainEvent.Action == "Rescheduled"
                    ? "student-affairs.summon.rescheduled"
                    : "student-affairs.summon.scheduled",
                CorrelationId = domainEvent.EventId,
                DeduplicationKey = deduplicationKey,
                DeliveryStatus = NotificationDeliveryStatus.Delivered,
                DeliveredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        summon.GuardianNotifiedAt = now;
        summon.UpdatedAt = now;
        // Audit columns are foreign keys to Users. Preserve the real actor captured in the
        // immutable domain event instead of writing a synthetic identifier that cannot exist.
        summon.UpdatedByUserId = domainEvent.ActorUserId;
    }

    private async Task MessageReleasedAsync(MessageReleaseDueEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = await _context.ConversationMessages.AsNoTracking()
            .Where(item => item.Id == domainEvent.ConversationMessageId && item.SchoolId == domainEvent.SchoolId)
            .Select(item => new
            {
                item.ConversationThread.StudentId,
                item.ConversationThread.Subject,
                Recipients = item.Receipts.Where(receipt => receipt.DeliveryState == MessageDeliveryState.Delivered)
                    .Select(receipt => receipt.RecipientUserId).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (message is null) return;
        var now = _timeProvider.GetUtcNow();
        foreach (var recipient in message.Recipients)
        {
            var deduplicationKey = $"student-affairs.message:{domainEvent.ConversationMessageId}:{recipient}";
            if (await _context.Notifications.AsNoTracking().AnyAsync(item =>
                    item.SchoolId == domainEvent.SchoolId && item.UserId == recipient
                    && item.DeduplicationKey == deduplicationKey, cancellationToken).ConfigureAwait(false))
                continue;
            _context.Notifications.Add(new Notification
            {
                SchoolId = domainEvent.SchoolId,
                UserId = recipient,
                StudentId = message.StudentId,
                Title = "رسالة جديدة",
                Message = message.Subject,
                Type = "ConversationMessage",
                RelatedEntityType = nameof(ConversationMessage),
                RelatedEntityId = domainEvent.ConversationMessageId.ToString(),
                Priority = NotificationPriority.Normal,
                TemplateKey = "student-affairs.messaging.delivered",
                CorrelationId = domainEvent.EventId,
                DeduplicationKey = deduplicationKey,
                DeliveryStatus = NotificationDeliveryStatus.Delivered,
                DeliveredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private async Task ProcessClassroomEntryPermitAsync(
        ClassroomEntryPermitIssuedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await CreateGuardianNotificationsAsync(
            domainEvent,
            domainEvent.StudentId,
            DateOnly.FromDateTime(domainEvent.IssuedAt.Date),
            nameof(ClassroomEntryPermit),
            domainEvent.ClassroomEntryPermitId,
            "تصريح دخول الفصل",
            "تم إصدار تصريح دخول فصل للطالب وهو بانتظار إقرار المعلم.",
            "student-affairs.classroom-entry-permit.guardian",
            NotificationPriority.Normal,
            false,
            cancellationToken).ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow();
        var deduplicationKey = $"student-affairs.classroom-entry-permit.teacher:{domainEvent.EventId:N}:{domainEvent.TargetInstructorUserId}";
        if (!await _context.Notifications.AsNoTracking().AnyAsync(notification =>
                notification.SchoolId == domainEvent.SchoolId
                && notification.UserId == domainEvent.TargetInstructorUserId
                && notification.DeduplicationKey == deduplicationKey,
                cancellationToken).ConfigureAwait(false))
        {
            _context.Notifications.Add(new Notification
            {
                SchoolId = domainEvent.SchoolId,
                UserId = domainEvent.TargetInstructorUserId,
                StudentId = domainEvent.StudentId,
                Title = "تصريح دخول فصل بانتظار الإقرار",
                Message = $"يوجد تصريح دخول فصل صالح حتى {domainEvent.ValidUntil:O}.",
                Type = "TeacherAcknowledgementRequired",
                RelatedEntityType = nameof(ClassroomEntryPermit),
                RelatedEntityId = domainEvent.ClassroomEntryPermitId.ToString(),
                Priority = NotificationPriority.High,
                TemplateKey = "student-affairs.classroom-entry-permit.teacher",
                CorrelationId = domainEvent.EventId,
                DeduplicationKey = deduplicationKey,
                DeliveryStatus = NotificationDeliveryStatus.Delivered,
                DeliveredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private async Task TimetableChangedAsync(TeacherTimetableChangedEvent change, CancellationToken ct)
    {
        var prefix = $"timetable:{change.EventId:N}:";
        var delivered = await _context.Notifications.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.SchoolId == change.SchoolId && n.CorrelationId == change.EventId)
            .Select(n => n.UserId).ToListAsync(ct);
        foreach (var userId in change.TeacherUserIds.Distinct().Except(delivered))
            _context.Notifications.Add(new Notification {
                SchoolId = change.SchoolId, UserId = userId, Title = "تغيير في الجدول الدراسي",
                Message = change.Kind == "Substitution"
                    ? $"تم اعتماد احتياطي يوم {change.LocalDate:yyyy-MM-dd}. راجع جدولك اليومي (المراجعة {change.Revision})."
                    : $"تم تبديل حصص في جدولك المنشور وأصبح التغيير سارياً فوراً (المراجعة {change.Revision}).",
                Type = "TeacherTimetableChanged", RelatedEntityType = nameof(TimetableSubstitution), RelatedEntityId = change.SubstitutionId.ToString(),
                CorrelationId = change.EventId, DeduplicationKey = prefix + userId, Priority = NotificationPriority.High,
                DeliveryStatus = NotificationDeliveryStatus.Delivered, DeliveredAt = _timeProvider.GetUtcNow()
            });
    }

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
