using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Messaging;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AlFalah.Infrastructure.Repositories;

public sealed class MessagingWorkflowRepository : IMessagingWorkflowRepository
{
    private readonly AlFalahDbContext _context;
    private readonly IBellScheduleRepository _schedules;
    private readonly TimeProvider _timeProvider;

    public MessagingWorkflowRepository(AlFalahDbContext context, IBellScheduleRepository schedules, TimeProvider timeProvider)
    {
        _context = context;
        _schedules = schedules;
        _timeProvider = timeProvider;
    }

    public async Task<bool> IsParticipantAsync(
        int schoolId,
        string userId,
        int conversationId,
        CancellationToken cancellationToken)
    {
        var threadType = await _context.ConversationParticipants.AsNoTracking()
            .Where(
            participant => participant.SchoolId == schoolId
                && participant.ConversationThreadId == conversationId
                && participant.ApplicationUserId == userId
                && !participant.IsDeleted)
            .Select(participant => (ConversationThreadType?)participant.ConversationThread.ThreadType)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!threadType.HasValue) return false;
        var roles = await _context.UserSchoolRoles.AsNoTracking()
            .Where(assignment => assignment.SchoolId == schoolId && assignment.UserId == userId
                && assignment.IsActive && !assignment.IsDeleted)
            .Select(assignment => assignment.Role.Name!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return threadType.Value switch
        {
            ConversationThreadType.GuardianTeacher => roles.Any(role => role is RoleNames.Guardian or RoleNames.Instructor),
            ConversationThreadType.GuardianStudentAffairs => roles.Any(role => role is RoleNames.Guardian or RoleNames.StudentAffairsOfficer),
            ConversationThreadType.GuardianSocialWorker => roles.Any(role => role is RoleNames.Guardian or RoleNames.SocialWorker),
            _ => false
        };
    }

