using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationDateCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCompensationDateCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompensationPackageId = table.Column<int>(type: "int", nullable: false),
                    PreviousPackageId = table.Column<int>(type: "int", nullable: true),
                    OldEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    NewEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    OldPreviousEffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    NewPreviousEffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    FromVersion = table.Column<int>(type: "int", nullable: false),
                    ToVersion = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompensationDateCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationDateCorrections_EmployeeCompensationPackages_CompensationPackageId",
                        column: x => x.CompensationPackageId,
                        principalTable: "EmployeeCompensationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationDateCorrections_EmployeeCompensationPackages_PreviousPackageId",
                        column: x => x.PreviousPackageId,
                        principalTable: "EmployeeCompensationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationDateCorrections_CompensationPackageId",
                table: "EmployeeCompensationDateCorrections",
                column: "CompensationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationDateCorrections_PreviousPackageId",
                table: "EmployeeCompensationDateCorrections",
                column: "PreviousPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationDateCorrections_TenantId_CompensationPackageId_ToVersion",
                table: "EmployeeCompensationDateCorrections",
                columns: new[] { "TenantId", "CompensationPackageId", "ToVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM EmployeeCompensationDateCorrections)
                    THROW 50004, 'Ucret tarih duzeltme gecmisi varken migration geri alinamaz.', 1;
                """);

            migrationBuilder.DropTable(
                name: "EmployeeCompensationDateCorrections");
        }
    }
}
