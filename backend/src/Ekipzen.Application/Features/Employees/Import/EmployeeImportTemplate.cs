using ClosedXML.Excel;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// İçe aktarma şablonu. Başlıklar buradan üretilir ve ayrıştırıcı da buradan
/// okur — tek kaynak olduğu için şablon ile ayrıştırıcı ayrışamaz.
/// </summary>
public static class EmployeeImportTemplate
{
    public const string SayfaAdi = "Personel";

    public static readonly string[] SutunBasliklari =
    [
        "Ad", "Soyad", "Unvan", "Departman", "İşe Giriş Tarihi",
        "TC Kimlik No", "Durum", "Kişisel E-posta", "Telefon", "IBAN",
    ];

    /// <summary>Zorunlu sütunlar — başlık doğrulamasında aranır.</summary>
    public static readonly string[] ZorunluSutunlar =
        ["Ad", "Soyad", "Unvan", "Departman", "İşe Giriş Tarihi"];

    public static byte[] Olustur()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SayfaAdi);

        for (var i = 0; i < SutunBasliklari.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = SutunBasliklari[i];
            cell.Style.Font.Bold = true;
        }

        // Örnek satır: kullanıcı beklenen biçimi görsün (özellikle tarih).
        string[] ornek =
        [
            "Ahmet", "Yılmaz", "Yazılım Uzmanı", "Yazılım", "01.03.2026",
            "12345678901", "active", "ahmet@ornek.com", "0555 111 22 33", "TR000000000000000000000000",
        ];
        for (var i = 0; i < ornek.Length; i++)
        {
            // Metin olarak yazılır: TC ve IBAN sayıya dönüşüp bilimsel gösterime kaymasın.
            sheet.Cell(2, i + 1).SetValue(ornek[i]);
            sheet.Cell(2, i + 1).Style.NumberFormat.Format = "@";
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
