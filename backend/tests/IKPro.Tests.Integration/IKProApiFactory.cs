using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace IKPro.Tests.Integration;

/// <summary>
/// Entegrasyon testleri için API fabrikası. Development ortamında ayağa kalkar;
/// Program.cs migration + demo seed'i otomatik uygular. Deterministik koşu için
/// test veritabanı her fabrika oluşumunda sıfırdan kurulur.
///
/// ÖNEMLİ: Bağlantı dizesi ortam değişkeniyle geçilir. Minimal hosting'de
/// (WebApplication.CreateBuilder) fabrikanın ConfigureAppConfiguration kaynakları
/// uygulamanın kendi appsettings.*.json kaynaklarından ÖNCE geldiği için ezilir;
/// ortam değişkenleri ise appsettings'ten sonra eklenir ve her zaman kazanır.
/// </summary>
public sealed class IKProApiFactory : WebApplicationFactory<Program>
{
    private const string TestDatabaseName = "IKProDb_Test";

    /// <summary>Testlerin ürettiği tüm kiracı kataloglarının öneki.</summary>
    internal const string KiraciKatalogOneki = "IKPro_test_";
    private const string PlatformTestDatabaseName = "IKProPlatform_Test";

    /// <summary>Testlerde kiracı provizyon ucunu çağırmak için platform anahtarı.</summary>
    public const string PlatformKey = "test-platform-key";

    /// <summary>Davet e-postalarının (outbox) yazıldığı kök — davet token'ını okumak için.</summary>
    public string StorageRoot { get; }

    /// <summary>
    /// Sunucu bağlantısı IKPRO_TEST_SQL ile geçersiz kılınabilir; varsayılan yerel
    /// Windows kimlik doğrulamasıdır. CI'da (Linux + SQL Server servis konteyneri)
    /// Trusted_Connection kullanılamadığı için sa bağlantısı bu değişkenle verilir.
    /// Veritabanı adı her zaman burada belirlenir, dışarıdan gelen değer ezilir.
    /// </summary>
    private const string DefaultServerConnection =
        "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string database) =>
        new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("IKPRO_TEST_SQL") ?? DefaultServerConnection)
        {
            InitialCatalog = database,
        }.ConnectionString;

    /// <summary>Testlerin de kullanabilmesi için açık: kiracı katalogları aynı sunucuda doğar.</summary>
    internal static readonly string TestConnectionString = ConnectionFor(TestDatabaseName);
    private static readonly string PlatformTestConnectionString = ConnectionFor(PlatformTestDatabaseName);

    public IKProApiFactory()
    {
        StorageRoot = Path.Combine(Path.GetTempPath(), "ikpro-test-storage", Guid.NewGuid().ToString("N"));

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformConnection", PlatformTestConnectionString);
        Environment.SetEnvironmentVariable("Platform__ProvisioningKey", PlatformKey);
        // Testler çok sayıda login/provizyon yapar → rate limit'i etkisiz kıl.
        // ("signup" politikası, self-servis kayıt ucuyla birlikte kaldırıldı.)
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitPerMinute", "1000000");
        Environment.SetEnvironmentVariable("Storage__Root", StorageRoot);
        // Kiracı katalogları testlere ÖZEL önekle doğar. Bu olmadan test koşumu,
        // geliştiricinin makinesindeki gerçek IKPro_demo / IKPro_globex
        // veritabanlarını düşürüp yeniden kurardı — yani "testleri çalıştırmak"
        // demo ortamını silmek anlamına gelirdi.
        Environment.SetEnvironmentVariable("Tenancy__DatabasePrefix", KiraciKatalogOneki);

        DropDatabase(TestDatabaseName);
        DropDatabase(PlatformTestDatabaseName);
        KiraciKataloglariniSupur();
    }

    /// <summary>
    /// Testlerin ürettiği TÜM kiracı kataloglarını (<c>IKPro_test_*</c>) koşum
    /// başında süpürür — demo kiracıları dahil.
    ///
    /// Testler kendi açtıklarını bitişte düşürüyor (bkz.
    /// <c>TenancyTestBase.DisposeAsync</c>); bu süpürme iki şey için: koşum
    /// ORTASINDA çöken bir süreçten kalan artıklar ve her koşumda sıfırdan kurulan
    /// demo kiracıları. Olmasaydı artık katalog bir sonraki koşuda "veritabanı
    /// zaten var" hatasıyla provizyonu reddettirirdi — yani tek bir çökme,
    /// sonraki tüm koşumları kırardı.
    /// </summary>
    private static void KiraciKataloglariniSupur()
    {
        using var connection = new SqlConnection(ConnectionFor("master"));
        connection.Open();

        List<string> adlar = [];
        using (var oku = connection.CreateCommand())
        {
            oku.CommandText =
                $"SELECT name FROM sys.databases WHERE name LIKE '{KiraciKatalogOneki.Replace("_", "[_]")}%';";
            using var reader = oku.ExecuteReader();
            while (reader.Read())
            {
                adlar.Add(reader.GetString(0));
            }
        }

        foreach (var ad in adlar)
        {
            DropDatabase(ad);
        }
    }

    private static void DropDatabase(string databaseName)
    {
        using var connection = new SqlConnection(ConnectionFor("master"));
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID('{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// Tüm API test sınıfları tek koleksiyonda koşar: tek host + tek test veritabanı,
/// sınıflar arası paralel koşu çakışması olmaz.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<IKProApiFactory>
{
    public const string Name = "api";
}
