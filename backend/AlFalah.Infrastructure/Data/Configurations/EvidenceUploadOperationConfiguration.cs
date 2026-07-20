using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public sealed class EvidenceUploadOperationConfiguration : IEntityTypeConfiguration<EvidenceUploadOperation>
{
    public void Configure(EntityTypeBuilder<EvidenceUploadOperation> builder)
    {
        builder.ToTable("EvidenceUploadOperations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestId).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TeacherId, x.RequestId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
    }
}
