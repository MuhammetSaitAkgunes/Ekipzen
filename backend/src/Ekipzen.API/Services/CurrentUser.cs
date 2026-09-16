using Ekipzen.Application.Common.Interfaces;
using System.Security.Claims;

namespace Ekipzen.API.Services;

/// <summary>
/// HttpContext üzerinden oturum açmış kullanıcıyı sağlar. JWT kısa claim adlarıyla
/// üretilir (MapInboundClaims kapalı): sub, name, email, role, employeeId.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId =>
        Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue("name")
        ?? Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("email")
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public string? Email =>
        Principal?.FindFirstValue("email") ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll("role").Select(c => c.Value)
            .Concat(Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [])
            .Distinct()
            .ToArray() ?? [];

    public int? EmployeeId =>
        int.TryParse(Principal?.FindFirstValue("employeeId"), out var id) ? id : null;
}
