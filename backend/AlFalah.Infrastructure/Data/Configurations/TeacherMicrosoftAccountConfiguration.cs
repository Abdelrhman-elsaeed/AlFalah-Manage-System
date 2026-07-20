using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeacherMicrosoftAccountConfiguration : IEntityTypeConfiguration<TeacherMicrosoftAccount>
{
    public void Configure(EntityTypeBuilder<TeacherMicrosoftAccount> builder)
    {
        builder.ToTable("TeacherMicrosoftAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(64);
        builder.Property(x => x.ObjectId).HasMaxLength(64);
        builder.Property(x => x.MicrosoftEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedMicrosoftEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.TeacherId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ObjectId }).IsUnique();
        builder.HasIndex(x => x.NormalizedMicrosoftEmail);
        builder.HasOne(x => x.Teacher).WithOne().HasForeignKey<TeacherMicrosoftAccount>(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
    }
}
