using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateStageDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateStageDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FromVersion = table.Column<int>(type: "int", nullable: false),
                    ToVersion = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateStageDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateStageDecisions_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateStageDecisions_CandidateId",
                table: "CandidateStageDecisions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateStageDecisions_TenantId_CandidateId_ToVersion",
                table: "CandidateStageDecisions",
                columns: new[] { "TenantId", "CandidateId", "ToVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateStageDecisions");
        }
    }
}
