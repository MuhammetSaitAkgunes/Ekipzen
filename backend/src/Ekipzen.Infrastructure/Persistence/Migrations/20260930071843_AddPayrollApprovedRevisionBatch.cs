using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollApprovedRevisionBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "PayrollApprovedRowRevisions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollApprovedRevisionBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    RemediationCaseId = table.Column<int>(type: "int", nullable: false),
                    SourcePayrollRowId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    PreviewToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpertReviewReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollApprovedRevisionBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowRevisions_BatchId",
                table: "PayrollApprovedRowRevisions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRevisionBatches_TenantId_SourcePayrollRowId",
                table: "PayrollApprovedRevisionBatches",
                columns: new[] { "TenantId", "SourcePayrollRowId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollApprovedRowRevisions_PayrollApprovedRevisionBatches_BatchId",
                table: "PayrollApprovedRowRevisions",
                column: "BatchId",
                principalTable: "PayrollApprovedRevisionBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollApprovedRowRevisions_PayrollApprovedRevisionBatches_BatchId",
                table: "PayrollApprovedRowRevisions");

            migrationBuilder.DropTable(
                name: "PayrollApprovedRevisionBatches");

            migrationBuilder.DropIndex(
                name: "IX_PayrollApprovedRowRevisions_BatchId",
                table: "PayrollApprovedRowRevisions");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PayrollApprovedRowRevisions");
        }
    }
}
