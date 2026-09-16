using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Puantaj modülü SQL nesnesi: dbo.vw_MonthlyAttendanceSummary — çalışan-ay bazlı
    /// agregasyon (gün sayıları, çalışılan/fazla mesai dakika toplamları).
    /// Fazla mesai toplamı bordro motoruna (Faz 6) girdi olur.
    /// </summary>
    public partial class AttendanceSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_MonthlyAttendanceSummary
                AS
                SELECT
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
                GROUP BY a.EmployeeId, YEAR(a.WorkDate), MONTH(a.WorkDate);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_MonthlyAttendanceSummary;");
        }
    }
}
