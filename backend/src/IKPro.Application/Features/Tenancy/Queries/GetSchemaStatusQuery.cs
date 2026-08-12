using IKPro.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Tenancy.Queries;

/// <summary>
/// Her kiracının şema durumu: güncel mi, değilse neyin gerisinde.
///
/// Operatörün "dağıtım tamamlandı mı?" sorusuna cevap veren yer burasıdır.
/// Migration'ı açılıştan çıkarmanın bedeli, adımın unutulabilmesiydi; bu uç
/// (artı açılışta yazılan uyarı logu ve 503) o bedeli görünür kılar.
/// </summary>
public sealed record GetSchemaStatusQuery : IRequest<IReadOnlyList<TenantSchemaStatusDto>>;

public sealed record TenantSchemaStatusDto(
    int TenantId,
    string Slug,
    string? DatabaseName,
    bool Guncel,
    IReadOnlyList<string> BekleyenMigrationlar,
    string? Hata);

public sealed class GetSchemaStatusQueryHandler(
    IPlatformDbContext platform, ITenantSchemaInitializer schemaInitializer)
    : IRequestHandler<GetSchemaStatusQuery, IReadOnlyList<TenantSchemaStatusDto>>
{
    public async Task<IReadOnlyList<TenantSchemaStatusDto>> Handle(
        GetSchemaStatusQuery request, CancellationToken cancellationToken)
    {
        var kiracilar = await platform.Tenants
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Slug, t.DatabaseName })
            .ToListAsync(cancellationToken);

        List<TenantSchemaStatusDto> sonuc = [];
        foreach (var kiraci in kiracilar)
        {
            try
            {
                var bekleyen = await schemaInitializer.PendingMigrationsAsync(kiraci.Id, cancellationToken);
                sonuc.Add(new TenantSchemaStatusDto(
                    kiraci.Id, kiraci.Slug, kiraci.DatabaseName, bekleyen.Count == 0, bekleyen, null));
            }
            catch (Exception ex)
            {
                // Okunamayan kiracı listeden DÜŞMEZ — tam tersine, operatörün görmesi
                // gereken en önemli satır odur. Sessizce atlamak "her şey yolunda"
                // izlenimi verirdi.
                sonuc.Add(new TenantSchemaStatusDto(
                    kiraci.Id, kiraci.Slug, kiraci.DatabaseName, false, [], ex.Message));
            }
        }

        return sonuc;
    }
}
