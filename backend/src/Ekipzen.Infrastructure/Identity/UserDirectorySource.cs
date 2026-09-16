using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Infrastructure.Identity;

/// <summary>Kiracının kullanıcı e-postalarını uygulama veritabanından okur.</summary>
public sealed class UserDirectorySource(ITenantScopeFactory tenantScopeFactory) : IUserDirectorySource
{
    public async Task<IReadOnlyList<string>> NormalizedEmailsAsync(
        int tenantId, CancellationToken cancellationToken)
    {
        // Bu sınıf `RebuildDirectoryCommandHandler` tarafından TAM OLARAK dizini kurtarmak
        // için çağrılır (bkz. RebuildDirectoryCommand). Constructor'da doğrudan AppDbContext
        // enjekte edilseydi bu, çağıranın HTTP kapsamına bağlı AMBIENT context olurdu —
        // platform-key'li, anonim, kiracısız olabilir. Faz 1b'de tüm kiracılar aynı DB'yi
        // paylaştığından ve sorguda açık `u.TenantId == tenantId` filtresi olduğundan bu
        // bugün ZARARSIZ görünür. Ama Faz 2'de kiracı başına DB'ye geçilince ambient context
        // YANLIŞ katalogda koşar → 0 e-posta döner → `TenantDirectory.RebuildForTenantAsync`
        // önce eski satırları silip SONRA hiç yazmadığı için o kiracının TÜM dizin satırları
        // silinir ve HİÇBİRİ geri yazılmaz → kiracının tüm kullanıcıları kilitlenir. Bu, tam
        // olarak onları kurtarmak için çalıştırılan prosedürün İÇİNDE olur. Bu yüzden (bkz.
        // TenantPurger'daki aynı gerekçe) taze, `tenantId`'ye SABİTLENMİŞ bir kapsamdan
        // AppDbContext çözülür — ambient context'e asla güvenilmez.
        using var kapsam = tenantScopeFactory.Create(tenantId);
        var context = kapsam.Services.GetRequiredService<AppDbContext>();

        return await context.Set<ApplicationUser>()
            .Where(u => u.TenantId == tenantId && u.NormalizedEmail != null)
            .Select(u => u.NormalizedEmail!)
            .ToListAsync(cancellationToken);
    }
}
