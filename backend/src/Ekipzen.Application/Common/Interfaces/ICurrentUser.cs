namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// İstek bağlamındaki oturum açmış kullanıcı. Audit damgalama ve yetki kapsamı için kullanılır.
/// API katmanında HttpContext üzerinden implemente edilir.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }

    /// <summary>Bağlı çalışan kaydı (yetki kapsamı: employee → kendisi, manager → ekibi).</summary>
    int? EmployeeId { get; }
}
