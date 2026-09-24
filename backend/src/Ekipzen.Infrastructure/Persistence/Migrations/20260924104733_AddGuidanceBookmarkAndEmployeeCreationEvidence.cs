using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceBookmarkAndEmployeeCreationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCreationEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCreationEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCreationEvidences_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceBookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProcessKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceBookmarks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCreationEvidences_EmployeeId",
                table: "EmployeeCreationEvidences",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCreationEvidences_TenantId_UserId_EmployeeId",
                table: "EmployeeCreationEvidences",
                columns: new[] { "TenantId", "UserId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceBookmarks_TenantId_UserId_ProcessKey",
                table: "GuidanceBookmarks",
                columns: new[] { "TenantId", "UserId", "ProcessKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCreationEvidences");

            migrationBuilder.DropTable(
                name: "GuidanceBookmarks");
        }
    }
}
