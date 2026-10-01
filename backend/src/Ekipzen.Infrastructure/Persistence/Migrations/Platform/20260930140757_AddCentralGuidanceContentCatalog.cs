using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddCentralGuidanceContentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceContentRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    BasedOnId = table.Column<int>(type: "int", nullable: true),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContributorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEditedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpertRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpertReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PublishedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceContentRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuidanceContentEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceContentEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceContentEvent_GuidanceContentRevisions_ContentId",
                        column: x => x.ContentId,
                        principalTable: "GuidanceContentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContentEvent_ContentId_Revision",
                table: "GuidanceContentEvent",
                columns: new[] { "ContentId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContentRevisions_ContentKey_ContentVersion",
                table: "GuidanceContentRevisions",
                columns: new[] { "ContentKey", "ContentVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceContentRevisions_ProcessKey_State_EffectiveFrom",
                table: "GuidanceContentRevisions",
                columns: new[] { "ProcessKey", "State", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [GuidanceContentRevisions])
                    THROW 51000, 'Rehber içerik ve inceleme geçmişi varken katalog migrationi geri alınamaz.', 1;
                """);
            migrationBuilder.DropTable(
                name: "GuidanceContentEvent");

            migrationBuilder.DropTable(
                name: "GuidanceContentRevisions");
        }
    }
}
