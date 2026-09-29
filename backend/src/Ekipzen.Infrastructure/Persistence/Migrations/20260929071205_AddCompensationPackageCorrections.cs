using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationPackageCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "EmployeeCompensationPackages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "EmployeeCompensationPackageCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompensationPackageId = table.Column<int>(type: "int", nullable: false),
                    FromVersion = table.Column<int>(type: "int", nullable: false),
                    ToVersion = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompensationPackageCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompensationPackageCorrections_EmployeeCompensationPackages_CompensationPackageId",
                        column: x => x.CompensationPackageId,
                        principalTable: "EmployeeCompensationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPackageCorrections_CompensationPackageId",
                table: "EmployeeCompensationPackageCorrections",
                column: "CompensationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationPackageCorrections_TenantId_CompensationPackageId_ToVersion",
                table: "EmployeeCompensationPackageCorrections",
                columns: new[] { "TenantId", "CompensationPackageId", "ToVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM EmployeeCompensationPackageCorrections)
                    THROW 50002, 'Ucret duzeltme gecmisi varken migration geri alinamaz.', 1;
                """);

            migrationBuilder.DropTable(
                name: "EmployeeCompensationPackageCorrections");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EmployeeCompensationPackages");
        }
    }
}
