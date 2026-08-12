using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

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
}
