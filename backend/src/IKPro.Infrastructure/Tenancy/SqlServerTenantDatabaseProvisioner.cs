using IKPro.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IKPro.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ITenantDatabaseProvisioner"/>'ın SQL Server uygulaması.
///
/// Bağlantı, paylaşılan dizeden katalog adı <c>master</c> yapılarak TÜRETİLİR:
/// kiracı veritabanları aynı sunucuda doğar, dolayısıyla ayrı bir bağlantı ayarı
/// eklemek iki yerde tutulan ve zamanla ayrışan bir gerçek yaratırdı.
/// </summary>
public sealed partial class SqlServerTenantDatabaseProvisioner(
    IConfiguration configuration,
    ILogger<SqlServerTenantDatabaseProvisioner> logger) : ITenantDatabaseProvisioner
{
    /// <summary>
    /// Kiracı veritabanlarının harmanlaması AÇIKÇA verilir, sunucu varsayılanına
    /// BIRAKILMAZ. Sebebi somut: yerel makineler Turkish_CI_AS, CI konteyneri
    /// SQL_Latin1_General_CP1_CI_AS ile geliyor. Varsayılana bırakılsaydı aynı kod
    /// iki ortamda farklı sıralama ve farklı eşitlik davranışı üretirdi — "ş"/"s"
    /// ayrımı, İ/ı büyük-küçük eşlemesi gibi Türkçe'ye özgü yerlerde.
    /// </summary>
    private const string Harmanlama = "Turkish_CI_AS";

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,128}$")]
    private static partial Regex GecerliAd();

    public async Task<bool> ExistsAsync(string databaseName, CancellationToken cancellationToken)
    {
        var ad = Dogrula(databaseName);

        // Bu sorgu PARAMETRELİ olabiliyor (ad burada tanımlayıcı değil, veri) —
        // olabildiğinde parametre kullanılır.
        await using var baglanti = await MasterAcAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = "SELECT 1 FROM sys.databases WHERE name = @ad;";
        komut.Parameters.AddWithValue("@ad", ad);
        return await komut.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task CreateAsync(string databaseName, CancellationToken cancellationToken)
    {
        var ad = Dogrula(databaseName);
        if (await ExistsAsync(ad, cancellationToken))
        {
            logger.LogInformation("Kiracı veritabanı {Ad} zaten var; oluşturma atlandı.", ad);
            return;
        }

        await using var baglanti = await MasterAcAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        // Enterpolasyon burada KAÇINILMAZ: CREATE DATABASE parametre kabul etmez.
        // Güvenlik Dogrula()'dan gelir — köşeli parantez tek başına yeterli değildir
        // (ad ']' içerirse kaçış kırılır), bu yüzden desen zaten ']' kabul etmiyor.
        komut.CommandText = $"CREATE DATABASE [{ad}] COLLATE {Harmanlama};";
        await komut.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Kiracı veritabanı oluşturuldu: {Ad} ({Harmanlama})", ad, Harmanlama);
    }

    public async Task DropAsync(string databaseName, CancellationToken cancellationToken)
    {
        var ad = Dogrula(databaseName);
        if (!await ExistsAsync(ad, cancellationToken))
        {
            return;
        }

        // Havuzdaki boşta bağlantılar da "aktif oturum" sayılır ve DROP'u engeller;
        // SINGLE_USER onları koparır, havuzun temizlenmesi ise havuzun az sonra
        // yok olacak bir katalogla yeniden bağlanmaya çalışmasını önler.
        //
        // YALNIZ BU KATALOĞUN havuzu temizlenir, ClearAllPools() DEĞİL: havuzlar
        // bağlantı dizesine göre anahtarlanır ve ClearAllPools süreçteki HER
        // veritabanının havuzunu boşaltırdı. Üretimde bu, tek bir kiracının
        // silinmesinin diğer bütün kiracıları bağlantı yeniden kurmaya zorlaması
        // demek olurdu — silinen kiracıyla hiç ilgisi olmayan isteklerde gecikme.
        using (var hedef = new SqlConnection(BaglantiDizesi(ad)))
        {
            SqlConnection.ClearPool(hedef);
        }

        await using var baglanti = await MasterAcAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText =
            $"ALTER DATABASE [{ad}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{ad}];";
        await komut.ExecuteNonQueryAsync(cancellationToken);
        logger.LogWarning("Kiracı veritabanı düşürüldü: {Ad}", ad);
    }

    /// <summary>
    /// Adın SQL'e gömülmeye uygun olduğunu kanıtlar. Geçersizse İSTİSNA fırlatır —
    /// sessizce temizlemek (ör. geçersiz karakterleri atmak) iki farklı kiracıyı
    /// aynı ada indirgeyebilirdi.
    /// </summary>
    private static string Dogrula(string databaseName)
    {
        if (!GecerliAd().IsMatch(databaseName ?? string.Empty))
        {
            throw new ArgumentException(
                $"Geçersiz veritabanı adı: '{databaseName}'. Yalnız harf, rakam ve alt çizgi " +
                "kabul edilir (1–128 karakter).",
                nameof(databaseName));
        }

        return databaseName!;
    }

    private async Task<SqlConnection> MasterAcAsync(CancellationToken cancellationToken)
    {
        var baglanti = new SqlConnection(BaglantiDizesi("master"));
        await baglanti.OpenAsync(cancellationToken);
        return baglanti;
    }

    /// <summary>
    /// Paylaşılan dizeden verilen katalog için bağlantı dizesi türetir. Kiracı
    /// veritabanları aynı sunucuda doğduğu için sunucu/kimlik bilgisi tek yerde kalır.
    /// Bağlantı çözücü de aynı türetmeyi yapar — havuz anahtarları böylece eşleşir.
    /// </summary>
    private string BaglantiDizesi(string katalog)
    {
        var paylasilan = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil.");

        return new SqlConnectionStringBuilder(paylasilan) { InitialCatalog = katalog }.ConnectionString;
    }
}
