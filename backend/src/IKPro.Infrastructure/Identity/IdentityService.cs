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
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> implementasyonu: ASP.NET Core Identity + JWT.
/// Lockout SignInManager üzerinden işler; refresh token'lar rotasyonla yenilenir
/// ve DB'de hash'lenmiş saklanır.
/// </summary>
public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
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

    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        // Dizin login'in ÖN adımıdır: Faz 2'de kullanıcı tablosu kiracının kendi
        // veritabanında olacak, dolayısıyla hangi veritabanına bakılacağı
        // bilinmeden kullanıcı aranamaz. Dizinde kayıt yoksa (ör. dizin bütünlüğü
        // bozulmuşsa) hesabın var olup olmadığını sızdırmadan genel mesajla reddet.
        var dizinKiraciId = await directory.FindTenantIdAsync(email, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentialsMessage);

        // FAZ 2 NOTU (bu dal — Faz 1b — burayı ÇÖZMEZ): `dizinKiraciId` yukarıda DOĞRU
        // kiracıyı bulur, ama aşağıdaki `userManager` (constructor'da enjekte edilen
        // `UserManager<ApplicationUser>`, dolayısıyla onun içindeki `AppDbContext`) hâlâ
        // bu sınıf inşa edilirken kurulmuş AMBIENT/kiracısız HTTP kapsamının bağlantısını
        // kullanır — `dizinKiraciId`'ye SABİTLENMEMİŞTİR. Bugün doğru sonuç verir çünkü
        // Faz 1b'de tüm kiracılar aynı veritabanını paylaşıyor (bkz. TenantConnectionResolver).
        // Faz 2'de kiracı başına veritabanına geçilince bu satır YANLIŞ kiracının
        // veritabanında kullanıcı arar (dizinKiraciId'nin veritabanında değil) — login
        // burada SESSİZCE bozulur (muhtemelen "kullanıcı yok" hatasıyla, gerçek nedeni
        // gizleyerek). Bu yüzden LoginAsync'in Faz 2'de YENİDEN YAPILANDIRILMASI gerekir:
        // kiracı yalnızca BURADA, dizin sorgusundan SONRA bilinir, dolayısıyla constructor
        // enjeksiyonu (sıra: DI kapsamı kurulur → sonra kiracı öğrenilir) yapısal olarak
        // yetersizdir. Olası yaklaşım: userManager/context'i constructor'da almak yerine,
        // `dizinKiraciId` bilindikten SONRA `ITenantScopeFactory.Create(dizinKiraciId)` ile
        // taze bir kapsam açıp o kapsamdan UserManager/AppDbContext çözmek (bkz. TenantPurger
        // ve UserDirectorySource'taki aynı desen) — ya da login akışını iki aşamaya bölüp
        // ikinci aşamayı kiracıya sabitlenmiş bir kapsamda çalıştırmak.
        var user = await userManager.FindByEmailAsync(email)
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

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
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
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => await userManager.FindByEmailAsync(email) is not null;

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

        // Dizine CreateInvitedUserAsync KOŞULSUZ yazar (idempotent ITenantDirectory.ReserveAsync).
        // Çağıran burada directory.ReserveAsync ile önceden rezervasyon yapmış olabilir
        // (TenantOnboarding) — o durumda bu ikinci yazım aynı kiracı için sessizce
        // no-op'tur. Rezervasyon yapılmamışsa (ör. eski bir çağıran unutursa) bu
        // çağrı yine de dizine yazar; "dizine hiç yazılmadı" sınıfı hatalar artık
        // derleyici tarafından değil, tek bir kod yolu tarafından imkansız kılınır.
        await CreateInvitedUserAsync(user, Roles.HrAdmin, companyName, cancellationToken);
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
        await CreateInvitedUserAsync(user, Roles.Employee, companyName, cancellationToken);
    }

    public async Task AcceptInviteAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new ValidationException([new ValidationFailure("email", "Davet bulunamadı.")]);

        // Şifresiz oluşturulan hesabın ilk şifresini davet (reset) token'ıyla belirler.
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
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
        ApplicationUser user, string role, string companyName, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(user.Email!) is not null)
        {
            throw new ConflictException($"'{user.Email}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        await directory.ReserveAsync(user.Email!, user.TenantId, cancellationToken);

        var createResult = await userManager.CreateAsync(user); // şifresiz → davet gerektirir
        if (!createResult.Succeeded)
        {
            throw new ValidationException(createResult.Errors
                .Select(e => new ValidationFailure("email", e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
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
        // FAZ 2 NOTU (bu görev — Faz 2a/Görev 1 — burayı ÇÖZMEZ): kullanıcı aşağıda hâlâ
        // AMBIENT `userManager` (constructor'da enjekte edilen, HTTP kapsamının
        // AppDbContext'ine bağlı) ile çözülür. Faz 1b/2a'da tüm kiracılar aynı uygulama
        // veritabanını paylaştığından doğru sonuç verir. Kiracı başına veritabanına
        // geçilince (Faz 2b) bu satır YANLIŞ kiracının veritabanında arar (stored.TenantId
        // yerine ambient/JWT kiracısını kullanır) — o zaman LoginAsync ile aynı desene
        // (bkz. oradaki not): `ITenantScopeFactory.Create(stored.TenantId)` ile taze bir
        // kapsam açıp kullanıcıyı ORADAN çözmek gerekir. Bu görevin kapsamı yalnız
        // token'ın DEPOLANDIĞI veritabanını değiştirmektir; kullanıcı çözümünün kiracı
        // kapsamına taşınması sonraki göreve bırakılmıştır.
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await platform.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hash, cancellationToken);

        if (stored is null || !stored.IsActive)
        {
            throw new UnauthorizedException("Geçersiz veya süresi dolmuş yenileme token'ı.");
        }

        var user = await userManager.FindByIdAsync(stored.UserId);
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

        return await IssueTokensAsync(user, cancellationToken);
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
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Kullanıcı kaydı bulunamadı.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
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
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await BuildUserDtoAsync(user);
    }

    /// <summary>
    /// Erişim kapısı artık KOŞULSUZDUR (POST /api/auth/register kaldırılmasıyla
    /// birlikte — bkz. AuthController xmldoc): daha önce bu metodun tek çağıranı
    /// olan anonim self-servis kayıt <c>skipAccessCheck: true</c> geçerek kapıyı
    /// bilinçli atlıyordu, bu da kiracı sızıntısı güvenlik açığının bir parçasıydı.
    /// Ucun kaldırılmasıyla o istisna da ortadan kalktı: bundan sonra hiçbir token
    /// üretimi (login, refresh) bu kapıyı atlayamaz.
    /// </summary>
    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Kapı burada: login ve refresh'in ORTAK yolu burasıdır, dolayısıyla
        // ikisi de tek noktadan korunur. Faz 1a'da kapı yalnız login'deydi ve
        // refresh onu atlıyordu — dondurulmuş bir kiracının kullanıcısı elindeki
        // refresh token'la oturumunu süresiz uzatabiliyordu.
        await accessGuard.EnsureAccessibleAsync(user.TenantId, cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
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

    private async Task<UserDto> BuildUserDtoAsync(ApplicationUser user)
        => ToUserDto(user, await userManager.GetRolesAsync(user), await TenantNameAsync(user.TenantId, default));

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
