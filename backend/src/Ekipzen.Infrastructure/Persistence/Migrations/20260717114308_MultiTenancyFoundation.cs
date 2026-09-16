using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenancyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RefreshTokens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Positions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PayrollResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PayrollPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PayrollEmployees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "NotificationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LeaveBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InterviewNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "IncomeTaxBrackets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Holidays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "GlobalActions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EngagementMetrics",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EmployeeProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EmployeeMetricSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ComplianceDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CompanyProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CandidateSkills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Candidates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CandidateHistory",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CandidateExperiences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CandidateEvaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AttendanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            // Multi-tenant backfill: mevcut (tek-kiracı) veri bir "varsayılan" kiracıya
            // taşınır. TenantId sütunu olan HER tabloyu dinamik olarak günceller ki yeni
            // tablo eklenince burayı güncellemeyi unutmak sorun olmasın. Taze veritabanında
            // (henüz veri yok) yalnız varsayılan kiracıyı oluşturur; seed onu impersone eder.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Tenants])
                BEGIN
                    INSERT INTO [Tenants] ([Name], [Slug], [IsActive], [CreatedAtUtc])
                    VALUES (N'Demo Şirket', N'demo', 1, SYSUTCDATETIME());
                END

                DECLARE @tid INT = (SELECT TOP 1 [Id] FROM [Tenants] ORDER BY [Id]);

                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql = @sql +
                    'UPDATE [' + c.TABLE_NAME + '] SET [TenantId] = @t WHERE [TenantId] = 0;' + CHAR(10)
                FROM INFORMATION_SCHEMA.COLUMNS c
                JOIN INFORMATION_SCHEMA.TABLES t
                    ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_TYPE = 'BASE TABLE'
                WHERE c.COLUMN_NAME = 'TenantId' AND c.TABLE_NAME <> 'Tenants';

                IF (@sql <> N'') EXEC sp_executesql @sql, N'@t INT', @t = @tid;
            ");

            // Audit trigger'ları ham SQL ile AuditLog satırı ekler → interceptor/global
            // filtreyi baypas eder. Multi-tenant'ta AuditLog satırı, etkilenen kaydın
            // TenantId'sini taşımalı; yoksa yönetsel denetim izi kiracı filtresiyle boşalır.
            // Trigger'ları TenantId içerecek şekilde yeniden oluştur.
            var auditTargets = new (string Table, string Trigger, string Module, string EntityName)[]
            {
                ("Employees", "TR_Employees_Audit", "Personel", "Employee"),
                ("LeaveRequests", "TR_LeaveRequests_Audit", "İzin", "LeaveRequest"),
                ("PayrollEmployees", "TR_PayrollEmployees_Audit", "Bordro", "PayrollEmployee"),
                ("ComplianceDocuments", "TR_ComplianceDocuments_Audit", "Uyum", "ComplianceDocument"),
            };

            foreach (var (table, trigger, module, entityName) in auditTargets)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS dbo.{trigger};");
                migrationBuilder.Sql($"""
                    CREATE TRIGGER dbo.{trigger}
                    ON dbo.{table}
                    AFTER INSERT, UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        DECLARE @action nvarchar(64) =
                            CASE
                                WHEN EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted) THEN N'update'
                                WHEN EXISTS (SELECT 1 FROM inserted) THEN N'insert'
                                ELSE N'delete'
                            END;

                        INSERT INTO dbo.AuditLogs (TenantId, Actor, [Action], Module, Detail, EntityName, EntityId, CreatedAtUtc)
                        SELECT
                            COALESCE(i.TenantId, d.TenantId, 0),
                            COALESCE(i.UpdatedBy, i.CreatedBy, d.UpdatedBy, d.CreatedBy, ORIGINAL_LOGIN()),
                            @action,
                            N'{module}',
                            NULL,
                            N'{entityName}',
                            CAST(COALESCE(i.Id, d.Id) AS nvarchar(64)),
                            SYSUTCDATETIME()
                        FROM inserted i
                        FULL OUTER JOIN deleted d ON d.Id = i.Id;
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollResults");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollEmployees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InterviewNotes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IncomeTaxBrackets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GlobalActions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EngagementMetrics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeMetricSnapshots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ComplianceDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CompanyProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CandidateSkills");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CandidateHistory");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CandidateExperiences");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CandidateEvaluations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AttendanceRecords");
        }
    }
}
