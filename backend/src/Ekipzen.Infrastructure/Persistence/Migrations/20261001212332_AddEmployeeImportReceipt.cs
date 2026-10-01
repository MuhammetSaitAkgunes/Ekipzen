using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeImportReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeImportReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ImportRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviewToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeImportReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportReceipts_TenantId_UserId_ImportRequestId",
                table: "EmployeeImportReceipts",
                columns: new[] { "TenantId", "UserId", "ImportRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [EmployeeImportReceipts])
                    THROW 51000, 'Employee import receipts exist; rollback would remove retry protection.', 1;
                """);
            migrationBuilder.DropTable(
                name: "EmployeeImportReceipts");
        }
    }
}
