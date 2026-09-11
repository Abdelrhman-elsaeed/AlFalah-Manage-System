using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class TeachingAssignmentConfiguration : IEntityTypeConfiguration<TeachingAssignment>
{
    public void Configure(EntityTypeBuilder<TeachingAssignment> b)
    {
        b.ToTable("TeachingAssignments", t => t.HasCheckConstraint("CK_TeachingAssignment_Mode", "[Mode] IN ('SingleTeacher', 'CoTeaching', 'SplitQuota')"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.Id });
        b.HasIndex(x => x.ClassSubjectRequirementId).IsUnique().HasFilter("[IsDeleted] = 0");
        b.Property(x => x.Mode).HasMaxLength(20);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        b.HasOne(x => x.Requirement).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.ClassSubjectRequirementId })
            .HasPrincipalKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class TeachingAssignmentMemberConfiguration : IEntityTypeConfiguration<TeachingAssignmentMember>
{
    public void Configure(EntityTypeBuilder<TeachingAssignmentMember> b)
    {
        b.ToTable("TeachingAssignmentMembers", t => t.HasCheckConstraint("CK_TeachingAssignmentMember_Allocation",
            "[AllocatedPeriodCount] >= 0 AND [AllocatedPairedBlockCount] >= 0 AND 2 * [AllocatedPairedBlockCount] <= [AllocatedPeriodCount]"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TeachingAssignmentId, x.TeacherTimetableProfileId }).IsUnique();
        b.HasOne(x => x.Assignment).WithMany(x => x.Members)
            .HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.TeachingAssignmentId })
            .HasPrincipalKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Teacher).WithMany()
            .HasForeignKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.TeacherTimetableProfileId })
            .HasPrincipalKey(x => new { x.SchoolId, x.TimetableSetupProfileId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.Assignment.IsDeleted);
    }
}
