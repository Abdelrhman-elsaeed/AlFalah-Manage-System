using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeacherTaskStatusConfiguration : IEntityTypeConfiguration<TeacherTaskStatus>
{
    public void Configure(EntityTypeBuilder<TeacherTaskStatus> builder)
    {
        builder.ToTable("TeacherTaskStatuses");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TeacherId, x.TaskId, x.AcademicYearId }).IsUnique();
        builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.CellStatus });
        builder.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
    }
}
