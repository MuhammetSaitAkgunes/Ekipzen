using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IKPro.API.Middleware;

/// <summary>
/// Erişim kapısının HTTP ayağı. <c>IdentityService.IssueTokensAsync</c> (login ve
/// refresh'in ortak yolu) yalnız token ÜRETİMİNİ korur; elinde kiracı
/// dondurulmadan ÖNCE alınmış hâlâ geçerli bir access token'ı olan istemci o
/// kontrolü hiç görmez. Bu middleware kimliği doğrulanmış HER istekte kapıyı
/// yeniden çalıştırarak o boşluğu kapatır.
///
/// <para>
/// Kapı UCUN kendi yetkilendirme gereksinimine bağlıdır — isteğin bir Bearer
/// token TAŞIYIP taşımadığına DEĞİL. (Düzeltme turu 1, IMPORTANT #1: ilk sürüm
/// yalnız <c>tenant</c> claim'inin varlığına bakıyordu; bu, ucun anonim olup
/// olmadığını değil, isteğin token taşıyıp taşımadığını kontrol ediyordu. Sonuç:
/// dondurulmuş bir kiracıdan kalma, süresi henüz dolmamış bir Bearer token'ı
/// taşıyan bir istemci — ör. Postman/mobil/entegrasyon — <c>POST /api/auth/login</c>
/// ile BAŞKA ve AKTİF bir kiracının hesabına girmeye çalışırsa, o eski token'ın
/// kiracısı yüzünden 403 alıyordu. Login/signup gibi uçlarda taşınan bir token
/// varsa bile, o uca YÖNELİK isteğin niyetiyle ilgisizdir.)
/// </para>
///
/// İki koşuldan biri sağlanırsa istek DOKUNULMADAN geçer:
/// <list type="bullet">
/// <item><description>Uç <c>[AllowAnonymous]</c> taşıyor (login/refresh/logout/
/// accept-invite/signup, <c>X-Platform-Key</c> korumalı TenancyController) —
/// bu uçlar taşınan bir token'ın kiracı bağlamını YOK SAYMALI.</description></item>
/// <item><description>Uç hiç <c>[Authorize]</c> taşımıyor — bu API'de varsayılan bir
/// yetkilendirme politikası (fallback policy) YOK (bkz. Program.cs), yani
/// <c>[Authorize]</c> işaretlenmemiş bir uç zaten anonim erişime açıktır (ör.
/// <c>/health</c>); kapı orada da isteğe bağlı taşınan bir token'a bakıp
/// gereksiz yere engellememeli.</description></item>
/// </list>
///
/// JWT'deki <c>tenant</c> claim'ini okur (bkz. <see cref="IKPro.API.Services.CurrentTenant"/>
/// ile aynı claim adı) — yalnız kapı gerçekten çalışması gereken (yukarıdaki iki
/// koşuldan hiçbirine uymayan, yani <c>[Authorize]</c> gerektiren) uçlarda.
///
/// Pipeline sırası önemli: <c>UseAuthentication()</c>'dan SONRA (claim'ler ancak
/// kimlik doğrulandıktan sonra okunabilir; endpoint metadata'sı da bu noktada
/// çözülmüş olur), <c>UseAuthorization()</c>'dan ÖNCE (dondurulmuş kiracıyı rol
/// kontrolünden önce keser — gereksiz yetki değerlendirmesi çalıştırmaz).
/// </summary>
public sealed class TenantAccessMiddleware(RequestDelegate next)
{
    private const string TenantClaimType = "tenant";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantAccessGuard accessGuard,
        IApplicationDbContext applicationDb)
    {
        var endpoint = context.GetEndpoint();

        var allowsAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var requiresAuthorization = endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null;

        if (allowsAnonymous || !requiresAuthorization)
        {
            await next(context);
            return;
        }

        var tenantClaim = context.User.FindFirst(TenantClaimType)?.Value;
        if (tenantClaim is null || !int.TryParse(tenantClaim, out var tenantId))
        {
            await next(context);
            return;
        }

        // Bulunamazsa/erişilebilir değilse TenantInaccessibleException fırlatır —
        // GlobalExceptionHandler (UseExceptionHandler, bu middleware'den ÖNCE
        // pipeline'a eklendi) onu 403'e çevirir.
        await accessGuard.EnsureAccessibleAsync(tenantId, context.RequestAborted);

        // JWT stateless'tir: personel pasife alınmadan önce üretilmiş access token
        // aksi halde süresi dolana kadar çalışır. Bağlı personel her korumalı istekte
        // aktif olmalı; hr-admin gibi personel bağı olmayan hesaplar bu kontrolden geçer.
        var employeeClaim = context.User.FindFirst("employeeId")?.Value;
        if (int.TryParse(employeeClaim, out var employeeId))
        {
            var employeeActive = await applicationDb.Employees
                .AsNoTracking()
                .AnyAsync(e => e.Id == employeeId && e.Status == EmployeeStatus.Active, context.RequestAborted);

            if (!employeeActive)
            {
                throw new UnauthorizedException("Personel hesabı pasif durumda.");
            }
        }

        await next(context);
    }
}
