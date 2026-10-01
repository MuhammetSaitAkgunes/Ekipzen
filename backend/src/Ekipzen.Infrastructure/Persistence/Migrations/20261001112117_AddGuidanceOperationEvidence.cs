using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceOperationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "EmployeeCreationEvidences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "CandidateReviewEvidences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuidanceLeaveDecisionEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false),
                    DecisionStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuidanceSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceLeaveDecisionEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceLeaveDecisionEvidences_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceLeaveDecisionEvidences_LeaveRequestId",
                table: "GuidanceLeaveDecisionEvidences",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceLeaveDecisionEvidences_TenantId_UserId_LeaveRequestId",
                table: "GuidanceLeaveDecisionEvidences",
                columns: new[] { "TenantId", "UserId", "LeaveRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [GuidanceLeaveDecisionEvidences])
                    OR EXISTS (SELECT 1 FROM [EmployeeCreationEvidences] WHERE [GuidanceSnapshotJson] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [CandidateReviewEvidences] WHERE [GuidanceSnapshotJson] IS NOT NULL)
                    THROW 51000, 'Guidance operation evidence must be preserved before rollback.', 1;
                """);
            migrationBuilder.DropTable(
                name: "GuidanceLeaveDecisionEvidences");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "EmployeeCreationEvidences");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "CandidateReviewEvidences");
        }
    }
}
