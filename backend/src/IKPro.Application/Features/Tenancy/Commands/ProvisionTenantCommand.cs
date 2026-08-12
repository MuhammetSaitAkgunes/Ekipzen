using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Tenancy.Commands;

/// <summary>
/// Yeni bir müşteri şirketi (kiracı) ve onun ilk <c>hr-admin</c> hesabını oluşturur.
/// Platform seviyesi bir işlemdir (normal rol sisteminin dışında); TenancyController
/// bunu platform anahtarıyla korur. Faz 1: geçici şifre; Faz 5: davet + şifre-belirleme.
/// </summary>
public sealed record ProvisionTenantCommand(
    string CompanyName,
    string Slug,
    string AdminName,
    string AdminEmail) : IRequest<ProvisionTenantResult>;

public sealed record ProvisionTenantResult(int TenantId, string Slug, string AdminEmail);

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty().MaximumLength(64)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug yalnız küçük harf, rakam ve tire içerebilir (ör. 'acme').");
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class ProvisionTenantCommandHandler(
    IPlatformDbContext platform, IIdentityService identityService, ITenantDirectory directory,
    ITenantRegistry registry)
    : IRequestHandler<ProvisionTenantCommand, ProvisionTenantResult>
{
    public async Task<ProvisionTenantResult> Handle(
        ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await platform.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            throw new ConflictException($"'{slug}' kısa adıyla bir şirket zaten var.");
        }

        // Kiracı doğrudan Active oluşturulur: bu uca ulaşmak platform anahtarını bilmeyi
        // gerektirir, yani çağıran zaten operatördür — doğrulanacak bir şey kalmaz.
        // (Kiracıyı Provisioning'de bırakıp davet kabulüyle aktifleştiren self-servis
        // yol kaldırıldı; artık tek onboarding yolu burasıdır.)
        var tenant = await TenantOnboarding.CreateWithAdminAsync(
            platform, identityService, directory, registry,
            request.CompanyName.Trim(), slug,
            request.AdminName.Trim(), request.AdminEmail.Trim(),
            hedefDurum: TenantStatus.Active, cancellationToken);

        return new ProvisionTenantResult(tenant.Id, tenant.Slug, request.AdminEmail.Trim());
    }
}
