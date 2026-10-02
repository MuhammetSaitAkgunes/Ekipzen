using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Settings;

namespace Ekipzen.Application.Features.Settings;

/// <summary>Şirket profili — settings.js "Şirket Bilgileri" sekmesi.</summary>
public sealed record CompanyProfileDto(
    string Name,
    string? Website,
    string? SystemEmail,
    string? Phone,
    string? HeadquartersAddress,
    string? LogoPath, string? RecordToken = null, string? LogoRecordToken = null);

/// <summary>E-posta bildirim toggle'ları — settings.js "Bildirimler" sekmesi.</summary>
public sealed record NotificationSettingsDto(
    bool NewPersonnelEmail,
    bool LeaveRequestEmail,
    bool WeeklyReportEmail, string? RecordToken = null);

/// <summary>Güvenlik tercihleri — settings.js "Güvenlik & Yetki" sekmesi (2FA toggle).</summary>
public sealed record SecuritySettingsDto(bool TwoFactorSmsEnabled);

/// <summary>Abonelik/fatura görünümü — settings.js "Abonelik & Fatura" sekmesi.</summary>
public sealed record SubscriptionDto(
    string Plan,
    string? PlanName,
    string? BillingCycle,
    decimal Price,
    DateOnly? RenewalDate,
    string? PaymentMethodMasked);

/// <summary>Ayarlar ekranının tek seferde yüklediği birleşik görünüm.</summary>
public sealed record SettingsDto(
    CompanyProfileDto Company,
    NotificationSettingsDto Notifications,
    SecuritySettingsDto Security,
    SubscriptionDto Subscription);

public static class SettingsMappings
{
    public static CompanyProfileDto ToDto(this CompanyProfile profile) => new(
        profile.Name, profile.Website, profile.SystemEmail, profile.Phone,
        profile.HeadquartersAddress, profile.LogoPath, PreparationOperationEvidence.RecordToken(profile.Name, profile.Website,
            profile.SystemEmail, profile.Phone, profile.HeadquartersAddress), PreparationOperationEvidence.RecordToken("company-logo", profile.LogoPath));

    public static NotificationSettingsDto ToNotificationsDto(this NotificationSettings settings)
        => new(settings.NewPersonnelEmail, settings.LeaveRequestEmail, false, PreparationOperationEvidence.RecordToken("notifications",
            settings.NewPersonnelEmail.ToString(), settings.LeaveRequestEmail.ToString(), settings.WeeklyReportEmail.ToString()));

    public static SecuritySettingsDto ToSecurityDto(this NotificationSettings settings)
        // Eski kayıtta true olsa da gerçek bir 2FA akışı yok; etkin güvenlik gibi sunulamaz.
        => new(false);

    public static SubscriptionDto ToDto(this Subscription subscription) => new(
        subscription.Plan, subscription.PlanName, subscription.BillingCycle,
        subscription.Price, subscription.RenewalDate, subscription.PaymentMethodMasked);
}
