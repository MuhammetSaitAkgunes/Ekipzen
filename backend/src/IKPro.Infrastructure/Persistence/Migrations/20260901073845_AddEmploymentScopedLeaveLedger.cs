using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentScopedLeaveLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_LeaveBalanceSummary;");

            migrationBuilder.AddColumn<int>(
                name: "EmploymentPeriodId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnnualLeaveEntitlementDays",
                table: "EmployeeEmploymentPeriods",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LeaveAccrualManagedFrom",
                table: "EmployeeEmploymentPeriods",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaveLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmploymentPeriodId = table.Column<int>(type: "int", nullable: false),
                    LeaveRequestId = table.Column<int>(type: "int", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    ServiceYear = table.Column<int>(type: "int", nullable: true),
                    SourceKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveLedgerEntries_EmployeeEmploymentPeriods_EmploymentPeriodId",
                        column: x => x.EmploymentPeriodId,
                        principalTable: "EmployeeEmploymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveLedgerEntries_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE requestRow
                SET EmploymentPeriodId = period.Id
                FROM LeaveRequests AS requestRow
                CROSS APPLY (
                    SELECT TOP (1) candidate.Id
                    FROM EmployeeEmploymentPeriods AS candidate
                    WHERE candidate.EmployeeId = requestRow.EmployeeId
                      AND candidate.TenantId = requestRow.TenantId
                      AND candidate.StartDate <= requestRow.StartDate
                      AND (candidate.EndDate IS NULL OR candidate.EndDate >= requestRow.EndDate)
                    ORDER BY candidate.StartDate DESC
                ) AS period;

                IF EXISTS (SELECT 1 FROM LeaveRequests WHERE EmploymentPeriodId IS NULL)
                    THROW 51000, 'İstihdam dönemine bağlanamayan izin talebi var.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM LeaveBalances AS balance
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM EmployeeEmploymentPeriods AS candidate
                        WHERE candidate.EmployeeId = balance.EmployeeId
                          AND candidate.TenantId = balance.TenantId
                          AND candidate.StartDate <= DATEFROMPARTS(balance.[Year], 12, 31)
                          AND (candidate.EndDate IS NULL
                               OR candidate.EndDate >= DATEFROMPARTS(balance.[Year], 1, 1))
                    )
                )
                    THROW 51001, 'İstihdam dönemine bağlanamayan izin bakiyesi var.', 1;

                INSERT INTO LeaveLedgerEntries
                    (EmploymentPeriodId, LeaveRequestId, OccurredOn, [Type], [Days], ServiceYear,
                     SourceKey, SourceReference, TenantId, CreatedAtUtc)
                SELECT
                    period.Id, NULL,
                    CASE WHEN period.StartDate > DATEFROMPARTS(balance.[Year], 1, 1)
                         THEN period.StartDate ELSE DATEFROMPARTS(balance.[Year], 1, 1) END,
                    N'LegacyOpening',
                    balance.EntitledDays + balance.CarriedOverDays, NULL,
                    CONCAT(N'legacy-balance:', balance.Id), N'Takvim yılı bakiyesinden geçiş',
                    balance.TenantId, SYSUTCDATETIME()
                FROM LeaveBalances AS balance
                CROSS APPLY (
                    SELECT TOP (1) candidate.Id, candidate.StartDate
                    FROM EmployeeEmploymentPeriods AS candidate
                    WHERE candidate.EmployeeId = balance.EmployeeId
                      AND candidate.TenantId = balance.TenantId
                      AND candidate.StartDate <= DATEFROMPARTS(balance.[Year], 12, 31)
                      AND (candidate.EndDate IS NULL OR candidate.EndDate >= DATEFROMPARTS(balance.[Year], 1, 1))
                    ORDER BY candidate.StartDate DESC
                ) AS period;

                ;WITH RequestYears AS (
                    SELECT requestRow.Id, requestRow.EmploymentPeriodId, requestRow.TenantId,
                           requestRow.StartDate, requestRow.EndDate, YEAR(requestRow.StartDate) AS SegmentYear
                    FROM LeaveRequests AS requestRow
                    INNER JOIN LeaveTypes AS leaveType ON leaveType.Id = requestRow.LeaveTypeId
                    WHERE requestRow.[Status] = N'Approved'
                      AND leaveType.DeductsFromAnnualBalance = 1
                    UNION ALL
                    SELECT Id, EmploymentPeriodId, TenantId, StartDate, EndDate, SegmentYear + 1
                    FROM RequestYears
                    WHERE SegmentYear < YEAR(EndDate)
                )
                INSERT INTO LeaveLedgerEntries
                    (EmploymentPeriodId, LeaveRequestId, OccurredOn, [Type], [Days], ServiceYear,
                     SourceKey, SourceReference, TenantId, CreatedAtUtc)
                SELECT EmploymentPeriodId, Id,
                       CASE WHEN StartDate > DATEFROMPARTS(SegmentYear, 1, 1)
                            THEN StartDate ELSE DATEFROMPARTS(SegmentYear, 1, 1) END,
                       N'Usage',
                       -dbo.fn_WorkingDays(
                           CASE WHEN StartDate > DATEFROMPARTS(SegmentYear, 1, 1)
                                THEN StartDate ELSE DATEFROMPARTS(SegmentYear, 1, 1) END,
                           CASE WHEN EndDate < DATEFROMPARTS(SegmentYear, 12, 31)
                                THEN EndDate ELSE DATEFROMPARTS(SegmentYear, 12, 31) END,
                           TenantId),
                       NULL, CONCAT(N'leave-request:', Id, N':usage:', SegmentYear),
                       CONCAT(N'İzin talebi #', Id, N' kullanımı'), TenantId, SYSUTCDATETIME()
                FROM RequestYears
                WHERE dbo.fn_WorkingDays(
                    CASE WHEN StartDate > DATEFROMPARTS(SegmentYear, 1, 1)
                         THEN StartDate ELSE DATEFROMPARTS(SegmentYear, 1, 1) END,
                    CASE WHEN EndDate < DATEFROMPARTS(SegmentYear, 12, 31)
                         THEN EndDate ELSE DATEFROMPARTS(SegmentYear, 12, 31) END,
                    TenantId) > 0
                OPTION (MAXRECURSION 100);

                UPDATE period
                SET LeaveAccrualManagedFrom = COALESCE(legacy.ManagedFrom, period.StartDate)
                FROM EmployeeEmploymentPeriods AS period
                OUTER APPLY (
                    SELECT DATEFROMPARTS(MAX(balance.[Year]) + 1, 1, 1) AS ManagedFrom
                    FROM LeaveBalances AS balance
                    WHERE balance.EmployeeId = period.EmployeeId
                      AND balance.TenantId = period.TenantId
                      AND period.StartDate <= DATEFROMPARTS(balance.[Year], 12, 31)
                      AND (period.EndDate IS NULL OR period.EndDate >= DATEFROMPARTS(balance.[Year], 1, 1))
                ) AS legacy;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "EmploymentPeriodId",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmploymentPeriodId",
                table: "LeaveRequests",
                column: "EmploymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_EmploymentPeriodId",
                table: "LeaveLedgerEntries",
                column: "EmploymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_LeaveRequestId",
                table: "LeaveLedgerEntries",
                column: "LeaveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_TenantId_EmploymentPeriodId_OccurredOn",
                table: "LeaveLedgerEntries",
                columns: new[] { "TenantId", "EmploymentPeriodId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgerEntries_TenantId_SourceKey",
                table: "LeaveLedgerEntries",
                columns: new[] { "TenantId", "SourceKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_EmployeeEmploymentPeriods_EmploymentPeriodId",
                table: "LeaveRequests",
                column: "EmploymentPeriodId",
                principalTable: "EmployeeEmploymentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_EmployeeEmploymentPeriods_EmploymentPeriodId",
                table: "LeaveRequests");

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CarriedOverDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntitledDays = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_Year",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_TenantId",
                table: "LeaveBalances",
                column: "TenantId");

            migrationBuilder.Sql("""
                INSERT INTO LeaveBalances
                    (EmployeeId, [Year], EntitledDays, CarriedOverDays, TenantId, CreatedAtUtc)
                SELECT period.EmployeeId, YEAR(entryRow.OccurredOn),
                       SUM(CASE WHEN entryRow.[Days] > 0 AND entryRow.[Type] <> N'UsageReversal'
                                THEN entryRow.[Days] ELSE 0 END),
                       0, entryRow.TenantId, SYSUTCDATETIME()
                FROM LeaveLedgerEntries AS entryRow
                INNER JOIN EmployeeEmploymentPeriods AS period ON period.Id = entryRow.EmploymentPeriodId
                GROUP BY period.EmployeeId, YEAR(entryRow.OccurredOn), entryRow.TenantId;
                """);

            migrationBuilder.DropTable(name: "LeaveLedgerEntries");
            migrationBuilder.DropIndex(name: "IX_LeaveRequests_EmploymentPeriodId", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "EmploymentPeriodId", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "AnnualLeaveEntitlementDays", table: "EmployeeEmploymentPeriods");
            migrationBuilder.DropColumn(name: "LeaveAccrualManagedFrom", table: "EmployeeEmploymentPeriods");

            migrationBuilder.Sql("""
                CREATE VIEW dbo.vw_LeaveBalanceSummary
                AS
                SELECT b.TenantId, b.EmployeeId, b.[Year], b.EntitledDays, b.CarriedOverDays,
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
        }
    }
}
