namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracıdan veritabanı bağlantı dizesine giden ayrılma noktası.
///
/// Bu soyutlamanın var olma nedeni buydu: uygulamanın hiçbir yeri "hangi
/// veritabanı" sorusunu sormaz, yalnız aktif kiracıyı bilir. Kiracı başına
/// veritabanına geçerken çağıranların HİÇBİRİ değişmedi — yalnız bu arayüzün
/// arkası değişti.
/// </summary>
public interface ITenantConnectionResolver
{
    /// <summary>
    /// Kiracının veritabanı bağlantı dizesi.
    ///
    /// <paramref name="tenantId"/> null ise (migration, platform işlemleri,
    /// login'in dizin sorgusundan önceki an) paylaşılan dize döner. Kiracının
    /// katalog adı boşsa — yani henüz ayrılmamışsa — yine paylaşılan dize döner.
    /// </summary>
    string ResolveFor(int? tenantId);
}
