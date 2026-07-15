using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlFalah.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandVisitSequenceToSix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Value 4 represented FollowUp before the sequence was expanded.
            // Move those historical rows to the new FollowUp value (7) before
            // value 4 is interpreted as the fourth planned visit.
            migrationBuilder.Sql("UPDATE Visits SET VisitSequence = 7 WHERE VisitSequence = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Visits SET VisitSequence = 4 WHERE VisitSequence = 7");
        }
    }
}
