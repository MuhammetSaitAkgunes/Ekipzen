using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ITenantScopeFactory"/>'nin üretim uygulaması. <see cref="IServiceScopeFactory"/>
/// ile KÖKTEN yeni, bağımsız bir DI kapsamı açar — çağıranın kendi kapsamının İÇİNE değil,
/// container'ın KÖKÜNE göre kurulur (framework'ün <see cref="IServiceScopeFactory"/>'si her
/// zaman böyle çalışır). Bu yüzden bir HTTP isteği kapsamının veya başka bir scoped servisin
/// İÇİNDEN çağrılması "captive dependency" ya da kapsam sızıntısı yaratmaz: yeni kapsam
/// tamamen bağımsızdır ve <c>using</c> ile normal şekilde dispose edilir.
///
/// Kapsam, döndürülmeden ÖNCE <see cref="ICurrentTenant.Impersonate"/> ile kiracıya
/// sabitlenir. Böylece kapsamdan sonra çözülen <c>AppDbContext</c> (bağlantısı kapsam
/// başına, ilk çözümde <see cref="ICurrentTenant"/>'tan okunur) doğru kiracıyı görür —
/// çağıranın "önce impersone et, sonra servisi çöz" sırasına uyup uymadığına bakılmaksızın.
/// </summary>
public sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory) : ITenantScopeFactory
{
    public ITenantScope Create(int tenantId)
    {
        var scope = scopeFactory.CreateScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Impersonate(tenantId);
            return new TenantScope(scope);
        }
        catch
        {
            // Impersonate/GetRequiredService patlarsa açılan kapsamı sızdırma.
            scope.Dispose();
            throw;
        }
    }

    private sealed class TenantScope(IServiceScope scope) : ITenantScope
    {
        public IServiceProvider Services => scope.ServiceProvider;

        public void Dispose() => scope.Dispose();
    }
}
