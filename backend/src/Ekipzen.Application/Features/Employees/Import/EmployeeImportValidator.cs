using Ekipzen.Application.Features.Employees.Upsert;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// Doğrulama sonucu. Mükerrer satırlar <see cref="Sorunlar"/> içinde DEĞİLDİR:
/// hata değil, bilinçli olarak atlanan satırlardır.
/// </summary>
public sealed record ValidatedImport(
    IReadOnlyList<EmployeeUpsertModel> Gecerli,
    IReadOnlyList<ImportRowIssueDto> Sorunlar,
    IReadOnlyList<string> BilinmeyenDepartmanlar,
    int MukerrerSatir,
    IReadOnlyList<ValidatedImportRow> Satirlar);

public sealed record ValidatedImportRow(ImportRowPreviewDto Preview, EmployeeUpsertModel? Model);

/// <summary>
/// Ham satırları iş kurallarına göre doğrular. Excel bilmez — düz nesnelerle
/// test edilebilir. Kaynak değişiklikleri ayrıca önizleme belirteciyle denetlenir.
/// </summary>
public static class EmployeeImportValidator
{
    private static readonly Regex TcRegex = new(@"^\d{11}$", RegexOptions.Compiled);
    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);
    private static readonly Regex BosluklarRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly CultureInfo Turkce = new("tr-TR");

    /// <summary>
    /// Departman eşleme anahtarı. Türkçe kültür kullanılır: InvariantCulture'da
    /// "I" → "i" dönüşümü yanlıştır ve "IK" ile "ık" eşleşmez.
    /// </summary>
    public static string Normalize(string? deger) =>
        BosluklarRegex.Replace((deger ?? string.Empty).Trim(), " ").ToLower(Turkce);

    public static ValidatedImport Dogrula(
        IReadOnlyList<ImportRow> satirlar,
        IReadOnlyDictionary<string, int> departmanlar,
        ISet<string> mevcutTcler)
    {
        var gecerli = new List<EmployeeUpsertModel>();
        var sorunlar = new List<ImportRowIssueDto>();
        var bilinmeyenDepartmanlar = new List<string>();
        var dosyadakiTcler = new HashSet<string>();
        var mukerrer = 0;
        var sonuclar = new List<ValidatedImportRow>();

        foreach (var satir in satirlar)
        {
            var satirSorunlari = new List<ImportRowIssueDto>();
            void Sorun(string alan, string mesaj) =>
                satirSorunlari.Add(new ImportRowIssueDto(satir.SatirNo, alan, mesaj));

            if (string.IsNullOrWhiteSpace(satir.Ad)) Sorun("Ad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Soyad)) Sorun("Soyad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Unvan)) Sorun("Unvan", "Zorunlu alan.");
            if (satir.Ad?.Trim().Length > 128) Sorun("Ad", "En fazla 128 karakter olmalı.");
            if (satir.Soyad?.Trim().Length > 128) Sorun("Soyad", "En fazla 128 karakter olmalı.");
            if (satir.Unvan?.Trim().Length > 128) Sorun("Unvan", "En fazla 128 karakter olmalı.");
            if (satir.Telefon?.Trim().Length > 32) Sorun("Telefon", "En fazla 32 karakter olmalı.");

            // --- Departman ---
            var departmanId = 0;
            if (string.IsNullOrWhiteSpace(satir.Departman))
            {
                Sorun("Departman", "Zorunlu alan.");
            }
            else if (departmanlar.TryGetValue(Normalize(satir.Departman), out var id))
            {
                if (id <= 0) Sorun("Departman", "Bu ad birden fazla departmanla eşleşiyor. Departman adlarını ayırt edip dosyayı yeniden yükleyin.");
                else departmanId = id;
            }

            else
            {
                Sorun("Departman", $"'{satir.Departman}' sistemde yok. Önce departmanı oluşturun.");
                if (!bilinmeyenDepartmanlar.Contains(satir.Departman))
                {
                    bilinmeyenDepartmanlar.Add(satir.Departman);
                }
            }

            // --- İşe giriş tarihi ---
            // Hücre metin ya da tarih olabilir; Türkçe biçim (gg.aa.yyyy) önce denenir.
            DateOnly iseGiris = default;
            if (string.IsNullOrWhiteSpace(satir.IseGirisTarihi))
            {
                Sorun("İşe Giriş Tarihi", "Zorunlu alan.");
            }
            else if (!DateOnly.TryParse(satir.IseGirisTarihi, Turkce, DateTimeStyles.None, out iseGiris)
                  && !DateOnly.TryParse(satir.IseGirisTarihi, CultureInfo.InvariantCulture, DateTimeStyles.None, out iseGiris))
            {
                Sorun("İşe Giriş Tarihi", $"'{satir.IseGirisTarihi}' tarih olarak okunamadı (örn. 01.03.2026).");
            }
            if (iseGiris == DateOnly.MinValue && !string.IsNullOrWhiteSpace(satir.IseGirisTarihi)
                && !satirSorunlari.Any(x => x.Alan == "İşe Giriş Tarihi"))
                Sorun("İşe Giriş Tarihi", "Geçerli bir işe giriş tarihi girin.");

            // --- TC kimlik ---
            string? tc = null;
            if (!string.IsNullOrWhiteSpace(satir.TcKimlikNo))
            {
                tc = satir.TcKimlikNo.Trim();
                if (!TcRegex.IsMatch(tc))
                {
                    Sorun("TC Kimlik No", "11 haneli rakam olmalı.");
                }
                else if (!dosyadakiTcler.Add(tc))
                {
                    Sorun("TC Kimlik No", "Bu TC dosyada birden fazla satırda geçiyor.");
                }
            }

            // --- Durum ---
            var durum = string.IsNullOrWhiteSpace(satir.Durum)
                ? "active"
                : satir.Durum.Trim().ToLower(Turkce);
            if (durum != "active")
            {
                Sorun("Durum", "Yeni personel yalnız 'active' oluşturulabilir (boş bırakılırsa active). Pasife alma için işten ayrılış sürecini kullanın.");
            }

            // --- IBAN ---
            string? iban = null;
            if (!string.IsNullOrWhiteSpace(satir.Iban))
            {
                iban = satir.Iban.Replace(" ", "");
                if (!IbanRegex.IsMatch(iban)) Sorun("IBAN", "'TR' ile başlayan 26 karakter olmalı.");
            }

            // --- E-posta ---
            var eposta = string.IsNullOrWhiteSpace(satir.KisiselEposta) ? null : satir.KisiselEposta.Trim();
            if (eposta is not null && (eposta.Length > 200 || eposta.IndexOf('@') <= 0
                || eposta.LastIndexOf('@') != eposta.IndexOf('@') || eposta.EndsWith('@') || eposta.Any(char.IsWhiteSpace)))
            {
                Sorun("Kişisel E-posta", "Geçerli bir e-posta olmalı.");
            }

            // Mükerrer kontrolü yalnız SORUNSUZ satırlar için anlamlıdır: hatalı satır
            // zaten aktarılmayacak, ayrıca "mükerrer" saymak sayımları çift bozar.
            if (satirSorunlari.Count == 0 && tc is not null && mevcutTcler.Contains(tc))
            {
                mukerrer++;
                sonuclar.Add(new(Preview("duplicate", "TC Kimlik No ile kayıtlı personel var; mevcut kayıt değişmez."), null));
                continue;
            }

            if (satirSorunlari.Count > 0)
            {
                sorunlar.AddRange(satirSorunlari);
                sonuclar.Add(new(Preview("invalid", "Hatalı satır atlanacak; dosyada düzeltip yeniden önizleyin."), null));
                continue;
            }

            var model = new EmployeeUpsertModel(
                FirstName: satir.Ad!.Trim(),
                LastName: satir.Soyad!.Trim(),
                Title: satir.Unvan!.Trim(),
                DepartmentId: departmanId,
                HireDate: iseGiris,
                NationalId: tc,
                ManagerId: null,
                Status: durum,
                Profile: new EmployeeProfileUpsertModel(
                    PersonalEmail: eposta,
                    MobilePhone: string.IsNullOrWhiteSpace(satir.Telefon) ? null : satir.Telefon.Trim(),
                    Iban: iban));
            gecerli.Add(model);
            sonuclar.Add(new(Preview("ready", "Aktif personel ve açık istihdam kaydı oluşturulacak; sözleşme belirsiz süreli."), model));

            ImportRowPreviewDto Preview(string result, string explanation) => new(
                satir.SatirNo, $"{satir.Ad?.Trim()} {satir.Soyad?.Trim()}".Trim(),
                satir.Unvan?.Trim() ?? "", satir.Departman?.Trim() ?? "",
                iseGiris == default ? null : iseGiris, result, explanation, satirSorunlari);
        }

        return new ValidatedImport(gecerli, sorunlar, bilinmeyenDepartmanlar, mukerrer, sonuclar);
    }
}