    public async Task<MessageReleaseResult> ReleaseDueMessageAsync(int messageId, CancellationToken cancellationToken)
    {
        var message = await _context.ConversationMessages
            .Include(item => item.Receipts)
            .SingleOrDefaultAsync(item => item.Id == messageId && !item.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (message is null || message.Receipts.All(receipt => receipt.DeliveryState != MessageDeliveryState.Pending))
            return new MessageReleaseResult(true, null);

        var teacherUserIds = await _context.InstructorProfiles.AsNoTracking()
            .Where(profile => profile.SchoolId == message.SchoolId && profile.IsActive
                && message.Receipts.Select(receipt => receipt.RecipientUserId).Contains(profile.UserId))
            .Select(profile => new { profile.Id, profile.UserId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (teacherUserIds.Count != 1) return new MessageReleaseResult(true, null);
        var now = _timeProvider.GetUtcNow();
        var next = await FindNextEligibleAsync(message.SchoolId, teacherUserIds[0].Id, now, cancellationToken).ConfigureAwait(false);
        if (!next.HasValue)
        {
            message.NextEligibleSendAt = null;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new MessageReleaseResult(true, null);
        }
        if (next.Value > now)
        {
            message.NextEligibleSendAt = next;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new MessageReleaseResult(false, next);
        }

        foreach (var receipt in message.Receipts.Where(receipt => receipt.DeliveryState == MessageDeliveryState.Pending))
        {
            receipt.DeliveryState = MessageDeliveryState.Delivered;
            receipt.DeliveredAt = now;
            receipt.FailedAt = null;
            receipt.FailureReason = null;
        }
        message.NextEligibleSendAt = null;
        message.ReleasedAt = now;
        message.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new MessageReleaseResult(true, null);
    }

    public async Task ReconcileOfficeHoursAsync(
        int schoolId,
        IReadOnlyCollection<string>? teacherUserIds,
        CancellationToken cancellationToken)
    {
        await using var reconciliationTransaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var query = _context.TeacherOfficeHourConfigurations
            .Include(configuration => configuration.Slots)
            .Where(configuration => configuration.SchoolId == schoolId
                && configuration.IsCurrent && !configuration.IsDeleted);
        if (teacherUserIds is { Count: > 0 })
            query = query.Where(configuration => teacherUserIds.Contains(configuration.InstructorProfile.UserId));
        var configurations = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var configuration in configurations)
        {
            var beforeSnapshot = JsonSerializer.Serialize(configuration.Slots.Select(slot => new
            {
                slot.StableSlotKey,
                slot.IsActive,
                slot.IsConflicted,
                slot.ConflictReason
            }));
            var aggregate = await BuildOfficeHoursAggregateAsync(schoolId, configuration.InstructorProfileId, cancellationToken).ConfigureAwait(false);
            var eligible = aggregate.Slots.Where(slot => slot.IsEligible)
                .ToDictionary(slot => (slot.DayOfWeek, slot.PeriodSequence));
            foreach (var slot in configuration.Slots.Where(item => item.IsActive && !item.IsDeleted))
            {
                if (eligible.TryGetValue((ToDayOfWeek(slot.Day), slot.Period ?? 0), out var replacement)
                    && replacement.StartsAt == slot.LocalStartTime && replacement.EndsAt == slot.LocalEndTime)
                {
                    slot.StableSlotKey = replacement.StableKey;
                    slot.SchoolTimetableId = replacement.SchoolTimetableId;
                    slot.TimetableRevision = replacement.TimetableRevision;
                    slot.BellScheduleRevisionId = replacement.BellScheduleRevisionId;
                    slot.IsConflicted = false;
                    slot.ConflictReason = null;
                }
                else
                {
                    slot.IsConflicted = true;
                    slot.ConflictReason = "The selected Bell period became a lesson, standby, break, or was removed by timetable publication";
                }
            }
            if (aggregate.SchoolTimetableId > 0)
            {
                configuration.SchoolTimetableId = aggregate.SchoolTimetableId;
                configuration.TimetableRevision = aggregate.TimetableRevision;
                configuration.BellScheduleRevisionId = aggregate.BellScheduleRevisionId;
            }
            configuration.UpdatedAt = _timeProvider.GetUtcNow();
            _context.TeacherOfficeHourAudits.Add(new TeacherOfficeHourAudit
            {
                SchoolId = schoolId,
                TeacherOfficeHourConfigurationId = configuration.Id,
                InstructorProfileId = configuration.InstructorProfileId,
                ActorUserId = "system:office-hours-reconciliation",
                Reason = teacherUserIds is { Count: > 0 }
                    ? "Timetable substitution reconciliation"
                    : "Published timetable reconciliation",
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = JsonSerializer.Serialize(configuration.Slots.Select(slot => new
                {
                    slot.StableSlotKey,
                    slot.IsActive,
                    slot.IsConflicted,
                    slot.ConflictReason
                })),
                OccurredAt = configuration.UpdatedAt,
                CorrelationId = Guid.NewGuid()
            });
        }
        if (configurations.Count > 0) await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var instructorId in configurations.Select(item => item.InstructorProfileId).Distinct())
            await RecalculateQueuedMessagesAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
        if (reconciliationTransaction is not null)
            await reconciliationTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsConversationTargetAllowedAsync(
        int schoolId,
        string creatorUserId,
        CreateConversationRequestDto request,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var localDate = DateOnly.FromDateTime(instant.UtcDateTime);
        PublishedBellScheduleCandidate? schedule = null;
        if (request.ThreadType == ConversationThreadType.GuardianTeacher)
        {
            try
            {
                var candidates = await _schedules.GetPublishedCandidatesAsync(schoolId, instant, cancellationToken)
                    .ConfigureAwait(false);
                if (candidates.Count != 1) return false;
                schedule = candidates[0];
                localDate = DateOnly.FromDateTime(BellScheduleResolver.LocalTime(schedule.Schedule, instant).DateTime);
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        var isSocialWorkerCaseStart = request.ThreadType == ConversationThreadType.GuardianSocialWorker
            && request.ReferralId.HasValue
            && request.TargetGuardianProfileId.HasValue
            && string.IsNullOrWhiteSpace(request.TargetStaffUserId)
            && request.TargetInstructorProfileId is null;
        if (isSocialWorkerCaseStart)
        {
            return await _context.StudentReferrals.AsNoTracking().AnyAsync(referral =>
                referral.Id == request.ReferralId!.Value
                && referral.SchoolId == schoolId
                && referral.StudentId == request.StudentId
                && referral.AssignedSocialWorkerUserId == creatorUserId
                && !referral.IsDeleted
                && referral.Status != StudentReferralStatus.Resolved
                && referral.Status != StudentReferralStatus.Closed
                && _context.UserSchoolRoles.Any(assignment => assignment.SchoolId == schoolId
                    && assignment.UserId == creatorUserId
                    && assignment.IsActive
                    && !assignment.IsDeleted
                    && assignment.Role.Name == RoleNames.SocialWorker)
                && _context.GuardianProfiles.Any(guardian => guardian.Id == request.TargetGuardianProfileId.GetValueOrDefault()
                    && guardian.SchoolId == schoolId
                    && guardian.IsActive
                    && !guardian.IsDeleted
                    && guardian.ApplicationUser.IsActive
                    && guardian.Students.Any(link => link.StudentId == request.StudentId
                        && !link.IsDeleted
                        && link.ValidFrom <= localDate
                        && (link.ValidTo == null || link.ValidTo >= localDate))),
                cancellationToken).ConfigureAwait(false);
        }

        var guardianProfileId = await _context.GuardianProfiles
            .AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId
                && profile.ApplicationUserId == creatorUserId
                && profile.IsActive
                && !profile.IsDeleted
                && profile.ApplicationUser.IsActive)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (guardianProfileId is null || request.StudentId <= 0) return false;

        var linked = await _context.StudentGuardians
            .AsNoTracking()
            .AnyAsync(link => link.SchoolId == schoolId
                && link.GuardianProfileId == guardianProfileId.Value
                && link.StudentId == request.StudentId
                && !link.IsDeleted
                && link.Student.IsActive
                && !link.Student.IsDeleted
                && link.ValidFrom <= localDate
                && (link.ValidTo == null || link.ValidTo >= localDate),
                cancellationToken)
            .ConfigureAwait(false);
        if (!linked) return false;

        if (request.ThreadType == ConversationThreadType.GuardianTeacher)
        {
            if (request.TargetInstructorProfileId is null
                || !string.IsNullOrWhiteSpace(request.TargetStaffUserId)) return false;
            var publishedSchedule = schedule!;

            var classroomIds = await _context.StudentEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.SchoolId == schoolId
                    && enrollment.StudentId == request.StudentId
                    && enrollment.Status == StudentEnrollmentStatus.Active
                    && enrollment.AcademicTerm.AcademicYearId == publishedSchedule.Schedule.AcademicYearId
                    && enrollment.AcademicTerm.Semester == publishedSchedule.Schedule.Semester
                    && enrollment.EnrolledOn <= localDate
                    && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= localDate))
                .Select(enrollment => enrollment.ClassroomId)
                .Distinct()
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            if (classroomIds.Length == 0) return false;

            return await _context.SchoolTimetableEntries
                .AsNoTracking()
                .AnyAsync(entry => entry.SchoolId == schoolId
                    && entry.SchoolTimetableId == publishedSchedule.SchoolTimetableId
                    && entry.SchoolTimetable.IsPublished
                    && !entry.SchoolTimetable.IsDeleted
                    && !entry.IsDeleted
                    && entry.EntryType == TimetableEntryType.Lesson
                    && entry.InstructorProfileId == request.TargetInstructorProfileId.Value
                    && entry.InstructorProfile.IsActive
                    && classroomIds.Contains(entry.ClassroomId!.Value),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var requiredRole = request.ThreadType switch
        {
            ConversationThreadType.GuardianStudentAffairs => RoleNames.StudentAffairsOfficer,
            ConversationThreadType.GuardianSocialWorker => RoleNames.SocialWorker,
            _ => null
        };
        if (requiredRole is null
            || string.IsNullOrWhiteSpace(request.TargetStaffUserId)
            || !string.Equals(request.TargetStaffRole, requiredRole, StringComparison.Ordinal)
            || request.TargetInstructorProfileId is not null)
            return false;

        if (request.ThreadType == ConversationThreadType.GuardianSocialWorker)
        {
            return await _context.StudentReferrals.AsNoTracking().AnyAsync(
                referral => referral.SchoolId == schoolId
                    && (!request.ReferralId.HasValue || referral.Id == request.ReferralId.Value)
                    && referral.StudentId == request.StudentId
                    && !referral.IsDeleted
                    && referral.Status != StudentReferralStatus.Resolved
                    && referral.Status != StudentReferralStatus.Closed
                    && referral.AssignedSocialWorkerUserId == request.TargetStaffUserId
                    && referral.AssignedSocialWorkerUser != null
                    && referral.AssignedSocialWorkerUser.IsActive
                    && _context.UserSchoolRoles.Any(assignment => assignment.SchoolId == schoolId
                        && assignment.UserId == request.TargetStaffUserId
                        && assignment.IsActive
                        && !assignment.IsDeleted
                        && assignment.Role.Name == RoleNames.SocialWorker),
                cancellationToken).ConfigureAwait(false);
        }

        return await _context.UserSchoolRoles.AsNoTracking().AnyAsync(
            assignment => assignment.SchoolId == schoolId
                && assignment.UserId == request.TargetStaffUserId
                && assignment.User.IsActive
                && assignment.IsActive
                && !assignment.IsDeleted
                && assignment.Role.Name == requiredRole,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<ConversationDto>> GetConversationsAsync(
        int schoolId,
        string userId,
        ConversationListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.ConversationThreads
            .AsNoTracking()
            .Where(ct => ct.SchoolId == schoolId
                && ct.Participants.Any(p => p.ApplicationUserId == userId && !p.IsDeleted));
        var allowedThreadTypes = await GetAllowedThreadTypesAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
        dbQuery = dbQuery.Where(ct => allowedThreadTypes.Contains(ct.ThreadType));

        if (query.StudentId.HasValue)
        {
            dbQuery = dbQuery.Where(ct => ct.StudentId == query.StudentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(ct =>
                ct.Subject.Contains(term)
                || (ct.Student != null && (
                    ct.Student.FirstName.Contains(term)
                    || ct.Student.LastName.Contains(term)
                    || ct.Student.StudentNumber.Contains(term))));
        }

        if (query.IsUnread == true)
        {
            dbQuery = dbQuery.Where(ct => ct.Messages.Any(m =>
                m.SenderUserId != userId
                && m.Receipts.Any(r => r.RecipientUserId == userId
                    && r.DeliveryState == MessageDeliveryState.Delivered && r.ReadAt == null)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(ct => ct.UpdatedAt)
            .ThenByDescending(ct => ct.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ct => new
            {
                ct.Id,
                ct.StudentId,
                ct.StudentReferralId,
                ct.Subject,
                ct.ThreadType,
                ct.Status,
                ct.UpdatedAt,
                ct.RowVersion,
                StudentNumber = ct.Student != null ? ct.Student.StudentNumber : string.Empty,
                StudentDisplayName = ct.Student != null
                    ? (ct.Student.FirstName + " " + (ct.Student.MiddleName ?? string.Empty) + " " + ct.Student.LastName).Trim()
                    : string.Empty,
                StudentIsActive = ct.Student != null && ct.Student.IsActive,
                Participants = ct.Participants
                    .Where(p => !p.IsDeleted)
                    .Select(p => new
                    {
                        p.ApplicationUserId,
                        DisplayName = (p.ApplicationUser.FirstName + " " + p.ApplicationUser.LastName).Trim(),
                        Role = p.ParticipantRoleSnapshot
                    })
                    .ToList(),
                UnreadCount = ct.Messages.Count(m =>
                    m.SenderUserId != userId
                    && m.Receipts.Any(r => r.RecipientUserId == userId
                        && r.DeliveryState == MessageDeliveryState.Delivered && r.ReadAt == null))
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p =>
        {
            var studentSummary = new StudentSummaryDto(
                p.StudentId ?? 0,
                p.StudentNumber,
                p.StudentDisplayName,
                null,
                null,
                p.StudentIsActive,
                null);

            var participants = p.Participants
                .Select(part => new ConversationParticipantDto(
                    part.ApplicationUserId,
                    string.IsNullOrWhiteSpace(part.DisplayName) ? part.Role : part.DisplayName,
                    part.Role))
                .ToList();

            return new ConversationDto(
                p.Id,
                studentSummary,
                p.Subject,
                p.ThreadType,
                p.Status,
                participants,
                p.UnreadCount,
                p.UpdatedAt,
                Convert.ToBase64String(p.RowVersion),
                p.StudentReferralId);
        }).ToList();

        return new PagedResult<ConversationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<MessagingAuditThreadDto>> GetMessagingAuditAsync(
        int schoolId,
        MessagingAuditQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var dbQuery = _context.ConversationThreads.AsNoTracking()
            .Where(thread => thread.SchoolId == schoolId && !thread.IsDeleted);

        if (query.ThreadType.HasValue)
            dbQuery = dbQuery.Where(thread => thread.ThreadType == query.ThreadType.Value);
        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(thread => thread.Status == query.Status.Value);

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await dbQuery
            .OrderByDescending(thread => thread.UpdatedAt)
            .ThenByDescending(thread => thread.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(thread => new MessagingAuditThreadDto(
                thread.Id,
                thread.ThreadType,
                thread.Status,
                thread.Participants.Where(participant => !participant.IsDeleted)
                    .Select(participant => participant.ParticipantRoleSnapshot)
                    .Distinct()
                    .OrderBy(role => role)
                    .ToList(),
                thread.CreatedAt,
                thread.UpdatedAt,
                thread.Messages.Count(message => !message.IsDeleted),
                thread.Messages.Where(message => !message.IsDeleted)
                    .SelectMany(message => message.Receipts)
                    .Count(receipt => receipt.DeliveryState == MessageDeliveryState.Pending),
                thread.Messages.Where(message => !message.IsDeleted)
                    .SelectMany(message => message.Receipts)
                    .Count(receipt => receipt.DeliveryState == MessageDeliveryState.Delivered),
                thread.Messages.Where(message => !message.IsDeleted)
                    .SelectMany(message => message.Receipts)
                    .Count(receipt => receipt.DeliveryState == MessageDeliveryState.Failed)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<MessagingAuditThreadDto>
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<GuardianTeacherOptionDto>> GetGuardianTeacherOptionsAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var publications = await _schedules.GetPublishedCandidatesAsync(schoolId, instant, cancellationToken).ConfigureAwait(false);
        if (publications.Count != 1) return Array.Empty<GuardianTeacherOptionDto>();
        DateOnly localDate;
        try { localDate = DateOnly.FromDateTime(BellScheduleResolver.LocalTime(publications[0].Schedule, instant).DateTime); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return Array.Empty<GuardianTeacherOptionDto>(); }
        var classroomIds = await _context.StudentGuardians.AsNoTracking()
            .Where(link => link.SchoolId == schoolId && link.StudentId == studentId && !link.IsDeleted
                && link.GuardianProfile.ApplicationUserId == guardianUserId && link.GuardianProfile.IsActive
                && link.ValidFrom <= localDate && (link.ValidTo == null || link.ValidTo >= localDate))
            .SelectMany(link => link.Student.Enrollments
                .Where(enrollment => enrollment.Status == StudentEnrollmentStatus.Active && !enrollment.IsDeleted
                    && enrollment.EnrolledOn <= localDate && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= localDate))
                .Select(enrollment => enrollment.ClassroomId))
            .Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
        if (classroomIds.Count == 0) return Array.Empty<GuardianTeacherOptionDto>();
        var rows = await _context.SchoolTimetableEntries.AsNoTracking()
            .Where(entry => entry.SchoolId == schoolId && entry.SchoolTimetableId == publications[0].SchoolTimetableId
                && !entry.IsDeleted && entry.EntryType == TimetableEntryType.Lesson
                && entry.ClassroomId.HasValue && classroomIds.Contains(entry.ClassroomId.Value)
                && entry.InstructorProfile.IsActive)
            .Select(entry => new
            {
                entry.InstructorProfileId,
                DisplayName = (entry.InstructorProfile.User.FirstName + " " + entry.InstructorProfile.User.LastName).Trim(),
                entry.Subject
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.GroupBy(row => new { row.InstructorProfileId, row.DisplayName })
            .Select(group => new GuardianTeacherOptionDto(group.Key.InstructorProfileId, group.Key.DisplayName,
                string.Join(", ", group.Select(row => row.Subject).Where(subject => !string.IsNullOrWhiteSpace(subject)).Distinct())))
            .OrderBy(item => item.DisplayName).ToList();
    }

    public async Task<IReadOnlyList<GuardianStaffOptionDto>> GetGuardianStaffOptionsAsync(
        int schoolId,
        string guardianUserId,
        int studentId,
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        DateOnly localDate;
        try
        {
            var publications = await _schedules.GetPublishedCandidatesAsync(
                schoolId, instant, cancellationToken).ConfigureAwait(false);
            if (publications.Count != 1) return Array.Empty<GuardianStaffOptionDto>();
            localDate = DateOnly.FromDateTime(
                BellScheduleResolver.LocalTime(publications[0].Schedule, instant).DateTime);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return Array.Empty<GuardianStaffOptionDto>();
        }

        var linked = await _context.StudentGuardians.AsNoTracking().AnyAsync(link =>
            link.SchoolId == schoolId
            && link.StudentId == studentId
            && !link.IsDeleted
            && link.Student.IsActive
            && !link.Student.IsDeleted
            && link.GuardianProfile.ApplicationUserId == guardianUserId
            && link.GuardianProfile.IsActive
            && !link.GuardianProfile.IsDeleted
            && link.GuardianProfile.ApplicationUser.IsActive
            && link.ValidFrom <= localDate
            && (link.ValidTo == null || link.ValidTo >= localDate),
            cancellationToken).ConfigureAwait(false);
        if (!linked) return Array.Empty<GuardianStaffOptionDto>();

        var officers = await _context.UserSchoolRoles.AsNoTracking()
            .Where(assignment => assignment.SchoolId == schoolId
                && assignment.IsActive
                && !assignment.IsDeleted
                && assignment.User.IsActive
                && assignment.Role.Name == RoleNames.StudentAffairsOfficer)
            .Select(assignment => new GuardianStaffOptionDto(
                assignment.UserId,
                (assignment.User.FirstName + " " + assignment.User.LastName).Trim(),
                RoleNames.StudentAffairsOfficer,
                ConversationThreadType.GuardianStudentAffairs))
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var socialWorkers = await _context.StudentReferrals.AsNoTracking()
            .Where(referral => referral.SchoolId == schoolId
                && referral.StudentId == studentId
                && !referral.IsDeleted
                && referral.Status != StudentReferralStatus.Resolved
                && referral.Status != StudentReferralStatus.Closed
                && referral.AssignedSocialWorkerUserId != null
                && referral.AssignedSocialWorkerUser != null
                && referral.AssignedSocialWorkerUser.IsActive
                && _context.UserSchoolRoles.Any(assignment => assignment.SchoolId == schoolId
                    && assignment.UserId == referral.AssignedSocialWorkerUserId
                    && assignment.IsActive
                    && !assignment.IsDeleted
                    && assignment.Role.Name == RoleNames.SocialWorker))
            .Select(referral => new GuardianStaffOptionDto(
                referral.AssignedSocialWorkerUserId!,
                (referral.AssignedSocialWorkerUser!.FirstName + " "
                    + referral.AssignedSocialWorkerUser.LastName).Trim(),
                RoleNames.SocialWorker,
                ConversationThreadType.GuardianSocialWorker))
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return officers.Concat(socialWorkers)
            .OrderBy(option => option.Role)
            .ThenBy(option => option.DisplayName)
            .ToArray();
    }

    public async Task<ConversationDto?> GetConversationByIdAsync(
        int schoolId,
        string userId,
        int conversationId,
        CancellationToken cancellationToken)
    {
        if (!await IsParticipantAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false))
            return null;
        var projection = await _context.ConversationThreads
            .AsNoTracking()
            .Where(ct => ct.Id == conversationId
                && ct.SchoolId == schoolId
                && ct.Participants.Any(p => p.ApplicationUserId == userId && !p.IsDeleted))
            .Select(ct => new
            {
                ct.Id,
                ct.StudentId,
                ct.StudentReferralId,
                ct.Subject,
                ct.ThreadType,
                ct.Status,
                ct.UpdatedAt,
                ct.RowVersion,
                StudentNumber = ct.Student != null ? ct.Student.StudentNumber : string.Empty,
                StudentDisplayName = ct.Student != null
                    ? (ct.Student.FirstName + " " + (ct.Student.MiddleName ?? string.Empty) + " " + ct.Student.LastName).Trim()
                    : string.Empty,
                StudentIsActive = ct.Student != null && ct.Student.IsActive,
                Participants = ct.Participants
                    .Where(p => !p.IsDeleted)
                    .Select(p => new
                    {
                        p.ApplicationUserId,
                        DisplayName = (p.ApplicationUser.FirstName + " " + p.ApplicationUser.LastName).Trim(),
                        Role = p.ParticipantRoleSnapshot
                    })
                    .ToList(),
                UnreadCount = ct.Messages.Count(m =>
                    m.SenderUserId != userId
                    && m.Receipts.Any(r => r.RecipientUserId == userId
                        && r.DeliveryState == MessageDeliveryState.Delivered && r.ReadAt == null))
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projection is null) return null;

        var studentSummary = new StudentSummaryDto(
            projection.StudentId ?? 0,
            projection.StudentNumber,
            projection.StudentDisplayName,
            null,
            null,
            projection.StudentIsActive,
            null);

        var participants = projection.Participants
            .Select(part => new ConversationParticipantDto(
                part.ApplicationUserId,
                string.IsNullOrWhiteSpace(part.DisplayName) ? part.Role : part.DisplayName,
                part.Role))
            .ToList();

        return new ConversationDto(
            projection.Id,
            studentSummary,
            projection.Subject,
            projection.ThreadType,
            projection.Status,
            participants,
            projection.UnreadCount,
            projection.UpdatedAt,
            Convert.ToBase64String(projection.RowVersion),
            projection.StudentReferralId);
    }

    public async Task<PagedResult<ConversationMessageDto>> GetConversationMessagesAsync(
        int schoolId,
        string userId,
        int conversationId,
        ConversationMessageQuery query,
        CancellationToken cancellationToken)
    {
        var isParticipant = await IsParticipantAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false);

        if (!isParticipant)
            return new PagedResult<ConversationMessageDto>
            {
                Items = new List<ConversationMessageDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            };

        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 50 : query.PageSize;

        var dbQuery = _context.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationThreadId == conversationId
                && m.SchoolId == schoolId
                && !m.IsDeleted
                && (m.SenderUserId == userId
                    || m.Receipts.Any(receipt => receipt.RecipientUserId == userId
                        && receipt.DeliveryState == MessageDeliveryState.Delivered)));

        if (query.BeforeMessageId.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.Id < query.BeforeMessageId.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderBy(m => m.SentAt ?? m.QueuedAt)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.ConversationThreadId,
                m.SenderUserId,
                SenderDisplayName = (m.SenderUser.FirstName + " " + m.SenderUser.LastName).Trim(),
                m.Body,
                m.ReplyToMessageId,
                CreatedAt = m.SentAt ?? m.CreatedAt,
                m.OfficeHoursDisposition,
                m.NextEligibleSendAt,
                Receipts = m.Receipts.Select(r => new
                {
                    r.RecipientUserId,
                    RecipientDisplayName = (r.RecipientUser.FirstName + " " + r.RecipientUser.LastName).Trim(),
                    r.DeliveryState,
                    r.DeliveredAt,
                    r.ReadAt
                }).ToList()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p =>
        {
            var senderRole = p.SenderUserId == userId ? "Me" : "Sender";
            var sender = new ActorSummaryDto(
                p.SenderUserId,
                string.IsNullOrWhiteSpace(p.SenderDisplayName) ? "User" : p.SenderDisplayName,
                senderRole);

            var receipts = p.Receipts.Select(r => new NotificationDeliveryDto(
                string.IsNullOrWhiteSpace(r.RecipientDisplayName) ? r.RecipientUserId : r.RecipientDisplayName,
                "Recipient",
                r.DeliveryState == MessageDeliveryState.Delivered
                    ? NotificationDeliveryStatus.Delivered
                    : r.DeliveryState == MessageDeliveryState.Failed
                        ? NotificationDeliveryStatus.Failed
                        : NotificationDeliveryStatus.Pending,
                r.DeliveredAt,
                r.ReadAt)).ToList();

            var deliveryState = p.Receipts.Count == 0
                ? MessageDeliveryState.Delivered
                : p.Receipts.All(r => r.DeliveryState == MessageDeliveryState.Delivered)
                    ? MessageDeliveryState.Delivered
                    : p.Receipts.Any(r => r.DeliveryState == MessageDeliveryState.Failed)
                        ? MessageDeliveryState.Failed
                        : MessageDeliveryState.Pending;

            return new ConversationMessageDto(
                p.Id,
                p.ConversationThreadId,
                sender,
                p.Body,
                p.ReplyToMessageId,
                p.CreatedAt,
                deliveryState,
                p.OfficeHoursDisposition,
                p.NextEligibleSendAt,
                receipts);
        }).ToList();

        return new PagedResult<ConversationMessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ConversationDto> CreateConversationAsync(
        int schoolId,
        string creatorUserId,
        CreateConversationRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedKey = request.IdempotencyKey.Trim();
        var creationScope = $"create|{request.StudentId}|{request.ThreadType}|{request.Subject.Trim()}|{request.TargetInstructorProfileId}|{request.TargetStaffUserId}|{request.TargetStaffRole}|{request.ReferralId}|{request.TargetGuardianProfileId}";
        var creationPayloadHash = Hash($"{creationScope}||{request.InitialBody.Trim()}");
        var existingMessage = await _context.ConversationMessages.AsNoTracking()
            .Where(message => message.SchoolId == schoolId && message.SenderUserId == creatorUserId
                && message.IdempotencyKey == normalizedKey)
            .Select(message => new
            {
                message.ConversationThreadId,
                message.IdempotencyPayloadHash
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existingMessage is not null)
        {
            if (!string.Equals(existingMessage.IdempotencyPayloadHash, creationPayloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency conflict: the key was already used with different content");
            return (await GetConversationByIdAsync(schoolId, creatorUserId, existingMessage.ConversationThreadId, cancellationToken).ConfigureAwait(false))!;
        }

        string? targetUserId = request.TargetStaffUserId;
        var referralId = request.ReferralId;
        var socialWorkerInitiatedCase = request.ThreadType == ConversationThreadType.GuardianSocialWorker
            && request.ReferralId.HasValue
            && request.TargetGuardianProfileId.HasValue
            && string.IsNullOrWhiteSpace(request.TargetStaffUserId);
        if (socialWorkerInitiatedCase)
        {
            targetUserId = await _context.GuardianProfiles.AsNoTracking()
                .Where(profile => profile.Id == request.TargetGuardianProfileId!.Value
                    && profile.SchoolId == schoolId
                    && profile.IsActive
                    && !profile.IsDeleted)
                .Select(profile => profile.ApplicationUserId)
                .SingleAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (string.IsNullOrWhiteSpace(targetUserId) && request.TargetInstructorProfileId.HasValue)
        {
            var instructor = await _context.InstructorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.TargetInstructorProfileId.Value && i.SchoolId == schoolId, cancellationToken)
                .ConfigureAwait(false);
            targetUserId = instructor?.UserId;
        }

        if (request.ThreadType == ConversationThreadType.GuardianSocialWorker
            && !referralId.HasValue
            && !string.IsNullOrWhiteSpace(targetUserId))
        {
            referralId = await _context.StudentReferrals.AsNoTracking()
                .Where(referral => referral.SchoolId == schoolId
                    && referral.StudentId == request.StudentId
                    && referral.AssignedSocialWorkerUserId == targetUserId
                    && !referral.IsDeleted
                    && referral.Status != StudentReferralStatus.Resolved
                    && referral.Status != StudentReferralStatus.Closed)
                .OrderByDescending(referral => referral.UpdatedAt)
                .Select(referral => (int?)referral.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        if (request.ThreadType == ConversationThreadType.GuardianSocialWorker && !referralId.HasValue)
            throw new InvalidOperationException("An active assigned referral is required for social-worker messaging");
        if (string.IsNullOrWhiteSpace(targetUserId) || targetUserId == creatorUserId)
            throw new InvalidOperationException("The conversation recipient could not be resolved");

        if (referralId.HasValue)
        {
            var existingThreadId = await _context.ConversationThreads.AsNoTracking()
                .Where(thread => thread.SchoolId == schoolId
                    && thread.StudentReferralId == referralId.Value
                    && thread.ThreadType == ConversationThreadType.GuardianSocialWorker
                    && thread.Status == ConversationThreadStatus.Open
                    && !thread.IsDeleted
                    && thread.Participants.Any(participant => participant.ApplicationUserId == creatorUserId && !participant.IsDeleted)
                    && thread.Participants.Any(participant => participant.ApplicationUserId == targetUserId && !participant.IsDeleted))
                .Select(thread => (int?)thread.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (existingThreadId.HasValue)
            {
                await SendMessageCoreAsync(
                    schoolId,
                    creatorUserId,
                    existingThreadId.Value,
                    new SendMessageRequestDto(request.InitialBody, null, normalizedKey),
                    creationScope,
                    cancellationToken).ConfigureAwait(false);
                return (await GetConversationByIdAsync(
                    schoolId, creatorUserId, existingThreadId.Value, cancellationToken).ConfigureAwait(false))!;
            }
        }

        var now = _timeProvider.GetUtcNow();
        var thread = new ConversationThread
        {
            SchoolId = schoolId,
            StudentId = request.StudentId > 0 ? request.StudentId : null,
            StudentReferralId = referralId,
            ThreadType = request.ThreadType,
            Subject = request.Subject.Trim(),
            Status = ConversationThreadStatus.Open,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedByUserId = creatorUserId,
            UpdatedAt = now
        };

        thread.Participants.Add(new ConversationParticipant
        {
            SchoolId = schoolId,
            ApplicationUserId = creatorUserId,
            ParticipantRoleSnapshot = socialWorkerInitiatedCase ? RoleNames.SocialWorker : RoleNames.Guardian,
            JoinedAt = now,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedByUserId = creatorUserId,
            UpdatedAt = now
        });

        thread.Participants.Add(new ConversationParticipant
            {
                SchoolId = schoolId,
                ApplicationUserId = targetUserId,
                ParticipantRoleSnapshot = socialWorkerInitiatedCase
                    ? RoleNames.Guardian
                    : request.TargetStaffRole ?? RoleNames.Instructor,
                JoinedAt = now,
                CreatedByUserId = creatorUserId,
                CreatedAt = now,
                UpdatedByUserId = creatorUserId,
                UpdatedAt = now
            });

        await using var createTransaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        _context.ConversationThreads.Add(thread);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var initialMessage = await SendMessageCoreAsync(
            schoolId, creatorUserId, thread.Id,
            new SendMessageRequestDto(request.InitialBody, null, normalizedKey),
            creationScope,
            cancellationToken).ConfigureAwait(false);

        if (initialMessage.Message.ConversationId != thread.Id)
        {
            if (createTransaction is not null)
                await createTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            return (await GetConversationByIdAsync(
                schoolId, creatorUserId, initialMessage.Message.ConversationId, cancellationToken).ConfigureAwait(false))!;
        }

        var created = (await GetConversationByIdAsync(schoolId, creatorUserId, thread.Id, cancellationToken).ConfigureAwait(false))!;
        if (createTransaction is not null)
            await createTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    public Task<SendMessageResultDto> SendMessageAsync(
        int schoolId,
        string senderUserId,
        int conversationId,
        SendMessageRequestDto request,
        CancellationToken cancellationToken) =>
        SendMessageCoreAsync(schoolId, senderUserId, conversationId, request, null, cancellationToken);

    private async Task<SendMessageResultDto> SendMessageCoreAsync(
        int schoolId,
        string senderUserId,
        int conversationId,
        SendMessageRequestDto request,
        string? idempotencyPayloadScope,
        CancellationToken cancellationToken)
    {
        var normalizedKey = request.IdempotencyKey.Trim();
        if (normalizedKey.Length is < 8 or > 200)
            throw new InvalidOperationException("A valid idempotency key is required");
        var payloadHash = Hash($"{idempotencyPayloadScope ?? conversationId.ToString()}|{request.ReplyToMessageId}|{request.Body.Trim()}");
        var replay = await _context.ConversationMessages.AsNoTracking()
            .FirstOrDefaultAsync(message => message.SchoolId == schoolId
                && message.SenderUserId == senderUserId
                && message.IdempotencyKey == normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            if (!string.Equals(replay.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency conflict: the key was already used with different content");
            return await ToSendResultAsync(replay, senderUserId, cancellationToken).ConfigureAwait(false);
        }

        var thread = await _context.ConversationThreads
            .Include(ct => ct.Participants.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(ct => ct.Id == conversationId
                && ct.SchoolId == schoolId
                && ct.Participants.Any(p => p.ApplicationUserId == senderUserId && !p.IsDeleted), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Conversation was not found");

        if (thread.Status != ConversationThreadStatus.Open)
            throw new InvalidOperationException("Closed conversations do not accept new messages");
        if (request.ReplyToMessageId.HasValue && !await _context.ConversationMessages.AsNoTracking()
                .AnyAsync(message => message.Id == request.ReplyToMessageId.Value
                    && message.ConversationThreadId == conversationId
                    && message.SchoolId == schoolId
                    && !message.IsDeleted, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Reply target must belong to the same conversation");

        var now = _timeProvider.GetUtcNow();
        var decision = await ResolveDeliveryAsync(thread, senderUserId, now, cancellationToken).ConfigureAwait(false);
        var message = new ConversationMessage
        {
            SchoolId = schoolId,
            ConversationThreadId = conversationId,
            SenderUserId = senderUserId,
            Body = request.Body.Trim(),
            ReplyToMessageId = request.ReplyToMessageId.HasValue ? (int?)request.ReplyToMessageId.Value : null,
            SentAt = now,
            QueuedAt = now,
            OfficeHoursDisposition = decision.Disposition,
            IdempotencyKey = normalizedKey,
            IdempotencyPayloadHash = payloadHash,
            NextEligibleSendAt = decision.NextEligibleSendAt,
            ReleasedAt = decision.DeliverNow ? now : null,
            CreatedByUserId = senderUserId,
            CreatedAt = now,
            UpdatedByUserId = senderUserId,
            UpdatedAt = now
        };

        foreach (var participant in thread.Participants.Where(p => p.ApplicationUserId != senderUserId))
        {
            message.Receipts.Add(new MessageReceipt
            {
                SchoolId = schoolId,
                RecipientUserId = participant.ApplicationUserId,
                DeliveryState = decision.DeliverNow ? MessageDeliveryState.Delivered : MessageDeliveryState.Pending,
                DeliveredAt = decision.DeliverNow ? now : null,
                CreatedAt = now
            });
        }

        if (decision.DeliverNow || decision.NextEligibleSendAt.HasValue)
        {
            var eventId = Guid.NewGuid();
            message.ReleaseOutboxEventId = eventId;
            _context.OutboxMessages.Add(new OutboxMessage
            {
                SchoolId = schoolId,
                EventId = eventId,
                EventType = typeof(AlFalah.Domain.Events.MessageReleaseDueEvent).FullName!,
                PayloadJson = "{}",
                OccurredAt = now,
                NextAttemptAt = decision.DeliverNow ? now : decision.NextEligibleSendAt
            });
        }

        thread.UpdatedAt = now;
        thread.UpdatedByUserId = senderUserId;

        await using var sendTransaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        _context.ConversationMessages.Add(message);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            if (sendTransaction is not null)
                await sendTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            var winner = await _context.ConversationMessages.AsNoTracking()
                .SingleOrDefaultAsync(existing => existing.SchoolId == schoolId
                    && existing.SenderUserId == senderUserId
                    && existing.IdempotencyKey == normalizedKey, cancellationToken).ConfigureAwait(false);
            if (winner is null) throw;
            if (!string.Equals(winner.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency conflict: the key was already used with different content");
            return await ToSendResultAsync(winner, senderUserId, cancellationToken).ConfigureAwait(false);
        }

        if (message.ReleaseOutboxEventId.HasValue)
        {
            var scheduled = await _context.OutboxMessages.SingleAsync(item => item.EventId == message.ReleaseOutboxEventId.Value, cancellationToken).ConfigureAwait(false);
            scheduled.PayloadJson = JsonSerializer.Serialize(new AlFalah.Domain.Events.MessageReleaseDueEvent(
                scheduled.EventId, schoolId, message.Id, now));
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await ToSendResultAsync(message, senderUserId, cancellationToken).ConfigureAwait(false);
        if (sendTransaction is not null)
            await sendTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<bool> MarkConversationReadAsync(
        int schoolId,
        string userId,
        int conversationId,
        long throughMessageId,
        CancellationToken cancellationToken)
    {
        if (!await IsParticipantAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false))
            return false;
        var unreadReceipts = await _context.MessageReceipts
            .Where(r => r.SchoolId == schoolId
                && r.RecipientUserId == userId
                && r.ConversationMessage.ConversationThreadId == conversationId
                && r.ConversationMessage.Id <= throughMessageId
                && r.DeliveryState == MessageDeliveryState.Delivered
                && r.ReadAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unreadReceipts.Count == 0) return true;

        var now = _timeProvider.GetUtcNow();
        foreach (var receipt in unreadReceipts)
        {
            receipt.ReadAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<ConversationDto?> CloseConversationAsync(
        int schoolId,
        string userId,
        int conversationId,
        CloseConversationRequestDto request,
        CancellationToken cancellationToken)
    {
        var thread = await _context.ConversationThreads
            .FirstOrDefaultAsync(ct => ct.Id == conversationId
                && ct.SchoolId == schoolId
                && ct.Participants.Any(p => p.ApplicationUserId == userId && !p.IsDeleted), cancellationToken)
            .ConfigureAwait(false);

        if (thread is null) return null;

        byte[] expected;
        try { expected = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new InvalidOperationException("The conversation row version is invalid"); }
        if (!thread.RowVersion.SequenceEqual(expected))
            throw new InvalidOperationException("The conversation was modified by another user");
        if (thread.Status == ConversationThreadStatus.Closed)
            return await GetConversationByIdAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false);

        thread.Status = ConversationThreadStatus.Closed;
        thread.UpdatedByUserId = userId;
        thread.UpdatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetConversationByIdAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OfficeHoursAggregateDto> GetEligibleOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken)
    {
        var instructorId = await GetInstructorIdAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
        return await BuildOfficeHoursAggregateAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OfficeHoursAggregateDto> GetMyOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken)
    {
        var instructorId = await GetInstructorIdAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
        return await BuildOfficeHoursAggregateAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OfficeHoursAggregateDto> UpdateMyOfficeHoursAsync(
        int schoolId,
        string userId,
        UpdateMyOfficeHoursRequestDto request,
        CancellationToken cancellationToken)
    {
        var instructorId = await GetInstructorIdAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
        return await SaveOfficeHoursAsync(
            schoolId, instructorId, userId, request.SelectedSlotKeys, request.EffectiveFrom,
            request.RowVersion, TeacherOfficeHourSource.TeacherSelected, "Teacher selection", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OfficeHoursAggregateDto> GetTeacherOfficeHoursAsync(
        int schoolId,
        string requesterUserId,
        int instructorId,
        CancellationToken cancellationToken)
    {
        if (!await _context.InstructorProfiles.AsNoTracking().AnyAsync(profile =>
                profile.Id == instructorId && profile.SchoolId == schoolId && profile.IsActive,
                cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Instructor was not found");
        var roles = await _context.UserSchoolRoles.AsNoTracking()
            .Where(item => item.SchoolId == schoolId && item.UserId == requesterUserId && item.IsActive && !item.IsDeleted)
            .Select(item => item.Role.Name!).ToListAsync(cancellationToken).ConfigureAwait(false);
        var allowed = roles.Contains(RoleNames.SchoolManager)
            || roles.Contains(RoleNames.Instructor) && await _context.InstructorProfiles.AsNoTracking().AnyAsync(
                profile => profile.Id == instructorId && profile.UserId == requesterUserId && profile.SchoolId == schoolId,
                cancellationToken).ConfigureAwait(false);
        if (!allowed && roles.Contains(RoleNames.Guardian))
        {
            var now = _timeProvider.GetUtcNow();
            var publications = await _schedules.GetPublishedCandidatesAsync(schoolId, now, cancellationToken).ConfigureAwait(false);
            if (publications.Count == 1)
            {
                var localDate = DateOnly.FromDateTime(BellScheduleResolver.LocalTime(publications[0].Schedule, now).DateTime);
                allowed = await _context.StudentGuardians.AsNoTracking().AnyAsync(link =>
                    link.SchoolId == schoolId && !link.IsDeleted
                    && link.GuardianProfile.ApplicationUserId == requesterUserId
                    && link.ValidFrom <= localDate && (link.ValidTo == null || link.ValidTo >= localDate)
                    && link.Student.Enrollments.Any(enrollment => enrollment.SchoolId == schoolId
                        && enrollment.Status == StudentEnrollmentStatus.Active
                        && enrollment.EnrolledOn <= localDate && (enrollment.WithdrawnOn == null || enrollment.WithdrawnOn >= localDate)
                        && _context.SchoolTimetableEntries.Any(entry => entry.SchoolTimetableId == publications[0].SchoolTimetableId
                            && entry.ClassroomId == enrollment.ClassroomId && entry.InstructorProfileId == instructorId
                            && entry.EntryType == TimetableEntryType.Lesson && !entry.IsDeleted)), cancellationToken).ConfigureAwait(false);
            }
        }
        if (!allowed) throw new InvalidOperationException("Instructor was not found");
        return await BuildOfficeHoursAggregateAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SchoolInstructorOptionDto>> GetSchoolInstructorOptionsAsync(
        int schoolId,
        string? search,
        CancellationToken cancellationToken)
    {
        var normalized = search?.Trim();
        var query = _context.InstructorProfiles.AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId && profile.IsActive && !profile.IsDeleted
                && profile.User.IsActive && !profile.User.IsDeleted);

        if (!string.IsNullOrWhiteSpace(normalized))
            query = query.Where(profile =>
                (profile.User.FirstName + " " + profile.User.LastName).Contains(normalized)
                || (profile.SubjectSpecialization != null && profile.SubjectSpecialization.Contains(normalized)));

        return await query
            .OrderBy(profile => profile.User.FirstName)
            .ThenBy(profile => profile.User.LastName)
            .ThenBy(profile => profile.Id)
            .Take(100)
            .Select(profile => new SchoolInstructorOptionDto(
                profile.Id,
                (profile.User.FirstName + " " + profile.User.LastName).Trim(),
                profile.SubjectSpecialization ?? string.Empty))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OfficeHoursAggregateDto> OverrideTeacherOfficeHoursAsync(
        int schoolId,
        string adminUserId,
        int instructorId,
        OverrideTeacherOfficeHoursRequestDto request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new InvalidOperationException("A manager override reason is required and must not exceed 2000 characters");
        if (!await _context.InstructorProfiles.AsNoTracking().AnyAsync(profile =>
                profile.Id == instructorId && profile.SchoolId == schoolId && profile.IsActive,
                cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Instructor was not found");
        return await SaveOfficeHoursAsync(
            schoolId, instructorId, adminUserId, request.SelectedSlotKeys, request.EffectiveFrom,
            request.RowVersion, TeacherOfficeHourSource.ManagerOverride, reason, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<int> GetInstructorIdAsync(int schoolId, string userId, CancellationToken cancellationToken)
    {
        var ids = await _context.InstructorProfiles.AsNoTracking()
            .Where(profile => profile.SchoolId == schoolId && profile.UserId == userId && profile.IsActive)
            .Select(profile => profile.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return ids.Count == 1 ? ids[0] : throw new InvalidOperationException("Instructor was not found");
    }

    private async Task<OfficeHoursAggregateDto> BuildOfficeHoursAggregateAsync(
        int schoolId,
        int instructorId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        IReadOnlyList<PublishedBellScheduleCandidate> publications;
        try { publications = await _schedules.GetPublishedCandidatesAsync(schoolId, now, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return EmptyOfficeHours(instructorId, "The published schedule has an invalid school timezone");
        }
        if (publications.Count != 1)
            return EmptyOfficeHours(instructorId, publications.Count == 0
                ? "No published schedule is effective for the school date"
                : "More than one published schedule is effective for the school date");

        var publication = publications[0];
        DateTimeOffset localNow;
        try { localNow = BellScheduleResolver.LocalTime(publication.Schedule, now); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return EmptyOfficeHours(instructorId, "The published schedule has an invalid school timezone");
        }
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var terms = await _context.AcademicTerms.AsNoTracking()
            .Where(term => term.SchoolId == schoolId && term.AcademicYearId == publication.Schedule.AcademicYearId
                && term.Semester == publication.Schedule.Semester && term.IsActive && !term.IsDeleted
                && term.StartsOn <= localDate && term.EndsOn >= localDate)
            .Select(term => new { term.Id, term.StartsOn, term.EndsOn })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (terms.Count != 1)
            return EmptyOfficeHours(instructorId, "The active academic term is missing or ambiguous");
        var term = terms[0];

        var busy = await _context.SchoolTimetableEntries.AsNoTracking()
            .Where(entry => entry.SchoolId == schoolId
                && entry.SchoolTimetableId == publication.SchoolTimetableId
                && entry.InstructorProfileId == instructorId
                && !entry.IsDeleted
                && (entry.EntryType == TimetableEntryType.Lesson || entry.EntryType == TimetableEntryType.Standby))
            .Select(entry => new { entry.Day, entry.Period })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var busyKeys = busy.Select(item => $"{(int)item.Day}:{item.Period}").ToHashSet(StringComparer.Ordinal);

        var current = await _context.TeacherOfficeHourConfigurations.AsNoTracking()
            .Where(configuration => configuration.SchoolId == schoolId
                && configuration.InstructorProfileId == instructorId
                && configuration.IsCurrent && !configuration.IsDeleted)
            .Select(configuration => new
            {
                Configuration = configuration,
                Slots = configuration.Slots.Where(slot => !slot.IsDeleted).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var selectedByKey = current?.Slots.ToDictionary(slot => slot.StableSlotKey, StringComparer.Ordinal)
            ?? new Dictionary<string, TeacherOfficeHour>(StringComparer.Ordinal);

        var candidates = new List<OfficeHourSlotDto>();
        foreach (var day in publication.Schedule.Days.Where(day => day.IsStudyDay))
        {
            var timetableDay = (TimetableDay)day.Day;
            var periods = BellScheduleResolver.EffectivePeriods(publication.Schedule, timetableDay)
                .OrderBy(period => period.StartLocalTime).ToArray();
            if (periods.Zip(periods.Skip(1)).Any(pair => pair.First.EndLocalTime > pair.Second.StartLocalTime))
                continue;
            var breaks = BellScheduleResolver.EffectiveBreaks(publication.Schedule, timetableDay);
            foreach (var period in periods)
            {
                var overlapsBreak = breaks.Any(item => item.StartLocalTime < period.EndLocalTime && period.StartLocalTime < item.EndLocalTime);
                if (overlapsBreak || busyKeys.Contains($"{day.Day}:{period.Sequence}")) continue;
                var key = StableSlotKey(publication, timetableDay, period.Sequence);
                var selected = selectedByKey.GetValueOrDefault(key);
                candidates.Add(new OfficeHourSlotDto(
                    key, ToDayOfWeek(timetableDay), period.Sequence, period.StartLocalTime, period.EndLocalTime,
                    true, selected is not null && !selected.IsConflicted, selected?.IsConflicted ?? false,
                    selected?.ConflictReason, selected?.Source ?? TeacherOfficeHourSource.DerivedFromPublishedTimetable,
                    publication.SchoolTimetableId, publication.TimetableRevision, publication.Schedule.RevisionId));
            }
        }

        foreach (var conflict in selectedByKey.Values.Where(slot => slot.IsConflicted || candidates.All(item => item.StableKey != slot.StableSlotKey)))
        {
            candidates.Add(new OfficeHourSlotDto(
                conflict.StableSlotKey, ToDayOfWeek(conflict.Day), conflict.Period ?? 0,
                conflict.LocalStartTime ?? TimeOnly.MinValue, conflict.LocalEndTime ?? TimeOnly.MinValue,
                false, true, true, conflict.ConflictReason ?? "The selected Bell period is no longer eligible",
                conflict.Source, conflict.SchoolTimetableId, conflict.TimetableRevision, conflict.BellScheduleRevisionId));
        }

        var rowVersion = current is null
            ? NewConfigurationToken(instructorId, term.Id, publication)
            : Convert.ToBase64String(current.Configuration.RowVersion);
        return new OfficeHoursAggregateDto(
            current?.Configuration.Id, instructorId, term.Id, publication.SchoolTimetableId,
            publication.TimetableRevision, publication.Schedule.RevisionId,
            current?.Configuration.EffectiveFrom ?? localDate, current?.Configuration.EffectiveUntil,
            rowVersion, current?.Configuration.Source ?? TeacherOfficeHourSource.DerivedFromPublishedTimetable,
            current?.Configuration.UpdatedByUserId ?? string.Empty, current?.Configuration.UpdatedAt ?? now,
            candidates.Count == 0 ? "No eligible free Bell periods are available" : null,
            candidates.OrderBy(slot => slot.DayOfWeek).ThenBy(slot => slot.PeriodSequence).ToList());
    }

    private async Task<OfficeHoursAggregateDto> SaveOfficeHoursAsync(
        int schoolId,
        int instructorId,
        string actorUserId,
        IReadOnlyList<string> selectedSlotKeys,
        DateOnly effectiveFrom,
        string rowVersion,
        TeacherOfficeHourSource source,
        string reason,
        CancellationToken cancellationToken)
    {
        var before = await BuildOfficeHoursAggregateAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
        if (before.AcademicTermId <= 0 || before.SchoolTimetableId <= 0)
            throw new InvalidOperationException(before.StatusReason ?? "No published schedule is available");
        if (!string.Equals(before.RowVersion, rowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("The office-hours configuration was modified by another user");
        var normalized = selectedSlotKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length != selectedSlotKeys.Count)
            throw new InvalidOperationException("Selected office-hour slot keys must be unique and non-empty");
        var eligible = before.Slots.Where(slot => slot.IsEligible).ToDictionary(slot => slot.StableKey, StringComparer.Ordinal);
        if (normalized.Any(key => !eligible.ContainsKey(key)))
            throw new InvalidOperationException("One or more selected office-hour slots are no longer eligible");
        var term = await _context.AcademicTerms.AsNoTracking().SingleAsync(item => item.Id == before.AcademicTermId && item.SchoolId == schoolId, cancellationToken).ConfigureAwait(false);
        if (effectiveFrom < term.StartsOn || effectiveFrom > term.EndsOn)
            throw new InvalidOperationException("EffectiveFrom must be inside the active academic term");

        var now = _timeProvider.GetUtcNow();
        await using var officeHoursTransaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var existing = await _context.TeacherOfficeHourConfigurations
            .Include(configuration => configuration.Slots)
            .SingleOrDefaultAsync(configuration => configuration.SchoolId == schoolId
                && configuration.InstructorProfileId == instructorId
                && configuration.IsCurrent && !configuration.IsDeleted, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            byte[] expectedRowVersion;
            try { expectedRowVersion = Convert.FromBase64String(rowVersion); }
            catch (FormatException) { throw new InvalidOperationException("The office-hours row version is invalid"); }
            _context.Entry(existing).Property(configuration => configuration.RowVersion).OriginalValue = expectedRowVersion;
            existing.IsCurrent = false;
            existing.EffectiveUntil = effectiveFrom > existing.EffectiveFrom ? effectiveFrom.AddDays(-1) : existing.EffectiveFrom;
            existing.UpdatedAt = now;
            existing.UpdatedByUserId = actorUserId;
            foreach (var slot in existing.Slots) slot.IsActive = false;
        }

        var configuration = new TeacherOfficeHourConfiguration
        {
            SchoolId = schoolId,
            InstructorProfileId = instructorId,
            AcademicTermId = before.AcademicTermId,
            SchoolTimetableId = before.SchoolTimetableId,
            TimetableRevision = before.TimetableRevision,
            BellScheduleRevisionId = before.BellScheduleRevisionId,
            EffectiveFrom = effectiveFrom,
            Source = source,
            OverrideReason = source == TeacherOfficeHourSource.ManagerOverride ? reason : null,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
            UpdatedAt = now,
            UpdatedByUserId = actorUserId
        };
        foreach (var key in normalized)
        {
            var slot = eligible[key];
            configuration.Slots.Add(new TeacherOfficeHour
            {
                SchoolId = schoolId,
                InstructorProfileId = instructorId,
                AcademicTermId = before.AcademicTermId,
                StableSlotKey = slot.StableKey,
                SchoolTimetableId = slot.SchoolTimetableId,
                TimetableRevision = slot.TimetableRevision,
                BellScheduleRevisionId = slot.BellScheduleRevisionId,
                Day = ToTimetableDay(slot.DayOfWeek),
                Period = slot.PeriodSequence,
                LocalStartTime = slot.StartsAt,
                LocalEndTime = slot.EndsAt,
                Source = source,
                EffectiveFrom = effectiveFrom,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId
            });
        }
        _context.TeacherOfficeHourConfigurations.Add(configuration);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("The office-hours configuration was modified by another user", exception);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException("The office-hours configuration was modified concurrently", exception);
        }

        _context.TeacherOfficeHourAudits.Add(new TeacherOfficeHourAudit
        {
            SchoolId = schoolId,
            TeacherOfficeHourConfigurationId = configuration.Id,
            InstructorProfileId = instructorId,
            ActorUserId = actorUserId,
            Reason = reason,
            BeforeSnapshotJson = JsonSerializer.Serialize(before),
            AfterSnapshotJson = JsonSerializer.Serialize(new
            {
                configuration.InstructorProfileId,
                configuration.AcademicTermId,
                configuration.SchoolTimetableId,
                configuration.TimetableRevision,
                configuration.BellScheduleRevisionId,
                configuration.EffectiveFrom,
                configuration.Source,
                SelectedSlots = configuration.Slots.Select(slot => new
                {
                    slot.StableSlotKey,
                    slot.Day,
                    slot.Period,
                    slot.LocalStartTime,
                    slot.LocalEndTime
                })
            }),
            OccurredAt = now,
            CorrelationId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RecalculateQueuedMessagesAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
        var result = await BuildOfficeHoursAggregateAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
        if (officeHoursTransaction is not null)
            await officeHoursTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<DeliveryDecision> ResolveDeliveryAsync(
        ConversationThread thread,
        string senderUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (thread.ThreadType != ConversationThreadType.GuardianTeacher)
            return new DeliveryDecision(true, OfficeHoursDisposition.SentImmediately, null);

        var participantIds = thread.Participants.Select(participant => participant.ApplicationUserId).ToArray();
        var teacher = await _context.InstructorProfiles.AsNoTracking()
            .Where(profile => profile.SchoolId == thread.SchoolId && profile.IsActive && participantIds.Contains(profile.UserId))
            .Select(profile => new { profile.Id, profile.UserId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The conversation teacher was not found");
        var next = await FindNextEligibleAsync(thread.SchoolId, teacher.Id, now, cancellationToken).ConfigureAwait(false);
        var inside = next.HasValue && next.Value <= now;
        if (senderUserId == teacher.UserId && !inside)
            throw new InvalidOperationException(next.HasValue
                ? $"Teacher routine replies are allowed only during office hours; next eligible instant is {next.Value:O}"
                : "Teacher routine replies are allowed only during office hours; no eligible occurrence is configured");
        return inside
            ? new DeliveryDecision(true, OfficeHoursDisposition.SentImmediately, null)
            : new DeliveryDecision(false, OfficeHoursDisposition.QueuedUntilOfficeHours, next);
    }

    private async Task<DateTimeOffset?> FindNextEligibleAsync(
        int schoolId,
        int instructorId,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var configuration = await _context.TeacherOfficeHourConfigurations.AsNoTracking()
            .Where(item => item.SchoolId == schoolId && item.InstructorProfileId == instructorId
                && item.IsCurrent && !item.IsDeleted)
            .Select(item => new
            {
                item.AcademicTermId,
                item.SchoolTimetableId,
                item.TimetableRevision,
                item.BellScheduleRevisionId,
                item.EffectiveFrom,
                item.EffectiveUntil,
                Slots = item.Slots.Where(slot => slot.IsActive && !slot.IsDeleted && !slot.IsConflicted)
                    .Select(slot => new { slot.Day, slot.Period }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (configuration is null || configuration.Slots.Count == 0) return null;

        var publications = await _schedules.GetPublishedCandidatesAsync(schoolId, from, cancellationToken).ConfigureAwait(false);
        if (publications.Count != 1) return null;
        var publication = publications[0];
        if (publication.SchoolTimetableId != configuration.SchoolTimetableId
            || publication.TimetableRevision != configuration.TimetableRevision
            || publication.Schedule.RevisionId != configuration.BellScheduleRevisionId)
            return null;

        DateTimeOffset localFrom;
        try { localFrom = BellScheduleResolver.LocalTime(publication.Schedule, from); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
        var term = await _context.AcademicTerms.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == configuration.AcademicTermId && item.SchoolId == schoolId && item.IsActive && !item.IsDeleted,
                cancellationToken).ConfigureAwait(false);
        if (term is null) return null;

        var substitutions = await _context.Set<TimetableSubstitutionMovement>().AsNoTracking()
            .Where(movement => movement.SchoolId == schoolId
                && movement.Substitution.SchoolTimetableId == configuration.SchoolTimetableId
                && movement.Substitution.LocalDate >= DateOnly.FromDateTime(localFrom.DateTime)
                && movement.Substitution.LocalDate <= term.EndsOn
                && movement.ToTeacherId == instructorId)
            .Select(movement => new { movement.Substitution.LocalDate, Day = movement.ToDay ?? movement.Day, Period = movement.ToPeriod })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var substitutedBusy = substitutions.Select(item => $"{item.LocalDate:yyyyMMdd}:{item.Day}:{item.Period}").ToHashSet(StringComparer.Ordinal);

        var firstDate = DateOnly.FromDateTime(localFrom.DateTime);
        var lastDate = configuration.EffectiveUntil is { } until && until < term.EndsOn ? until : term.EndsOn;
        if (firstDate < configuration.EffectiveFrom) firstDate = configuration.EffectiveFrom;
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var day = BellScheduleResolver.ToDay(date.DayOfWeek);
            var dayDefinition = publication.Schedule.Days.SingleOrDefault(item => item.Day == (int)day);
            if (dayDefinition is null || !dayDefinition.IsStudyDay) continue;
            var periods = BellScheduleResolver.EffectivePeriods(publication.Schedule, day);
            var breaks = BellScheduleResolver.EffectiveBreaks(publication.Schedule, day);
            foreach (var slot in configuration.Slots.Where(item => item.Day == day && item.Period.HasValue))
            {
                var period = periods.SingleOrDefault(item => item.Sequence == slot.Period);
                if (period is null || breaks.Any(item => item.StartLocalTime < period.EndLocalTime && period.StartLocalTime < item.EndLocalTime)) continue;
                if (substitutedBusy.Contains($"{date:yyyyMMdd}:{(int)day}:{period.Sequence}")) continue;
                var startsAt = LocalBoundary(date, period.StartLocalTime, publication.Schedule.SchoolTimeZoneId);
                var endsAt = LocalBoundary(date, period.EndLocalTime, publication.Schedule.SchoolTimeZoneId);
                if (!startsAt.HasValue || !endsAt.HasValue) continue;
                if (startsAt.Value <= from && from < endsAt.Value) return from;
                if (startsAt.Value > from) return startsAt.Value;
            }
        }
        return null;
    }

    private async Task RecalculateQueuedMessagesAsync(int schoolId, int instructorId, CancellationToken cancellationToken)
    {
        var teacherUserId = await _context.InstructorProfiles.AsNoTracking()
            .Where(profile => profile.Id == instructorId && profile.SchoolId == schoolId)
            .Select(profile => profile.UserId).SingleAsync(cancellationToken).ConfigureAwait(false);
        var messages = await _context.ConversationMessages
            .Where(message => message.SchoolId == schoolId
                && message.OfficeHoursDisposition == OfficeHoursDisposition.QueuedUntilOfficeHours
                && message.Receipts.Any(receipt => receipt.RecipientUserId == teacherUserId
                    && receipt.DeliveryState == MessageDeliveryState.Pending))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var releaseEventIds = messages.Where(message => message.ReleaseOutboxEventId.HasValue)
            .Select(message => message.ReleaseOutboxEventId!.Value).Distinct().ToArray();
        var releaseEvents = releaseEventIds.Length == 0
            ? new Dictionary<Guid, OutboxMessage>()
            : await _context.OutboxMessages
                .Where(item => releaseEventIds.Contains(item.EventId) && item.ProcessedAt == null)
                .ToDictionaryAsync(item => item.EventId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var next = await FindNextEligibleAsync(schoolId, instructorId, now, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            message.NextEligibleSendAt = next;
            OutboxMessage? existing = null;
            if (message.ReleaseOutboxEventId.HasValue
                && releaseEvents.TryGetValue(message.ReleaseOutboxEventId.Value, out existing))
            {
                existing.NextAttemptAt = next;
                if (!next.HasValue) existing.DeadLetteredAt = now;
            }
            if (next.HasValue && (!message.ReleaseOutboxEventId.HasValue
                || existing is null
                || existing.DeadLetteredAt is not null))
            {
                var eventId = Guid.NewGuid();
                message.ReleaseOutboxEventId = eventId;
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    SchoolId = schoolId,
                    EventId = eventId,
                    EventType = typeof(AlFalah.Domain.Events.MessageReleaseDueEvent).FullName!,
                    PayloadJson = JsonSerializer.Serialize(new AlFalah.Domain.Events.MessageReleaseDueEvent(eventId, schoolId, message.Id, now)),
                    OccurredAt = now,
                    NextAttemptAt = next
                });
            }
        }
        if (messages.Count > 0) await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SendMessageResultDto> ToSendResultAsync(
        ConversationMessage message,
        string senderUserId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.ConversationMessages.AsNoTracking()
            .Where(item => item.Id == message.Id)
            .Select(item => new
            {
                Message = item,
                SenderName = (item.SenderUser.FirstName + " " + item.SenderUser.LastName).Trim(),
                DeliveryState = item.Receipts.Count == 0 || item.Receipts.All(receipt => receipt.DeliveryState == MessageDeliveryState.Delivered)
                    ? MessageDeliveryState.Delivered
                    : item.Receipts.Any(receipt => receipt.DeliveryState == MessageDeliveryState.Failed)
                        ? MessageDeliveryState.Failed : MessageDeliveryState.Pending
            })
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        var dto = new ConversationMessageDto(
            projection.Message.Id, projection.Message.ConversationThreadId,
            new ActorSummaryDto(senderUserId, string.IsNullOrWhiteSpace(projection.SenderName) ? "Sender" : projection.SenderName, "Sender"),
            projection.Message.Body, projection.Message.ReplyToMessageId,
            projection.Message.SentAt ?? projection.Message.CreatedAt, projection.DeliveryState,
            projection.Message.OfficeHoursDisposition, projection.Message.NextEligibleSendAt,
            Array.Empty<NotificationDeliveryDto>());
        return new SendMessageResultDto(dto, projection.Message.OfficeHoursDisposition, projection.Message.NextEligibleSendAt);
    }

    private static DateTimeOffset? LocalBoundary(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static string StableSlotKey(PublishedBellScheduleCandidate publication, TimetableDay day, int period) =>
        $"{publication.SchoolTimetableId}:{publication.TimetableRevision}:{publication.Schedule.RevisionId}:{(int)day}:{period}";

    private static string NewConfigurationToken(int instructorId, int termId, PublishedBellScheduleCandidate publication) =>
        $"new:{instructorId}:{termId}:{publication.SchoolTimetableId}:{publication.TimetableRevision}:{publication.Schedule.RevisionId}";

    private static OfficeHoursAggregateDto EmptyOfficeHours(int instructorId, string reason) =>
        new(null, instructorId, 0, 0, 0, 0, default, null, string.Empty,
            TeacherOfficeHourSource.DerivedFromPublishedTimetable, string.Empty, default, reason, Array.Empty<OfficeHourSlotDto>());

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<ConversationThreadType[]> GetAllowedThreadTypesAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _context.UserSchoolRoles.AsNoTracking()
            .Where(assignment => assignment.SchoolId == schoolId && assignment.UserId == userId
                && assignment.IsActive && !assignment.IsDeleted)
            .Select(assignment => assignment.Role.Name!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var allowed = new List<ConversationThreadType>(3);
        if (roles.Contains(RoleNames.Guardian) || roles.Contains(RoleNames.Instructor))
            allowed.Add(ConversationThreadType.GuardianTeacher);
        if (roles.Contains(RoleNames.Guardian) || roles.Contains(RoleNames.StudentAffairsOfficer))
            allowed.Add(ConversationThreadType.GuardianStudentAffairs);
        if (roles.Contains(RoleNames.Guardian) || roles.Contains(RoleNames.SocialWorker))
            allowed.Add(ConversationThreadType.GuardianSocialWorker);
        return allowed.Distinct().ToArray();
    }

    private sealed record DeliveryDecision(bool DeliverNow, OfficeHoursDisposition Disposition, DateTimeOffset? NextEligibleSendAt);

    private static TimetableDay ToTimetableDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => TimetableDay.Sunday,
        DayOfWeek.Monday => TimetableDay.Monday,
        DayOfWeek.Tuesday => TimetableDay.Tuesday,
        DayOfWeek.Wednesday => TimetableDay.Wednesday,
        DayOfWeek.Thursday => TimetableDay.Thursday,
        DayOfWeek.Saturday => TimetableDay.Saturday,
        _ => TimetableDay.Friday
    };

    private static DayOfWeek ToDayOfWeek(TimetableDay day) => day switch
    {
        TimetableDay.Sunday => DayOfWeek.Sunday,
        TimetableDay.Monday => DayOfWeek.Monday,
        TimetableDay.Tuesday => DayOfWeek.Tuesday,
        TimetableDay.Wednesday => DayOfWeek.Wednesday,
        TimetableDay.Thursday => DayOfWeek.Thursday,
        TimetableDay.Saturday => DayOfWeek.Saturday,
        _ => DayOfWeek.Sunday
    };
}
