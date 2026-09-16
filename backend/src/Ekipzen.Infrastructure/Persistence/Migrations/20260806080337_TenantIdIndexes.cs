using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId",
                table: "Positions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollResults_TenantId",
                table: "PayrollResults",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_TenantId",
                table: "PayrollPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_TenantId",
                table: "PayrollEmployees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSettings_TenantId",
                table: "NotificationSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_TenantId",
                table: "LeaveTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_TenantId",
                table: "LeaveRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_TenantId",
                table: "LeaveBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewNotes_TenantId",
                table: "InterviewNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTaxBrackets_TenantId",
                table: "IncomeTaxBrackets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalActions_TenantId",
                table: "GlobalActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EngagementMetrics_TenantId",
                table: "EngagementMetrics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeProfiles_TenantId",
                table: "EmployeeProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMetricSnapshots_TenantId",
                table: "EmployeeMetricSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_TenantId",
                table: "EmployeeDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId",
                table: "Departments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDocuments_TenantId",
                table: "ComplianceDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_TenantId",
                table: "CompanyProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkills_TenantId",
                table: "CandidateSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_TenantId",
                table: "Candidates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateHistory_TenantId",
                table: "CandidateHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_TenantId",
                table: "CandidateExperiences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateEvaluations_TenantId",
                table: "CandidateEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId",
                table: "AttendanceRecords",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Positions_TenantId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollResults_TenantId",
                table: "PayrollResults");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_TenantId",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEmployees_TenantId",
                table: "PayrollEmployees");

            migrationBuilder.DropIndex(
                name: "IX_NotificationSettings_TenantId",
                table: "NotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTypes_TenantId",
                table: "LeaveTypes");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_TenantId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveBalances_TenantId",
                table: "LeaveBalances");

            migrationBuilder.DropIndex(
                name: "IX_InterviewNotes_TenantId",
                table: "InterviewNotes");

            migrationBuilder.DropIndex(
                name: "IX_IncomeTaxBrackets_TenantId",
                table: "IncomeTaxBrackets");

            migrationBuilder.DropIndex(
                name: "IX_GlobalActions_TenantId",
                table: "GlobalActions");

            migrationBuilder.DropIndex(
                name: "IX_EngagementMetrics_TenantId",
                table: "EngagementMetrics");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeProfiles_TenantId",
                table: "EmployeeProfiles");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeMetricSnapshots_TenantId",
                table: "EmployeeMetricSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_TenantId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_ComplianceDocuments_TenantId",
                table: "ComplianceDocuments");

            migrationBuilder.DropIndex(
                name: "IX_CompanyProfiles_TenantId",
                table: "CompanyProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CandidateSkills_TenantId",
                table: "CandidateSkills");

            migrationBuilder.DropIndex(
                name: "IX_Candidates_TenantId",
                table: "Candidates");

            migrationBuilder.DropIndex(
                name: "IX_CandidateHistory_TenantId",
                table: "CandidateHistory");

            migrationBuilder.DropIndex(
                name: "IX_CandidateExperiences_TenantId",
                table: "CandidateExperiences");

            migrationBuilder.DropIndex(
                name: "IX_CandidateEvaluations_TenantId",
                table: "CandidateEvaluations");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_TenantId",
                table: "AttendanceRecords");
        }
    }
}
