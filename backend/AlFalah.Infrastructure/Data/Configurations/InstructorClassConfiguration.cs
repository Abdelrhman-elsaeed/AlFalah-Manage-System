using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

/// <summary>
/// D-74 — Configuration for the new <see cref="InstructorClass"/> table.
/// Stores the class labels taught by an instructor (e.g. "3/1", "3/2"),
/// ordered by SortOrder so the visit-form dropdown keeps the teacher's
/// intended order.
/// </summary>
public class InstructorClassConfiguration : IEntityTypeConfiguration<InstructorClass>
{
    public void Configure(EntityTypeBuilder<InstructorClass> builder)
    {
        builder.HasKey(x => x.Id);
        // D-31: Arabic text → Unicode + Arabic_CI_AS.
        builder.Property(x => x.ClassLabel).HasMaxLength(50).IsUnicode(true).UseCollation("Arabic_CI_AS");

        builder.HasOne(x => x.InstructorProfile)
            .WithMany(p => p.Classes)
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InstructorProfileId);
        builder.HasIndex(x => x.IsDeleted);
        // One (active) class label per profile — soft-deletes still allowed
        // and excluded by the global query filter.
        builder.HasIndex(x => new { x.InstructorProfileId, x.ClassLabel })
            .HasFilter("[IsDeleted] = 0")
            .IsUnique();
    }
}
