namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// HTTP dışı bağlamlarda (arka plan servisi, seed, purge, test yardımcıları) belirli bir
/// kiracıya SABİTLENMİŞ bir DI kapsamı açar. Kiracı, kapsam çağırana dönmeden ÖNCE
/// <see cref="ICurrentTenant.Impersonate"/> ile sabitlenir — bu yüzden kapsamdan çözülen
/// HER servis (özellikle <c>AppDbContext</c>: bağlantısı kapsam başına, ilk çözümde
/// <see cref="ICurrentTenant"/>'tan okunur, bkz. <c>Infrastructure.DependencyInjection</c>)
/// doğru kiracıyı görür.
///
/// Bunun var olma nedeni: "önce Impersonate çağır, sonra servisi DI'dan çöz" sırasına
/// GÜVENMEK yerine bu sırayı YANLIŞ YAPMAYI İMKÂNSIZ kılmak. Impersonate'i context/servis
/// zaten çözülmüşken çağırmak SESSİZCE yanlış kiracının bağlantısıyla çalışmaya devam eder
/// — hata fırlatmaz, sadece yanlış veritabanına yazar/okur.
/// </summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// Verilen kiracıya sabitlenmiş, kullan-ve-at (<c>using</c> ile) bir DI kapsamı açar.
    /// Dispose edilene kadar kapsam içindeki tüm scoped servisler yaşar.
    /// </summary>
    ITenantScope Create(int tenantId);
}

/// <summary>
/// <see cref="ITenantScopeFactory.Create"/> ile açılan, belirli bir kiracıya sabitlenmiş
/// DI kapsamı. <see cref="IDisposable.Dispose"/> içindeki tüm scoped servisleri serbest
/// bırakır (ör. <c>AppDbContext</c>).
/// </summary>
public interface ITenantScope : IDisposable
{
    /// <summary>Bu kapsamdan servis çözmek için kullanılan sağlayıcı.</summary>
    IServiceProvider Services { get; }
}
