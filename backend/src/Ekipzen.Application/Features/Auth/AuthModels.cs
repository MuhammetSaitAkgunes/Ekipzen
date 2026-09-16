namespace Ekipzen.Application.Features.Auth;

/// <summary>
/// Oturum kullanıcısı — frontend demoUsers şekliyle birebir
/// (id, name, email, role, roleLabel, initials) + Employee bağı.
/// </summary>
public sealed record UserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    string RoleLabel,
    string Initials,
    int? EmployeeId,
    int TenantId,
    string TenantName);

/// <summary>Login/register/refresh yanıtı: erişim + yenileme token'ları ve kullanıcı.</summary>
public sealed record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserDto User);
