using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Kiracı sınırını aşan tekil indeksleri kiracı kapsamına alır.
    ///
    /// Düzeltilen davranış: departman adı, TC Kimlik No ve bordro dönemi (yıl+ay)
    /// SİSTEM GENELİNDE tekildi. Sonuç olarak ikinci müşteri, birincinin kullandığı
    /// departman adını açamıyor, aynı kişiyi işe alamıyor ve aynı ayın bordro
    /// dönemini hiç oluşturamıyordu.
    ///
    /// Güvenlik notu: yeni indeksler eskilerinden DAHA GEVŞEKTİR (aynı sütunlar +
    /// TenantId). Eski kısıtı sağlayan her veri yenisini de sağlar; bu yüzden
    /// oluşturma adımı mevcut veride kırılamaz.
    ///
    /// Tekil indeksler TenantId ile başladığı için ayrı IX_*_TenantId indeksleri
    /// gereksizleşti ve düşürüldü — AppDbContext'teki otomatik indeks kuralıyla
    /// aynı mantık: TenantId ile başlayan indeks zaten varsa ikincisi eklenmez.
    /// </summary>
    public partial class TenantScopedUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_TenantId",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_Year_Month",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Employees_NationalId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_TenantId_Year_Month",
                table: "PayrollPeriods",
                columns: new[] { "TenantId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_NationalId",
                table: "Employees",
                columns: new[] { "TenantId", "NationalId" },
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_Name",
                table: "Departments",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_TenantId_Year_Month",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_NationalId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId_Name",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_TenantId",
                table: "PayrollPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_Year_Month",
                table: "PayrollPeriods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_NationalId",
                table: "Employees",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId",
                table: "Departments",
                column: "TenantId");
        }
    }
}
