using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOffboardingSettlementRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeOffboardingSettlementRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OffboardingCaseId = table.Column<int>(type: "int", nullable: false),
                    RemediationCaseId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    BasedOnApprovedRevisionId = table.Column<int>(type: "int", nullable: true),
                    MonthlyGrossSalarySnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RegularPremiumSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RoadAllowanceSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MealAllowanceSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OtherRegularBenefitsSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnusedLeaveDays = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnusedLeavePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SeverancePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NoticePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SettlementReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SettlementNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OpenedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpertReviewReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOffboardingSettlementRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingSettlementRevisions_EmployeeCompensationRemediationCases_RemediationCaseId",
                        column: x => x.RemediationCaseId,
                        principalTable: "EmployeeCompensationRemediationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingSettlementRevisions_EmployeeOffboardingCases_OffboardingCaseId",
                        column: x => x.OffboardingCaseId,
                        principalTable: "EmployeeOffboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingSettlementRevisions_OffboardingCaseId",
                table: "EmployeeOffboardingSettlementRevisions",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingSettlementRevisions_RemediationCaseId",
                table: "EmployeeOffboardingSettlementRevisions",
                column: "RemediationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingSettlementRevisions_TenantId_OffboardingCaseId_RevisionNumber",
                table: "EmployeeOffboardingSettlementRevisions",
                columns: new[] { "TenantId", "OffboardingCaseId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingSettlementRevisions_TenantId_OffboardingCaseId_Status",
                table: "EmployeeOffboardingSettlementRevisions",
                columns: new[] { "TenantId", "OffboardingCaseId", "Status" },
                unique: true,
                filter: "[Status] = 'draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeOffboardingSettlementRevisions");
        }
    }
}
