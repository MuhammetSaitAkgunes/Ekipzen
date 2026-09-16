namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// E-posta → kiracı yönlendirme dizini. Kiracı veritabanları ayrıldığında
/// (Faz 2) login'in hangi veritabanına bakacağını buradan çözülür.
///
/// TÜRETİLMİŞ bir tablodur: asıl doğruluk kiracı veritabanındaki kullanıcılardır.
/// Bu yüzden <see cref="RebuildForTenantAsync"/> ile yetkili kaynaktan yeniden
/// kurulabilir.
///
/// Neden Identity'den ayrı: dizine artık login yolu ve bağlantı katmanı da
/// bakıyor; Identity'nin içinde kalsaydı bu katmanlar Identity'ye bağımlı olurdu.
/// </summary>
public interface ITenantDirectory
{
    /// <summary>
    /// E-postayı kiracıya rezerve eder. İdempotenttir: aynı kiracı için tekrar
    /// çağrılırsa no-op; BAŞKA bir kiracıya aitse <c>ConflictException</c>.
    /// "Tek e-posta = tek kiracı" kuralı burada, veritabanı seviyesinde uygulanır.
    /// </summary>
    Task ReserveAsync(string email, int tenantId, CancellationToken cancellationToken);

    /// <summary>E-postanın hangi kiracıya ait olduğunu döner; yoksa null.</summary>
    Task<int?> FindTenantIdAsync(string email, CancellationToken cancellationToken);

    /// <summary>Kiracının tüm dizin satırlarını siler; silinen satır sayısını döner.</summary>
    Task<int> RemoveForTenantAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracının dizinini verilen normalize e-posta listesinden yeniden kurar.
    /// Başka kiracıya ait e-postalar ATLANIR ve sonuçta raporlanır — tek çakışma
    /// yüzünden tüm yeniden kurma başarısız olmamalı (bu bir kurtarma aracıdır).
    /// </summary>
    Task<RebuildOutcome> RebuildForTenantAsync(
        int tenantId, IReadOnlyList<string> normalizedEmails, CancellationToken cancellationToken);
}

/// <summary>Yeniden kurma sonucu: yazılan kayıt ve atlanan (çakışan) e-postalar.</summary>
public sealed record RebuildOutcome(int YazilanKayit, IReadOnlyList<string> CakisanEpostalar);
