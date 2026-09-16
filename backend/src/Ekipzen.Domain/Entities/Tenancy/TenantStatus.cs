namespace Ekipzen.Domain.Entities.Tenancy;

/// <summary>
/// Kiracının yaşam döngüsü durumu. Yalnız <see cref="Active"/> erişime izin verir.
///
/// İki durumlu bir bayrak (eski IsActive) yetersizdi: "kayıt henüz doğrulanmadı"
/// ile "kiracı kapatıldı" aynı değere sıkışıyordu ve bakım/geri yükleme sırasında
/// kiracıyı dondurmanın ayrı bir yolu yoktu.
/// </summary>
public enum TenantStatus
{
    /// <summary>Kurulum sürüyor ya da yarıda kaldı — erişim kapalı, müşteri verisi yok.</summary>
    Provisioning,

    /// <summary>Normal çalışma durumu — tek erişilebilir durum.</summary>
    Active,

    /// <summary>Bakım ya da geri yükleme sürüyor — erişim geçici olarak kapalı.</summary>
    Frozen,

    /// <summary>Silme sürüyor ya da yarıda kaldı — erişim kalıcı olarak kapalı.</summary>
    Purging,
}
