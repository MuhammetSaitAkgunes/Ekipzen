using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IKPro.Application.Common.Interfaces;

/// <summary>
/// Platform veritabanı: kiracı kimliği ve yönlendirme. Kiracı İK verisi burada
/// DEĞİLDİR — o <see cref="IApplicationDbContext"/> tarafındadır.
///
/// Bu context'te global kiracı filtresi YOKTUR: platform tablolarının kendisi
/// kiracı-üstüdür. Kiracıya göre süzmek gerektiğinde açıkça yazılır.
/// </summary>
public interface IPlatformDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<TenantDirectoryEntry> Directory { get; }

    /// <summary>
    /// Refresh token'lar burada, kiracı İK verisinin yanında (IApplicationDbContext)
    /// DEĞİL: elinde yalnız token dizesi olan sunucu, kiracı DB'leri ayrıldığında
    /// hangi katalogda arayacağını başka türlü bilemez. Satırdaki TenantId
    /// yönlendirmeyi verir.
    /// </summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Açık transaction — provizyonun ilk adımı için.
    ///
    /// Neden gerekti: kiracı satırı ile yönlendirme rezervasyonu iki ayrı
    /// <c>SaveChanges</c> gerektirir (rezervasyon, kiracının veritabanı tarafından
    /// üretilen kimliğini bilmek zorunda). Bunlar tek bir işlem gibi davranmazsa,
    /// rezervasyon çakışırsa geriye sahipsiz bir kiracı satırı kalır — hem
    /// slug'ı işgal eder hem de temizlenmesi gereken bir artık olur.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
