namespace Ekipzen.API.Services;

/// <summary>
/// Doğrulanmamış kiracı zamanlanmış temizlik ayarları (bölüm: <c>Cleanup:UnverifiedTenants</c>).
/// Yıkıcı işlem olduğu için varsayılan KAPALI; üretimde bilinçli açılır.
/// </summary>
public sealed class UnverifiedTenantCleanupOptions
{
    public const string SectionName = "Cleanup:UnverifiedTenants";

    /// <summary>Arka plan temizliği çalışsın mı (varsayılan false — opt-in).</summary>
    public bool Enabled { get; set; }

    /// <summary>Temizlik geçişleri arasındaki süre (saat).</summary>
    public double IntervalHours { get; set; } = 24;

    /// <summary>Bundan eski + doğrulanmamış kiracılar silinir (gün).</summary>
    public int OlderThanDays { get; set; } = 30;
}
