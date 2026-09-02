using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminationCalculationDraftRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CalculationGeneratedAtUtc",
                table: "EmployeeOffboardingCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationGeneratedByUserId",
                table: "EmployeeOffboardingCases",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalculationRuleId",
                table: "EmployeeOffboardingCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalculationRuleVersion",
                table: "EmployeeOffboardingCases",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TerminationCalculationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DailyWageDivisor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SeveranceDaysPerYear = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumSeveranceServiceDays = table.Column<int>(type: "int", nullable: false),
                    SeveranceCeiling = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NoticeDaysUnderSixMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeDaysSixToEighteenMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeDaysEighteenToThirtySixMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeDaysOverThirtySixMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeFirstThresholdMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeSecondThresholdMonths = table.Column<int>(type: "int", nullable: false),
                    NoticeThirdThresholdMonths = table.Column<int>(type: "int", nullable: false),
                    IncludeRegularPremiumInDressedWage = table.Column<bool>(type: "bit", nullable: false),
                    IncludeRoadAllowanceInDressedWage = table.Column<bool>(type: "bit", nullable: false),
                    IncludeMealAllowanceInDressedWage = table.Column<bool>(type: "bit", nullable: false),
                    IncludeOtherRegularBenefitsInDressedWage = table.Column<bool>(type: "bit", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminationCalculationRules", x => x.Id);
                    table.CheckConstraint("CK_TerminationCalculationRules_PositiveValues", "[DailyWageDivisor] > 0 AND [SeveranceDaysPerYear] > 0 AND [MinimumSeveranceServiceDays] > 0 AND [SeveranceCeiling] > 0 AND [NoticeDaysUnderSixMonths] >= 0 AND [NoticeDaysSixToEighteenMonths] >= 0 AND [NoticeDaysEighteenToThirtySixMonths] >= 0 AND [NoticeDaysOverThirtySixMonths] >= 0 AND [NoticeFirstThresholdMonths] > 0 AND [NoticeSecondThresholdMonths] > [NoticeFirstThresholdMonths] AND [NoticeThirdThresholdMonths] > [NoticeSecondThresholdMonths]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingCases_CalculationRuleId",
                table: "EmployeeOffboardingCases",
                column: "CalculationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminationCalculationRules_TenantId_EffectiveFrom",
                table: "TerminationCalculationRules",
                columns: new[] { "TenantId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeOffboardingCases_TerminationCalculationRules_CalculationRuleId",
                table: "EmployeeOffboardingCases",
                column: "CalculationRuleId",
                principalTable: "TerminationCalculationRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeOffboardingCases_TerminationCalculationRules_CalculationRuleId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropTable(
                name: "TerminationCalculationRules");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeOffboardingCases_CalculationRuleId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "CalculationGeneratedAtUtc",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "CalculationGeneratedByUserId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "CalculationRuleId",
                table: "EmployeeOffboardingCases");

            migrationBuilder.DropColumn(
                name: "CalculationRuleVersion",
                table: "EmployeeOffboardingCases");
        }
    }
}
