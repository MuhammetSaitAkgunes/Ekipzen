using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// Kütüğün önbelleği SINGLETON'dır (tüm istekler paylaşır), ama platform
/// context'i SCOPED'dır. Bu yüzden okuma anında kendi kapsamını açar —
/// singleton'ın scoped bir bağımlılığı yakalaması (captive dependency)
/// bağlantıyı ilk isteğe yapıştırırdı.
/// </summary>
public sealed class TenantRegistry(IServiceScopeFactory scopeFactory, IMemoryCache cache) : ITenantRegistry
{
    // Kısa TTL yalnız emniyet ağıdır: asıl tazeleme Invalidate ile ANINDA olur.
    // Bir yol Invalidate çağırmayı unutursa değişiklik en geç bu süre içinde görünür.
    private static readonly TimeSpan Omur = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Durum ve katalog adı TEK kayıtta önbelleklenir: ikisi de aynı satırdan
    /// geliyor, ayrı ayrı önbelleklemek aynı satırı iki kez okumak olurdu.
    /// </summary>
    private sealed record Kayit(TenantStatus Status, string? DatabaseName);

    private static string Anahtar(int tenantId) => $"kiraci-{tenantId}";

    public async Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken)
    {
        if (Onbellekte(tenantId, out var onbellekli))
        {
            return onbellekli?.Status;
        }

        var kayit = await OkuAsync(tenantId, cancellationToken);
        cache.Set(Anahtar(tenantId), kayit, Omur);
        return kayit?.Status;
    }

    public string? GetDatabaseName(int tenantId)
    {
        if (Onbellekte(tenantId, out var onbellekli))
        {
            return onbellekli?.DatabaseName;
        }

        // SENKRON sorgu — bilinçli. Çağıran (bağlantı çözücü) AddDbContext
        // yapılandırma geri çağrısının içinden çalışır ve orası async'e
        // çevrilemez. Sync-over-async (.Result / .GetAwaiter().GetResult())
        // bir thread'i bloke ederdi; burada bloke edilen thread zaten
        // senkron olarak bekliyor, üstelik bu yol yalnız önbellek ıskasında
        // (kiracı başına ~5 dakikada bir) çalışıyor.
        using var scope = scopeFactory.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kayit = platform.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new Kayit(t.Status, t.DatabaseName))
            .FirstOrDefault();

        cache.Set(Anahtar(tenantId), kayit, Omur);
        return kayit?.DatabaseName;
    }

    public void Invalidate(int tenantId) => cache.Remove(Anahtar(tenantId));

    // "Kiracı yok" da önbelleklenebilir bir cevaptır (kayıt null olarak saklanır).
    // Bu yüzden "önbellekte kayıt VAR MI" ile "kaydın değeri null MI" ayrımı
    // korunur — karıştırılsaydı var olmayan bir kiracı her sorguda veritabanına
    // giderdi ve geçersiz bir kimlikle yapılan istek ucuz bir DoS yüzeyi olurdu.
    private bool Onbellekte(int tenantId, out Kayit? kayit) =>
        cache.TryGetValue(Anahtar(tenantId), out kayit);

    private async Task<Kayit?> OkuAsync(int tenantId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        return await platform.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => new Kayit(t.Status, t.DatabaseName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
