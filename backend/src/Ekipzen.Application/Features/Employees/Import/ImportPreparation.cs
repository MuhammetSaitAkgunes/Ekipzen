using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;

namespace Ekipzen.Application.Features.Employees.Import;

internal sealed record PreparedImportFile(byte[] Bytes, string Fingerprint);
internal sealed record PreparedImport(ImportPreviewDto Preview, IReadOnlyList<ValidatedImportRow> Rows);

/// <summary>Dosya içeriği ve satırların gerçek kayıt planı birlikte karşılaştırılır.</summary>
internal static class ImportPreparation
{
    public static async Task<PreparedImportFile> ReadAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        return new(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static async Task<PreparedImport> PrepareAsync(PreparedImportFile file, IApplicationDbContext context, CancellationToken ct)
    {
        using var stream = new MemoryStream(file.Bytes);
        var (rows, issues) = EmployeeImportParser.Ayristir(stream);
        if (issues.Count > 0) return new(new(0, 0, 0, 0, [], issues, [], null), []);
        var (departments, nationalIds) = await ImportLookups.YukleAsync(context, ct);
        var validated = EmployeeImportValidator.Dogrula(rows, departments, nationalIds);
        var preview = new ImportPreviewDto(rows.Count, validated.Gecerli.Count,
            rows.Count - validated.Gecerli.Count - validated.MukerrerSatir, validated.MukerrerSatir,
            validated.BilinmeyenDepartmanlar, validated.Sorunlar, validated.Satirlar.Select(x => x.Preview).ToArray(), null);
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Version = 1, file.Fingerprint, Preview = preview, Models = validated.Satirlar.Select(x => x.Model)
        }))));
        return new(preview with { PreviewToken = token }, validated.Satirlar);
    }
}
