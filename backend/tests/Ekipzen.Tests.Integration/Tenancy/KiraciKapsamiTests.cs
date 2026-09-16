using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Kiracıya sabitlenmiş kapsam: kiracı, kapsam açılırken sabitlenir ve
/// içinden çıkan HER servis onu görür. Böylece "önce impersonate, sonra
/// context al" sırasını yanlış yapmak imkânsızlaşır — o hata sessizce
/// yanlış veritabanına bağlanmakla sonuçlanırdı.
/// </summary>
[Collection(ApiCollection.Name)]
public class KiraciKapsamiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Kapsam_IcindekiServisleriVerilenKiraciyaBaglar()
    {
        var kiraci = await ProvisionAndActivateAsync("Kapsam", $"kps-{Guid.NewGuid():N}@ornek.local");

        var fabrika = Factory.Services.GetRequiredService<ITenantScopeFactory>();
        using var kapsam = fabrika.Create(kiraci.TenantId);

        kapsam.Services.GetRequiredService<ICurrentTenant>().TenantId
            .Should().Be(kiraci.TenantId, "kapsam kiracıyı, içinden servis çözülmeden ÖNCE sabitlemeli");
    }
}
