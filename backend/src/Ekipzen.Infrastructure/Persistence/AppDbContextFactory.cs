using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Tasarım zamanı (dotnet ef) DbContext üretici. Migration üretirken uygulama host'unu
/// başlatmadan bağlam kurar. Bağlantı dizesi EKIPZEN_CONNECTION ortam değişkeninden ya da
/// varsayılan yerel MSSQL'den alınır (migration üretimi için içerik önemsizdir).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EKIPZEN_CONNECTION")
            ?? "Server=localhost,1433;Database=EkipzenDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
