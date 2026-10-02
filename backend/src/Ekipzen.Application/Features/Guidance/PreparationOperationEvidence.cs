using System.Security.Cryptography;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;

namespace Ekipzen.Application.Features.Guidance;

public static class PreparationOperationEvidence
{
    public static string RecordToken(params string?[] values)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));

    public static void RequireToken(string? expected, string current, bool guided)
    {
        if (guided && string.IsNullOrWhiteSpace(expected))
            throw new FluentValidation.ValidationException("Rehberli düzeltmede incelenen kaydın belirteci gereklidir.");
        if (expected is not null && expected != current)
            throw new ConflictException("Kayıt siz incelerken değişti. Taslağınızı koruyup güncel kaydı yeniden inceleyin.");
    }

    public static async Task SaveAsync(IApplicationDbContext db, GuidanceOperationContext guidance, ICurrentUser user,
        string operation, string kind, int id, IReadOnlyList<string> fields, CancellationToken ct)
    {
        if (!guidance.IsGuided) return;
        var evidence = new GuidancePreparationEvidence {
            UserId = user.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi."),
            OperationKey = operation, ResourceKind = kind, ResourceId = id,
            ChangedFieldsJson = JsonSerializer.Serialize(fields),
        };
        guidance.Stamp(evidence); db.GuidancePreparationEvidences.Add(evidence);
        // Çağıran komut iş kaydıyla birlikte tenant transaction/lock içinde çalışır.
        await db.SaveChangesAsync(ct);
    }
}
