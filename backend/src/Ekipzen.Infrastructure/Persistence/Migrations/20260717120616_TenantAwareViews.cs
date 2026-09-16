using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantAwareViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Holidays_Date",
                table: "Holidays");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_TenantId_Date",
                table: "Holidays",
                columns: new[] { "TenantId", "Date" },
                unique: true);

            // === Multi-tenant: SQL view'ları + iş-günü fonksiyonu global filtreyi BAYPAS eder;
            // her biri TenantId taşımalı/filtrelemeli. Önce bağımlı nesneler düşürülür. ===
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_DepartmentRisk;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_EmployeeRiskMetric;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_LeaveBalanceSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_MonthlyAttendanceSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PayrollPeriodSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ComplianceReadiness;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_WorkingDays;");

            // İş-günü fonksiyonu: tatiller artık kiracıya özel → @tenantId ile filtrele.
            migrationBuilder.Sql("""
                CREATE FUNCTION dbo.fn_WorkingDays(@start date, @end date, @tenantId int)
                RETURNS int
                AS
                BEGIN
                    IF @start IS NULL OR @end IS NULL OR @end < @start
                        RETURN 0;

                    DECLARE @count int = 0, @d date = @start;
                    WHILE @d <= @end
                    BEGIN
                        IF (DATEDIFF(day, '19000101', @d) % 7) < 5
                           AND NOT EXISTS (SELECT 1 FROM dbo.Holidays h
                                           WHERE h.[Date] = @d AND h.TenantId = @tenantId)
                            SET @count += 1;
                        SET @d = DATEADD(day, 1, @d);
                    END

                    RETURN @count;
                END
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_LeaveBalanceSummary
                AS
                SELECT
                    b.TenantId,
                    b.EmployeeId,
                    b.[Year],
                    b.EntitledDays,
                    b.CarriedOverDays,
                    ISNULL(u.UsedDays, 0) AS UsedDays,
                    b.EntitledDays + b.CarriedOverDays - ISNULL(u.UsedDays, 0) AS RemainingDays
                FROM dbo.LeaveBalances b
                OUTER APPLY (
                    SELECT SUM(r.[Days]) AS UsedDays
                    FROM dbo.LeaveRequests r
                    INNER JOIN dbo.LeaveTypes t ON t.Id = r.LeaveTypeId
                    WHERE r.EmployeeId = b.EmployeeId
                      AND YEAR(r.StartDate) = b.[Year]
                      AND r.[Status] = N'Approved'
                      AND t.DeductsFromAnnualBalance = 1
                ) u;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_MonthlyAttendanceSummary
                AS
                SELECT
                    a.TenantId,
                    a.EmployeeId,
                    YEAR(a.WorkDate)  AS [Year],
                    MONTH(a.WorkDate) AS [Month],
                    COUNT(*) AS TotalDays,
                    SUM(CASE WHEN a.[Status] <> N'Absent' THEN 1 ELSE 0 END) AS PresentDays,
                    SUM(CASE WHEN a.[Status] =  N'Absent' THEN 1 ELSE 0 END) AS AbsentDays,
                    SUM(CASE WHEN a.[Status] =  N'Late'   THEN 1 ELSE 0 END) AS LateDays,
                    SUM(a.WorkedMinutes)   AS TotalWorkedMinutes,
                    SUM(a.OvertimeMinutes) AS TotalOvertimeMinutes
                FROM dbo.AttendanceRecords a
                GROUP BY a.TenantId, a.EmployeeId, YEAR(a.WorkDate), MONTH(a.WorkDate);
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_PayrollPeriodSummary
                AS
                SELECT
                    pe.TenantId,
                    pe.PayrollPeriodId,
                    COUNT(*) AS EmployeeCount,
                    SUM(CASE WHEN pe.ApprovalStatus = N'Approved' THEN 1 ELSE 0 END) AS ApprovedCount,
                    ISNULL(SUM(r.GrossEarnings), 0)   AS TotalGross,
                    ISNULL(SUM(r.NetPay), 0)          AS TotalNet,
                    ISNULL(SUM(r.TotalDeductions), 0) AS TotalDeductions,
                    ISNULL(SUM(r.EmployerCost), 0)    AS TotalEmployerCost
                FROM dbo.PayrollEmployees pe
                LEFT JOIN dbo.PayrollResults r ON r.PayrollEmployeeId = pe.Id
                GROUP BY pe.TenantId, pe.PayrollPeriodId;
                """);

            // Kritik: bu view önceden TÜM kiracıları tek satırda topluyordu → sızıntı.
            // Artık TenantId'ye göre gruplanır (kiracı başına bir satır).
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_ComplianceReadiness
                AS
                SELECT
                    d.TenantId,
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
                FROM dbo.ComplianceDocuments d
                GROUP BY d.TenantId;
                """);

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
                    e.TenantId,
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

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_DepartmentRisk
                AS
                SELECT
                    r.TenantId,
                    r.DepartmentId,
                    r.DepartmentName,
                    COUNT(*) AS EmployeeCount,
                    CAST(ROUND(AVG(CAST(r.RiskScore AS FLOAT)), 0) AS INT) AS RiskScore,
                    SUM(CASE WHEN r.AttritionRisk = N'high' THEN 1 ELSE 0 END) AS HighAttritionCount,
                    SUM(CASE WHEN r.BurnoutRisk = N'high' THEN 1 ELSE 0 END) AS HighBurnoutCount
                FROM dbo.vw_EmployeeRiskMetric r
                GROUP BY r.TenantId, r.DepartmentId, r.DepartmentName;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Holidays_TenantId_Date",
                table: "Holidays");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                table: "Holidays",
                column: "Date",
                unique: true);

            // Kiracı-farkında nesneleri düşür, orijinal (kiracısız) tanımları geri yükle.
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_DepartmentRisk;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_EmployeeRiskMetric;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_LeaveBalanceSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_MonthlyAttendanceSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PayrollPeriodSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_ComplianceReadiness;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_WorkingDays;");

            migrationBuilder.Sql("""
                CREATE FUNCTION dbo.fn_WorkingDays(@start date, @end date)
                RETURNS int
                AS
                BEGIN
                    IF @start IS NULL OR @end IS NULL OR @end < @start
                        RETURN 0;
                    DECLARE @count int = 0, @d date = @start;
                    WHILE @d <= @end
                    BEGIN
                        IF (DATEDIFF(day, '19000101', @d) % 7) < 5
                           AND NOT EXISTS (SELECT 1 FROM dbo.Holidays h WHERE h.[Date] = @d)
                            SET @count += 1;
                        SET @d = DATEADD(day, 1, @d);
                    END
                    RETURN @count;
                END
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_LeaveBalanceSummary
                AS
                SELECT
                    b.EmployeeId, b.[Year], b.EntitledDays, b.CarriedOverDays,
                    ISNULL(u.UsedDays, 0) AS UsedDays,
                    b.EntitledDays + b.CarriedOverDays - ISNULL(u.UsedDays, 0) AS RemainingDays
                FROM dbo.LeaveBalances b
                OUTER APPLY (
                    SELECT SUM(r.[Days]) AS UsedDays
                    FROM dbo.LeaveRequests r
                    INNER JOIN dbo.LeaveTypes t ON t.Id = r.LeaveTypeId
                    WHERE r.EmployeeId = b.EmployeeId AND YEAR(r.StartDate) = b.[Year]
                      AND r.[Status] = N'Approved' AND t.DeductsFromAnnualBalance = 1
                ) u;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_MonthlyAttendanceSummary
                AS
                SELECT
                    a.EmployeeId, YEAR(a.WorkDate) AS [Year], MONTH(a.WorkDate) AS [Month],
                    COUNT(*) AS TotalDays,
                    SUM(CASE WHEN a.[Status] <> N'Absent' THEN 1 ELSE 0 END) AS PresentDays,
                    SUM(CASE WHEN a.[Status] =  N'Absent' THEN 1 ELSE 0 END) AS AbsentDays,
                    SUM(CASE WHEN a.[Status] =  N'Late'   THEN 1 ELSE 0 END) AS LateDays,
                    SUM(a.WorkedMinutes) AS TotalWorkedMinutes,
                    SUM(a.OvertimeMinutes) AS TotalOvertimeMinutes
                FROM dbo.AttendanceRecords a
                GROUP BY a.EmployeeId, YEAR(a.WorkDate), MONTH(a.WorkDate);
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_PayrollPeriodSummary
                AS
                SELECT
                    pe.PayrollPeriodId, COUNT(*) AS EmployeeCount,
                    SUM(CASE WHEN pe.ApprovalStatus = N'Approved' THEN 1 ELSE 0 END) AS ApprovedCount,
                    ISNULL(SUM(r.GrossEarnings), 0) AS TotalGross,
                    ISNULL(SUM(r.NetPay), 0) AS TotalNet,
                    ISNULL(SUM(r.TotalDeductions), 0) AS TotalDeductions,
                    ISNULL(SUM(r.EmployerCost), 0) AS TotalEmployerCost
                FROM dbo.PayrollEmployees pe
                LEFT JOIN dbo.PayrollResults r ON r.PayrollEmployeeId = pe.Id
                GROUP BY pe.PayrollPeriodId;
                """);

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
                         ELSE CAST(ROUND(100.0 * SUM(CASE WHEN d.Status = N'Completed' THEN 1 ELSE 0 END) / COUNT(*), 0) AS INT)
                    END AS DocumentComplianceScore,
                    CASE WHEN COUNT(*) = 0 THEN 100
                         ELSE CASE WHEN 100 - SUM(CASE WHEN d.Status = N'Missing' THEN 6 WHEN d.Status = N'DueSoon' THEN 3 WHEN d.Status = N'InReview' THEN 2 ELSE 0 END) < 0
                                   THEN 0
                                   ELSE 100 - SUM(CASE WHEN d.Status = N'Missing' THEN 6 WHEN d.Status = N'DueSoon' THEN 3 WHEN d.Status = N'InReview' THEN 2 ELSE 0 END)
                              END
                    END AS ReadinessScore
                FROM dbo.ComplianceDocuments d;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_EmployeeRiskMetric
                AS
                WITH LatestSnapshot AS (
                    SELECT s.*, ROW_NUMBER() OVER (PARTITION BY s.EmployeeId ORDER BY s.PeriodDate DESC) AS RowNo
                    FROM dbo.EmployeeMetricSnapshots s
                )
                SELECT
                    e.Id AS EmployeeId, e.FirstName + N' ' + e.LastName AS FullName, e.Title,
                    e.DepartmentId, d.Name AS DepartmentName, e.ManagerId,
                    m.FirstName + N' ' + m.LastName AS ManagerName,
                    s.PeriodDate, s.AbsencePct, s.LatenessPct, s.OvertimePct, s.UnusedLeavePct,
                    s.Pulse, s.Performance, s.RoleCriticality, s.TrendNote, s.RecommendedAction,
                    CAST(ROUND(s.AbsencePct * 0.18 + s.LatenessPct * 0.14 + s.OvertimePct * 0.20 +
                        s.UnusedLeavePct * 0.15 + (100 - s.Pulse) * 0.18 + (100 - s.Performance) * 0.15, 0) AS INT) AS RiskScore,
                    CASE WHEN s.Pulse < 55 OR s.RoleCriticality > 85 THEN N'high'
                         WHEN s.Pulse < 65 OR s.RoleCriticality > 75 THEN N'medium' ELSE N'low' END AS AttritionRisk,
                    CASE WHEN s.OvertimePct > 65 AND s.UnusedLeavePct > 65 THEN N'high'
                         WHEN s.OvertimePct > 55 OR s.UnusedLeavePct > 65 THEN N'medium' ELSE N'low' END AS BurnoutRisk
                FROM LatestSnapshot s
                JOIN dbo.Employees e ON e.Id = s.EmployeeId
                JOIN dbo.Departments d ON d.Id = e.DepartmentId
                LEFT JOIN dbo.Employees m ON m.Id = e.ManagerId
                WHERE s.RowNo = 1;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_DepartmentRisk
                AS
                SELECT
                    r.DepartmentId, r.DepartmentName, COUNT(*) AS EmployeeCount,
                    CAST(ROUND(AVG(CAST(r.RiskScore AS FLOAT)), 0) AS INT) AS RiskScore,
                    SUM(CASE WHEN r.AttritionRisk = N'high' THEN 1 ELSE 0 END) AS HighAttritionCount,
                    SUM(CASE WHEN r.BurnoutRisk = N'high' THEN 1 ELSE 0 END) AS HighBurnoutCount
                FROM dbo.vw_EmployeeRiskMetric r
                GROUP BY r.DepartmentId, r.DepartmentName;
                """);
        }
    }
}
