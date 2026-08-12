using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Infrastructure.Persistence;

/// <summary>
/// Platform veritabanının (IKProPlatform) context'i. Yalnız kiracı kimliğini
/// tutar; İK verisi <see cref="AppDbContext"/> tarafındadır.
///
/// Migration'ları AYRI bir klasörde tutulur (Migrations/Platform) ve ayrı bir
/// __EFMigrationsHistory tablosuna yazılır — iki context'in geçmişi birbirine
/// karışmaz.
/// </summary>
public class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options), IPlatformDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantDirectoryEntry> Directory => Set<TenantDirectoryEntry>();

    /// <summary>
    /// Refresh token'lar burada, kiracı İK verisinin yanında (AppDbContext) DEĞİL:
    /// elinde yalnız token dizesi olan sunucu, kiracı DB'leri ayrıldığında hangi
    /// katalogda arayacağını başka türlü bilemez. Satırdaki TenantId yönlendirmeyi verir.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(64);
            b.HasIndex(t => t.Slug).IsUnique();

            // Okunabilirlik: durum veritabanında metin olarak durur (AppDbContext ile aynı kural).
            b.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        });

        builder.Entity<TenantDirectoryEntry>(b =>
        {
            b.HasKey(d => d.NormalizedEmail);
            b.Property(d => d.NormalizedEmail).HasMaxLength(256);
            b.HasIndex(d => d.TenantId);
        });

        builder.Entity<RefreshToken>(b =>
        {
            // Token sütununda ham değer değil SHA-256 hash saklanır (Base64, 44 karakter).
            b.Property(t => t.Token).IsRequired().HasMaxLength(256);
            b.Property(t => t.UserId).IsRequired().HasMaxLength(450);
            b.Ignore(t => t.IsActive);

            b.HasIndex(t => t.Token).IsUnique();

            // Kullanıcıya FK YOK: kullanıcı kiracı veritabanında. Kiracı bazlı
            // silme ve yönlendirme TenantId üzerinden yapılır.
            b.HasIndex(t => t.TenantId);
        });
    }
}
