namespace Ekipzen.Application.Common.Security;

/// <summary>
/// Yetkilendirme policy adları — frontend routes.js roles[] matrisinin backend karşılığı.
/// Frontend guard kozmetiktir; bağlayıcı kontrol bu policy'lerdir.
/// </summary>
public static class Policies
{
    /// <summary>Yalnız hr-admin: işe alım, bordro hesap/ayarları, sistem ayarları.</summary>
    public const string HrAdminOnly = "HrAdminOnly";

    /// <summary>hr-admin + manager: risk merkezi, personel, puantaj, yönetici konsolu.</summary>
    public const string Management = "Management";

    /// <summary>hr-admin + employee: bordro görüntüleme (routes.js /payroll).</summary>
    public const string PayrollAccess = "PayrollAccess";
}
