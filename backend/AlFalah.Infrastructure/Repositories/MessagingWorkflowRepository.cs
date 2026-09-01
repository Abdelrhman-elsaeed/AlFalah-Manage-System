using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Messaging;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class MessagingWorkflowRepository : IMessagingWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public MessagingWorkflowRepository(AlFalahDbContext context)
    {
        _context = context;
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
                && m.Receipts.Any(r => r.RecipientUserId == userId && r.ReadAt == null)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(ct => ct.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ct => new
            {
                ct.Id,
                ct.StudentId,
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
                    && m.Receipts.Any(r => r.RecipientUserId == userId && r.ReadAt == null))
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
                Convert.ToBase64String(p.RowVersion));
        }).ToList();

        return new PagedResult<ConversationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ConversationDto?> GetConversationByIdAsync(
        int schoolId,
        string userId,
        int conversationId,
        CancellationToken cancellationToken)
    {
        var projection = await _context.ConversationThreads
            .AsNoTracking()
            .Where(ct => ct.Id == conversationId
                && ct.SchoolId == schoolId
                && ct.Participants.Any(p => p.ApplicationUserId == userId && !p.IsDeleted))
            .Select(ct => new
            {
                ct.Id,
                ct.StudentId,
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
                    && m.Receipts.Any(r => r.RecipientUserId == userId && r.ReadAt == null))
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
            Convert.ToBase64String(projection.RowVersion));
    }

    public async Task<PagedResult<ConversationMessageDto>> GetConversationMessagesAsync(
        int schoolId,
        string userId,
        int conversationId,
        ConversationMessageQuery query,
        CancellationToken cancellationToken)
    {
        var isParticipant = await _context.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(p => p.ConversationThreadId == conversationId
                && p.SchoolId == schoolId
                && p.ApplicationUserId == userId
                && !p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

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
                && !m.IsDeleted);

        if (query.BeforeMessageId.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.Id < query.BeforeMessageId.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderBy(m => m.SentAt ?? m.QueuedAt)
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
                r.ReadAt.HasValue ? NotificationDeliveryStatus.Delivered : NotificationDeliveryStatus.Pending,
                r.DeliveredAt,
                r.ReadAt)).ToList();

            var deliveryState = p.Receipts.Any(r => r.ReadAt.HasValue)
                ? MessageDeliveryState.Delivered
                : (p.Receipts.FirstOrDefault()?.DeliveryState ?? MessageDeliveryState.Delivered);

            return new ConversationMessageDto(
                p.Id,
                p.ConversationThreadId,
                sender,
                p.Body,
                p.ReplyToMessageId,
                p.CreatedAt,
                deliveryState,
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
        var now = DateTimeOffset.UtcNow;
        var thread = new ConversationThread
        {
            SchoolId = schoolId,
            StudentId = request.StudentId > 0 ? request.StudentId : null,
            ThreadType = request.ThreadType,
            Subject = request.Subject.Trim(),
            Status = ConversationThreadStatus.Open,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedByUserId = creatorUserId,
            UpdatedAt = now
        };

        // Add creator participant
        thread.Participants.Add(new ConversationParticipant
        {
            SchoolId = schoolId,
            ApplicationUserId = creatorUserId,
            ParticipantRoleSnapshot = "Creator",
            JoinedAt = now,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedByUserId = creatorUserId,
            UpdatedAt = now
        });

        // Add target participant if specified
        string? targetUserId = request.TargetStaffUserId;
        if (string.IsNullOrWhiteSpace(targetUserId) && request.TargetInstructorProfileId.HasValue)
        {
            var instructor = await _context.InstructorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.TargetInstructorProfileId.Value && i.SchoolId == schoolId, cancellationToken)
                .ConfigureAwait(false);
            targetUserId = instructor?.UserId;
        }

        if (!string.IsNullOrWhiteSpace(targetUserId) && targetUserId != creatorUserId)
        {
            thread.Participants.Add(new ConversationParticipant
            {
                SchoolId = schoolId,
                ApplicationUserId = targetUserId,
                ParticipantRoleSnapshot = request.TargetStaffRole ?? RoleNames.Instructor,
                JoinedAt = now,
                CreatedByUserId = creatorUserId,
                CreatedAt = now,
                UpdatedByUserId = creatorUserId,
                UpdatedAt = now
            });
        }

        if (!string.IsNullOrWhiteSpace(request.InitialBody))
        {
            var message = new ConversationMessage
            {
                SchoolId = schoolId,
                SenderUserId = creatorUserId,
                Body = request.InitialBody.Trim(),
                SentAt = now,
                QueuedAt = now,
                OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
                CreatedByUserId = creatorUserId,
                CreatedAt = now,
                UpdatedByUserId = creatorUserId,
                UpdatedAt = now
            };

            if (!string.IsNullOrWhiteSpace(targetUserId) && targetUserId != creatorUserId)
            {
                message.Receipts.Add(new MessageReceipt
                {
                    SchoolId = schoolId,
                    RecipientUserId = targetUserId,
                    DeliveryState = MessageDeliveryState.Pending,
                    DeliveredAt = now,
                    CreatedAt = now
                });
            }

            thread.Messages.Add(message);
        }

        _context.ConversationThreads.Add(thread);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await GetConversationByIdAsync(schoolId, creatorUserId, thread.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<SendMessageResultDto> SendMessageAsync(
        int schoolId,
        string senderUserId,
        int conversationId,
        SendMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        var thread = await _context.ConversationThreads
            .Include(ct => ct.Participants.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(ct => ct.Id == conversationId && ct.SchoolId == schoolId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Conversation was not found");

        var now = DateTimeOffset.UtcNow;
        var message = new ConversationMessage
        {
            SchoolId = schoolId,
            ConversationThreadId = conversationId,
            SenderUserId = senderUserId,
            Body = request.Body.Trim(),
            ReplyToMessageId = request.ReplyToMessageId.HasValue ? (int?)request.ReplyToMessageId.Value : null,
            SentAt = now,
            QueuedAt = now,
            OfficeHoursDisposition = OfficeHoursDisposition.SentImmediately,
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
                DeliveryState = MessageDeliveryState.Pending,
                DeliveredAt = now,
                CreatedAt = now
            });
        }

        thread.UpdatedAt = now;
        thread.UpdatedByUserId = senderUserId;

        _context.ConversationMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var senderUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == senderUserId, cancellationToken)
            .ConfigureAwait(false);

        var senderName = senderUser != null ? $"{senderUser.FirstName} {senderUser.LastName}".Trim() : "Sender";
        var senderSummary = new ActorSummaryDto(senderUserId, senderName, "Sender");

        var messageDto = new ConversationMessageDto(
            message.Id,
            conversationId,
            senderSummary,
            message.Body,
            message.ReplyToMessageId,
            message.SentAt ?? message.CreatedAt,
            MessageDeliveryState.Delivered,
            Array.Empty<NotificationDeliveryDto>());

        return new SendMessageResultDto(messageDto, OfficeHoursDisposition.SentImmediately, null);
    }

    public async Task<bool> MarkConversationReadAsync(
        int schoolId,
        string userId,
        int conversationId,
        long throughMessageId,
        CancellationToken cancellationToken)
    {
        var unreadReceipts = await _context.MessageReceipts
            .Where(r => r.SchoolId == schoolId
                && r.RecipientUserId == userId
                && r.ConversationMessage.ConversationThreadId == conversationId
                && r.ConversationMessage.Id <= throughMessageId
                && r.ReadAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unreadReceipts.Count == 0) return true;

        var now = DateTimeOffset.UtcNow;
        foreach (var receipt in unreadReceipts)
        {
            receipt.DeliveryState = MessageDeliveryState.Delivered;
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
            .FirstOrDefaultAsync(ct => ct.Id == conversationId && ct.SchoolId == schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (thread is null) return null;

        thread.Status = ConversationThreadStatus.Closed;
        thread.UpdatedByUserId = userId;
        thread.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetConversationByIdAsync(schoolId, userId, conversationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OfficeHourSlotDto>> GetEligibleOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken)
    {
        return await GetMyOfficeHoursAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OfficeHourSlotDto>> GetMyOfficeHoursAsync(
        int schoolId,
        string userId,
        CancellationToken cancellationToken)
    {
        var slots = await _context.TeacherOfficeHours
            .AsNoTracking()
            .Where(oh => oh.SchoolId == schoolId
                && oh.InstructorProfile.UserId == userId
                && oh.IsActive
                && !oh.IsDeleted)
            .OrderBy(oh => oh.Day)
            .ThenBy(oh => oh.LocalStartTime)
            .Select(oh => new
            {
                oh.Id,
                oh.Day,
                oh.LocalStartTime,
                oh.LocalEndTime,
                oh.EffectiveFrom,
                oh.EffectiveUntil,
                oh.Source,
                oh.IsActive
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return slots.Select(s => new OfficeHourSlotDto(
            s.Id,
            ToDayOfWeek(s.Day),
            s.LocalStartTime ?? new TimeOnly(8, 0),
            s.LocalEndTime ?? new TimeOnly(14, 0),
            s.EffectiveFrom,
            s.EffectiveUntil,
            s.Source,
            s.IsActive,
            string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<OfficeHourSlotDto>> UpdateMyOfficeHoursAsync(
        int schoolId,
        string userId,
        UpdateMyOfficeHoursRequestDto request,
        CancellationToken cancellationToken)
    {
        return await GetMyOfficeHoursAsync(schoolId, userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OfficeHourSlotDto>> GetTeacherOfficeHoursAsync(
        int schoolId,
        int instructorId,
        CancellationToken cancellationToken)
    {
        var slots = await _context.TeacherOfficeHours
            .AsNoTracking()
            .Where(oh => oh.SchoolId == schoolId
                && oh.InstructorProfileId == instructorId
                && oh.IsActive
                && !oh.IsDeleted)
            .OrderBy(oh => oh.Day)
            .ThenBy(oh => oh.LocalStartTime)
            .Select(oh => new
            {
                oh.Id,
                oh.Day,
                oh.LocalStartTime,
                oh.LocalEndTime,
                oh.EffectiveFrom,
                oh.EffectiveUntil,
                oh.Source,
                oh.IsActive
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return slots.Select(s => new OfficeHourSlotDto(
            s.Id,
            ToDayOfWeek(s.Day),
            s.LocalStartTime ?? new TimeOnly(8, 0),
            s.LocalEndTime ?? new TimeOnly(14, 0),
            s.EffectiveFrom,
            s.EffectiveUntil,
            s.Source,
            s.IsActive,
            string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<OfficeHourSlotDto>> OverrideTeacherOfficeHoursAsync(
        int schoolId,
        string adminUserId,
        int instructorId,
        OverrideTeacherOfficeHoursRequestDto request,
        CancellationToken cancellationToken)
    {
        return await GetTeacherOfficeHoursAsync(schoolId, instructorId, cancellationToken).ConfigureAwait(false);
    }

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
