using Ekipzen.Application.Features.Guidance;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Settings;

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
    string? HeadquartersAddress, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest<CompanyProfileDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "company-profile-update";
}

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

public sealed class UpdateCompanyProfileCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock,
    ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<UpdateCompanyProfileCommand, CompanyProfileDto>
{
    public Task<CompanyProfileDto> Handle(UpdateCompanyProfileCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("tenant-setup", async ct =>
    {
        var profile = await context.CompanyProfiles.FirstOrDefaultAsync(ct);
        var initial = profile ?? new CompanyProfile();
        PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, initial.ToDto().RecordToken!, guidance.IsGuided);
        var fields = new List<string>();
        if (initial.Name != request.Name.Trim()) fields.Add("Şirket adı");
        if (initial.Website != request.Website?.Trim()) fields.Add("Web sitesi");
        if (initial.SystemEmail != request.SystemEmail?.Trim()) fields.Add("Sistem e-postası");
        if (initial.Phone != request.Phone?.Trim()) fields.Add("Telefon");
        if (initial.HeadquartersAddress != request.HeadquartersAddress?.Trim()) fields.Add("Merkez adresi");
        if (fields.Count == 0) return initial.ToDto();
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

        await context.SaveChangesAsync(ct);
        await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "company-profile", profile.Id, fields, ct);
        return profile.ToDto();
    }, cancellationToken);
}

// --- bildirim toggle'ları güncelle ---

public sealed record UpdateNotificationSettingsCommand(
    bool NewPersonnelEmail,
    bool LeaveRequestEmail,
    bool WeeklyReportEmail, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest<NotificationSettingsDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "company-notifications-update";
}

public sealed class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.WeeklyReportEmail).Equal(false)
            .WithMessage("Haftalık rapor gönderimi henüz desteklenmiyor.");
    }
}

public sealed class UpdateNotificationSettingsCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock,
    ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsDto>
{
    public Task<NotificationSettingsDto> Handle(UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("tenant-setup", async ct =>
        {
            var existing = await context.NotificationSettings.FirstOrDefaultAsync(ct);
            var settings = existing ?? new NotificationSettings();
            PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, settings.ToNotificationsDto().RecordToken!, guidance.IsGuided);
            var fields = new List<string>();
            if (settings.NewPersonnelEmail != request.NewPersonnelEmail) fields.Add("Yeni personel e-posta tercihi");
            if (settings.LeaveRequestEmail != request.LeaveRequestEmail) fields.Add("İzin talebi e-posta tercihi");
            if (settings.WeeklyReportEmail != request.WeeklyReportEmail) fields.Add("Haftalık rapor tercihi");
            if (fields.Count == 0) return settings.ToNotificationsDto();
            if (existing is null) context.NotificationSettings.Add(settings);
            settings.NewPersonnelEmail = request.NewPersonnelEmail;
            settings.LeaveRequestEmail = request.LeaveRequestEmail;
            settings.WeeklyReportEmail = request.WeeklyReportEmail;
            await context.SaveChangesAsync(ct);
            await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "notifications", settings.Id, fields, ct);
            return settings.ToNotificationsDto();
        }, cancellationToken);

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
    long Length, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest<string>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "company-logo-upload";
}

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

public sealed class UploadCompanyLogoCommandHandler(IApplicationDbContext context, IFileStorage storage,
    IDatabaseLock gate, ICurrentUser user, GuidanceOperationContext guidance, ILogger<UploadCompanyLogoCommandHandler> logger)
    : IRequestHandler<UploadCompanyLogoCommand, string>
{
    public Task<string> Handle(UploadCompanyLogoCommand request, CancellationToken ct)
        => GuidanceImageReplacement.ExecuteAsync(storage, gate, "tenant-setup", request.Content, request.FileName, "branding", async (upload, token) =>
        {
            var profile = await context.CompanyProfiles.FirstOrDefaultAsync(token);
            PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, (profile ?? new CompanyProfile()).ToDto().LogoRecordToken!, guidance.IsGuided);
            if (profile is null) { profile = new CompanyProfile { Name = "Şirket" }; context.CompanyProfiles.Add(profile); }
            var stored = await upload(token);
            var previous = profile.LogoPath;
            profile.LogoPath = stored.Path;
            await context.SaveChangesAsync(token);
            await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "company-profile", profile.Id, ["Şirket logosu"], token);
            return previous;
        }, logger, ct);
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
