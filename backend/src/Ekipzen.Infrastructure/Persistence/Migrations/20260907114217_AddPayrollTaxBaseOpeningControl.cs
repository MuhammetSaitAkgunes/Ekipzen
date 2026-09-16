using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollTaxBaseOpeningControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CumulativeIncomeTaxBaseConfirmed",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousEmployerIncomeTaxBase",
                table: "PayrollEmployees",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PreviousEmployerIncomeTaxBaseReference",
                table: "PayrollEmployees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CumulativeIncomeTaxBaseConfirmed",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "PreviousEmployerIncomeTaxBase",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "PreviousEmployerIncomeTaxBaseReference",
                table: "PayrollEmployees");
        }
    }
}
