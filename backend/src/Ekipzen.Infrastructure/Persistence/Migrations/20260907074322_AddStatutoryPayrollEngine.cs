using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryPayrollEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "PayrollSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "PayrollSettings",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManufacturingEmployerDiscountPoints",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "MealIncomeTaxDailyExemption",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 300m);

            migrationBuilder.AddColumn<decimal>(
                name: "MealSgkDailyExemption",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 158m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherSectorEmployerDiscountPoints",
                table: "PayrollSettings",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "PayrollSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "PayrollSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PayrollSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedEmployerSgkRate",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxExemptMealAllowance",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageIncomeTaxExemption",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageStampTaxExemption",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkExemptMealAllowance",
                table: "PayrollResults",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageIncomeTaxBaseBefore",
                table: "PayrollPeriods",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyMinimumWageTaxExemption",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BenefitPayFullyTaxableConfirmed",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmployerSgkDiscount",
                table: "PayrollEmployees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "EmployerSgkDiscountReference",
                table: "PayrollEmployees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MealDays",
                table: "PayrollEmployees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MealTreatment",
                table: "PayrollEmployees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<bool>(
                name: "OvertimeTreatmentConfirmed",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SgkPremiumDaysConfirmed",
                table: "PayrollEmployees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE PayrollSettings
                SET Status = 'Draft',
                    Version = 1,
                    MealSgkDailyExemption = 158,
                    MealIncomeTaxDailyExemption = 300,
                    OtherSectorEmployerDiscountPoints = 2,
                    ManufacturingEmployerDiscountPoints = 5;

                UPDATE PayrollEmployees
                SET ApplyMinimumWageTaxExemption = 1,
                    EmployerSgkDiscount = 'None',
                    MealTreatment = 'Unspecified';

                UPDATE PayrollPeriods
                SET MinimumWageIncomeTaxBaseBefore = (Month - 1) * 28075.50
                WHERE Year = 2026;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ManufacturingEmployerDiscountPoints",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "MealIncomeTaxDailyExemption",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "MealSgkDailyExemption",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "OtherSectorEmployerDiscountPoints",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "AppliedEmployerSgkRate",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "IncomeTaxExemptMealAllowance",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "MinimumWageIncomeTaxExemption",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "MinimumWageStampTaxExemption",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "SgkExemptMealAllowance",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "MinimumWageIncomeTaxBaseBefore",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ApplyMinimumWageTaxExemption",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "BenefitPayFullyTaxableConfirmed",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "EmployerSgkDiscount",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "EmployerSgkDiscountReference",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "MealDays",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "MealTreatment",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "OvertimeTreatmentConfirmed",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "SgkPremiumDaysConfirmed",
                table: "PayrollEmployees");
        }
    }
}
