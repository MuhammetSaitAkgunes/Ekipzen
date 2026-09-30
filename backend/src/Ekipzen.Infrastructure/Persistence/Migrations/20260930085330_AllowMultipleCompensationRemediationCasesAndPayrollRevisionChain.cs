using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleCompensationRemediationCasesAndPayrollRevisionChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeCompensationRemediationCases_TenantId_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases");

            migrationBuilder.AddColumn<int>(
                name: "BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollApprovedRowRevisions_BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions",
                column: "BasedOnRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationRemediationCases_TenantId_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases",
                columns: new[] { "TenantId", "CompensationPackageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollApprovedRowRevisions_PayrollApprovedRowRevisions_BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions",
                column: "BasedOnRevisionId",
                principalTable: "PayrollApprovedRowRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollApprovedRowRevisions_PayrollApprovedRowRevisions_BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollApprovedRowRevisions_BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCompensationRemediationCases_TenantId_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases");

            migrationBuilder.DropColumn(
                name: "BasedOnRevisionId",
                table: "PayrollApprovedRowRevisions");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompensationRemediationCases_TenantId_CompensationPackageId",
                table: "EmployeeCompensationRemediationCases",
                columns: new[] { "TenantId", "CompensationPackageId" },
                unique: true);
        }
    }
}
