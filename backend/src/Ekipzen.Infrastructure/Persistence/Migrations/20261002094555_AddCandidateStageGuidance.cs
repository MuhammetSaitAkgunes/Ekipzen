using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateStageGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "CandidateStageDecisions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CandidateStageDecisions] WHERE [GuidanceSnapshotJson] IS NOT NULL)
                    THROW 51000, N'Kaynaklı aday aşama kararları var; öğrenme geçmişi silinemez.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "CandidateStageDecisions");
        }
    }
}
