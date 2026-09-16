namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Yeni oluşturulmuş bir kiracı veritabanını KULLANILABİLİR hale getirir:
/// migration'ları uygular ve rolleri tohumlar.
///
/// Neden <see cref="ITenantDatabaseProvisioner"/>'dan ayrı: o katalog seviyesinde
/// çalışır (ada göre oluştur/düşür) ve şemadan haberi yoktur. Burası ise kiracıya
/// göre çalışır — bağlantıyı kütükten çözer ve EF modelini uygular.
///
/// <b>Roller neden burada:</b> boş bir kiracı veritabanında <c>AspNetRoles</c>
/// boştur. Admin kullanıcı oluşturulup <c>AddToRoleAsync</c> çağrıldığında rol
/// yoksa atama SESSİZCE düşer — kullanıcı oluşur, giriş yapar ve hiçbir şey
/// göremez. Rol tohumlaması bu yüzden şema hazırlığının parçasıdır, isteğe bağlı
/// bir seed adımı değil.
/// </summary>
public interface ITenantSchemaInitializer
{
    Task PrepareAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracıya HENÜZ uygulanmamış migration'ların adları; boşsa şema günceldir.
    ///
    /// Hem şema kapısı hem operatör raporu bunu kullanır: "geride mi?" ve "neyin
    /// gerisinde?" aynı kaynaktan cevaplanmalı, yoksa ikisi zamanla ayrışır.
    /// </summary>
    Task<IReadOnlyList<string>> PendingMigrationsAsync(int tenantId, CancellationToken cancellationToken);
}
