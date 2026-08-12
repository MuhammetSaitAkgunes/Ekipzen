using IKPro.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IKPro.Infrastructure.Tenancy;

/// <inheritdoc cref="ITenantDatabaseNamer"/>
public sealed class TenantDatabaseNamer(IConfiguration configuration) : ITenantDatabaseNamer
{
    /// <summary>Varsayılan önek: sunucuda İK Pro'ya ait katalogları bir bakışta ayırır.</summary>
    public const string VarsayilanOnek = "IKPro_";

    public string Prefix { get; } = configuration["Tenancy:DatabasePrefix"] ?? VarsayilanOnek;

    /// <summary>
    /// <c>{önek}{slug}</c> — slug'daki tireler alt çizgiye çevrilir.
    ///
    /// Tire dönüştürülür çünkü SQL tanımlayıcılarında sorun çıkarabilir ve katalog
    /// adı doğrulaması (<c>ITenantDatabaseProvisioner</c>) yalnız <c>[A-Za-z0-9_]</c>
    /// kabul eder. Dönüşüm ÇAKIŞMA ÜRETMEZ: slug deseni <c>^[a-z0-9-]+$</c> olduğu
    /// için alt çizgi içeren bir slug zaten var olamaz, dolayısıyla iki farklı slug
    /// aynı ada indirgenemez.
    ///
    /// Uzunluk da güvende: slug en fazla 64 karakter, önekle birlikte 70'ler —
    /// SQL Server'ın 128 sınırının altında.
    /// </summary>
    public string For(string slug) => Prefix + slug.Replace('-', '_');
}
