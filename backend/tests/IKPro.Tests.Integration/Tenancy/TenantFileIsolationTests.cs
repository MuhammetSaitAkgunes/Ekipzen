using FluentAssertions;
using IKPro.Application.Common.Models;
using IKPro.Application.Features.Employees;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;
using System.Net;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Evrak/dosya erişimi kiracıya izoledir: bir kiracı, diğerinin evrakına ID
/// tahminiyle erişemez.
///
/// <b>Test bir kez yeniden yazıldı, sebebi öğreticidir.</b> Eskiden "yabancı ID →
/// 404" iddia ediliyordu ve bu, tüm kiracılar aynı veritabanını paylaşırken
/// doğruydu: kimlikler global olarak tekildi, bir kiracının personel kimliği
/// diğerinde YOKTU. Kiracı başına veritabanına geçilince her katalog kendi kimlik
/// dizisini saydı; artık 1 numaralı personel HER kiracıda var. Yani kurgu
/// kurulamaz oldu — "yabancı" bir kimlik bulmak mümkün değil.
///
/// Bu bir gerileme değil, izolasyonun GÜÇLENMESİDİR: erişim artık sorgu filtresine
/// değil, fiziksel ayrılığa dayanıyor. Test bu yüzden asıl güvenceyi ölçüyor:
/// başka kiracının cevabında ötekinin verisi ASLA görünmez.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantFileIsolationTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Documents_AreNotAccessibleAcrossTenants()
    {
        var adminEmail = $"admin-{Guid.NewGuid():N}@files.local";
        var tenant = await ProvisionAndActivateAsync("Files A.Ş.", adminEmail);

        // Globex kiracısında bir personel + evrak (fiziksel dosya gerekmez; erişim kontrolü test edilir).
        var dept = new Department { Name = "Files Ekibi" };
        var employee = new Employee
        {
            FirstName = "Zed", LastName = "Zephyr", Title = "Dev",
            Department = dept, HireDate = new DateOnly(2026, 1, 1),
            Status = EmployeeStatus.Active, Profile = new EmployeeProfile(),
        };
        var document = new EmployeeDocument
        {
            Employee = employee, DocumentType = "Sözleşme", FileName = "gizli.pdf",
            FilePath = "files/gizli.pdf", ContentType = "application/pdf", SizeBytes = 42,
        };
        await SeedInTenantAsync(tenant.TenantId, db =>
        {
            db.AddRange(dept, employee, document);
            return Task.CompletedTask;
        });

        // Bu kiracının admin'i kendi personelinin evrakını LİSTELEYEBİLİR (pozitif kontrol).
        var owner = await AuthedClientAsync(adminEmail);
        var ownDocs = await GetAsync<List<EmployeeDocumentDto>>(
            owner, $"/api/employees/{employee.Id}/documents");
        ownDocs.Should().ContainSingle().Which.FileName.Should().Be("gizli.pdf");

        var demoAdmin = await AuthedClientAsync("ik@hrmaster.local");

        // AYNI kimlikle sorulduğunda: varsayılan kiracının admin'i ya kendi
        // personelini görür ya 404 alır — ama öteki kiracının evrakı cevapta
        // ASLA geçmez. Asıl güvence bu.
        var ayniKimlik = await demoAdmin.GetAsync($"/api/employees/{employee.Id}/documents");
        (await ayniKimlik.Content.ReadAsStringAsync())
            .Should().NotContain("gizli.pdf", "başka kiracının evrakı hiçbir koşulda sızmamalı");

        var indirme = await demoAdmin.GetAsync(
            $"/api/employees/{employee.Id}/documents/{document.Id}");
        (await indirme.Content.ReadAsStringAsync())
            .Should().NotContain("gizli.pdf", "başka kiracının evrakı indirilememeli");

        // Gerçekten var olmayan bir kimlik hâlâ 404 dönmeli: yukarıdaki iddianın
        // "uç zaten hep boş dönüyor" diye geçmediğini bu ayırt eder.
        (await demoAdmin.GetAsync("/api/employees/999999/documents"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
