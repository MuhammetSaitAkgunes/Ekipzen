using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekipzen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollSettingsEffectiveFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ÖNEMLİ: Sütun önce eklenip mevcut kayıtlar DEVREDİLİR, sonra Year düşürülür.
            // Scaffold'un ürettiği sıra (önce drop, sonra sabit varsayılanla add) tüm ayar
            // setlerini aynı tarihe çökertir; hem bordro geçmişi bozulur hem de yeni tekil
            // indeks ihlal edilir.
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "PayrollSettings",
                type: "date",
                nullable: true);

            // Yıl bazlı set, o yılın ilk gününden yürürlüğe girmiş sayılır.
            migrationBuilder.Sql(
                "UPDATE [PayrollSettings] SET [EffectiveFrom] = DATEFROMPARTS([Year], 1, 1);");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "PayrollSettings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_PayrollSettings_Year",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "PayrollSettings");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSettings_TenantId_EffectiveFrom",
                table: "PayrollSettings",
                columns: new[] { "TenantId", "EffectiveFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alırken de veri devredilir: yürürlük tarihinin yılı Year'a yazılır.
            // (Yıl içi ikinci setler geri dönüşte aynı yıla düşer; bu kayıp geri almanın
            // doğası gereğidir, bu yüzden geri alma öncesi yedek alınmalıdır.)
            migrationBuilder.DropIndex(
                name: "IX_PayrollSettings_TenantId_EffectiveFrom",
                table: "PayrollSettings");

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "PayrollSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [PayrollSettings] SET [Year] = YEAR([EffectiveFrom]);");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "PayrollSettings");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSettings_Year",
                table: "PayrollSettings",
                column: "Year");
        }
    }
}
