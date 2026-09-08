using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Settings;

// --- birleşik görünüm ---

/// <summary>Ayarlar ekranının tamamı: profil + bildirim + güvenlik + abonelik (tekil kayıtlar).</summary>
public sealed record GetSettingsQuery : IRequest<SettingsDto>;

public sealed class GetSettingsQueryHandler(
    IApplicationDbContext context, IPlatformDbContext platform, ICurrentTenant currentTenant)
    : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var profile = await context.CompanyProfiles.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken) ?? new CompanyProfile();
        var notifications = await context.NotificationSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken) ?? new NotificationSettings();

        // Abonelik PLATFORM veritabanındadır ve orada global kiracı filtresi YOKTUR —
        // kiracı AÇIKÇA filtrelenmeli. Unutulsaydı bu sorgu başka bir müşterinin
        // planını gösterirdi; filtreyi zorunlu kılan bir mekanizma bu context'te yok,
        // o yüzden burada elle ve görünür duruyor.
        var tenantId = currentTenant.TenantIdOrThrow();
        var subscription = await platform.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            ?? new Subscription();

        return new SettingsDto(
            profile.ToDto(),
            notifications.ToNotificationsDto(),
            notifications.ToSecurityDto(),
            subscription.ToDto());
    }
}

// --- şirket profili güncelle ---

public sealed record UpdateCompanyProfileCommand(
    string Name,
    string? Website,
    string? SystemEmail,
    string? Phone,
    string? HeadquartersAddress) : IRequest<CompanyProfileDto>;

public sealed class UpdateCompanyProfileCommandValidator : AbstractValidator<UpdateCompanyProfileCommand>
{
    public UpdateCompanyProfileCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Website).MaximumLength(200);
        RuleFor(x => x.SystemEmail).EmailAddress().MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.SystemEmail));
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.HeadquartersAddress).MaximumLength(500);
    }
}

public sealed class UpdateCompanyProfileCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateCompanyProfileCommand, CompanyProfileDto>
{
    public async Task<CompanyProfileDto> Handle(
        UpdateCompanyProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await context.CompanyProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new CompanyProfile();
            context.CompanyProfiles.Add(profile);
        }

        profile.Name = request.Name.Trim();
        profile.Website = request.Website?.Trim();
        profile.SystemEmail = request.SystemEmail?.Trim();
        profile.Phone = request.Phone?.Trim();
        profile.HeadquartersAddress = request.HeadquartersAddress?.Trim();

        await context.SaveChangesAsync(cancellationToken);
        return profile.ToDto();
    }
}

// --- bildirim toggle'ları güncelle ---

public sealed record UpdateNotificationSettingsCommand(
    bool NewPersonnelEmail,
    bool LeaveRequestEmail,
    bool WeeklyReportEmail) : IRequest<NotificationSettingsDto>;

public sealed class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.WeeklyReportEmail).Equal(false)
            .WithMessage("Haftalık rapor gönderimi henüz desteklenmiyor.");
    }
}

public sealed class UpdateNotificationSettingsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsDto>
{
    public async Task<NotificationSettingsDto> Handle(
        UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(context, cancellationToken);

        settings.NewPersonnelEmail = request.NewPersonnelEmail;
        settings.LeaveRequestEmail = request.LeaveRequestEmail;
        settings.WeeklyReportEmail = request.WeeklyReportEmail;

        await context.SaveChangesAsync(cancellationToken);
        return settings.ToNotificationsDto();
    }

    internal static async Task<NotificationSettings> GetOrCreateAsync(
        IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var settings = await context.NotificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new NotificationSettings();
            context.NotificationSettings.Add(settings);
        }

        return settings;
    }
}

// --- güvenlik (2FA toggle) güncelle ---

public sealed record UpdateSecuritySettingsCommand(bool TwoFactorSmsEnabled)
    : IRequest<SecuritySettingsDto>;

public sealed class UpdateSecuritySettingsCommandValidator : AbstractValidator<UpdateSecuritySettingsCommand>
{
    public UpdateSecuritySettingsCommandValidator()
    {
        RuleFor(x => x.TwoFactorSmsEnabled).Equal(false)
            .WithMessage("SMS ile iki aşamalı doğrulama henüz desteklenmiyor.");
    }
}

public sealed class UpdateSecuritySettingsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateSecuritySettingsCommand, SecuritySettingsDto>
{
    public async Task<SecuritySettingsDto> Handle(
        UpdateSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await UpdateNotificationSettingsCommandHandler
            .GetOrCreateAsync(context, cancellationToken);

        settings.TwoFactorSmsEnabled = request.TwoFactorSmsEnabled;

        await context.SaveChangesAsync(cancellationToken);
        return settings.ToSecurityDto();
    }
}

// --- logo yükle / indir ---

/// <summary>Şirket logosu yükler (settings.js: PNG/JPG, maksimum 2 MB).</summary>
public sealed record UploadCompanyLogoCommand(
    Stream Content,
    string FileName,
    long Length) : IRequest<string>;

public sealed class UploadCompanyLogoCommandValidator : AbstractValidator<UploadCompanyLogoCommand>
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];

    public UploadCompanyLogoCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("Dosya boş olamaz.")
            .LessThanOrEqualTo(MaxBytes).WithMessage("Logo en fazla 2 MB olabilir.");

        RuleFor(x => x.FileName)
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("Logo JPG veya PNG olmalı.");
    }
}

public sealed class UploadCompanyLogoCommandHandler(IApplicationDbContext context, IFileStorage fileStorage)
    : IRequestHandler<UploadCompanyLogoCommand, string>
{
    public async Task<string> Handle(UploadCompanyLogoCommand request, CancellationToken cancellationToken)
    {
        var profile = await context.CompanyProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new CompanyProfile { Name = "Şirket" };
            context.CompanyProfiles.Add(profile);
        }

        var stored = await fileStorage.SaveAsync(request.Content, request.FileName, "branding", cancellationToken);

        var oldLogoPath = profile.LogoPath;
        profile.LogoPath = stored.Path;
        await context.SaveChangesAsync(cancellationToken);

        if (oldLogoPath is not null)
        {
            await fileStorage.DeleteAsync(oldLogoPath, cancellationToken);
        }

        return stored.Path;
    }
}

/// <summary>Şirket logosunu akış olarak döner (layout header'ı tüm roller görür).</summary>
public sealed record GetCompanyLogoQuery : IRequest<(Stream Content, string FileName)>;

public sealed class GetCompanyLogoQueryHandler(IApplicationDbContext context, IFileStorage fileStorage)
    : IRequestHandler<GetCompanyLogoQuery, (Stream Content, string FileName)>
{
    public async Task<(Stream Content, string FileName)> Handle(
        GetCompanyLogoQuery request, CancellationToken cancellationToken)
    {
        var logoPath = await context.CompanyProfiles.AsNoTracking()
            .Select(p => p.LogoPath)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şirket logosu", "logo");

        var stream = await fileStorage.OpenReadAsync(logoPath, cancellationToken);
        return (stream, Path.GetFileName(logoPath));
    }
}
