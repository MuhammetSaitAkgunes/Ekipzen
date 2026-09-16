using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Faz 9: denetim hazırlık skoru view'ı (tek satır). Skor formülleri Faz 8'deki
    /// GetComplianceRiskQuery hesabıyla ve dashboard.js complianceMetrics ile birebirdir.
    /// Enum'lar tabloda string saklanır (Missing | InReview | DueSoon | Completed).
    /// </summary>
    public partial class ComplianceReadinessView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_ComplianceReadiness
                AS
                SELECT
                    COUNT(*) AS TotalCount,
                    SUM(CASE WHEN d.Status = N'Completed' THEN 1 ELSE 0 END) AS CompletedCount,
                    SUM(CASE WHEN d.Status = N'Missing'   THEN 1 ELSE 0 END) AS MissingCount,
                    SUM(CASE WHEN d.Status = N'DueSoon'   THEN 1 ELSE 0 END) AS DueSoonCount,
                    SUM(CASE WHEN d.Status = N'InReview'  THEN 1 ELSE 0 END) AS InReviewCount,
                    SUM(CASE WHEN d.OwnerName IS NOT NULL AND d.OwnerName <> N'' THEN 1 ELSE 0 END) AS OwnedCount,
                    CASE WHEN COUNT(*) = 0 THEN 100
                         ELSE CAST(ROUND(100.0 * SUM(CASE WHEN d.Status = N'Completed' THEN 1 ELSE 0 END)
                                         / COUNT(*), 0) AS INT)
                    END AS DocumentComplianceScore,
                    CASE WHEN COUNT(*) = 0 THEN 100
                         ELSE
                             CASE WHEN 100 - SUM(CASE WHEN d.Status = N'Missing'  THEN 6
                                                      WHEN d.Status = N'DueSoon'  THEN 3
                                                      WHEN d.Status = N'InReview' THEN 2
                                                      ELSE 0 END) < 0 THEN 0
                                  ELSE 100 - SUM(CASE WHEN d.Status = N'Missing'  THEN 6
                                                      WHEN d.Status = N'DueSoon'  THEN 3
                                                      WHEN d.Status = N'InReview' THEN 2
                                                      ELSE 0 END)
                             END
                    END AS ReadinessScore
                FROM dbo.ComplianceDocuments d;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ComplianceReadiness;");
        }
    }
}
