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
            var name = EmployeeImportValidator.Normalize(departman.Name);
            if (!sozluk.TryAdd(name, departman.Id)) sozluk[name] = 0;
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
        var file = await ImportPreparation.ReadAsync(request.Dosya, cancellationToken);
        return (await ImportPreparation.PrepareAsync(file, context, cancellationToken)).Preview;
    }
}
