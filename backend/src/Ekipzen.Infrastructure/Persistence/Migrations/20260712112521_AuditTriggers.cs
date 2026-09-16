using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Kritik tablolara (Employees, LeaveRequests, PayrollEmployees, ComplianceDocuments)
    /// append-only audit trigger'ları ekler. Aktör, interceptor'ın yazdığı
    /// CreatedBy/UpdatedBy kolonlarından okunur; yoksa ORIGINAL_LOGIN() kullanılır.
    /// </summary>
    public partial class AuditTriggers : Migration
    {
        private static readonly (string Table, string Trigger, string Module, string EntityName)[] Targets =
        [
            ("Employees", "TR_Employees_Audit", "Personel", "Employee"),
            ("LeaveRequests", "TR_LeaveRequests_Audit", "İzin", "LeaveRequest"),
            ("PayrollEmployees", "TR_PayrollEmployees_Audit", "Bordro", "PayrollEmployee"),
            ("ComplianceDocuments", "TR_ComplianceDocuments_Audit", "Uyum", "ComplianceDocument"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, trigger, module, entityName) in Targets)
            {
                // CREATE TRIGGER batch'in ilk komutu olmalı: her trigger ayrı Sql() çağrısı.
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

                        INSERT INTO dbo.AuditLogs (Actor, [Action], Module, Detail, EntityName, EntityId, CreatedAtUtc)
                        SELECT
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
            foreach (var (_, trigger, _, _) in Targets)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS dbo.{trigger};");
            }
        }
    }
}
