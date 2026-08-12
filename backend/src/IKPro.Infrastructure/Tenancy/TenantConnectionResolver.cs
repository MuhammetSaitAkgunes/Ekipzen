using IKPro.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace IKPro.Infrastructure.Tenancy;

/// <summary>
/// Kiracıdan veritabanına giden ayrılma noktası. Kiracının katalog adı doluysa
/// bağlantı O KATALOĞA, boşsa paylaşılan veritabanına kurulur.
///
/// Sunucu, kimlik bilgisi ve diğer tüm ayarlar paylaşılan dizeden gelir; yalnız
/// katalog adı değişir. Böylece kiracı başına ayrı bir bağlantı ayarı tutulmaz —
/// tutulsaydı sunucu taşındığında N tane dizenin güncellenmesi gerekirdi.
/// </summary>
public sealed class TenantConnectionResolver(IConfiguration configuration, ITenantRegistry registry)
    : ITenantConnectionResolver
{
    public string ResolveFor(int? tenantId)
    {
        var paylasilan = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil.");

        // Kiracı bağlamı yok: migration, platform işlemleri, login'in dizin
        // sorgusundan ÖNCEKİ anı. Bunların hepsi paylaşılan veritabanına gider.
        if (tenantId is null)
        {
            return paylasilan;
        }

        // Katalog adı boş = kiracı henüz ayrılmamış, paylaşılan veritabanında.
        // Ayrılma kiracı kiracı yapıldığı için bu, geçiş boyunca NORMAL durumdur.
        var katalog = registry.GetDatabaseName(tenantId.Value);
        if (string.IsNullOrWhiteSpace(katalog))
        {
            return paylasilan;
        }

        return new SqlConnectionStringBuilder(paylasilan) { InitialCatalog = katalog }.ConnectionString;
    }
}
