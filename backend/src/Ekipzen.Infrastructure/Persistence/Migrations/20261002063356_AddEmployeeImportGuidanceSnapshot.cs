using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeImportGuidanceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "EmployeeImportReceipts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [EmployeeImportReceipts] WHERE [GuidanceSnapshotJson] IS NOT NULL)
                    THROW 51000, 'Import guidance snapshots exist; rollback would remove their learning source.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "EmployeeImportReceipts");
        }
    }
}
