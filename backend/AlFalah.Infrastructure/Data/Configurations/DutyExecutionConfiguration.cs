using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class DutyExecutionConfiguration : IEntityTypeConfiguration<DutyReportDraft>, IEntityTypeConfiguration<DutySignedDocument>,
    IEntityTypeConfiguration<DutyArchiveIntent>, IEntityTypeConfiguration<StudentDismissalLateness>,
    IEntityTypeConfiguration<DismissalLatenessRevision>, IEntityTypeConfiguration<DismissalNoticeBatch>, IEntityTypeConfiguration<DismissalNoticeRecipient>
{
    private static void Scope<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table); b.HasKey("Id"); b.HasAlternateKey("SchoolId", "Id");
        b.HasOne<School>().WithMany().HasForeignKey("SchoolId").OnDelete(DeleteBehavior.NoAction);
    }
    private static void Assignment<T>(EntityTypeBuilder<T> b) where T : class => b.HasOne<DutyAssignment>().WithMany()
        .HasForeignKey("SchoolId", "AssignmentId").HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    public void Configure(EntityTypeBuilder<DutyReportDraft> b)
    {
        Scope(b, "DutyReportDrafts"); Assignment(b);
        b.ToTable("DutyReportDrafts", t => t.UseSqlOutputClause(false));
        b.Property(x => x.AuthorUserId).HasMaxLength(450); b.Property(x => x.CorrectionReason).HasMaxLength(1000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.SchoolId, x.AssignmentId, x.AuthorUserId }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutySignedDocument>().WithMany().HasForeignKey(x => x.PreviousDocumentId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutySignedDocument> b)
    {
        Scope(b, "DutySignedDocuments"); Assignment(b); b.ToTable("DutySignedDocuments", t => t.UseSqlOutputClause(false));
        b.Property(x => x.Kind).HasMaxLength(20); b.Property(x => x.AuthorUserId).HasMaxLength(450);
        b.Property(x => x.TemplateVersion).HasMaxLength(80); b.Property(x => x.CorrectionReason).HasMaxLength(1000);
        b.Property(x => x.SignatureHash).HasMaxLength(64); b.Property(x => x.SnapshotHash).HasMaxLength(64);
        b.HasIndex(x => new { x.SchoolId, x.AssignmentId, x.AssignmentRevision, x.AuthorUserId, x.Kind }).HasFilter("[Kind] = 'Letter'").IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutySignedDocument>().WithMany().HasForeignKey(x => x.PreviousDocumentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<DutyReportDraft>().WithMany().HasForeignKey(x => new { x.SchoolId, x.DraftId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DutyArchiveIntent> b)
    {
        Scope(b, "DutyArchiveIntents"); b.Property(x => x.State).HasMaxLength(40); b.HasIndex(x => x.DocumentId).IsUnique();
        b.HasOne<DutySignedDocument>().WithMany().HasForeignKey(x => new { x.SchoolId, x.DocumentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("DutyArchiveIntents", t => t.UseSqlOutputClause(false));
        b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.OperationIdentity).HasMaxLength(200);
        b.HasIndex(x => x.OperationIdentity).IsUnique().HasFilter("[OperationIdentity] IS NOT NULL");
        b.Property(x => x.ProviderItemId).HasMaxLength(256); b.Property(x => x.DriveId).HasMaxLength(256);
        b.Property(x => x.SchoolRootItemId).HasMaxLength(256); b.Property(x => x.FolderItemId).HasMaxLength(256);
        b.Property(x => x.ErrorCode).HasMaxLength(80); b.HasIndex(x => new { x.State, x.NextAttemptAt });
        b.HasOne<DutyPdfArtifact>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ArtifactId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AlFalah.Domain.Entities.Storage.StorageFolder>().WithMany().HasForeignKey(x => new { x.SchoolId, x.FolderId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AlFalah.Domain.Entities.Storage.StoredFileVersion>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StoredFileVersionId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<StudentDismissalLateness> b)
    {
        Scope(b, "StudentDismissalLateness"); Assignment(b); b.Property(x => x.Revision).IsConcurrencyToken();
        b.ToTable("StudentDismissalLateness", t => t.UseSqlOutputClause(false));
        // One stable identity, including after retraction. Reactivation is a reasoned revision.
        b.HasIndex(x => new { x.SchoolId, x.StudentId, x.DutyDate }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.DutyDate, x.IsRetracted, x.ClassroomId });
        b.Property(x => x.AuthorUserId).HasMaxLength(450); b.Property(x => x.StudentName).HasMaxLength(300);
        b.Property(x => x.Classroom).HasMaxLength(200); b.Property(x => x.TimeZoneId).HasMaxLength(100);
        b.Property(x => x.Action).HasMaxLength(1000); b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne<Student>().WithMany().HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x => new { x.SchoolId, x.EnrollmentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Classroom>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ClassroomId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.SchoolId, x.AcademicTermId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DismissalLatenessRevision> b)
    {
        Scope(b, "DismissalLatenessRevisions"); b.ToTable("DismissalLatenessRevisions", t => t.UseSqlOutputClause(false));
        b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.ActorUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.FactId, x.Revision }).IsUnique(); Fact(b);
    }
    private static void Fact<T>(EntityTypeBuilder<T> b) where T : class => b.HasOne<StudentDismissalLateness>().WithMany()
        .HasForeignKey("SchoolId", "FactId").HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
    public void Configure(EntityTypeBuilder<DismissalNoticeBatch> b)
    {
        Scope(b, "DismissalNoticeBatches"); Fact(b); b.Property(x => x.State).HasMaxLength(40);
        b.HasIndex(x => new { x.FactId, x.Revision }).IsUnique(); b.HasIndex(x => new { x.State, x.NextAttemptAt });
    }
    public void Configure(EntityTypeBuilder<DismissalNoticeRecipient> b)
    {
        Scope(b, "DismissalNoticeRecipients"); b.Property(x => x.RecipientUserId).HasMaxLength(450);
        b.ToTable("DismissalNoticeRecipients", t => t.UseSqlOutputClause(false));
        b.Property(x => x.DashboardState).HasMaxLength(40); b.Property(x => x.MessageState).HasMaxLength(40); b.Property(x => x.ErrorCode).HasMaxLength(80);
        b.HasIndex(x => new { x.BatchId, x.RecipientUserId }).IsUnique(); b.HasIndex(x => x.CorrelationId).IsUnique();
        b.HasOne<DismissalNoticeBatch>().WithMany().HasForeignKey(x => new { x.SchoolId, x.BatchId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ConversationMessage>().WithMany().HasForeignKey(x => new { x.SchoolId, x.MessageId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ConversationThread>().WithMany().HasForeignKey(x => new { x.SchoolId, x.ThreadId }).HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.NoAction);
    }
}
