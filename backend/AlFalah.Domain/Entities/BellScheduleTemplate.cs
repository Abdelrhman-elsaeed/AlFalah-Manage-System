using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

public sealed class BellScheduleTemplate
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public TimetableSemester Semester { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public ICollection<BellScheduleRevision> Revisions { get; set; } = new List<BellScheduleRevision>();
}

/// <summary>Append-only: published timetables keep this exact revision.</summary>
public sealed class BellScheduleRevision
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int BellScheduleTemplateId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchoolTimeZoneId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public BellScheduleTemplate Template { get; set; } = null!;
    public ICollection<BellScheduleDay> Days { get; set; } = new List<BellScheduleDay>();
}

public sealed class BellScheduleDay
{
    public int Id { get; set; }
    public int BellScheduleRevisionId { get; set; }
    /// <summary>0 owns the default schedule; 1–7 are TimetableDay values.</summary>
    public int Day { get; set; }
    public bool IsStudyDay { get; set; }
    public bool UsesDefaultSchedule { get; set; }
    public bool UsesDefaultBreaks { get; set; } = true;
    public BellScheduleRevision Revision { get; set; } = null!;
    public ICollection<BellPeriod> Periods { get; set; } = new List<BellPeriod>();
    public ICollection<ScheduleBreakDefinition> Breaks { get; set; } = new List<ScheduleBreakDefinition>();
}

public sealed class BellPeriod
{
    public int Id { get; set; }
    public int BellScheduleDayId { get; set; }
    public int Sequence { get; set; }
    public string? DisplayLabel { get; set; }
    public TimeOnly StartLocalTime { get; set; }
    public TimeOnly EndLocalTime { get; set; }
    public BellScheduleDay Day { get; set; } = null!;
}
