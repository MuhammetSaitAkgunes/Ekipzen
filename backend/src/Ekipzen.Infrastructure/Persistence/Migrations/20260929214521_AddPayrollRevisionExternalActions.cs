using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRevisionExternalActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollApprovedRowExternalActionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RevisionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PreviousRecordId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollApprovedRowExternalActionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollApprovedRowExternalActionRecords_PayrollApprovedRowRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "PayrollApprovedRowRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowExternalActionRecords_RevisionId",
                table: "PayrollApprovedRowExternalActionRecords",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowExternalActionRecords_TenantId_RevisionId_Kind_Id",
                table: "PayrollApprovedRowExternalActionRecords",
                columns: new[] { "TenantId", "RevisionId", "Kind", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollApprovedRowExternalActionRecords");
        }
    }
}
