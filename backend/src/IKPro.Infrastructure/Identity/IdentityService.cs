using FluentValidation;
using FluentValidation.Results;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Auth;
using IKPro.Domain.Constants;
using IKPro.Domain.Entities.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> implementasyonu: ASP.NET Core Identity + JWT.
/// Lockout SignInManager üzerinden işler; refresh token'lar rotasyonla yenilenir
/// ve DB'de hash'lenmiş saklanır.
/// </summary>
public sealed class IdentityService(
    ITenantScopeFactory tenantScopeFactory,
    JwtTokenService tokenService,
    ICurrentTenant currentTenant,
    IEmailSender emailSender,
    IConfiguration configuration,
    IPlatformDbContext platform,
    ITenantDirectory directory,
    ITenantRegistry registry,
    ITenantAccessGuard accessGuard,
    ILogger<IdentityService> logger) : IIdentityService
{
    private const string InvalidCredentialsMessage = "E-posta veya şifre hatalı.";

    /// <summary>
    /// Kimlik servislerini KİRACIYA SABİTLENMİŞ bir kapsamdan çözer.
    ///
    /// <see cref="UserManager{TUser}"/>, <c>AddEntityFrameworkStores&lt;AppDbContext&gt;</c>
    /// ile uygulama context'ine bağlıdır; o da bağlantısını <see cref="ICurrentTenant"/>'tan
    /// alır. Bu yüzden bu servisler constructor'da enjekte EDİLEMEZ: DI kapsamı, kiracı
    /// daha öğrenilmeden kurulur (login'de kiracı ancak dizin sorgusundan SONRA bilinir).
    /// Kiracı başına veritabanına geçildiğinde (Faz 2b) constructor enjeksiyonu YANLIŞ
    /// katalogda arama yapardı ve bu sessizce olurdu.
    ///
    /// İkisi AYNI kapsamdan çözülür — ayrı kapsamlardan çözülürse
    /// <see cref="SignInManager{TUser}.CheckPasswordSignInAsync"/>'in yazdığı lockout
    /// sayaçları başka bir context'e gider ve kilitleme sessizce çalışmaz.
    /// </summary>
    private static (UserManager<ApplicationUser> Kullanicilar, SignInManager<ApplicationUser> Girisler)
        KimlikServisleri(ITenantScope kapsam) =>
        (kapsam.Services.GetRequiredService<UserManager<ApplicationUser>>(),
         kapsam.Services.GetRequiredService<SignInManager<ApplicationUser>>());

    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        // Dizin login'in ÖN adımıdır: Faz 2'de kullanıcı tablosu kiracının kendi
        // veritabanında olacak, dolayısıyla hangi veritabanına bakılacağı
        // bilinmeden kullanıcı aranamaz. Dizinde kayıt yoksa (ör. dizin bütünlüğü
        // bozulmuşsa) hesabın var olup olmadığını sızdırmadan genel mesajla reddet.
        var dizinKiraciId = await directory.FindTenantIdAsync(email, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentialsMessage);

        // Kiracı ancak BURADA, dizin sorgusundan sonra bilinir — bu yüzden kimlik
        // servisleri constructor'dan değil, kiracıya sabitlenmiş bu kapsamdan çözülür.
        using var kapsam = tenantScopeFactory.Create(dizinKiraciId);
        var (kullanicilar, girisler) = KimlikServisleri(kapsam);

        var user = await kullanicilar.FindByEmailAsync(email)
            ?? throw new UnauthorizedException(InvalidCredentialsMessage);

        if (user.TenantId != dizinKiraciId)
        {
            // Dizin ile Identity'nin kendi kaydı UYUŞMUYOR: bu bir bütünlük hatasıdır.
            // Faz 2'de bu, yanlış kiracının veritabanına bakmak demek olurdu — bu
            // yüzden yüksek sesle loglanır. Kullanıcıya yine genel mesaj dönülür,
            // iç tutarsızlık sızdırılmaz.
            logger.LogError(
                "Dizin/Identity tutarsızlığı: E-posta={Email}, DizinTenantId={DizinTenantId}, " +
                "KullaniciTenantId={KullaniciTenantId}",
                email, dizinKiraciId, user.TenantId);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Hesap pasif durumda. Yöneticinizle iletişime geçin.");
        }

        var result = await girisler.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            throw new UnauthorizedException("Çok sayıda hatalı deneme; hesap geçici olarak kilitlendi.");
        }

        if (!result.Succeeded)
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        // Kiracı erişim kontrolü (aktif/donmuş/vs.) artık burada YAPILMAZ — tek
        // doğruluk kaynağı IssueTokensAsync'in başındaki erişim kapısıdır (bkz.
        // orada). Burada tekrarlamak iki kaynak demekti ve refresh yolunu (aynı
        // kapıdan geçmeyen eski RefreshAsync) korumasız bırakmıştı.
        return await IssueTokensAsync(kapsam, user, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        // Dizinde kayıt yoksa kullanıcı da yoktur — kapsam açmaya gerek kalmadan
        // cevap verilir. Dizin, e-postanın hangi kiracıya ait olduğunu bilen tek yer.
        var kiraciId = await directory.FindTenantIdAsync(email, cancellationToken);
        if (kiraciId is null)
        {
            return false;
        }

        // FAZ 2B NOTU: dizin TÜRETİLMİŞ bir tablodur ve geri yükleme sonrası
        // sapabilir — karşılığı olmayan bir TenantId'ye işaret edebilir (test
        // altyapısı bunu bilerek üretiyor: PlatformKatmaniTests, TenantId = -1).
        // Bugün zararsız çünkü çözücü tenantId'yi yok sayıyor. Kiracı başına
        // veritabanına geçilince Create(...) çözülemeyen kiracıda FIRLATIR ve bu
        // metot sözleşmesi gereği boolean dönmesi gerekirken 500'e dönüşür —
        // provizyon, self-servis kayıt ve işe alım yollarını kırar. O fazda
        // "çözülemeyen kiracı → false" davranışı eklenmeli.
        using var kapsam = tenantScopeFactory.Create(kiraciId.Value);
        var (kullanicilar, _) = KimlikServisleri(kapsam);
        return await kullanicilar.FindByEmailAsync(email) is not null;
    }

    public async Task CreateTenantAdminAsync(
        int tenantId, string name, string email, string companyName, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = name,
            Initials = DeriveInitials(name),
            TenantId = tenantId,
        };

        // Kiracı parametreden gelir (provizyon yolu) — dizine sormaya gerek yok.
        using var kapsam = tenantScopeFactory.Create(tenantId);

        // Dizine CreateInvitedUserAsync KOŞULSUZ yazar (idempotent ITenantDirectory.ReserveAsync).
        // Çağıran burada directory.ReserveAsync ile önceden rezervasyon yapmış olabilir
        // (TenantOnboarding) — o durumda bu ikinci yazım aynı kiracı için sessizce
        // no-op'tur. Rezervasyon yapılmamışsa (ör. eski bir çağıran unutursa) bu
        // çağrı yine de dizine yazar; "dizine hiç yazılmadı" sınıfı hatalar artık
        // derleyici tarafından değil, tek bir kod yolu tarafından imkansız kılınır.
        await CreateInvitedUserAsync(kapsam, user, Roles.HrAdmin, companyName, cancellationToken);
    }

    public async Task CreateEmployeeLoginAsync(
        int employeeId, string name, string email, CancellationToken cancellationToken)
    {
        // İşe alım her zaman kimliği doğrulanmış bir hr-admin tarafından yapılır →
        // yeni personel login'i o admin'in kiracısına bağlanır.
        var tenantId = currentTenant.TenantIdOrThrow();
        var companyName = await TenantNameAsync(tenantId, cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = name,
            Initials = DeriveInitials(name),
            EmployeeId = employeeId,
            TenantId = tenantId,
        };
        using var kapsam = tenantScopeFactory.Create(tenantId);
        await CreateInvitedUserAsync(kapsam, user, Roles.Employee, companyName, cancellationToken);
    }

    public async Task AcceptInviteAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        // Davet kabulü ANONİM bir istektir — ambient kapsamda kiracı yoktur.
        // Kullanıcının hangi kiracıya ait olduğu yalnız dizinden bilinir.
        var kiraciId = await directory.FindTenantIdAsync(email, cancellationToken)
            ?? throw new ValidationException([new ValidationFailure("email", "Davet bulunamadı.")]);

        using var kapsam = tenantScopeFactory.Create(kiraciId);
        var (kullanicilar, _) = KimlikServisleri(kapsam);

        var user = await kullanicilar.FindByEmailAsync(email)
            ?? throw new ValidationException([new ValidationFailure("email", "Davet bulunamadı.")]);

        // Şifresiz oluşturulan hesabın ilk şifresini davet (reset) token'ıyla belirler.
        var result = await kullanicilar.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors
                .Select(e => new ValidationFailure("token", e.Description)));
        }

        // Şifre belirlendi = e-posta doğrulandı. Self-servis kayıtta Provisioning
        // durumunda oluşturulan kiracıyı ilk admin kabulünde etkinleştir.
        var tenant = await platform.Tenants.FirstOrDefaultAsync(
            t => t.Id == user.TenantId, cancellationToken);
        if (tenant is { Status: TenantStatus.Provisioning })
        {
            tenant.Status = TenantStatus.Active;
            await platform.SaveChangesAsync(cancellationToken);
            // Durum değişti — kütüğü düşür ki erişim kapısı bunu ANINDA görsün.
            registry.Invalidate(tenant.Id);
        }
    }

    /// <summary>
    /// Kullanıcıyı ŞİFRESİZ oluşturur, rol atar ve şifre-belirleme (davet) token'ını
    /// e-postayla gönderir. Kullanıcı <c>accept-invite</c> ile hesabını etkinleştirir.
    ///
    /// Dizine KOŞULSUZ ÖNCE yazar, kullanıcıyı SONRA oluşturur (bkz.
    /// <see cref="ITenantDirectory.ReserveAsync"/> — idempotenttir, çağıran önceden
    /// rezervasyon yapmışsa aynı kiracı için sessizce no-op'tur). Sıra bilinçlidir:
    /// userManager.CreateAsync hemen commit eder ve geri alınamaz. Ters sırada (önce
    /// CreateAsync, sonra ReserveAsync) ReserveAsync bir ConflictException fırlatırsa
    /// (TOCTOU — başka bir istek arada aynı e-postayı kaptı) kullanıcı uygulama
    /// DB'sinde KALICI olarak var ama dizinde YOK kalırdı: Faz 1b'de asla giriş
    /// yapamaz, rolü de atanmamış olur, ve EmailExistsAsync artık true döndüğünden
    /// aynı kişiyi yeniden işe alma denemesi (en çok <c>CreateEmployeeLoginAsync</c>
    /// üzerinden — ürünün en yüksek hacimli kullanıcı yaratma yolu) sonsuza kadar 409
    /// alır. Dizine önce yazmak bu sınıf hatayı imkansız kılar.
    /// </summary>
    private async Task CreateInvitedUserAsync(
        ITenantScope kapsam, ApplicationUser user, string role, string companyName, CancellationToken cancellationToken)
    {
        var (kullanicilar, _) = KimlikServisleri(kapsam);

        if (await kullanicilar.FindByEmailAsync(user.Email!) is not null)
        {
            throw new ConflictException($"'{user.Email}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        await directory.ReserveAsync(user.Email!, user.TenantId, cancellationToken);

        var createResult = await kullanicilar.CreateAsync(user); // şifresiz → davet gerektirir
        if (!createResult.Succeeded)
        {
            throw new ValidationException(createResult.Errors
                .Select(e => new ValidationFailure("email", e.Description)));
        }

        await kullanicilar.AddToRoleAsync(user, role);

        var token = await kullanicilar.GeneratePasswordResetTokenAsync(user);
        await SendInviteEmailAsync(user.DisplayName, user.Email!, companyName, token, cancellationToken);
    }

    private async Task SendInviteEmailAsync(
        string name, string email, string companyName, string token, CancellationToken cancellationToken)
    {
        var appUrl = (configuration["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{appUrl}/#/accept-invite?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var body =
            $"Merhaba {name},\n\n{companyName} için İK Pro hesabınız oluşturuldu. " +
            $"Şifrenizi belirleyip hesabınızı etkinleştirmek için:\n{link}\n\n" +
            $"Bağlantı çalışmazsa e-postanız ve şu davet kodunu kullanın:\nDAVET-KODU: {token}\n";
        await emailSender.SendAsync(new EmailMessage(email, "İK Pro hesap daveti", body), cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        // Refresh token PLATFORM veritabanındadır (bkz. RefreshToken.cs xmldoc): elinde
        // yalnız token dizesi olan sunucu, kiracı DB'leri ayrıldığında hangi katalogda
        // arayacağını başka türlü bilemez. Satır bulunduktan SONRA TenantId yönlendirmeyi
        // verir — tıpkı login'de e-postanın kiracısının dizin sorgusundan önce
        // bilinmemesi gibi, burada da kiracı token'ı bulmadan önce bilinmez.
        //
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await platform.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hash, cancellationToken);

        if (stored is null || !stored.IsActive)
        {
            throw new UnauthorizedException("Geçersiz veya süresi dolmuş yenileme token'ı.");
        }

        // Kiracı token satırından gelir — login'de dizinin oynadığı rolü burada
        // TenantId oynar. Kullanıcı o kiracıya sabitlenmiş kapsamdan çözülür.
        using var kapsam = tenantScopeFactory.Create(stored.TenantId);
        var (kullanicilar, _) = KimlikServisleri(kapsam);

        var user = await kullanicilar.FindByIdAsync(stored.UserId);
        if (user is null)
        {
            // Token platformda var ama kullanıcı kiracı veritabanında yok — ör. kiracı
            // geri yükleme (restore) sonrası ya da veri bütünlüğü bozulmuşsa mümkündür.
            // Sessizce yutulmaz: bu bir anomalidir, loglanır.
            logger.LogWarning(
                "Refresh token platformda bulundu ama kullanıcı yok: UserId={UserId}, TenantId={TenantId}",
                stored.UserId, stored.TenantId);
            throw new UnauthorizedException("Geçersiz veya süresi dolmuş yenileme token'ı.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Geçersiz veya süresi dolmuş yenileme token'ı.");
        }

        // Rotasyon: eski token tek kullanımlıktır.
        stored.RevokedAtUtc = DateTime.UtcNow;

        return await IssueTokensAsync(kapsam, user, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await platform.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hash, cancellationToken);

        // Idempotent: token bulunamazsa sessizce başarı (zaten çıkış yapılmış).
        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await platform.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        // Oturum içi işlem: kiracı JWT'den gelir.
        using var kapsam = tenantScopeFactory.Create(currentTenant.TenantIdOrThrow());
        var (kullanicilar, _) = KimlikServisleri(kapsam);

        var user = await kullanicilar.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Kullanıcı kaydı bulunamadı.");

        var result = await kullanicilar.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            throw new UnauthorizedException("Mevcut şifre hatalı.");
        }

        throw new ValidationException(result.Errors
            .Select(e => new ValidationFailure("newPassword", e.Description)));
    }

    public async Task<UserDto?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        // Oturum içi işlem: kiracı JWT'den gelir.
        using var kapsam = tenantScopeFactory.Create(currentTenant.TenantIdOrThrow());
        var (kullanicilar, _) = KimlikServisleri(kapsam);

        var user = await kullanicilar.FindByIdAsync(userId);
        return user is null ? null : await BuildUserDtoAsync(kapsam, user);
    }

    /// <summary>
    /// Erişim kapısı artık KOŞULSUZDUR (POST /api/auth/register kaldırılmasıyla
    /// birlikte — bkz. AuthController xmldoc): daha önce bu metodun tek çağıranı
    /// olan anonim self-servis kayıt <c>skipAccessCheck: true</c> geçerek kapıyı
    /// bilinçli atlıyordu, bu da kiracı sızıntısı güvenlik açığının bir parçasıydı.
    /// Ucun kaldırılmasıyla o istisna da ortadan kalktı: bundan sonra hiçbir token
    /// üretimi (login, refresh) bu kapıyı atlayamaz.
    /// </summary>
    /// <remarks>
    /// <paramref name="kapsam"/> İMZADA: <paramref name="user"/> o kapsamın context'ine
    /// bağlıdır ve rol okuma da aynı <c>UserManager</c>'dan yapılmalıdır. Kapsamı
    /// parametre almak yerine "çağıran doğru kapsam içinden çağırsın" demek, sırayı
    /// yine sözleşmeye bırakırdı — imzaya koymak yanlış kullanımı derleme hatasına çevirir.
    /// </remarks>
    private async Task<AuthResponse> IssueTokensAsync(
        ITenantScope kapsam, ApplicationUser user, CancellationToken cancellationToken)
    {
        // Kapı burada: login ve refresh'in ORTAK yolu burasıdır, dolayısıyla
        // ikisi de tek noktadan korunur. Faz 1a'da kapı yalnız login'deydi ve
        // refresh onu atlıyordu — dondurulmuş bir kiracının kullanıcısı elindeki
        // refresh token'la oturumunu süresiz uzatabiliyordu.
        await accessGuard.EnsureAccessibleAsync(user.TenantId, cancellationToken);

        var (kullanicilar, _) = KimlikServisleri(kapsam);
        var roles = await kullanicilar.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);
        var (rawRefreshToken, refreshEntity) = tokenService.CreateRefreshToken(user.Id);
        refreshEntity.TenantId = user.TenantId;

        // Platform veritabanına yazılır (bkz. RefreshToken.cs xmldoc) — uygulama
        // veritabanına DEĞİL.
        platform.RefreshTokens.Add(refreshEntity);
        await platform.SaveChangesAsync(cancellationToken);

        var tenantName = await TenantNameAsync(user.TenantId, cancellationToken);
        return new AuthResponse(accessToken, rawRefreshToken, expiresAtUtc, ToUserDto(user, roles, tenantName));
    }

    private async Task<UserDto> BuildUserDtoAsync(ITenantScope kapsam, ApplicationUser user)
    {
        var (kullanicilar, _) = KimlikServisleri(kapsam);
        return ToUserDto(user, await kullanicilar.GetRolesAsync(user), await TenantNameAsync(user.TenantId, default));
    }

    /// <summary>Kiracının görünen adı — /me ve auth yanıtlarında şirket bağlamı için.</summary>
    private async Task<string> TenantNameAsync(int tenantId, CancellationToken cancellationToken) =>
        await platform.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    private static UserDto ToUserDto(ApplicationUser user, IEnumerable<string> roles, string tenantName)
    {
        var role = roles.FirstOrDefault() ?? Roles.Employee;

        return new UserDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            role,
            Roles.LabelOf(role),
            user.Initials ?? DeriveInitials(user.DisplayName),
            user.EmployeeId,
            user.TenantId,
            tenantName);
    }

    /// <summary>Ad soyaddan baş harfler, ör. "Ahmet Yılmaz" → "AY" (frontend initials paritesi).</summary>
    private static string DeriveInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpper(new System.Globalization.CultureInfo("tr-TR")),
            _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpper(new System.Globalization.CultureInfo("tr-TR")),
        };
    }
}
