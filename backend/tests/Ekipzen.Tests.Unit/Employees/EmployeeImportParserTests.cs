using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportParserTests
{
    /// <summary>Verilen satırlardan bellekte .xlsx üretir (1. satır başlık).</summary>
    private static Stream DosyaUret(params string[][] satirlar)
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

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Ayristir_SatirlariVeExcelSatirNumarasiniOkur()
    {
        using var dosya = DosyaUret(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "12345678901", "active", "", "", ""]);

        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(dosya);

        sorunlar.Should().BeEmpty();
        satirlar.Should().ContainSingle();
        satirlar[0].SatirNo.Should().Be(2, "başlık 1. satır; ilk veri 2. satırdır");
        satirlar[0].Ad.Should().Be("Ayşe");
        satirlar[0].Departman.Should().Be("Yazılım");
        satirlar[0].KisiselEposta.Should().BeNull("boş hücre null olmalı");
    }

    [Fact]
    public void Ayristir_TamamenBosSatirlariAtlar()
    {
        using var dosya = DosyaUret(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "", "", "", "", ""],
            ["", "", "", "", "", "", "", "", "", ""],
            ["Mehmet", "Kaya", "Uzman", "Yazılım", "02.03.2026", "", "", "", "", ""]);

        var (satirlar, _) = EmployeeImportParser.Ayristir(dosya);

        satirlar.Should().HaveCount(2, "boş satır veri değildir");
        satirlar.Select(s => s.SatirNo).Should().Equal(2, 4);
    }

    [Fact]
    public void Ayristir_ZorunluSutunEksikse_SorunBildirirVeSatirOkumaz()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personel");
        sheet.Cell(1, 1).Value = "Ad";
        sheet.Cell(1, 2).Value = "Soyad";
        sheet.Cell(2, 1).Value = "Ayşe";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(ms);

        satirlar.Should().BeEmpty("başlık bozuksa satırları yorumlamak yanıltıcı olur");
        sorunlar.Should().ContainSingle();
        sorunlar[0].Mesaj.Should().Contain("Departman");
    }

    [Fact]
    public void Ayristir_SutunSirasiDegisseDeBasligaGoreOkur()
    {
        // Kullanıcı sütunları taşıyabilir; eşleme konuma değil BAŞLIĞA bağlı olmalı.
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personel");
        string[] tersBasliklar = ["İşe Giriş Tarihi", "Departman", "Unvan", "Soyad", "Ad"];
        for (var i = 0; i < tersBasliklar.Length; i++) sheet.Cell(1, i + 1).Value = tersBasliklar[i];
        string[] deger = ["01.03.2026", "Yazılım", "Analist", "Demir", "Ayşe"];
        for (var i = 0; i < deger.Length; i++) sheet.Cell(2, i + 1).SetValue(deger[i]);
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(ms);

        sorunlar.Should().BeEmpty();
        satirlar[0].Ad.Should().Be("Ayşe");
        satirlar[0].Departman.Should().Be("Yazılım");
    }

    [Fact]
    public void Ayristir_ExcelOlmayanDosyayiAnlasilirSorunaDonusturur()
    {
        using var stream = new MemoryStream("xlsx değil"u8.ToArray());
        var (rows, issues) = EmployeeImportParser.Ayristir(stream);
        rows.Should().BeEmpty(); issues.Should().ContainSingle(x => x.Alan == "Dosya");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ayristir_MukerrerBaslikVeyaBirdenFazlaSayfayiSessizceYorumlamaz(bool duplicateHeader)
    {
        using var source = DosyaUret(["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026"]);
        using var workbook = new XLWorkbook(source);
        if (duplicateHeader) workbook.Worksheet(1).Cell(1, 11).Value = "Ad";
        else workbook.Worksheets.Add("Diğer çalışanlar");
        using var stream = new MemoryStream(); workbook.SaveAs(stream); stream.Position = 0;
        var (rows, issues) = EmployeeImportParser.Ayristir(stream);
        rows.Should().BeEmpty(); issues.Should().ContainSingle();
    }
}
