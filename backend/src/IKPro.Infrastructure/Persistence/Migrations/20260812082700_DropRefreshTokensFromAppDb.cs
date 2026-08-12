using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKPro.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Refresh token'lar platform veritabanına taşındı (bkz. Platform/…_RefreshTokensToPlatform);
    /// uygulama veritabanındaki RefreshTokens tablosu artık gereksiz, düşürülür.
    ///
    /// YIKICI: bu tablodaki satırlar KOPYALANMADAN silinir — satırlar kaybolur,
    /// tüm aktif oturumlar geçersizleşir ve kullanıcılar bir kez yeniden giriş
    /// yapmak zorunda kalır. Uygulandığı anda sistemde gerçek müşteri verisi
    /// YOKTU (yalnız demo/test kiracıları), bu yüzden veri kaybı kabul edilmiştir
    /// (bkz. DropTenantsFromAppDb ile aynı emsal). Gerçek kullanıcı oturumu olan
    /// bir kuruluma uygulanmadan önce satırlar platform veritabanına
    /// kopyalanmalı, ya da kullanıcılara oturumlarının sıfırlanacağı önceden
    /// bildirilmelidir.
    /// </summary>
    public partial class DropRefreshTokensFromAppDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");
        }
    }
}
