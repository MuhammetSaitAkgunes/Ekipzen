using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Import;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Personnel;

/// <summary>
/// Excel'den toplu personel aktarımı uçtan uca: önizleme raporu, mükerrer
/// atlama, bilinmeyen departman ve yetki korumaları.
/// Seed departmanları: Yazılım, Tasarım, İnsan Kaynakları.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmployeeImportTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task Preview_GecerliVeHataliSatirlariAyirir_HicbirSeyKaydetmez()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var oncekiSayi = await PersonelSayisiAsync(admin);

        using var content = DosyaIcerigi(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "", "", "", "", ""],
            ["", "Kaya", "Uzman", "Yazılım", "02.03.2026", "", "", "", "", ""],
            ["Ali", "Vural", "Uzman", "Olmayan Departman", "03.03.2026", "", "", "", "", ""]);

        var response = await admin.PostAsync("/api/employees/import/preview", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rapor = (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
        rapor.ToplamSatir.Should().Be(3);
        rapor.GecerliSatir.Should().Be(1);
        rapor.HataliSatir.Should().Be(2);
        rapor.Sorunlar.Should().Contain(s => s.SatirNo == 3 && s.Alan == "Ad");
        rapor.BilinmeyenDepartmanlar.Should().Contain("Olmayan Departman");

        (await PersonelSayisiAsync(admin))
            .Should().Be(oncekiSayi, "önizleme HİÇBİR ŞEY kaydetmemeli");
    }

    [Fact]
    public async Task Preview_BozukBaslik_NetHataVerir()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personel");
        sheet.Cell(1, 1).Value = "İsim";
        sheet.Cell(1, 2).Value = "Soyisim";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        using var content = DosyaGovdesi(ms.ToArray());

        var response = await admin.PostAsync("/api/employees/import/preview", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rapor = (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
        rapor.GecerliSatir.Should().Be(0);
        rapor.Sorunlar.Should().ContainSingle(s => s.Alan == "Başlık");
    }

    [Fact]
    public async Task Preview_HrAdminDisindakileriReddeder()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        using var content = DosyaIcerigi(["A", "B", "C", "Yazılım", "01.03.2026", "", "", "", "", ""]);

        (await manager.PostAsync("/api/employees/import/preview", content))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Template_HrAdminIcinIndirilir()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var response = await admin.GetAsync("/api/employees/import/template");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();

        // İndirilen şablon gerçekten ayrıştırılabilmeli (şablon ↔ ayrıştırıcı uyumu).
        using var stream = new MemoryStream(bytes);
        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(stream);
        sorunlar.Should().BeEmpty("şablonun başlıkları ayrıştırıcıyla uyumlu olmalı");
        satirlar.Should().ContainSingle("şablonda bir örnek satır var");
    }

    [Fact]
    public async Task Import_GecerliSatirlariOlusturur_AyniDosyaTekrarYuklenirseMukerreriAtlar()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var tc = "988" + Random.Shared.Next(10000000, 99999999);
        string[] satir = ["Zeynep", "Aktaş", "Analist", "Yazılım", "01.03.2026", tc, "", "", "", ""];

        var oncekiSayi = await PersonelSayisiAsync(admin);

        using var ilk = DosyaIcerigi(satir);
        await AktarimHazirlaAsync(admin, ilk);
        var ilkYanit = await admin.PostAsync("/api/employees/import", ilk);
        ilkYanit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ilkYanit.Content.ReadFromJsonAsync<ImportResultDto>())!
            .OlusturulanSatir.Should().Be(1);
        (await PersonelSayisiAsync(admin)).Should().Be(oncekiSayi + 1);

        // Aynı dosya ikinci kez (sık rastlanan kaza): mükerrer atlanmalı.
        using var ikinci = DosyaIcerigi(satir);
        await AktarimHazirlaAsync(admin, ikinci);
        var ikinciYanit = await admin.PostAsync("/api/employees/import", ikinci);
        var sonuc = (await ikinciYanit.Content.ReadFromJsonAsync<ImportResultDto>())!;

        sonuc.OlusturulanSatir.Should().Be(0, "aynı TC zaten kayıtlı");
        sonuc.AtlananSatir.Should().Be(1);
        (await PersonelSayisiAsync(admin))
            .Should().Be(oncekiSayi + 1, "mükerrer yükleme yeni kayıt OLUŞTURMAMALI");
    }

    [Fact]
    public async Task Import_HataliSatirlariAtlar_GecerlileriKaydeder()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var oncekiSayi = await PersonelSayisiAsync(admin);

        using var content = DosyaIcerigi(
            ["Kerem", "Şahin", "Uzman", "Yazılım", "01.04.2026", "", "", "", "", ""],
            ["", "Eksik", "Uzman", "Yazılım", "01.04.2026", "", "", "", "", ""]);
        await AktarimHazirlaAsync(admin, content);

        var response = await admin.PostAsync("/api/employees/import", content);

        var sonuc = (await response.Content.ReadFromJsonAsync<ImportResultDto>())!;
        sonuc.OlusturulanSatir.Should().Be(1);
        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "Ad");
        (await PersonelSayisiAsync(admin)).Should().Be(oncekiSayi + 1);
    }

    [Fact]
    public async Task Import_HrAdminDisindakileriReddeder()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        using var content = DosyaIcerigi(["A", "B", "C", "Yazılım", "01.03.2026", "", "", "", "", ""]);

        (await manager.PostAsync("/api/employees/import", content))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- yardımcılar ---

    private static async Task AktarimHazirlaAsync(HttpClient client, MultipartFormDataContent content)
    {
        var bytes = await content.First().ReadAsByteArrayAsync();
        using var previewContent = DosyaGovdesi(bytes);
        var response = await client.PostAsync("/api/employees/import/preview", previewContent);
        response.EnsureSuccessStatusCode();
        var preview = (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
        content.Add(new StringContent(Guid.NewGuid().ToString()), "importRequestId");
        content.Add(new StringContent(preview.PreviewToken!), "expectedPreviewToken");
    }

    private static MultipartFormDataContent DosyaIcerigi(params string[][] satirlar)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(EmployeeImportTemplate.SayfaAdi);
        for (var i = 0; i < EmployeeImportTemplate.SutunBasliklari.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = EmployeeImportTemplate.SutunBasliklari[i];
        }

        for (var r = 0; r < satirlar.Length; r++)
        {
            for (var c = 0; c < satirlar[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).SetValue(satirlar[r][c]);
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return DosyaGovdesi(ms.ToArray());
    }

    private static MultipartFormDataContent DosyaGovdesi(byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "personel.xlsx");
        return content;
    }

    private static async Task<int> PersonelSayisiAsync(HttpClient admin)
    {
        var response = await admin.GetAsync("/api/employees?pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sayfa = (await response.Content
            .ReadFromJsonAsync<PagedResult<EmployeeListItemDto>>())!;
        return sayfa.Total;
    }

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"demo giriş başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
