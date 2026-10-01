using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceFinancialOperationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceOffboardingEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OffboardingCaseId = table.Column<int>(type: "int", nullable: false),
                    CaseVersion = table.Column<int>(type: "int", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceOffboardingEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceOffboardingEvidences_EmployeeOffboardingCases_OffboardingCaseId",
                        column: x => x.OffboardingCaseId,
                        principalTable: "EmployeeOffboardingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidancePayrollPeriodEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidancePayrollPeriodEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidancePayrollPeriodEvidences_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidancePayrollRowEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidancePayrollRowEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidancePayrollRowEvidences_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceOffboardingEvidences_OffboardingCaseId",
                table: "GuidanceOffboardingEvidences",
                column: "OffboardingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceOffboardingEvidences_TenantId_UserId",
                table: "GuidanceOffboardingEvidences",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidancePayrollPeriodEvidences_PayrollPeriodId",
                table: "GuidancePayrollPeriodEvidences",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidancePayrollPeriodEvidences_TenantId_UserId",
                table: "GuidancePayrollPeriodEvidences",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidancePayrollRowEvidences_PayrollEmployeeId",
                table: "GuidancePayrollRowEvidences",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidancePayrollRowEvidences_TenantId_UserId",
                table: "GuidancePayrollRowEvidences",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [GuidancePayrollPeriodEvidences])
                    OR EXISTS (SELECT 1 FROM [GuidancePayrollRowEvidences])
                    OR EXISTS (SELECT 1 FROM [GuidanceOffboardingEvidences])
                    THROW 51000, 'Financial guidance evidence exists; preserve or explicitly archive it before rollback.', 1;
                """);
            migrationBuilder.DropTable(
                name: "GuidanceOffboardingEvidences");

            migrationBuilder.DropTable(
                name: "GuidancePayrollPeriodEvidences");

            migrationBuilder.DropTable(
                name: "GuidancePayrollRowEvidences");
        }
    }
}
