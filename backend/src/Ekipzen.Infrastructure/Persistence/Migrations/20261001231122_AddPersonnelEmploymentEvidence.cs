using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonnelEmploymentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompensationPackageId",
                table: "GuidancePersonnelEvidences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentPeriodId",
                table: "GuidancePersonnelEvidences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FromVersion",
                table: "GuidancePersonnelEvidences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedPackageId",
                table: "GuidancePersonnelEvidences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToVersion",
                table: "GuidancePersonnelEvidences",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [GuidancePersonnelEvidences]
                    WHERE [EmploymentPeriodId] IS NOT NULL OR [CompensationPackageId] IS NOT NULL
                        OR [RelatedPackageId] IS NOT NULL OR [FromVersion] IS NOT NULL OR [ToVersion] IS NOT NULL)
                    THROW 51000, 'Employment evidence exists; rollback would remove its resource and version context.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "CompensationPackageId",
                table: "GuidancePersonnelEvidences");

            migrationBuilder.DropColumn(
                name: "EmploymentPeriodId",
                table: "GuidancePersonnelEvidences");

            migrationBuilder.DropColumn(
                name: "FromVersion",
                table: "GuidancePersonnelEvidences");

            migrationBuilder.DropColumn(
                name: "RelatedPackageId",
                table: "GuidancePersonnelEvidences");

            migrationBuilder.DropColumn(
                name: "ToVersion",
                table: "GuidancePersonnelEvidences");
        }
    }
}
