using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollApprovedRowRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollApprovedRowRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRowId = table.Column<int>(type: "int", nullable: false),
                    RemediationCaseId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpertReviewReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollApprovedRowRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollApprovedRowRevisions_EmployeeCompensationRemediationCases_RemediationCaseId",
                        column: x => x.RemediationCaseId,
                        principalTable: "EmployeeCompensationRemediationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollApprovedRowRevisions_PayrollEmployees_PayrollRowId",
                        column: x => x.PayrollRowId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowRevisions_PayrollRowId",
                table: "PayrollApprovedRowRevisions",
                column: "PayrollRowId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowRevisions_RemediationCaseId",
                table: "PayrollApprovedRowRevisions",
                column: "RemediationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowRevisions_TenantId_PayrollRowId_RevisionNumber",
                table: "PayrollApprovedRowRevisions",
                columns: new[] { "TenantId", "PayrollRowId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollApprovedRowRevisions");
        }
    }
}
