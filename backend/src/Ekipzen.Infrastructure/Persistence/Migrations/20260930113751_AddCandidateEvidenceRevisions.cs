using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateEvidenceRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateEvidenceRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CandidateId = table.Column<int>(type: "int", nullable: false),
                    InterviewNoteId = table.Column<int>(type: "int", nullable: true),
                    EvaluationId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FromCandidateVersion = table.Column<int>(type: "int", nullable: false),
                    ToCandidateVersion = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateEvidenceRevisions", x => x.Id);
                    table.CheckConstraint("CK_CandidateEvidenceRevisions_OneSource", "([InterviewNoteId] IS NOT NULL AND [EvaluationId] IS NULL) OR ([InterviewNoteId] IS NULL AND [EvaluationId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CandidateEvidenceRevisions_CandidateEvaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "CandidateEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateEvidenceRevisions_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateEvidenceRevisions_InterviewNotes_InterviewNoteId",
                        column: x => x.InterviewNoteId,
                        principalTable: "InterviewNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvidenceRevisions_CandidateId",
                table: "CandidateEvidenceRevisions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvidenceRevisions_EvaluationId",
                table: "CandidateEvidenceRevisions",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvidenceRevisions_InterviewNoteId",
                table: "CandidateEvidenceRevisions",
                column: "InterviewNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvidenceRevisions_TenantId_EvaluationId_Version",
                table: "CandidateEvidenceRevisions",
                columns: new[] { "TenantId", "EvaluationId", "Version" },
                unique: true,
                filter: "[EvaluationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvidenceRevisions_TenantId_InterviewNoteId_Version",
                table: "CandidateEvidenceRevisions",
                columns: new[] { "TenantId", "InterviewNoteId", "Version" },
                unique: true,
                filter: "[InterviewNoteId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateEvidenceRevisions");
        }
    }
}
