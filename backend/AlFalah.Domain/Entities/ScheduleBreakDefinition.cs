namespace AlFalah.Domain.Entities;

/// <summary>A named interval in an immutable schedule revision. Each day variation is a separate definition.</summary>
public sealed class ScheduleBreakDefinition
{
    public int Id { get; set; }
    public int BellScheduleDayId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public BellScheduleDay Day { get; set; } = null!;
    public ScheduleBreakWindow Window { get; set; } = null!;
}

/// <summary>Exact local times only. There is deliberately no period identifier or sequence.</summary>
public sealed class ScheduleBreakWindow
{
    public int Id { get; set; }
    public int ScheduleBreakDefinitionId { get; set; }
    public TimeOnly StartLocalTime { get; set; }
    public TimeOnly EndLocalTime { get; set; }
    public ScheduleBreakDefinition Definition { get; set; } = null!;
}
