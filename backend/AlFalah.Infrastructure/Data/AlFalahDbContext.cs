using System.Text.Json;
using System.Text.Json.Serialization;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Events;
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
    private static readonly JsonSerializerOptions DomainEventJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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

    // Student analyzer
    public DbSet<StudentAnalyzerAccessGrant> StudentAnalyzerAccessGrants => Set<StudentAnalyzerAccessGrant>();
    public DbSet<SchoolStudentAnalyzerSettings> SchoolStudentAnalyzerSettings => Set<SchoolStudentAnalyzerSettings>();
    public DbSet<StudentAnalyzerSourceFile> StudentAnalyzerSourceFiles => Set<StudentAnalyzerSourceFile>();
    public DbSet<StudentAnalyzerReport> StudentAnalyzerReports => Set<StudentAnalyzerReport>();

    // Student Affairs - academic foundation
    public DbSet<Student> Students => Set<Student>();
    public DbSet<GuardianProfile> GuardianProfiles => Set<GuardianProfile>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<SchoolStudentAffairsSettings> SchoolStudentAffairsSettings => Set<SchoolStudentAffairsSettings>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();

    // Student Affairs - attendance and conduct
    public DbSet<DailyStudentAttendance> DailyStudentAttendances => Set<DailyStudentAttendance>();
    public DbSet<AbsenceExcuse> AbsenceExcuses => Set<AbsenceExcuse>();
    public DbSet<AbsenceExcuseAttachment> AbsenceExcuseAttachments => Set<AbsenceExcuseAttachment>();
    public DbSet<MorningArrivalDelay> MorningArrivalDelays => Set<MorningArrivalDelay>();
    public DbSet<SessionDelay> SessionDelays => Set<SessionDelay>();
    public DbSet<AcademicConcern> AcademicConcerns => Set<AcademicConcern>();
    public DbSet<BehaviorIncident> BehaviorIncidents => Set<BehaviorIncident>();
    public DbSet<StudentRecognition> StudentRecognitions => Set<StudentRecognition>();
    public DbSet<NoorAbsenceCorrectionBatch> NoorAbsenceCorrectionBatches => Set<NoorAbsenceCorrectionBatch>();
    public DbSet<NoorAbsenceCorrectionBatchItem> NoorAbsenceCorrectionBatchItems => Set<NoorAbsenceCorrectionBatchItem>();

    // Student Affairs - workflows
    public DbSet<ClassroomEntryPermit> ClassroomEntryPermits => Set<ClassroomEntryPermit>();
    public DbSet<GatePass> GatePasses => Set<GatePass>();
    public DbSet<GatePassTransition> GatePassTransitions => Set<GatePassTransition>();
    public DbSet<StudentReferral> StudentReferrals => Set<StudentReferral>();
    public DbSet<GuardianSummon> GuardianSummons => Set<GuardianSummon>();
    public DbSet<GuardianSummonStatusHistory> GuardianSummonStatusHistories => Set<GuardianSummonStatusHistory>();
    public DbSet<StudentCaseAction> StudentCaseActions => Set<StudentCaseAction>();

    // Student Affairs - messaging and office hours
    public DbSet<ConversationThread> ConversationThreads => Set<ConversationThread>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<MessageReceipt> MessageReceipts => Set<MessageReceipt>();
    public DbSet<TeacherOfficeHour> TeacherOfficeHours => Set<TeacherOfficeHour>();

    // Student Affairs - automation and reliable events
    public DbSet<AutomationRuleDefinition> AutomationRuleDefinitions => Set<AutomationRuleDefinition>();
    public DbSet<StudentTermMetric> StudentTermMetrics => Set<StudentTermMetric>();
    public DbSet<AutomationTriggerLedger> AutomationTriggerLedgers => Set<AutomationTriggerLedger>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

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
        builder.Entity<StudentAnalyzerAccessGrant>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<StudentAnalyzerSourceFile>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<StudentAnalyzerReport>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Notification>().HasQueryFilter(x => !x.IsDeleted);

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
        var sources = GetDomainEventSources();
        if (sources.Count == 0) return base.SaveChanges();

        var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction is null;
        using var transaction = ownsTransaction ? Database.BeginTransaction() : null;
        try
        {
            var affectedRows = base.SaveChanges();
            AppendOutboxMessages(sources);
            affectedRows += base.SaveChanges();
            transaction?.Commit();
            ClearDomainEvents(sources);
            return affectedRows;
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        var sources = GetDomainEventSources();
        if (sources.Count == 0)
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            var affectedRows = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            AppendOutboxMessages(sources);
            affectedRows += await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            ClearDomainEvents(sources);
            return affectedRows;
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private List<IHasDomainEvents> GetDomainEventSources() => ChangeTracker.Entries()
        .Select(entry => entry.Entity)
        .OfType<IHasDomainEvents>()
        .Where(source => source.DomainEvents.Count > 0)
        .ToList();

    private void AppendOutboxMessages(IEnumerable<IHasDomainEvents> sources)
    {
        var messages = sources.SelectMany(source => source.DomainEvents.Select(domainEvent =>
        {
            var eventWithAggregateId = domainEvent.WithAggregateId(source.DomainEventAggregateId);
            return new OutboxMessage
            {
                SchoolId = eventWithAggregateId.SchoolId,
                EventId = eventWithAggregateId.EventId,
                EventType = eventWithAggregateId.GetType().FullName
                    ?? eventWithAggregateId.GetType().Name,
                PayloadJson = JsonSerializer.Serialize(
                    eventWithAggregateId,
                    eventWithAggregateId.GetType(),
                    DomainEventJsonOptions),
                OccurredAt = eventWithAggregateId.OccurredAt
            };
        })).ToList();

        OutboxMessages.AddRange(messages);
    }

    private static void ClearDomainEvents(IEnumerable<IHasDomainEvents> sources)
    {
        foreach (var source in sources) source.ClearDomainEvents();
    }

    private void UpdateTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is IStudentAffairsMutableEntity studentAffairsEntity)
                studentAffairsEntity.UpdatedAt = now;

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
                case SchoolStudentAnalyzerSettings analyzerSettings: analyzerSettings.UpdatedAt = now; break;
                case Notification notification: notification.UpdatedAt = now; break;
            }
        }
    }
}
