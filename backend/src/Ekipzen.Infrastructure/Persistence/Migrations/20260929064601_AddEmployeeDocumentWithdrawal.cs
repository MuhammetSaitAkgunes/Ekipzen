using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocumentWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WithdrawalReason",
                table: "EmployeeDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAtUtc",
                table: "EmployeeDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnByUserId",
                table: "EmployeeDocuments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM EmployeeDocuments WHERE WithdrawnAtUtc IS NOT NULL)
                    THROW 50001, 'Geri cekilmis evraklar varken migration geri alinamaz.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "WithdrawalReason",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "WithdrawnAtUtc",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "WithdrawnByUserId",
                table: "EmployeeDocuments");
        }
    }
}
