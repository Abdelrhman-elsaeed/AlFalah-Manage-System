using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TimetableAnalysisConfiguration : IEntityTypeConfiguration<TimetableAnalysisRun>, IEntityTypeConfiguration<TimetableAnalysisFinding>
{
    public void Configure(EntityTypeBuilder<TimetableAnalysisRun> b)
    {
        b.ToTable("TimetableAnalysisRuns"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SchoolTimetableId, x.TimetableRevision });
        b.Property(x => x.AnalyzerVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        b.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SchoolTimetable).WithMany().HasForeignKey(x => x.SchoolTimetableId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BellScheduleRevision).WithMany().HasForeignKey(x => x.BellScheduleRevisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<TimetableAnalysisFinding> b)
    {
        b.ToTable("TimetableAnalysisFindings"); b.HasKey(x => x.Id);
        b.Property(x => x.MessageAr).HasMaxLength(1000).IsRequired();
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.Property(x => x.OverriddenByUserId).HasMaxLength(450);
        b.Property(x => x.IsOverridden).IsConcurrencyToken();
        b.HasOne(x => x.AnalysisRun).WithMany(x => x.Findings).HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Classroom).WithMany().HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InstructorProfile).WithMany().HasForeignKey(x => x.InstructorProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OverriddenByUser).WithMany().HasForeignKey(x => x.OverriddenByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
