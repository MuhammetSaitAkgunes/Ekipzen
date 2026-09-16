using FluentAssertions;
using Ekipzen.Application.Features.Compliance;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Faz 2 kanıtı: SQL view read-model'leri de kiracıya izole. Kritik vaka
/// vw_ComplianceReadiness'tir — eskiden TÜM kiracıların belgelerini tek satırda
/// topluyordu (sessiz çapraz-kiracı sızıntısı); artık TenantId'ye göre gruplanır.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantViewIsolationTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task ComplianceReadinessView_IsIsolatedPerTenant()
    {
        // Yeni (boş) bir kiracı: hiç uyum belgesi yok.
        var adminEmail = $"admin-{Guid.NewGuid():N}@readiness.local";
        await ProvisionAndActivateAsync("Readiness A.Ş.", adminEmail);

        // Yeni kiracının hazırlık özeti YALNIZ kendi (sıfır) belgesini yansıtmalı.
        // View izole değilse, varsayılan kiracının seed belgelerinin toplamı sızardı.
        var newAdmin = await AuthedClientAsync(adminEmail);
        var newReadiness = await GetAsync<ComplianceReadinessDto>(newAdmin, "/api/compliance/readiness");
        newReadiness.TotalCount.Should().Be(0, "yeni kiracının hiç uyum belgesi yok → view sızdırmamalı");

        // Varsayılan kiracının seed belgeleri hâlâ görünüyor (izolasyon çift yönlü).
        var demoAdmin = await AuthedClientAsync("ik@hrmaster.local");
        var demoReadiness = await GetAsync<ComplianceReadinessDto>(demoAdmin, "/api/compliance/readiness");
        demoReadiness.TotalCount.Should().BeGreaterThan(0,
            "varsayılan kiracının seed uyum belgeleri kendi hazırlık özetinde görünmeli");
    }
}
