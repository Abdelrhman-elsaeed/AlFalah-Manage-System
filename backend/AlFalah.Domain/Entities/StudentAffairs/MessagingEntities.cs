using System.ComponentModel.DataAnnotations;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Entities.StudentAffairs;

public sealed class ConversationThread : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int? StudentId { get; set; }
    public ConversationThreadType ThreadType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public ConversationThreadStatus Status { get; set; } = ConversationThreadStatus.Open;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
    public Student? Student { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}

public sealed class ConversationParticipant : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int ConversationThreadId { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string ParticipantRoleSnapshot { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeftAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ConversationThread ConversationThread { get; set; } = null!;
    public ApplicationUser ApplicationUser { get; set; } = null!;
}

public sealed class ConversationMessage : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int ConversationThreadId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public OfficeHoursDisposition OfficeHoursDisposition { get; set; }
    public int? ReplyToMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ConversationThread ConversationThread { get; set; } = null!;
    public ApplicationUser SenderUser { get; set; } = null!;
    public ConversationMessage? ReplyToMessage { get; set; }
    public ICollection<MessageReceipt> Receipts { get; set; } = new List<MessageReceipt>();
}

/// <summary>Retained delivery/read record for one message recipient.</summary>
public sealed class MessageReceipt
{
    public long Id { get; set; }
    public int SchoolId { get; set; }
    public int ConversationMessageId { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public MessageDeliveryState DeliveryState { get; set; } = MessageDeliveryState.Pending;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public School School { get; set; } = null!;
    public ConversationMessage ConversationMessage { get; set; } = null!;
    public ApplicationUser RecipientUser { get; set; } = null!;
}

public sealed class TeacherOfficeHour : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int InstructorProfileId { get; set; }
    public int AcademicTermId { get; set; }
    public TimetableDay Day { get; set; }
    public byte? Period { get; set; }
    public TimeOnly? LocalStartTime { get; set; }
    public TimeOnly? LocalEndTime { get; set; }
    public TeacherOfficeHourSource Source { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public InstructorProfile InstructorProfile { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
}
