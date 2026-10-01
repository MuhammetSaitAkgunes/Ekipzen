using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceProcessOperationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceAttendanceEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_GuidanceAttendanceEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceAttendanceEvidences_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceComplianceEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ComplianceDocumentId = table.Column<int>(type: "int", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceComplianceEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceComplianceEvidences_ComplianceDocuments_ComplianceDocumentId",
                        column: x => x.ComplianceDocumentId,
                        principalTable: "ComplianceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceSetupEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TemplateYear = table.Column<int>(type: "int", nullable: false),
                    AddedCompanyProfile = table.Column<bool>(type: "bit", nullable: false),
                    AddedLeaveTypeCount = table.Column<int>(type: "int", nullable: false),
                    AddedHolidayCount = table.Column<int>(type: "int", nullable: false),
                    AddedPayrollSettings = table.Column<bool>(type: "bit", nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceSetupEvidences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceAttendanceEvidences_AttendanceRecordId",
                table: "GuidanceAttendanceEvidences",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceAttendanceEvidences_TenantId_UserId",
                table: "GuidanceAttendanceEvidences",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceComplianceEvidences_ComplianceDocumentId",
                table: "GuidanceComplianceEvidences",
                column: "ComplianceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceComplianceEvidences_TenantId_UserId",
                table: "GuidanceComplianceEvidences",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceSetupEvidences_TenantId_UserId",
                table: "GuidanceSetupEvidences",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [GuidanceSetupEvidences])
                    OR EXISTS (SELECT 1 FROM [GuidanceAttendanceEvidences])
                    OR EXISTS (SELECT 1 FROM [GuidanceComplianceEvidences])
                    THROW 51000, 'Guidance process evidence exists; preserve or explicitly archive it before rollback.', 1;
                """);
            migrationBuilder.DropTable(
                name: "GuidanceAttendanceEvidences");

            migrationBuilder.DropTable(
                name: "GuidanceComplianceEvidences");

            migrationBuilder.DropTable(
                name: "GuidanceSetupEvidences");
        }
    }
}
