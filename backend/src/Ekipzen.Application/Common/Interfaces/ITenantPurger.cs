namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracı verisini kalıcı siler (KVKK unutulma hakkı). Tüm ITenantScoped tablolar +
/// kullanıcılar + refresh token'lar + fiziksel dosyalar + kiracı satırı, tek transaction.
/// Yalnız hedef kiracı etkilenir. Infrastructure'da EF metadata'sıyla implemente edilir.
/// </summary>
public interface ITenantPurger
{
    /// <summary>
    /// Verili kiracının tüm verisini siler. DB verisi (kiracı satırı, dizin, kullanıcılar,
    /// kiracı-kapsamlı tablolar) her koşulda silinir. Dönüş değeri yalnız FİZİKSEL DOSYA
    /// alanının silinip silinemediğini bildirir: <c>true</c> = dosyalar başarıyla silindi,
    /// <c>false</c> = DB temizlendi ama dosya alanı silinemedi (hata loglanmıştır; elle
    /// temizlik gerekir — bkz. <see cref="Infrastructure.Persistence.TenantPurger"/>). Dosya
    /// silme hatası purge'ü YARIDA KESMEZ, ama çağırana SESSİZ KALMAZ — operatör bu dönüş
    /// değerini görmeden "purge tamamlandı" sonucuna güvenemez (KVKK: PII dosyaların diskte
    /// kalması sessizce geçilemez).
    /// </summary>
    Task<bool> PurgeAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Doğrulanmamış self-servis kiracıları siler: pasif + <paramref name="createdBeforeUtc"/>'den
    /// eski + hiçbir kullanıcısı şifre belirlememiş (davet hiç kabul edilmemiş). Şifreli
    /// kullanıcısı olan (askıya alınmış) kiracılar korunur. Silinen kiracı sayısını döndürür.
    /// </summary>
    Task<int> PurgeUnverifiedAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken);
}
