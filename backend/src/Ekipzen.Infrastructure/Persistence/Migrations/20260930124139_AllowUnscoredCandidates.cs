using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowUnscoredCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "Candidates",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Candidates] WHERE [Score] IS NULL)
                   OR EXISTS (SELECT 1 FROM [CandidateCorrections]
                              WHERE JSON_VALUE([BeforeJson], '$.Score') IS NULL
                                 OR JSON_VALUE([AfterJson], '$.Score') IS NULL)
                    THROW 51000, 'Cannot remove unscored candidate support while current or historical null scores exist. Preserve these states before rollback.', 1;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "Candidates",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
