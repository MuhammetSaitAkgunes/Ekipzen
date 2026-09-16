using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// İzin modülü SQL nesneleri:
    /// - dbo.fn_WorkingDays: hafta sonu + Holidays tablosu hariç iş-günü sayısı.
    /// - dbo.vw_LeaveBalanceSummary: hak ediş + devreden − onaylı/yıllıktan düşen
    ///   taleplerin toplamı (kullanılan canlı hesaplanır, denormalize kolon yok).
    /// </summary>
    public partial class LeaveSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        -- 1900-01-01 Pazartesi: % 7 → 0..4 hafta içi, 5-6 hafta sonu (DATEFIRST'ten bağımsız).
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_LeaveBalanceSummary;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.fn_WorkingDays;");
        }
    }
}
