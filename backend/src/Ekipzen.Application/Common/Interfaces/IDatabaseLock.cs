namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Aynı kiracı veritabanındaki bir iş kaynağını, transaction boyunca tüm uygulama
/// instance'ları arasında münhasır kilit altında çalıştırır.
/// </summary>
public interface IDatabaseLock
{
    Task<T> ExecuteExclusiveAsync<T>(
        string resource,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);

    Task<T> ExecuteExclusiveAsync<T>(
        IReadOnlyCollection<string> resources,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}
