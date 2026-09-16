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
    int MukerrerSatir);

/// <summary>
/// Ham satırları iş kurallarına göre doğrular. Excel bilmez — düz nesnelerle
/// test edilebilir. Önizleme ve aktarım AYNI bu sınıfı kullanır; bu yüzden
/// "önizlemede temiz görünüp aktarımda patlama" durumu oluşamaz.
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

        foreach (var satir in satirlar)
        {
            var satirSorunlari = new List<ImportRowIssueDto>();
            void Sorun(string alan, string mesaj) =>
                satirSorunlari.Add(new ImportRowIssueDto(satir.SatirNo, alan, mesaj));

            if (string.IsNullOrWhiteSpace(satir.Ad)) Sorun("Ad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Soyad)) Sorun("Soyad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Unvan)) Sorun("Unvan", "Zorunlu alan.");

            // --- Departman ---
            var departmanId = 0;
            if (string.IsNullOrWhiteSpace(satir.Departman))
            {
                Sorun("Departman", "Zorunlu alan.");
            }
            else if (departmanlar.TryGetValue(Normalize(satir.Departman), out var id))
            {
                departmanId = id;
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
            if (durum is not ("active" or "passive"))
            {
                Sorun("Durum", "'active' veya 'passive' olmalı (boş bırakılırsa active).");
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
            if (eposta is not null && !eposta.Contains('@'))
            {
                Sorun("Kişisel E-posta", "Geçerli bir e-posta olmalı.");
            }

            // Mükerrer kontrolü yalnız SORUNSUZ satırlar için anlamlıdır: hatalı satır
            // zaten aktarılmayacak, ayrıca "mükerrer" saymak sayımları çift bozar.
            if (satirSorunlari.Count == 0 && tc is not null && mevcutTcler.Contains(tc))
            {
                mukerrer++;
                continue;
            }

            if (satirSorunlari.Count > 0)
            {
                sorunlar.AddRange(satirSorunlari);
                continue;
            }

            gecerli.Add(new EmployeeUpsertModel(
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
                    Iban: iban)));
        }

        return new ValidatedImport(gecerli, sorunlar, bilinmeyenDepartmanlar, mukerrer);
    }
}
