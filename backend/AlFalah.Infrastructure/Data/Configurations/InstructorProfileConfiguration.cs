using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class InstructorProfileConfiguration : IEntityTypeConfiguration<InstructorProfile>
{
    public void Configure(EntityTypeBuilder<InstructorProfile> builder)
    {
        builder.HasKey(x => x.Id);
        // D-31: explicit Unicode on every Arabic-text column + Arabic_CI_AS collation.
        builder.Property(x => x.SubjectSpecialization).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.QualificationAr).HasMaxLength(200).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.QualificationEn).HasMaxLength(200);
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.UserId).IsUnique();  // One profile per user
        builder.HasIndex(x => x.IsDeleted);
    }
}
