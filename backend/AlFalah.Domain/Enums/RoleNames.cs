namespace AlFalah.Domain.Enums;

/// <summary>
/// Role name constants used for seeding and reference.
/// Actual roles are database-driven via ApplicationRole.
/// </summary>
public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string MainManager = "MainManager";
    public const string SchoolManager = "SchoolManager";
    public const string Secretary = "Secretary";
    public const string Moderator = "Moderator";
    public const string Instructor = "Instructor";
    public const string Guardian = "Guardian";
    public const string StudentAffairsOfficer = "StudentAffairsOfficer";
    public const string SocialWorker = "SocialWorker";
    public const string SecurityGuard = "SecurityGuard";
}
