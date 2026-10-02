using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialExternalAndTimesheetReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewedTimesheetRevision",
                table: "PayrollEmployees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialExternalActionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRowId = table.Column<int>(type: "int", nullable: true),
                    OffboardingCaseId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PreviousRecordId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialExternalActionRecords", x => x.Id);
                    table.CheckConstraint("CK_FinancialExternal_Source", "([PayrollRowId] IS NOT NULL AND [OffboardingCaseId] IS NULL) OR ([PayrollRowId] IS NULL AND [OffboardingCaseId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FinancialExternalActionRecords_EmployeeOffboardingCases_OffboardingCaseId",
                        column: x => x.OffboardingCaseId,
                        principalTable: "EmployeeOffboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialExternalActionRecords_PayrollEmployees_PayrollRowId",
                        column: x => x.PayrollRowId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialExternalActionRecords_OffboardingCaseId",
                table: "FinancialExternalActionRecords",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialExternalActionRecords_PayrollRowId",
                table: "FinancialExternalActionRecords",
                column: "PayrollRowId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialExternalActionRecords_TenantId_OffboardingCaseId_Kind",
                table: "FinancialExternalActionRecords",
                columns: new[] { "TenantId", "OffboardingCaseId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialExternalActionRecords_TenantId_PayrollRowId_Kind",
                table: "FinancialExternalActionRecords",
                columns: new[] { "TenantId", "PayrollRowId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [FinancialExternalActionRecords]) OR EXISTS (SELECT 1 FROM [PayrollEmployees] WHERE [ReviewedTimesheetRevision] IS NOT NULL) THROW 51000, 'Finansal kanit veya puantaj inceleme izi varken bu migration geri alinamaz.', 1;");
            migrationBuilder.DropTable(
                name: "FinancialExternalActionRecords");

            migrationBuilder.DropColumn(
                name: "ReviewedTimesheetRevision",
                table: "PayrollEmployees");
        }
    }
}
