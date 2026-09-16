using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracının tüm verisini kalıcı siler (KVKK unutulma hakkı). Yıkıcıdır:
/// <c>ConfirmSlug</c> hedef kiracının slug'ıyla eşleşmezse reddedilir (yanlış-id koruması).
/// </summary>
public sealed record PurgeTenantCommand(int TenantId, string ConfirmSlug) : IRequest<PurgeTenantResult>;

/// <param name="DosyalarSilinemedi">
/// <c>true</c> ise kiracının veritabanı verisi silindi ama fiziksel dosya alanı
/// SİLİNEMEDİ — diskte PII kalmış olabilir, elle temizlik gerekir (hata sunucu
/// loglarına LOUD yazılmıştır, bkz. <see cref="ITenantPurger.PurgeAsync"/>).
/// Operatör 200 OK'i "her şey silindi" diye okumamalı; bu alanı kontrol etmeli.
/// </param>
public sealed record PurgeTenantResult(int TenantId, string Slug, bool DosyalarSilinemedi = false);

public sealed class PurgeTenantCommandValidator : AbstractValidator<PurgeTenantCommand>
{
    public PurgeTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.ConfirmSlug).NotEmpty();
    }
}

public sealed class PurgeTenantCommandHandler(IPlatformDbContext platform, ITenantPurger purger)
    : IRequestHandler<PurgeTenantCommand, PurgeTenantResult>
{
    public async Task<PurgeTenantResult> Handle(PurgeTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Kiracı", request.TenantId);

        if (!string.Equals(tenant.Slug, request.ConfirmSlug.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Onay kısa adı (slug) kiracıyla eşleşmiyor; silme iptal edildi.");
        }

        var slug = tenant.Slug;
        var dosyalarSilindi = await purger.PurgeAsync(tenant.Id, cancellationToken);
        return new PurgeTenantResult(request.TenantId, slug, DosyalarSilinemedi: !dosyalarSilindi);
    }
}
