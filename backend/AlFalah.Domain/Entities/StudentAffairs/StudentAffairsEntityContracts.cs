namespace AlFalah.Domain.Entities.StudentAffairs;

/// <summary>Common persistence contract for mutable Student Affairs rows.</summary>
public interface IStudentAffairsMutableEntity
{
    int Id { get; set; }
    int SchoolId { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    string CreatedByUserId { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    string UpdatedByUserId { get; set; }
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    string? DeletedByUserId { get; set; }
    School School { get; set; }
}

/// <summary>Marker for mutable aggregates protected by SQL Server rowversion.</summary>
public interface IStudentAffairsConcurrentEntity
{
    byte[] RowVersion { get; set; }
}
