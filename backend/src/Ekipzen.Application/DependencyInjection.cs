using FluentValidation;
using Ekipzen.Application.Common.Behaviors;
using Ekipzen.Application.Common.Notifications;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Ekipzen.Application;

/// <summary>
/// Application katmanı servis kayıtları: MediatR (CQRS), FluentValidation
/// (pipeline'da otomatik doğrulama) ve Mapster (DTO eşleme) konfigürasyonu.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // IRegister implementasyonları (modül DTO eşlemeleri) assembly'den taranır.
        TypeAdapterConfig.GlobalSettings.Scan(assembly);

        // Bildirim tetikleyicileri (Faz 11): ayar toggle'larına uyar, IEmailSender'a yazar.
        services.AddScoped<INotificationTrigger, NotificationTrigger>();

        // Kiracı kurmanın tek yolu. Scoped: platform context'i ve dizin ile AYNI
        // kapsamda olmalı — adım 1'in transaction'ı bu paylaşıma dayanıyor.
        services.AddScoped<Features.Tenancy.TenantOnboarding>();

        return services;
    }
}
