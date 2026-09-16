using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Yeni bir müşteri şirketi (kiracı) ve onun ilk <c>hr-admin</c> hesabını oluşturur.
/// Platform seviyesi bir işlemdir (normal rol sisteminin dışında); TenancyController
/// bunu platform anahtarıyla korur.
///
/// Kiracının VERİTABANI da burada kurulur — adımların sırası ve gerekçeleri
/// <see cref="TenantOnboarding"/> içindedir.
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
    IPlatformDbContext platform, TenantOnboarding onboarding)
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

        var tenant = await onboarding.CreateWithAdminAsync(
            request.CompanyName.Trim(), slug,
            request.AdminName.Trim(), request.AdminEmail.Trim(), cancellationToken);

        return new ProvisionTenantResult(tenant.Id, tenant.Slug, request.AdminEmail.Trim());
    }
}
