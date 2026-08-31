using IKPro.Application.Features.Auth;

namespace IKPro.Application.Common.Interfaces;

/// <summary>
/// Kimlik işlemleri soyutlaması. Infrastructure'da ASP.NET Core Identity + JWT ile
/// implemente edilir; Application katmanı Identity paketlerine bağımlı kalmaz.
/// </summary>
public interface IIdentityService
{
    /// <summary>E-posta + şifre ile giriş; başarısızsa <c>UnauthorizedException</c>.</summary>
    Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// E-posta ile kayıtlı bir hesap var mı (işe alım öncesi erken doğrulama).
    ///
    /// Semantik: e-posta önce yönlendirme DİZİNİNDEN kiracıya çözülür, sonra o
    /// kiracının kullanıcı deposunda aranır. Dizinde kayıt yoksa <c>false</c> döner.
    /// Yani cevap, dizinin tutarlı olduğu varsayımına dayanır — dizinden BAĞIMSIZ
    /// bir kontrol gerekiyorsa (ör. dizin ile kullanıcı deposunun SAPMASINI tespit
    /// etmek) bu metot kullanılamaz, depoya doğrudan bakılmalıdır.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracı provizyonunda ilk <c>hr-admin</c> login'ini verili kiracıda oluşturur.
    /// Şifresiz oluşturulur; kullanıcıya <b>davet</b> e-postası (şifre-belirleme token'ı)
    /// gönderilir. E-posta çakışmasında <c>ConflictException</c>.
    /// </summary>
    Task CreateTenantAdminAsync(int tenantId, string name, string email, string companyName, CancellationToken cancellationToken);

    /// <summary>
    /// Verili kiracıdaki, HENÜZ ŞİFRE BELİRLEMEMİŞ bir kullanıcıya davet
    /// e-postasını yeniden gönderir.
    ///
    /// Neden var: davet e-postası kaybolabilir, spam'e düşebilir ya da provizyon
    /// e-posta adımında kesilmiş olabilir. Bu uç olmadan operatörün elindeki tek
    /// kurtarma yolu kiracıyı silip baştan kurmaktı — yani bir e-posta gitmedi
    /// diye VERİ SİLMEK.
    ///
    /// Şifresi olan hesaba davet gönderilmez (<c>ConflictException</c>): davet,
    /// hesabı ilk kez kurmak içindir; etkin hesabın parolasını yenilemek ayrı bir
    /// akıştır ve o akışı bu uca yaptırmak, operatöre kullanıcı parolası
    /// sıfırlatma yetkisi vermek olurdu.
    /// </summary>
    Task ResendInviteAsync(int tenantId, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Davet token'ıyla kullanıcının ilk şifresini belirler ve hesabı etkinleştirir.
    /// Geçersiz/süresi geçmiş token → <c>ValidationException</c>.
    /// </summary>
    Task AcceptInviteAsync(string email, string token, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// İşe alınan aday için personel-bağlı, şifresiz <c>employee</c> login'i oluşturur
    /// ve davet e-postası gönderir. Aynı personel/e-posta için idempotenttir; mevcut
    /// kullanıcıyı tamamlayarak kimliğini döndürür. Farklı e-posta çakışmasında
    /// <c>ConflictException</c>.
    /// </summary>
    Task<string> CreateEmployeeLoginAsync(int employeeId, string name, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Personel durumuyla bağlı Identity hesabını eşitler. Pasife alınırken mevcut
    /// refresh token'ları da iptal edilir. Login'i olmayan personelde no-op'tur.
    /// </summary>
    Task SetEmployeeAccessAsync(int employeeId, bool isActive, CancellationToken cancellationToken);

    /// <summary>
    /// Personelin manager rolünü idempotent biçimde verir veya kaldırır. HR-admin korunur;
    /// diğer hesaplarda temel employee rolü geri yüklenir. Bağlı login yoksa kaldırma
    /// no-op, rol verme ise <c>ConflictException</c> olur.
    /// </summary>
    Task SetEmployeeManagerRoleAsync(int employeeId, bool isManager, CancellationToken cancellationToken);

    /// <summary>Refresh token rotasyonu: eskisini iptal eder, yeni çift üretir.</summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Refresh token'ı iptal eder (çıkış).</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task<UserDto?> GetUserAsync(string userId, CancellationToken cancellationToken);
}
