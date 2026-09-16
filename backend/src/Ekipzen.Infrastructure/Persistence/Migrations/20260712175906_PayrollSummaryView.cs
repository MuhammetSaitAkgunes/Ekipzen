using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bordro dönemi özeti SQL view'ı: satır/onay sayıları + onaylı sonuç
    /// (PayrollResults) tutar toplamları.
    /// </summary>
    public partial class PayrollSummaryView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_PayrollPeriodSummary
                AS
                SELECT
                    pe.PayrollPeriodId,
                    COUNT(*) AS EmployeeCount,
                    SUM(CASE WHEN pe.ApprovalStatus = N'Approved' THEN 1 ELSE 0 END) AS ApprovedCount,
                    ISNULL(SUM(r.GrossEarnings), 0)   AS TotalGross,
                    ISNULL(SUM(r.NetPay), 0)          AS TotalNet,
                    ISNULL(SUM(r.TotalDeductions), 0) AS TotalDeductions,
                    ISNULL(SUM(r.EmployerCost), 0)    AS TotalEmployerCost
                FROM dbo.PayrollEmployees pe
                LEFT JOIN dbo.PayrollResults r ON r.PayrollEmployeeId = pe.Id
                GROUP BY pe.PayrollPeriodId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PayrollPeriodSummary;");
        }
    }
}
