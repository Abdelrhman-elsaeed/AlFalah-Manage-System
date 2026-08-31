using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

internal sealed class NoorAbsenceCorrectionBatchConfiguration
    : StudentAffairsMutableEntityConfiguration<NoorAbsenceCorrectionBatch>
{
    protected override string TableName => "NoorAbsenceCorrectionBatches";

    protected override void ConfigureEntity(EntityTypeBuilder<NoorAbsenceCorrectionBatch> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(
            "CK_NoorAbsenceCorrectionBatches_Week", "[WeekEndsOn] >= [WeekStartsOn] AND [RowCount] >= 0"));
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsUnicode(false).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsUnicode(true);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.ExportedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.SchoolId, x.IdempotencyKey }).HasFilter("[IsDeleted] = 0").IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.WeekStartsOn, x.Status });
        builder.HasOne(x => x.ExportedByUser).WithMany().HasForeignKey(x => x.ExportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NoorAbsenceCorrectionBatchItemConfiguration
    : IEntityTypeConfiguration<NoorAbsenceCorrectionBatchItem>
{
    public void Configure(EntityTypeBuilder<NoorAbsenceCorrectionBatchItem> builder)
    {
        builder.ToTable("NoorAbsenceCorrectionBatchItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StudentNameSnapshot).HasMaxLength(300).IsUnicode(true).UseCollation("Arabic_CI_AS").IsRequired();
        builder.Property(x => x.NationalIdSnapshot).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.HasEnumCheckConstraints("NoorAbsenceCorrectionBatchItems");
        builder.HasIndex(x => new { x.SchoolId, x.BatchId, x.DailyStudentAttendanceId }).IsUnique();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Batch).WithMany(x => x.Items)
            .HasForeignKey(x => new { x.SchoolId, x.BatchId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DailyStudentAttendance).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.DailyStudentAttendanceId })
            .HasPrincipalKey(x => new { x.SchoolId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.StudentId }).HasPrincipalKey(x => new { x.SchoolId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
