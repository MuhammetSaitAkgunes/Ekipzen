using ClosedXML.Excel;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// .xlsx → ham satırlar. Yalnızca OKUR: iş kuralı bilmez, doğrulama yapmaz.
/// Bu ayrım sayesinde doğrulayıcı Excel olmadan, düz nesnelerle test edilebilir.
/// </summary>
public static class EmployeeImportParser
{
    public const int MaksimumSatir = 1000;

    public static (IReadOnlyList<ImportRow> Satirlar, IReadOnlyList<ImportRowIssueDto> Sorunlar)
        Ayristir(Stream stream)
    {
        var sorunlar = new List<ImportRowIssueDto>();

        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        // Eşleme konuma değil BAŞLIĞA bağlanır: kullanıcı sütunları taşıyabilir.
        var sutunlar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var baslik = cell.GetString().Trim();
            if (baslik.Length > 0)
            {
                sutunlar[baslik] = cell.Address.ColumnNumber;
            }
        }

        var eksik = EmployeeImportTemplate.ZorunluSutunlar
            .Where(s => !sutunlar.ContainsKey(s))
            .ToArray();
        if (eksik.Length > 0)
        {
            // Başlık bozuksa satırları yorumlamak yanıltıcı olur; erken dönülür.
            sorunlar.Add(new ImportRowIssueDto(1, "Başlık",
                $"Zorunlu sütunlar eksik: {string.Join(", ", eksik)}. Şablonu indirip kullanın."));
            return ([], sorunlar);
        }

        string? Deger(IXLRow row, string baslik)
        {
            if (!sutunlar.TryGetValue(baslik, out var col)) return null;
            var metin = row.Cell(col).GetFormattedString().Trim();
            return metin.Length > 0 ? metin : null;
        }

        var satirlar = new List<ImportRow>();
        var sonSatir = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= sonSatir; r++)
        {
            var row = sheet.Row(r);
            var satir = new ImportRow(
                r,
                Deger(row, "Ad"),
                Deger(row, "Soyad"),
                Deger(row, "Unvan"),
                Deger(row, "Departman"),
                Deger(row, "İşe Giriş Tarihi"),
                Deger(row, "TC Kimlik No"),
                Deger(row, "Durum"),
                Deger(row, "Kişisel E-posta"),
                Deger(row, "Telefon"),
                Deger(row, "IBAN"));

            // Tamamen boş satır veri değildir (Excel dosyalarında çok yaygın).
            var bosMu = satir.Ad is null && satir.Soyad is null && satir.Unvan is null
                && satir.Departman is null && satir.IseGirisTarihi is null
                && satir.TcKimlikNo is null && satir.Durum is null
                && satir.KisiselEposta is null && satir.Telefon is null && satir.Iban is null;
            if (bosMu) continue;

            satirlar.Add(satir);

            if (satirlar.Count > MaksimumSatir)
            {
                sorunlar.Add(new ImportRowIssueDto(r, "Dosya",
                    $"En fazla {MaksimumSatir} satır aktarılabilir. Dosyayı bölün."));
                return ([], sorunlar);
            }
        }

        return (satirlar, sorunlar);
    }
}
