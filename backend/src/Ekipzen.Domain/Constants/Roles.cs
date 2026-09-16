namespace Ekipzen.Domain.Constants;

/// <summary>
/// Sistem rolleri (frontend routes.js ile birebir). Identity rol adları ve
/// yetkilendirme policy'lerinde kullanılır.
/// </summary>
public static class Roles
{
    public const string HrAdmin = "hr-admin";
    public const string Manager = "manager";
    public const string Employee = "employee";

    public static readonly string[] All = [HrAdmin, Manager, Employee];

    /// <summary>Frontend demoUsers.roleLabel ile birebir görünen adlar.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [HrAdmin] = "İK Admin",
        [Manager] = "Yönetici",
        [Employee] = "Çalışan",
    };

    public static string LabelOf(string role) => Labels.TryGetValue(role, out var label) ? label : role;
}
