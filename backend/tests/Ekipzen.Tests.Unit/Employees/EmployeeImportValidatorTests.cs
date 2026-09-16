using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportValidatorTests
{
    private static readonly Dictionary<string, int> Departmanlar =
        new() { [EmployeeImportValidator.Normalize("Yazılım")] = 7 };

    private static ImportRow Satir(
        int no = 2, string? ad = "Ayşe", string? soyad = "Demir", string? unvan = "Analist",
        string? departman = "Yazılım", string? tarih = "01.03.2026", string? tc = null,
        string? durum = null, string? eposta = null, string? iban = null) =>
        new(no, ad, soyad, unvan, departman, tarih, tc, durum, eposta, null, iban);

    private static ValidatedImport Dogrula(params ImportRow[] satirlar) =>
        EmployeeImportValidator.Dogrula(satirlar, Departmanlar, new HashSet<string>());

    [Fact]
    public void GecerliSatir_ModelUretir_DepartmanIdCozulur()
    {
        var sonuc = Dogrula(Satir());

        sonuc.Sorunlar.Should().BeEmpty();
        sonuc.Gecerli.Should().ContainSingle();
        sonuc.Gecerli[0].DepartmentId.Should().Be(7);
        sonuc.Gecerli[0].Status.Should().Be("active", "durum boşsa varsayılan active");
        sonuc.Gecerli[0].ManagerId.Should().BeNull("yönetici v1'de aktarılmaz");
    }

    [Fact]
    public void ZorunluAlanEksikse_Sorun()
    {
        var sonuc = Dogrula(Satir(ad: null));

        sonuc.Gecerli.Should().BeEmpty();
        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "Ad" && s.SatirNo == 2);
    }

    [Fact]
    public void BilinmeyenDepartman_SorunVeListe()
    {
        var sonuc = Dogrula(Satir(departman: "Pazarlama"));

        sonuc.Gecerli.Should().BeEmpty();
        sonuc.BilinmeyenDepartmanlar.Should().Contain("Pazarlama");
    }

    [Fact]
    public void DepartmanEslesmesi_BuyukKucukHarfVeBoslukDuyarsiz()
    {
        var sonuc = Dogrula(Satir(departman: "  YAZILIM  "));

        sonuc.Gecerli.Should().ContainSingle();
        sonuc.Gecerli[0].DepartmentId.Should().Be(7);
    }

    [Fact]
    public void Normalize_TurkceIHarfiniDogruKucultur()
    {
        // InvariantCulture'da "IK" → "ik" olur ve "ık" ile eşleşmez; tr-TR şart.
        EmployeeImportValidator.Normalize("IK")
            .Should().Be(EmployeeImportValidator.Normalize("ık"));
    }

    [Fact]
    public void GecersizTc_Sorun()
    {
        var sonuc = Dogrula(Satir(tc: "123"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "TC Kimlik No");
    }

    [Fact]
    public void SistemdeKayitliTc_MukerrerSayilirVeAtlanir()
    {
        var sonuc = EmployeeImportValidator.Dogrula(
            [Satir(tc: "12345678901")], Departmanlar, new HashSet<string> { "12345678901" });

        sonuc.Gecerli.Should().BeEmpty("mevcut kayıt DEĞİŞTİRİLMEZ");
        sonuc.MukerrerSatir.Should().Be(1);
        sonuc.Sorunlar.Should().BeEmpty("mükerrer bir hata değil, atlanan satırdır");
    }

    [Fact]
    public void DosyaIcindeAyniTcIkiKez_IkincisiSorun()
    {
        var sonuc = Dogrula(
            Satir(no: 2, tc: "12345678901"),
            Satir(no: 3, tc: "12345678901"));

        sonuc.Gecerli.Should().ContainSingle("ilk satır geçerli");
        sonuc.Sorunlar.Should().ContainSingle(s => s.SatirNo == 3 && s.Alan == "TC Kimlik No");
    }

    [Fact]
    public void GecersizIban_Sorun()
    {
        var sonuc = Dogrula(Satir(iban: "TR123"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "IBAN");
    }

    [Fact]
    public void BosluklarlaYazilmisIban_Kabul()
    {
        var sonuc = Dogrula(Satir(iban: "TR33 0006 1005 1978 6457 8413 26"));

        sonuc.Sorunlar.Should().BeEmpty();
        sonuc.Gecerli[0].Profile!.Iban.Should().Be("TR330006100519786457841326");
    }

    [Fact]
    public void OkunamayanTarih_Sorun()
    {
        var sonuc = Dogrula(Satir(tarih: "yakında"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "İşe Giriş Tarihi");
    }

    [Fact]
    public void GecersizDurum_Sorun()
    {
        var sonuc = Dogrula(Satir(durum: "aktif"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "Durum");
    }
}
