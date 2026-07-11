using AlFalah.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlFalah.Infrastructure.Data.Configurations;

public class SchoolReportSettingsConfiguration : IEntityTypeConfiguration<SchoolReportSettings>
{
    public void Configure(EntityTypeBuilder<SchoolReportSettings> builder)
    {
        // D-31: Arabic-text columns get explicit Unicode + Arabic_CI_AS collation.
        builder.Property(x => x.ReportHeaderText).IsUnicode(true).UseCollation("Arabic_CI_AS");
        builder.Property(x => x.ReportFooterText).IsUnicode(true).UseCollation("Arabic_CI_AS");
    }
}