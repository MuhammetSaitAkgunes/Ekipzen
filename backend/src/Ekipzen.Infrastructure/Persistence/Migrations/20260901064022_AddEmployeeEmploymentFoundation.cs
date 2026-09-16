using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeEmploymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractType",
                table: "Employees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Indefinite");

            migrationBuilder.AddColumn<int>(
                name: "CompensationPackageId",
                table: "EmployeeOffboardingCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CompensationPackagePreviousEffectiveTo",
                table: "EmployeeOffboardingCases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractTypeSnapshot",
                table: "EmployeeOffboardingCases",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Indefinite");

            migrationBuilder.AddColumn<int>(
                name: "EmploymentPeriodId",
                table: "EmployeeOffboardingCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MealAllowanceSnapshot",
                table: "EmployeeOffboardingCases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyGrossSalarySnapshot",
                table: "EmployeeOffboardingCases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NoticeHandling",
                table: "EmployeeOffboardingCases",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.AddColumn<decimal>(
                name: "OtherRegularBenefitsSnapshot",
                table: "EmployeeOffboardingCases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularPremiumSnapshot",
                table: "EmployeeOffboardingCases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoadAllowanceSnapshot",
                table: "EmployeeOffboardingCases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "EmployeeOffboardingCases",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.CreateTable(
                name: "EmployeeEmploymentPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeEmploymentPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeEmploymentPeriods_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCompensationPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmploymentPeriodId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MonthlyGrossSalary = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RegularPremium = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RoadAllowance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MealAllowance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OtherRegularBenefits = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompensationPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationPackages_EmployeeEmploymentPeriods_EmploymentPeriodId",
                        column: x => x.EmploymentPeriodId,
                        principalTable: "EmployeeEmploymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO EmployeeEmploymentPeriods
                    (EmployeeId, StartDate, EndDate, ContractType, TenantId, CreatedAtUtc)
                SELECT Id, HireDate, TerminationDate, ContractType, TenantId, SYSUTCDATETIME()
                FROM Employees;

                UPDATE offboarding
                SET EmploymentPeriodId = period.Id
                FROM EmployeeOffboardingCases AS offboarding
                INNER JOIN EmployeeEmploymentPeriods AS period
                    ON period.EmployeeId = offboarding.EmployeeId
                    AND period.TenantId = offboarding.TenantId
                    AND period.EndDate = offboarding.TerminationDate
                WHERE offboarding.Status = 'Completed';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPackages_EmploymentPeriodId",
                table: "EmployeeCompensationPackages",
                column: "EmploymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPackages_TenantId_EmploymentPeriodId_EffectiveFrom",
                table: "EmployeeCompensationPackages",
                columns: new[] { "TenantId", "EmploymentPeriodId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmploymentPeriods_EmployeeId",
                table: "EmployeeEmploymentPeriods",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmploymentPeriods_TenantId_EmployeeId",
                table: "EmployeeEmploymentPeriods",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true,
                filter: "[EndDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmploymentPeriods_TenantId_EmployeeId_StartDate",
                table: "EmployeeEmploymentPeriods",
                columns: new[] { "TenantId", "EmployeeId", "StartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCompensationPackages");

            migrationBuilder.DropTable(
                name: "EmployeeEmploymentPeriods");

            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CompensationPackageId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "CompensationPackagePreviousEffectiveTo",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "ContractTypeSnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "EmploymentPeriodId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "MealAllowanceSnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "MonthlyGrossSalarySnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "NoticeHandling",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "OtherRegularBenefitsSnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "RegularPremiumSnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "RoadAllowanceSnapshot",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "EmployeeOffboardingCases");
        }
    }
}
