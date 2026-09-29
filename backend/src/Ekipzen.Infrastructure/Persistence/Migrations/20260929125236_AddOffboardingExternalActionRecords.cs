using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOffboardingExternalActionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeOffboardingExternalActionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementRevisionId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EmployeeOffboardingExternalActionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeOffboardingExternalActionRecords_EmployeeOffboardingSettlementRevisions_SettlementRevisionId",
                        column: x => x.SettlementRevisionId,
                        principalTable: "EmployeeOffboardingSettlementRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingExternalActionRecords_SettlementRevisionId",
                table: "EmployeeOffboardingExternalActionRecords",
                column: "SettlementRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOffboardingExternalActionRecords_TenantId_SettlementRevisionId_Kind_Id",
                table: "EmployeeOffboardingExternalActionRecords",
                columns: new[] { "TenantId", "SettlementRevisionId", "Kind", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeOffboardingExternalActionRecords");
        }
    }
}
