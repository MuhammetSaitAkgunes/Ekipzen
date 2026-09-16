using FluentAssertions;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Faz 0 kanıtı: multi-tenant izolasyon. Bir kiracının kullanıcısı DİĞER kiracının
/// verisini asla göremez (çift yönlü). EF Core global query filter + JWT tenant claim.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantIsolationTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Departments_AreIsolatedBetweenTenants()
    {
        const string globexDept = "Globex Yazılım Ekibi";
        var adminEmail = $"admin-{Guid.NewGuid():N}@globex.local";
        var tenant = await ProvisionAndActivateAsync("Globex Holding", adminEmail);
        await SeedInTenantAsync(tenant.TenantId, db =>
        {
            db.Departments.Add(new Department { Name = globexDept });
            return Task.CompletedTask;
        });

        // Globex admin yalnız Globex'in departmanını görür — varsayılan kiracınınkini DEĞİL.
        var globex = await AuthedClientAsync(adminEmail);
        var globexDepts = await GetAsync<List<DepartmentDto>>(globex, "/api/departments");
        globexDepts.Should().ContainSingle()
            .Which.Name.Should().Be(globexDept, "Globex admin yalnız kendi kiracısını görmeli");

        // Varsayılan kiracının admin'i Globex'in departmanını GÖREMEZ.
        var demoAdmin = await AuthedClientAsync("ik@hrmaster.local");
        var demoDepts = await GetAsync<List<DepartmentDto>>(demoAdmin, "/api/departments");
        demoDepts.Should().NotBeEmpty("varsayılan kiracının seed departmanları görünmeli");
        demoDepts.Should().NotContain(d => d.Name == globexDept,
            "varsayılan kiracı Globex verisini asla görmemeli");
    }
}
