namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// Ham satır: ayrıştırıcının çıktısı. Değerler yorumlanmamış metindir —
/// tarih, TC, IBAN gibi alanların anlamlandırılması doğrulayıcıya aittir.
/// </summary>
public sealed record ImportRow(
    int SatirNo,
    string? Ad,
    string? Soyad,
    string? Unvan,
    string? Departman,
    string? IseGirisTarihi,
    string? TcKimlikNo,
    string? Durum,
    string? KisiselEposta,
    string? Telefon,
    string? Iban);

/// <summary>
/// Tek bir sorun. <paramref name="SatirNo"/> EXCEL satır numarasıdır (başlık 1,
/// ilk veri 2) — kullanıcı hatayı dosyada doğrudan bulabilsin diye.
/// </summary>
public sealed record ImportRowIssueDto(int SatirNo, string Alan, string Mesaj);

/// <summary>Önizleme raporu: hiçbir şey kaydedilmeden ne olacağını gösterir.</summary>
public sealed record ImportPreviewDto(
    int ToplamSatir,
    int GecerliSatir,
    int HataliSatir,
    int MukerrerSatir,
    IReadOnlyList<string> BilinmeyenDepartmanlar,
    IReadOnlyList<ImportRowIssueDto> Sorunlar);

/// <summary>Aktarım sonucu.</summary>
public sealed record ImportResultDto(
    int OlusturulanSatir,
    int AtlananSatir,
    IReadOnlyList<ImportRowIssueDto> Sorunlar);
