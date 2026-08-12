namespace IKPro.Application.Features.Tenancy;

/// <summary>
/// Kiracının katalog adını slug'dan üretir. Ad provizyonda BİR KEZ üretilir ve
/// bir daha değişmez (bkz. <c>Tenant.DatabaseName</c>).
/// </summary>
public static class TenantDatabaseNaming
{
    /// <summary>Ortak önek: sunucuda İK Pro'ya ait katalogları bir bakışta ayırır.</summary>
    public const string Onek = "IKPro_";

    /// <summary>
    /// <c>IKPro_{slug}</c> — slug'daki tireler alt çizgiye çevrilir.
    ///
    /// Tire dönüştürülür çünkü SQL tanımlayıcılarında sorun çıkarabilir ve katalog
    /// adı doğrulaması (<c>ITenantDatabaseProvisioner</c>) yalnız <c>[A-Za-z0-9_]</c>
    /// kabul eder. Dönüşüm ÇAKIŞMA ÜRETMEZ: slug deseni <c>^[a-z0-9-]+$</c> olduğu
    /// için alt çizgi içeren bir slug zaten var olamaz, dolayısıyla iki farklı slug
    /// aynı ada indirgenemez.
    ///
    /// Uzunluk da güvende: slug en fazla 64 karakter, önekle birlikte 70 —
    /// SQL Server'ın 128 sınırının altında.
    /// </summary>
    public static string For(string slug) => Onek + slug.Replace('-', '_');
}
