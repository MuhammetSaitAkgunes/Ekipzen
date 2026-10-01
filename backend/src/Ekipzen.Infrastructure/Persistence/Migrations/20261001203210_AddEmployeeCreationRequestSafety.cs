using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCreationRequestSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreationRequestId",
                table: "EmployeeCreationEvidences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "EmployeeCreationEvidences",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCreationEvidences_TenantId_UserId_CreationRequestId",
                table: "EmployeeCreationEvidences",
                columns: new[] { "TenantId", "UserId", "CreationRequestId" },
                unique: true,
                filter: "[CreationRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [EmployeeCreationEvidences] WHERE [CreationRequestId] IS NOT NULL)
                    THROW 51000, 'Employee creation request receipts exist; rollback would remove retry protection.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_EmployeeCreationEvidences_TenantId_UserId_CreationRequestId",
                table: "EmployeeCreationEvidences");

            migrationBuilder.DropColumn(
                name: "CreationRequestId",
                table: "EmployeeCreationEvidences");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "EmployeeCreationEvidences");
        }
    }
}
