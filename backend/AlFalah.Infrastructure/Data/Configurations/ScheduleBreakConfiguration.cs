using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class ScheduleBreakDefinitionConfiguration : IEntityTypeConfiguration<ScheduleBreakDefinition>
{
    public void Configure(EntityTypeBuilder<ScheduleBreakDefinition> b)
    {
        b.ToTable("ScheduleBreakDefinitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.HasOne(x => x.Day).WithMany(x => x.Breaks).HasForeignKey(x => x.BellScheduleDayId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class ScheduleBreakWindowConfiguration : IEntityTypeConfiguration<ScheduleBreakWindow>
{
    public void Configure(EntityTypeBuilder<ScheduleBreakWindow> b)
    {
        b.ToTable("ScheduleBreakWindows", t => t.HasCheckConstraint("CK_ScheduleBreakWindows_Time", "[StartLocalTime] < [EndLocalTime]"));
        b.HasKey(x => x.Id);
        b.Property(x => x.StartLocalTime).HasColumnType("time");
        b.Property(x => x.EndLocalTime).HasColumnType("time");
        b.HasOne(x => x.Definition).WithOne(x => x.Window).HasForeignKey<ScheduleBreakWindow>(x => x.ScheduleBreakDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
