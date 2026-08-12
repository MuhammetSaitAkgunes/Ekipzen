namespace IKPro.Domain.Entities.Tenancy;

/// <summary>
/// Bir müşteri şirketi (kiracı). Multi-tenant izolasyonun köküdür.
/// Bilinçli olarak <c>BaseEntity</c>'den TÜREMEZ — kendisi kiracıya bağlı değildir
/// (kiracının kiracısı olmaz), bu yüzden global TenantId filtresine tabi tutulmaz.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    /// <summary>Görünen şirket adı, ör. "Acme Teknoloji A.Ş.".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL/alt alan dostu benzersiz kısa ad, ör. "acme".</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Yaşam döngüsü durumu. Yalnız <see cref="TenantStatus.Active"/> erişime izin verir.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    /// <summary>
    /// Kiracının kendi veritabanının katalog adı, ör. "IKPro_acme".
    ///
    /// <b>BOŞ OLABİLİR</b> ve bu bir hata değildir: henüz ayrılmamış (paylaşılan
    /// veritabanında duran) kiracılarda boştur ve bağlantı çözücü onları paylaşılan
    /// dizeye yönlendirir. Böylece ayrılma kiracı kiracı, tek seferde değil
    /// kademeli yapılabilir.
    ///
    /// Provizyonda BİR KEZ üretilir ve bir daha DEĞİŞMEZ. Değişmezliği önemli:
    /// kütük bu adı önbellekliyor ve yedekleme/geri yükleme prosedürleri kiracıyı
    /// bu adla buluyor — sonradan değişmesi, adı bilen her yeri sessizce yanıltırdı.
    ///
    /// Ad SQL'e enterpole edilir (CREATE/DROP DATABASE parametreleştirilemez), bu
    /// yüzden yalnız <c>[A-Za-z0-9_]</c> karakterleri kabul edilir — doğrulama
    /// <c>ITenantDatabaseProvisioner</c> içindedir ve tek savunmadır.
    /// </summary>
    public string? DatabaseName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
