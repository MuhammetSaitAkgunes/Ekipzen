using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentOperationGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "PositionCorrections",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "InterviewNotes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "CandidateEvidenceRevisions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuidanceOccurredAtUtc",
                table: "CandidateEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "CandidateEvaluations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceUserId",
                table: "CandidateEvaluations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuidanceSnapshotJson",
                table: "CandidateCorrections",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [PositionCorrections] WHERE [GuidanceSnapshotJson] IS NOT NULL) OR EXISTS (SELECT 1 FROM [CandidateCorrections] WHERE [GuidanceSnapshotJson] IS NOT NULL) OR EXISTS (SELECT 1 FROM [InterviewNotes] WHERE [GuidanceSnapshotJson] IS NOT NULL) OR EXISTS (SELECT 1 FROM [CandidateEvaluations] WHERE [GuidanceSnapshotJson] IS NOT NULL) OR EXISTS (SELECT 1 FROM [CandidateEvidenceRevisions] WHERE [GuidanceSnapshotJson] IS NOT NULL) THROW 51000, 'Recruitment learning sources exist; rollback would erase their history.', 1;");
            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "PositionCorrections");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "InterviewNotes");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "CandidateEvidenceRevisions");

            migrationBuilder.DropColumn(
                name: "GuidanceOccurredAtUtc",
                table: "CandidateEvaluations");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "CandidateEvaluations");

            migrationBuilder.DropColumn(
                name: "GuidanceUserId",
                table: "CandidateEvaluations");

            migrationBuilder.DropColumn(
                name: "GuidanceSnapshotJson",
                table: "CandidateCorrections");
        }
    }
}
