using System.ComponentModel.DataAnnotations.Schema;
using AlFalah.Domain.Events;

namespace AlFalah.Domain.Entities;

/// <summary>Immutable confirmed change; daily cover movements are effective on LocalDate only.</summary>
public sealed class TimetableSubstitution : IHasDomainEvents
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int SchoolTimetableId { get; set; }
    public Guid RequestId { get; set; }
    public string ProposalId { get; set; } = "";
    public string Kind { get; set; } = "Substitution";
    public DateOnly LocalDate { get; set; }
    public int BeforeRevision { get; set; }
    public int AfterRevision { get; set; }
    public string RequestedByUserId { get; set; } = "";
    public string ApprovedByUserId { get; set; } = "";
    public string? OverrideReason { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
    public int SchoolTimetableVersionId { get; set; }
    public SchoolTimetableVersion Version { get; set; } = null!;
    public SchoolTimetable Timetable { get; set; } = null!;
    public ICollection<TimetableSubstitutionMovement> Movements { get; set; } = [];
    private readonly List<IDomainEvent> events = [];
    [NotMapped] public int DomainEventAggregateId => Id;
    [NotMapped] public IReadOnlyCollection<IDomainEvent> DomainEvents => events;
    public void AppendDomainEvent(IDomainEvent domainEvent) => events.Add(domainEvent);
    public void ClearDomainEvents() => events.Clear();
}

public sealed class TimetableSubstitutionMovement
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int TimetableSubstitutionId { get; set; }
    public int SchoolTimetableEntryId { get; set; }
    public int FromTeacherId { get; set; }
    public int ToTeacherId { get; set; }
    public int Day { get; set; }
    public int? ToDay { get; set; }
    public int FromPeriod { get; set; }
    public int ToPeriod { get; set; }
    public TimetableSubstitution Substitution { get; set; } = null!;
}
