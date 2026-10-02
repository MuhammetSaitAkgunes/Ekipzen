using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Application.Features.Guidance;

// Dosya sistemi transaction'a katılmaz. Eski dosya yalnız iş kaydı ve kanıt commit olduktan sonra silinir.
internal static class GuidanceImageReplacement
{
    public static async Task<string> ExecuteAsync(IFileStorage storage, IDatabaseLock gate, string resource,
        Stream content, string fileName, string category,
        Func<Func<CancellationToken, Task<StoredFile>>, CancellationToken, Task<string?>> save,
        ILogger logger, CancellationToken ct)
    {
        StoredFile? stored = null;
        string? previous;
        try
        {
            previous = await gate.ExecuteExclusiveAsync(resource, token => save(async uploadToken =>
            {
                stored = await storage.SaveAsync(content, fileName, category, uploadToken);
                return stored;
            }, token), ct);
        }
        catch
        {
            if (stored is not null) await CleanupAsync(storage, stored.Path, logger);
            throw;
        }
        if (previous is not null && previous != stored!.Path) await CleanupAsync(storage, previous, logger);
        return stored!.Path;
    }

    private static async Task CleanupAsync(IFileStorage storage, string path, ILogger logger)
    {
        try { await storage.DeleteAsync(path, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Görsel değişiminden sonra dosya temizlenemedi: {Path}", path); }
    }
}
