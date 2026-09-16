using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Kiracı kimliği platform veritabanına taşındı; uygulama veritabanındaki
    /// Tenants tablosu düşürülür.
    ///
    /// YIKICI: bu tablodaki satırlar silinir. Uygulandığı anda sistemde gerçek
    /// müşteri verisi YOKTU (yalnız demo kiracılar) ve demo veri seed'den
    /// yeniden üretilmektedir. Gerçek veri bulunan bir kuruluma uygulanmadan
    /// önce satırlar platform veritabanına kopyalanmalıdır.
    /// </summary>
    public partial class DropTenantsFromAppDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", nullable: false)
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
        }
    }
}
