using FluentAssertions;
using Ekipzen.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Unit.Tenancy;

/// <summary>
/// AddInfrastructure, bağlantı dizesini AÇILIŞTA doğrulamalı — DbContext ilk
/// çözüldüğünde (ilk istekte) değil. Aksi halde bozuk yapılandırmayla yapılan
/// bir dağıtım temiz açılır, sağlıklı görünür ve hata ancak ilk gerçek istekte
/// 500 olarak çıkar. Bu, kapsam-başına çözüme (ITenantConnectionResolver)
/// geçişin görünmez bir yan etkisiydi — Görev 4 düzeltme turu 1.
/// </summary>
public class AcilisDogrulamaTests
{
    [Fact]
    public void AddInfrastructure_BaglantiDizesiYoksaAcilistaHemenPatlar()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        FluentActions.Invoking(() => services.AddInfrastructure(configuration))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }
}
