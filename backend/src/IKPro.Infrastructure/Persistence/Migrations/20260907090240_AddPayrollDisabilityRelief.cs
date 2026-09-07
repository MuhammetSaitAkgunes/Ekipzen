using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollDisabilityRelief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FirstDegreeDisabilityDeduction",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondDegreeDisabilityDeduction",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ThirdDegreeDisabilityDeduction",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DisabilityIncomeTaxBaseDeduction",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityReliefReference",
                table: "PayrollEmployees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityReliefType",
                table: "PayrollEmployees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<bool>(
                name: "OtherIncomeTaxBaseDeductionsConfirmedNone",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE PayrollSettings
                SET FirstDegreeDisabilityDeduction = 12000,
                    SecondDegreeDisabilityDeduction = 7000,
                    ThirdDegreeDisabilityDeduction = 3000
                WHERE EffectiveFrom = '2026-01-01'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstDegreeDisabilityDeduction",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "SecondDegreeDisabilityDeduction",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ThirdDegreeDisabilityDeduction",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "DisabilityIncomeTaxBaseDeduction",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "DisabilityReliefReference",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "DisabilityReliefType",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "OtherIncomeTaxBaseDeductionsConfirmedNone",
                table: "PayrollEmployees");
        }
    }
}
