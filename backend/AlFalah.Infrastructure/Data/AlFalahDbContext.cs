using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Data;

/// <summary>
/// Main EF Core DbContext. Extends IdentityDbContext to manage ASP.NET Core Identity tables
/// alongside Al-Falah domain entities.
/// Global soft-delete query filters are applied where applicable.
/// </summary>
public class AlFalahDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public AlFalahDbContext(DbContextOptions<AlFalahDbContext> options) : base(options) { }

    // Identity extensions
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSchoolRole> UserSchoolRoles => Set<UserSchoolRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Organization
    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolLocation> SchoolLocations => Set<SchoolLocation>();
    public DbSet<SchoolReportSettings> SchoolReportSettings => Set<SchoolReportSettings>();
    public DbSet<SchoolGoogleDrive> SchoolGoogleDrives => Set<SchoolGoogleDrive>();
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();

    // People
    public DbSet<InstructorProfile> InstructorProfiles => Set<InstructorProfile>();
    // D-74 — Teacher's class labels (auto-fill source for the visit form).
    public DbSet<InstructorClass> InstructorClasses => Set<InstructorClass>();
    public DbSet<TeacherDriveFolder> TeacherDriveFolders => Set<TeacherDriveFolder>();
    public DbSet<TeacherEvidenceSubmission> TeacherEvidenceSubmissions => Set<TeacherEvidenceSubmission>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<EvidenceTask> EvidenceTasks => Set<EvidenceTask>();
    public DbSet<TeacherTaskStatus> TeacherTaskStatuses => Set<TeacherTaskStatus>();
    public DbSet<EvidenceUploadOperation> EvidenceUploadOperations => Set<EvidenceUploadOperation>();

    // System
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Rubric (Phase 3)
    public DbSet<RubricVersion> RubricVersions => Set<RubricVersion>();
    public DbSet<RubricDomain> RubricDomains => Set<RubricDomain>();
    public DbSet<RubricStandard> RubricStandards => Set<RubricStandard>();

    // Visits (Phase 4)
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitScore> VisitScores => Set<VisitScore>();
    public DbSet<VisitAnalysis> VisitAnalyses => Set<VisitAnalysis>();
    public DbSet<VisitDomainAverage> VisitDomainAverages => Set<VisitDomainAverage>();

    // Visits (Phase 5) — instructor report-view tracking
    public DbSet<ReportViewLog> ReportViewLogs => Set<ReportViewLog>();

    // Phase 7: Improvement Plans & Follow-ups
    public DbSet<ImprovementPlan> ImprovementPlans => Set<ImprovementPlan>();
    public DbSet<PlanFollowUp> PlanFollowUps => Set<PlanFollowUp>();

    // Phase 8: Complaints
    public DbSet<Complaint> Complaints => Set<Complaint>();

    // Attendance
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // School timetables
    public DbSet<SchoolTimetable> SchoolTimetables => Set<SchoolTimetable>();
    public DbSet<SchoolTimetableEntry> SchoolTimetableEntries => Set<SchoolTimetableEntry>();
    public DbSet<SchoolTimetableVersion> SchoolTimetableVersions => Set<SchoolTimetableVersion>();
    public DbSet<TimetableEditorGrant> TimetableEditorGrants => Set<TimetableEditorGrant>();

    // Parent surveys
    public DbSet<ParentSurvey> ParentSurveys => Set<ParentSurvey>();
    public DbSet<ParentSurveyItem> ParentSurveyItems => Set<ParentSurveyItem>();
    public DbSet<ParentSurveySubmission> ParentSurveySubmissions => Set<ParentSurveySubmission>();
    public DbSet<ParentSurveyAnswer> ParentSurveyAnswers => Set<ParentSurveyAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity type configurations from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AlFalahDbContext).Assembly);

        // Rename Identity tables to use AlFalah prefix
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");

        // ─── Global soft-delete query filters (Phase 2) ──────────────────────
        // Soft-deleted rows are excluded from all queries by default. Services that
        // need to see deleted rows must use IgnoreQueryFilters().
        builder.Entity<School>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<SchoolLocation>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ApplicationUser>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<UserSchoolRole>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<InstructorProfile>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<InstructorClass>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<SchoolTimetable>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<SchoolTimetableEntry>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<TimetableEditorGrant>().HasQueryFilter(x => !x.IsDeleted);

        // Rubric soft-delete filters (Phase 3)
        builder.Entity<RubricVersion>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<RubricDomain>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<RubricStandard>().HasQueryFilter(x => !x.IsDeleted);

        // Visits soft-delete filters (Phase 4)
        builder.Entity<Visit>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<VisitScore>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<VisitAnalysis>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<VisitDomainAverage>().HasQueryFilter(x => !x.IsDeleted);

        // Visits (Phase 5) — report view tracking.
        builder.Entity<ReportViewLog>().HasQueryFilter(x => !x.IsDeleted);

        // Phase 7: Improvement Plans & Follow-ups soft-delete filters
        builder.Entity<ImprovementPlan>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<PlanFollowUp>().HasQueryFilter(x => !x.IsDeleted);

        // Parent surveys and reusable templates
        builder.Entity<ParentSurvey>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ParentSurveyItem>().HasQueryFilter(x => !x.IsDeleted);
    }

    /// <summary>
    /// Automatically update UpdatedAt on SaveChanges for entities that track it.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case School s: s.UpdatedAt = now; break;
                case SchoolLocation sl: sl.UpdatedAt = now; break;
                case ApplicationUser u: u.UpdatedAt = now; break;
                case InstructorProfile i: i.UpdatedAt = now; break;
                case InstructorClass ic: ic.UpdatedAt = now; break;
                case SchoolGoogleDrive sgd: sgd.UpdatedAtUtc = now; break;
                case TeacherDriveFolder df: df.UpdatedAtUtc = now; break;
                case TeacherEvidenceSubmission es: es.UpdatedAtUtc = now; break;
                case TeacherTaskStatus tts: tts.UpdatedAtUtc = now; break;
                case EvidenceUploadOperation operation: operation.UpdatedAtUtc = now; break;
                case UserSchoolRole usr: usr.UpdatedAt = now; break;
                case Visit v: v.UpdatedAt = now; break;
                case VisitScore vs: vs.UpdatedAt = now; break;
                case ImprovementPlan ip: ip.UpdatedAt = now; break;
                case PlanFollowUp pfu: pfu.UpdatedAt = now; break;
                case ParentSurvey ps: ps.UpdatedAt = now; break;
                case SchoolTimetable timetable: timetable.UpdatedAt = now; break;
                case SchoolTimetableEntry timetableEntry: timetableEntry.UpdatedAt = now; break;
            }
        }
    }
}
