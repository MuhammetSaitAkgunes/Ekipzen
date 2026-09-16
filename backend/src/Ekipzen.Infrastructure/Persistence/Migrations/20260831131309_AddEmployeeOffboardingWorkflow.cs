using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeOffboardingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OffboardingCaseId",
                table: "IdentityProvisioningOperations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeOffboardingCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SuccessorManagerId = table.Column<int>(type: "int", nullable: true),
                    UnusedLeaveDays = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnusedLeavePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SeverancePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NoticePayment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SettlementSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SettlementReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SettlementNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreviousExitCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOffboardingCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeOffboardingTeamTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OffboardingCaseId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PreviousManagerId = table.Column<int>(type: "int", nullable: true),
                    AssignedManagerId = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOffboardingTeamTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingTeamTransfers_EmployeeOffboardingCases_OffboardingCaseId",
                        column: x => x.OffboardingCaseId,
                        principalTable: "EmployeeOffboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingTeamTransfers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityProvisioningOperations_OffboardingCaseId",
                table: "IdentityProvisioningOperations",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityProvisioningOperations_TenantId_OffboardingCaseId",
                table: "IdentityProvisioningOperations",
                columns: new[] { "TenantId", "OffboardingCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingCases_EmployeeId",
                table: "EmployeeOffboardingCases",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingCases_TenantId_EmployeeId",
                table: "EmployeeOffboardingCases",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true,
                filter: "[Status] <> 'Reversed'");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingTeamTransfers_EmployeeId",
                table: "EmployeeOffboardingTeamTransfers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingTeamTransfers_OffboardingCaseId",
                table: "EmployeeOffboardingTeamTransfers",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingTeamTransfers_TenantId_OffboardingCaseId_EmployeeId",
                table: "EmployeeOffboardingTeamTransfers",
                columns: new[] { "TenantId", "OffboardingCaseId", "EmployeeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityProvisioningOperations_EmployeeOffboardingCases_OffboardingCaseId",
                table: "IdentityProvisioningOperations",
                column: "OffboardingCaseId",
                principalTable: "EmployeeOffboardingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdentityProvisioningOperations_EmployeeOffboardingCases_OffboardingCaseId",
                table: "IdentityProvisioningOperations");

            migrationBuilder.DropTable(
                name: "EmployeeOffboardingTeamTransfers");

            migrationBuilder.DropTable(
                name: "EmployeeOffboardingCases");

            migrationBuilder.DropIndex(
                name: "IX_IdentityProvisioningOperations_OffboardingCaseId",
                table: "IdentityProvisioningOperations");

            migrationBuilder.DropIndex(
                name: "IX_IdentityProvisioningOperations_TenantId_OffboardingCaseId",
                table: "IdentityProvisioningOperations");

            migrationBuilder.DropColumn(
                name: "OffboardingCaseId",
                table: "IdentityProvisioningOperations");
        }
    }
}
