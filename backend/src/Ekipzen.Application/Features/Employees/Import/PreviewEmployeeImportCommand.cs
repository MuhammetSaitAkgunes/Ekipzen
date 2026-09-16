using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>Dosyayı doğrular ve rapor üretir; HİÇBİR ŞEY kaydetmez.</summary>
public sealed record PreviewEmployeeImportCommand(Stream Dosya) : IRequest<ImportPreviewDto>;

/// <summary>
/// Doğrulama için gereken kiracı verisi: departman adları ve kayıtlı TC'ler.
/// Kiracı kapsamı global sorgu filtresinden gelir — elle TenantId filtresi YOK.
/// </summary>
public static class ImportLookups
{
    public static async Task<(Dictionary<string, int> Departmanlar, HashSet<string> Tcler)>
        YukleAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var departmanlar = await context.Departments
            .AsNoTracking()
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var tcler = await context.Employees
            .AsNoTracking()
            .Where(e => e.NationalId != null)
            .Select(e => e.NationalId!)
            .ToListAsync(cancellationToken);

        var sozluk = new Dictionary<string, int>();
        foreach (var departman in departmanlar)
        {
            // Aynı normalize ada sahip iki departman varsa ilki kazanır; doğrulayıcı
            // yine de deterministik davranır.
            sozluk.TryAdd(EmployeeImportValidator.Normalize(departman.Name), departman.Id);
        }

        return (sozluk, tcler.ToHashSet());
    }
}

public sealed class PreviewEmployeeImportCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewEmployeeImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(
        PreviewEmployeeImportCommand request, CancellationToken cancellationToken)
    {
        var (satirlar, ayristirmaSorunlari) = EmployeeImportParser.Ayristir(request.Dosya);
        if (ayristirmaSorunlari.Count > 0)
        {
            // Başlık bozuk ya da satır sınırı aşıldı: satır bazlı rapor anlamsız.
            return new ImportPreviewDto(0, 0, 0, 0, [], ayristirmaSorunlari);
        }

        var (departmanlar, tcler) = await ImportLookups.YukleAsync(context, cancellationToken);
        var sonuc = EmployeeImportValidator.Dogrula(satirlar, departmanlar, tcler);

        return new ImportPreviewDto(
            ToplamSatir: satirlar.Count,
            GecerliSatir: sonuc.Gecerli.Count,
            HataliSatir: satirlar.Count - sonuc.Gecerli.Count - sonuc.MukerrerSatir,
            MukerrerSatir: sonuc.MukerrerSatir,
            BilinmeyenDepartmanlar: sonuc.BilinmeyenDepartmanlar,
            Sorunlar: sonuc.Sorunlar);
    }
}
