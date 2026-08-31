using System.ComponentModel.DataAnnotations;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Domain.Entities.StudentAffairs;

public sealed class Student : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public StudentGender? Gender { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ProfilePhotoStorageKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ICollection<StudentGuardian> Guardians { get; set; } = new List<StudentGuardian>();
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}

public sealed class GuardianProfile : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public PreferredContactLanguage PreferredContactLanguage { get; set; } = PreferredContactLanguage.Arabic;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public ApplicationUser ApplicationUser { get; set; } = null!;
    public ICollection<StudentGuardian> Students { get; set; } = new List<StudentGuardian>();
}

public sealed class StudentGuardian : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int GuardianProfileId { get; set; }
    public GuardianRelationshipType RelationshipType { get; set; }
    public bool IsPrimary { get; set; }
    public bool ReceivesNotifications { get; set; } = true;
    public bool CanSubmitExcuses { get; set; } = true;
    public bool CanRequestGatePass { get; set; } = true;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public GuardianProfile GuardianProfile { get; set; } = null!;
}

public sealed class AcademicTerm : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public TimetableSemester Semester { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
}

public sealed class SchoolStudentAffairsSettings : IStudentAffairsMutableEntity, IStudentAffairsConcurrentEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int MorningDelayThresholdPerTerm { get; set; } = 10;
    public int BehaviorIncidentMultiplePerTerm { get; set; } = 10;
    public int AcademicConcernThresholdPerTerm { get; set; } = 3;
    public int ClassroomEntryPermitThresholdPerTerm { get; set; } = 5;
    public int AbsenceVisualAlertThresholdPerTerm { get; set; } = 3;
    public int AbsenceReferralThresholdPerTerm { get; set; } = 5;
    public int AbsenceChildRightsThresholdPerTerm { get; set; } = 10;
    public string BehaviorCountabilityPolicy { get; set; } = string.Empty;
    public TimeOnly ArrivalCutoffLocalTime { get; set; }
    public int ArrivalGraceMinutes { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public School School { get; set; } = null!;
}

public sealed class Classroom : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int AcademicYearId { get; set; }
    public SchoolStage Stage { get; set; }
    public byte GradeLevel { get; set; }
    public string Section { get; set; } = string.Empty;
    public string ClassLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}

public sealed class StudentEnrollment : IStudentAffairsMutableEntity
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public int StudentId { get; set; }
    public int ClassroomId { get; set; }
    public int AcademicTermId { get; set; }
    public int? RollNumber { get; set; }
    public DateOnly EnrolledOn { get; set; }
    public DateOnly? WithdrawnOn { get; set; }
    public StudentEnrollmentStatus Status { get; set; } = StudentEnrollmentStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Classroom Classroom { get; set; } = null!;
    public AcademicTerm AcademicTerm { get; set; } = null!;
}
