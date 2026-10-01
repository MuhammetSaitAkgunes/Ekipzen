using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceContentReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceContentReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ContentRevisionId = table.Column<int>(type: "int", nullable: false),
                    ContentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceContentReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContentReadings_TenantId_UserId_ContentRevisionId",
                table: "GuidanceContentReadings",
                columns: new[] { "TenantId", "UserId", "ContentRevisionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [GuidanceContentReadings]) THROW 51000, 'Guidance reading history must be preserved before rollback.', 1;");
            migrationBuilder.DropTable(
                name: "GuidanceContentReadings");
        }
    }
}
