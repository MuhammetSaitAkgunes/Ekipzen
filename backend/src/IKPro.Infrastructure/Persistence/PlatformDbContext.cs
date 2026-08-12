using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Settings;
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

    /// <inheritdoc cref="IPlatformDbContext.Subscriptions"/>
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);

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

            // SQL Server tanımlayıcı sınırı 128. Kasıtlı olarak NULLABLE ve
            // TEKİL DEĞİL: henüz ayrılmamış kiracılarda boştur ve birden çok
            // boş satır olacaktır (SQL Server'da unique index NULL'ları tek
            // sayardı — kiracıların çoğu boşken ikinci kiracı eklenemezdi).
            b.Property(t => t.DatabaseName).HasMaxLength(128);
        });

        builder.Entity<TenantDirectoryEntry>(b =>
        {
            b.HasKey(d => d.NormalizedEmail);
            b.Property(d => d.NormalizedEmail).HasMaxLength(256);
            b.HasIndex(d => d.TenantId);
        });

        builder.Entity<Subscription>(b =>
        {
            b.Property(s => s.Plan).IsRequired().HasMaxLength(64);
            b.Property(s => s.PlanName).HasMaxLength(128);
            b.Property(s => s.BillingCycle).HasMaxLength(32);
            b.Property(s => s.PaymentMethodMasked).HasMaxLength(64);

            // Kiracı başına EN FAZLA BİR abonelik. Tekillik burada, veritabanı
            // seviyesinde uygulanır: "tekil kayıt" varsayımı kodda birden çok yerde
            // (FirstOrDefault) var ve ikinci bir satır sessizce yanlış planı
            // gösterirdi.
            b.HasIndex(s => s.TenantId).IsUnique();
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
