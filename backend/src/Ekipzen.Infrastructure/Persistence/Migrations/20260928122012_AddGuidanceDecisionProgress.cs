using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceDecisionProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceDecisionProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProcessKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastSelectedChoice = table.Column<int>(type: "int", nullable: false),
                    LastCorrect = table.Column<bool>(type: "bit", nullable: false),
                    LastAnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceDecisionProgresses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceDecisionProgresses_TenantId_UserId_ProcessKey_StepKey_ContentVersion",
                table: "GuidanceDecisionProgresses",
                columns: new[] { "TenantId", "UserId", "ProcessKey", "StepKey", "ContentVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuidanceDecisionProgresses");
        }
    }
}
