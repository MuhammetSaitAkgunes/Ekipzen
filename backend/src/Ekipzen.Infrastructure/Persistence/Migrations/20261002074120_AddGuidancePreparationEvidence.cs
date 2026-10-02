using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidancePreparationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidancePreparationEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidancePreparationEvidences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidancePreparationEvidences_TenantId_UserId",
                table: "GuidancePreparationEvidences",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [GuidancePreparationEvidences]) THROW 51000, 'Preparation learning evidence exists; rollback would remove historical source.', 1;");
            migrationBuilder.DropTable(
                name: "GuidancePreparationEvidences");
        }
    }
}
