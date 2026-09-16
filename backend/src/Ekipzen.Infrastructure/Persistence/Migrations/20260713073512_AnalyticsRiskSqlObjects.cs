using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Faz 8 analitik SQL nesneleri: snapshot'a sinyal metinleri + çalışan/departman
    /// risk view'ları. Risk skor formülü ve seviye eşikleri dashboard.js
    /// getDashboardMetrics() ile birebir aynıdır (parite: DashboardTests).
    /// </summary>
    public partial class AnalyticsRiskSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecommendedAction",
                table: "EmployeeMetricSnapshots",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrendNote",
                table: "EmployeeMetricSnapshots",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Çalışan bazlı güncel risk: her çalışanın en son snapshot'ı + JS formülü.
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_EmployeeRiskMetric
                AS
                WITH LatestSnapshot AS (
                    SELECT s.*,
                           ROW_NUMBER() OVER (PARTITION BY s.EmployeeId
                                              ORDER BY s.PeriodDate DESC) AS RowNo
                    FROM dbo.EmployeeMetricSnapshots s
                )
                SELECT
                    e.Id                                AS EmployeeId,
                    e.FirstName + N' ' + e.LastName     AS FullName,
                    e.Title,
                    e.DepartmentId,
                    d.Name                              AS DepartmentName,
                    e.ManagerId,
                    m.FirstName + N' ' + m.LastName     AS ManagerName,
                    s.PeriodDate,
                    s.AbsencePct,
                    s.LatenessPct,
                    s.OvertimePct,
                    s.UnusedLeavePct,
                    s.Pulse,
                    s.Performance,
                    s.RoleCriticality,
                    s.TrendNote,
                    s.RecommendedAction,
                    CAST(ROUND(
                        s.AbsencePct * 0.18 + s.LatenessPct * 0.14 + s.OvertimePct * 0.20 +
                        s.UnusedLeavePct * 0.15 + (100 - s.Pulse) * 0.18 +
                        (100 - s.Performance) * 0.15, 0) AS INT) AS RiskScore,
                    CASE WHEN s.Pulse < 55 OR s.RoleCriticality > 85 THEN N'high'
                         WHEN s.Pulse < 65 OR s.RoleCriticality > 75 THEN N'medium'
                         ELSE N'low' END                AS AttritionRisk,
                    CASE WHEN s.OvertimePct > 65 AND s.UnusedLeavePct > 65 THEN N'high'
                         WHEN s.OvertimePct > 55 OR s.UnusedLeavePct > 65 THEN N'medium'
                         ELSE N'low' END                AS BurnoutRisk
                FROM LatestSnapshot s
                JOIN dbo.Employees e ON e.Id = s.EmployeeId
                JOIN dbo.Departments d ON d.Id = e.DepartmentId
                LEFT JOIN dbo.Employees m ON m.Id = e.ManagerId
                WHERE s.RowNo = 1;
                """);

            // Departman risk agregasyonu (dashboard.js departmentRisk kaynağı).
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_DepartmentRisk
                AS
                SELECT
                    r.DepartmentId,
                    r.DepartmentName,
                    COUNT(*) AS EmployeeCount,
                    CAST(ROUND(AVG(CAST(r.RiskScore AS FLOAT)), 0) AS INT) AS RiskScore,
                    SUM(CASE WHEN r.AttritionRisk = N'high' THEN 1 ELSE 0 END) AS HighAttritionCount,
                    SUM(CASE WHEN r.BurnoutRisk = N'high' THEN 1 ELSE 0 END) AS HighBurnoutCount
                FROM dbo.vw_EmployeeRiskMetric r
                GROUP BY r.DepartmentId, r.DepartmentName;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_DepartmentRisk;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_EmployeeRiskMetric;");

            migrationBuilder.DropColumn(
                name: "RecommendedAction",
                table: "EmployeeMetricSnapshots");

            migrationBuilder.DropColumn(
                name: "TrendNote",
                table: "EmployeeMetricSnapshots");
        }
    }
}
