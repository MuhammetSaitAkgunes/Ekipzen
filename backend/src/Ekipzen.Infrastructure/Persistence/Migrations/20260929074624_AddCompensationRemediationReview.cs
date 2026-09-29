using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationRemediationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCompensationRemediationCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CompensationPackageId = table.Column<int>(type: "int", nullable: false),
                    PackageVersion = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IssueDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProposedEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpactSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpenedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpertReviewReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompensationRemediationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationRemediationCases_EmployeeCompensationPackages_CompensationPackageId",
                        column: x => x.CompensationPackageId,
                        principalTable: "EmployeeCompensationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationRemediationCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationRemediationCases_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases",
                column: "CompensationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationRemediationCases_EmployeeId",
                table: "EmployeeCompensationRemediationCases",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationRemediationCases_TenantId_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases",
                columns: new[] { "TenantId", "CompensationPackageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM EmployeeCompensationRemediationCases)
                    THROW 50003, 'Ucret telafi inceleme kaydi varken migration geri alinamaz.', 1;
                """);

            migrationBuilder.DropTable(
                name: "EmployeeCompensationRemediationCases");
        }
    }
}
