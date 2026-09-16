using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Queries;

/// <summary>
/// Provizyonu ya da silmesi yarıda kalmış kiracıları listeler.
///
/// Kiracı satırı platform veritabanında, verisi uygulama veritabanında olduğu
/// için bu iki iş tek transaction değildir. Yarıda kalan bir iş sessiz enkaz
/// bırakmamalı; bu sorgu onu görünür kılar.
/// </summary>
public sealed record GetStuckTenantsQuery(int OlderThanMinutes) : IRequest<IReadOnlyList<StuckTenantDto>>;

public sealed record StuckTenantDto(int TenantId, string Slug, TenantStatus Status, DateTime CreatedAtUtc);

public sealed class GetStuckTenantsQueryHandler(IPlatformDbContext platform)
    : IRequestHandler<GetStuckTenantsQuery, IReadOnlyList<StuckTenantDto>>
{
    public async Task<IReadOnlyList<StuckTenantDto>> Handle(
        GetStuckTenantsQuery request, CancellationToken cancellationToken)
    {
        var esik = DateTime.UtcNow.AddMinutes(-request.OlderThanMinutes);

        // Yaş eşiği YALNIZ Provisioning'e uygulanır. Provizyon artık tek istek
        // içinde biter (veritabanı oluşturma + migration dahil), ama bu iş
        // saniyeler sürebilir — sağlıklı ilerleyen bir kurulumu "takılı" diye
        // listelememek için eşik gerekir. Purging ise çok daha kısadır ve orada
        // kalmış bir kiracı TANIMI GEREĞİ anormaldir; yaşına bakmanın anlamı yok.
        return await platform.Tenants
            .Where(t => (t.Status == TenantStatus.Provisioning && t.CreatedAtUtc < esik)
                        || t.Status == TenantStatus.Purging)
            .OrderBy(t => t.CreatedAtUtc)
            .Select(t => new StuckTenantDto(t.Id, t.Slug, t.Status, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
