using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationPayrollApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCompensationPayrollApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemediationCaseId = table.Column<int>(type: "int", nullable: false),
                    PayrollRowId = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompensationPayrollApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationPayrollApplications_EmployeeCompensationRemediationCases_RemediationCaseId",
                        column: x => x.RemediationCaseId,
                        principalTable: "EmployeeCompensationRemediationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationPayrollApplications_PayrollEmployees_PayrollRowId",
                        column: x => x.PayrollRowId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPayrollApplications_PayrollRowId",
                table: "EmployeeCompensationPayrollApplications",
                column: "PayrollRowId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPayrollApplications_RemediationCaseId",
                table: "EmployeeCompensationPayrollApplications",
                column: "RemediationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPayrollApplications_TenantId_RemediationCaseId_PayrollRowId",
                table: "EmployeeCompensationPayrollApplications",
                columns: new[] { "TenantId", "RemediationCaseId", "PayrollRowId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCompensationPayrollApplications");
        }
    }
}
