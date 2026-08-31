using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Automations;

public sealed class StudentAffairsAutomationRuleEngine
{
    private readonly AlFalahDbContext _context;
    private readonly TimeProvider _timeProvider;

    public StudentAffairsAutomationRuleEngine(AlFalahDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task ProcessAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        BehaviorIncidentLoggedEvent behavior => ProcessBehaviorAsync(behavior, cancellationToken),
        StudentAbsentRecordedEvent absence => ProcessAbsenceAsync(absence, cancellationToken),
        AbsenceExcuseAcceptedEvent accepted => ProcessAcceptedExcuseAsync(accepted, cancellationToken),
        MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3 delay => ProcessMorningDelayAsync(delay, cancellationToken),
        _ => Task.CompletedTask
    };

    private async Task ProcessBehaviorAsync(
        BehaviorIncidentLoggedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var count = await _context.BehaviorIncidents.AsNoTracking()
            .CountAsync(incident => incident.SchoolId == domainEvent.SchoolId
                && incident.StudentId == domainEvent.StudentId
                && incident.AcademicTermId == domainEvent.AcademicTermId
                && incident.IsUpheld,
                cancellationToken).ConfigureAwait(false);
        var actor = await ResolveAutomationActorAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var rule = await GetRuleAsync(settings, StudentTermMetricCode.CountableBehaviorIncident,
            settings.BehaviorIncidentMultiplePerTerm, true, actor, cancellationToken).ConfigureAwait(false);
        await RebuildMetricAsync(domainEvent, StudentTermMetricCode.CountableBehaviorIncident, count, actor, cancellationToken)
            .ConfigureAwait(false);

        var satisfiedOccurrences = count / settings.BehaviorIncidentMultiplePerTerm;
        for (var occurrence = 1; occurrence <= satisfiedOccurrences; occurrence++)
        {
            await EnsureTriggerAsync(
                domainEvent,
                rule,
                settings.BehaviorIncidentMultiplePerTerm,
                occurrence,
                count,
                ReferralSourceType.Behavior,
                domainEvent.BehaviorIncidentId,
                ReferralPriority.Critical,
                createReferralAndSummon: true,
                createChildRightsAction: false,
                createOfficerAlert: false,
                actor,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessAbsenceAsync(
        StudentAbsentRecordedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var count = await _context.DailyStudentAttendances.AsNoTracking()
            .Where(attendance => attendance.SchoolId == domainEvent.SchoolId
                && attendance.StudentId == domainEvent.StudentId
                && attendance.AcademicTermId == domainEvent.AcademicTermId
                && attendance.Status == StudentAttendanceStatus.Absent)
            .Select(attendance => attendance.AttendanceDate)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);
        var actor = await ResolveAutomationActorAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var rule = await GetRuleAsync(settings, StudentTermMetricCode.PenaltyAbsenceDay,
            settings.AbsenceVisualAlertThresholdPerTerm, false, actor, cancellationToken).ConfigureAwait(false);
        await RebuildMetricAsync(domainEvent, StudentTermMetricCode.PenaltyAbsenceDay, count, actor, cancellationToken)
            .ConfigureAwait(false);

        if (count >= settings.AbsenceVisualAlertThresholdPerTerm)
            await EnsureTriggerAsync(domainEvent, rule, settings.AbsenceVisualAlertThresholdPerTerm, 1, count,
                ReferralSourceType.Absence, domainEvent.DailyStudentAttendanceId, ReferralPriority.High,
                false, false, true, actor, cancellationToken).ConfigureAwait(false);
        if (count >= settings.AbsenceReferralThresholdPerTerm)
            await EnsureTriggerAsync(domainEvent, rule, settings.AbsenceReferralThresholdPerTerm, 1, count,
                ReferralSourceType.Absence, domainEvent.DailyStudentAttendanceId, ReferralPriority.High,
                true, false, false, actor, cancellationToken).ConfigureAwait(false);
        if (count >= settings.AbsenceChildRightsThresholdPerTerm)
            await EnsureTriggerAsync(domainEvent, rule, settings.AbsenceChildRightsThresholdPerTerm, 1, count,
                ReferralSourceType.Absence, domainEvent.DailyStudentAttendanceId, ReferralPriority.Critical,
                true, true, false, actor, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessMorningDelayAsync(
        MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3 domainEvent,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var count = await _context.MorningArrivalDelays.AsNoTracking()
            .CountAsync(delay => delay.SchoolId == domainEvent.SchoolId
                && delay.StudentId == domainEvent.StudentId
                && delay.AcademicTermId == domainEvent.AcademicTermId,
                cancellationToken).ConfigureAwait(false);
        var actor = await ResolveAutomationActorAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var rule = await GetRuleAsync(settings, StudentTermMetricCode.MorningArrivalDelay,
            settings.MorningDelayThresholdPerTerm, false, actor, cancellationToken).ConfigureAwait(false);
        await RebuildMetricAsync(domainEvent, StudentTermMetricCode.MorningArrivalDelay, count, actor, cancellationToken)
            .ConfigureAwait(false);
        if (count >= settings.MorningDelayThresholdPerTerm)
            await EnsureTriggerAsync(domainEvent, rule, settings.MorningDelayThresholdPerTerm, 1, count,
                ReferralSourceType.MorningDelay, domainEvent.MorningArrivalDelayId, ReferralPriority.High,
                true, false, false, actor, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessAcceptedExcuseAsync(
        AbsenceExcuseAcceptedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var count = await _context.DailyStudentAttendances.AsNoTracking()
            .Where(attendance => attendance.SchoolId == domainEvent.SchoolId
                && attendance.StudentId == domainEvent.StudentId
                && attendance.AcademicTermId == domainEvent.AcademicTermId
                && attendance.Status == StudentAttendanceStatus.Absent)
            .Select(attendance => attendance.AttendanceDate)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);
        var actor = await ResolveAutomationActorAsync(domainEvent.SchoolId, cancellationToken).ConfigureAwait(false);
        var rule = await GetRuleAsync(settings, StudentTermMetricCode.PenaltyAbsenceDay,
            settings.AbsenceVisualAlertThresholdPerTerm, false, actor, cancellationToken).ConfigureAwait(false);
        await RebuildMetricAsync(domainEvent, StudentTermMetricCode.PenaltyAbsenceDay, count, actor, cancellationToken)
            .ConfigureAwait(false);
        if (rule.Id == 0) return;

        var now = _timeProvider.GetUtcNow();
        var invalidated = await _context.AutomationTriggerLedgers
            .Where(ledger => ledger.SchoolId == domainEvent.SchoolId
                && ledger.StudentId == domainEvent.StudentId
                && ledger.AcademicTermId == domainEvent.AcademicTermId
                && ledger.RuleVersionId == rule.Id
                && ledger.Threshold > count
                && ledger.Validity == AutomationTriggerValidity.Satisfied)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var ledger in invalidated)
        {
            ledger.Validity = AutomationTriggerValidity.SourceNoLongerSatisfied;
            ledger.SourceInvalidatedAt = now;
            ledger.ReviewNote = $"Accepted excuse reduced the canonical absence count to {count}.";
        }
        var ledgerIds = invalidated.Select(ledger => ledger.Id).ToArray();
        if (ledgerIds.Length == 0) return;
        var summons = await _context.GuardianSummons
            .Where(summon => summon.SchoolId == domainEvent.SchoolId
                && summon.StudentReferral != null
                && summon.StudentReferral.RuleTriggerId != null
                && ledgerIds.Contains(summon.StudentReferral.RuleTriggerId.Value)
                && summon.Status == GuardianSummonStatus.Pending)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var summon in summons)
        {
            summon.RequiresOfficerReview = true;
            summon.OfficerReviewReason = $"Accepted excuse reduced absence count to {count}.";
            summon.OfficerReviewFlaggedAt = now;
            summon.UpdatedAt = now;
            summon.UpdatedByUserId = actor;
        }
    }

    private async Task<SchoolStudentAffairsSettings> GetSettingsAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        await _context.SchoolStudentAffairsSettings.AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.SchoolId == schoolId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Student Affairs automation settings are missing for school {schoolId}");

    private async Task<AutomationRuleDefinition> GetRuleAsync(
        SchoolStudentAffairsSettings settings,
        StudentTermMetricCode metricCode,
        int threshold,
        bool repeats,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var rule = await _context.AutomationRuleDefinitions
            .SingleOrDefaultAsync(item => item.SchoolId == settings.SchoolId
                && item.Version == settings.Version
                && item.MetricCode == metricCode,
                cancellationToken).ConfigureAwait(false);
        if (rule is not null) return rule;

        rule = new AutomationRuleDefinition
        {
            SchoolId = settings.SchoolId,
            SchoolStudentAffairsSettingsId = settings.Id,
            Version = settings.Version,
            MetricCode = metricCode,
            Threshold = threshold,
            RepeatsAtMultiples = repeats,
            PolicySnapshotJson = JsonSerializer.Serialize(new
            {
                settings.MorningDelayThresholdPerTerm,
                settings.BehaviorIncidentMultiplePerTerm,
                settings.AbsenceVisualAlertThresholdPerTerm,
                settings.AbsenceReferralThresholdPerTerm,
                settings.AbsenceChildRightsThresholdPerTerm
            }),
            EffectiveFrom = settings.EffectiveFrom,
            CompiledAt = _timeProvider.GetUtcNow(),
            CompiledByUserId = actorUserId
        };
        _context.AutomationRuleDefinitions.Add(rule);
        return rule;
    }

    private async Task RebuildMetricAsync(
        IDomainEvent domainEvent,
        StudentTermMetricCode metricCode,
        int count,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var (studentId, academicTermId) = EventStudentAndTerm(domainEvent);
        var now = _timeProvider.GetUtcNow();
        var metric = await _context.StudentTermMetrics
            .SingleOrDefaultAsync(item => item.SchoolId == domainEvent.SchoolId
                && item.StudentId == studentId
                && item.AcademicTermId == academicTermId
                && item.MetricCode == metricCode,
                cancellationToken).ConfigureAwait(false);
        if (metric is null)
        {
            _context.StudentTermMetrics.Add(new StudentTermMetric
            {
                SchoolId = domainEvent.SchoolId,
                StudentId = studentId,
                AcademicTermId = academicTermId,
                MetricCode = metricCode,
                Count = count,
                RecalculatedAt = now,
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            });
            return;
        }

        metric.Count = count;
        metric.RecalculatedAt = now;
        metric.UpdatedAt = now;
        metric.UpdatedByUserId = actorUserId;
    }

    private async Task EnsureTriggerAsync(
        IDomainEvent domainEvent,
        AutomationRuleDefinition rule,
        int threshold,
        int occurrence,
        int count,
        ReferralSourceType sourceType,
        int sourceEntityId,
        ReferralPriority priority,
        bool createReferralAndSummon,
        bool createChildRightsAction,
        bool createOfficerAlert,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var (studentId, academicTermId) = EventStudentAndTerm(domainEvent);
        if (rule.Id != 0 && await _context.AutomationTriggerLedgers.AsNoTracking().AnyAsync(
                ledger => ledger.SchoolId == domainEvent.SchoolId
                    && ledger.StudentId == studentId
                    && ledger.AcademicTermId == academicTermId
                    && ledger.RuleVersionId == rule.Id
                    && ledger.Threshold == threshold
                    && ledger.OccurrenceNumber == occurrence,
                cancellationToken).ConfigureAwait(false))
            return;

        var now = _timeProvider.GetUtcNow();
        var ledger = new AutomationTriggerLedger
        {
            SchoolId = domainEvent.SchoolId,
            StudentId = studentId,
            AcademicTermId = academicTermId,
            RuleVersion = rule,
            Threshold = threshold,
            OccurrenceNumber = occurrence,
            CountSnapshot = count,
            Validity = AutomationTriggerValidity.Satisfied,
            TriggeredAt = now,
            CorrelationId = domainEvent.EventId
        };
        _context.AutomationTriggerLedgers.Add(ledger);

        StudentReferral? referral = null;
        if (createReferralAndSummon)
        {
            var guardian = await ResolveGuardianAsync(domainEvent.SchoolId, studentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No active notification guardian is linked to student {studentId}");
            referral = new StudentReferral
            {
                SchoolId = domainEvent.SchoolId,
                StudentId = studentId,
                AcademicTermId = academicTermId,
                SourceType = sourceType,
                SourceEntityId = sourceEntityId,
                RuleTrigger = ledger,
                CountSnapshot = count,
                ThresholdSnapshot = threshold,
                Priority = priority,
                Status = StudentReferralStatus.Open,
                RecommendedActions = $"Automatic threshold trigger: {sourceType} ({count}/{threshold})",
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            };
            _context.StudentReferrals.Add(referral);
            _context.GuardianSummons.Add(new GuardianSummon
            {
                SchoolId = domainEvent.SchoolId,
                StudentId = studentId,
                AcademicTermId = academicTermId,
                StudentReferral = referral,
                CreatedReason = $"Automatic {sourceType} threshold reached ({count}/{threshold})",
                Priority = priority,
                SourceCountSnapshot = count,
                ThresholdSnapshot = threshold,
                Status = GuardianSummonStatus.Pending,
                GuardianProfileId = guardian.GuardianProfileId,
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            });
        }

        if (createChildRightsAction && referral is not null)
        {
            _context.StudentCaseActions.Add(new StudentCaseAction
            {
                SchoolId = domainEvent.SchoolId,
                StudentReferral = referral,
                ActionType = StudentCaseActionType.ChildRightsCommitteeReferral,
                Description = $"Automatic Child Rights referral after {count} distinct unexcused absence days",
                ActorUserId = actorUserId,
                ActionAt = now,
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            });
        }

        if (createOfficerAlert)
            await CreateOfficerAlertsAsync(domainEvent, studentId, academicTermId, threshold, count, occurrence,
                cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateOfficerAlertsAsync(
        IDomainEvent domainEvent,
        int studentId,
        int academicTermId,
        int threshold,
        int count,
        int occurrence,
        CancellationToken cancellationToken)
    {
        var officerIds = await _context.UserSchoolRoles.AsNoTracking()
            .Where(link => link.SchoolId == domainEvent.SchoolId
                && link.IsActive
                && link.User.IsActive
                && link.Role.Name == RoleNames.StudentAffairsOfficer)
            .Select(link => link.UserId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        foreach (var officerId in officerIds)
        {
            var deduplicationKey = $"absence-alert:{domainEvent.SchoolId}:{studentId}:{academicTermId}:{threshold}:{occurrence}:{officerId}";
            if (await _context.Notifications.AsNoTracking().AnyAsync(notification =>
                    notification.SchoolId == domainEvent.SchoolId
                    && notification.UserId == officerId
                    && notification.DeduplicationKey == deduplicationKey,
                    cancellationToken).ConfigureAwait(false)) continue;
            _context.Notifications.Add(new Notification
            {
                SchoolId = domainEvent.SchoolId,
                UserId = officerId,
                StudentId = studentId,
                Title = "تنبيه غياب متكرر",
                Message = $"بلغ الطالب {count} أيام غياب غير معذور خلال الفصل.",
                Type = "PenaltyAbsenceThreshold",
                RelatedEntityType = nameof(StudentTermMetric),
                RelatedEntityId = $"{studentId}:{academicTermId}",
                Priority = NotificationPriority.High,
                TemplateKey = "student-affairs.absence.threshold",
                CorrelationId = domainEvent.EventId,
                DeduplicationKey = deduplicationKey,
                DeliveryStatus = NotificationDeliveryStatus.Delivered,
                DeliveredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private async Task<string> ResolveAutomationActorAsync(int schoolId, CancellationToken cancellationToken)
    {
        var actor = await _context.UserSchoolRoles.AsNoTracking()
            .Where(link => link.SchoolId == schoolId && link.IsActive && link.User.IsActive)
            .OrderBy(link => link.Role.Name == RoleNames.StudentAffairsOfficer ? 0 : 1)
            .ThenBy(link => link.Id)
            .Select(link => link.UserId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return actor ?? throw new InvalidOperationException($"No active system actor exists for school {schoolId}");
    }

    private async Task<(int GuardianProfileId, string UserId)?> ResolveGuardianAsync(
        int schoolId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var guardian = await _context.StudentGuardians.AsNoTracking()
            .Where(link => link.SchoolId == schoolId
                && link.StudentId == studentId
                && link.ReceivesNotifications
                && link.GuardianProfile.IsActive
                && link.GuardianProfile.ApplicationUser.IsActive
                && link.ValidFrom <= today
                && (link.ValidTo == null || link.ValidTo >= today))
            .OrderByDescending(link => link.IsPrimary)
            .ThenBy(link => link.Id)
            .Select(link => new
            {
                link.GuardianProfileId,
                link.GuardianProfile.ApplicationUserId
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return guardian is null
            ? null
            : (guardian.GuardianProfileId, guardian.ApplicationUserId);
    }

    private static (int StudentId, int AcademicTermId) EventStudentAndTerm(IDomainEvent domainEvent) => domainEvent switch
    {
        BehaviorIncidentLoggedEvent behavior => (behavior.StudentId, behavior.AcademicTermId),
        StudentAbsentRecordedEvent absence => (absence.StudentId, absence.AcademicTermId),
        AbsenceExcuseAcceptedEvent accepted => (accepted.StudentId, accepted.AcademicTermId),
        MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3 delay => (delay.StudentId, delay.AcademicTermId),
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent))
    };
}
