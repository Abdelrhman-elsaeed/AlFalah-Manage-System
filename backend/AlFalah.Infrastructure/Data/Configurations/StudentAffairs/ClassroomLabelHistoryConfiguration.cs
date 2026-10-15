using AlFalah.Domain.Entities.StudentAffairs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations.StudentAffairs;

public sealed class ClassroomLabelHistoryConfiguration : IEntityTypeConfiguration<ClassroomLabelHistory>
{
    public void Configure(EntityTypeBuilder<ClassroomLabelHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(50).IsUnicode().UseCollation("Arabic_CI_AS");
        builder.HasIndex(x => new {x.SchoolId, x.AcademicYearId, x.Label}).IsUnique();
        builder.HasOne(x => x.Classroom).WithMany().HasForeignKey(x => new {x.SchoolId,x.AcademicYearId,x.ClassroomId})
            .HasPrincipalKey(x => new {x.SchoolId,x.AcademicYearId,x.Id}).OnDelete(DeleteBehavior.Restrict);
    }
}
