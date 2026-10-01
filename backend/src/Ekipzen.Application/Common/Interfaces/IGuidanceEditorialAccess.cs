namespace Ekipzen.Application.Common.Interfaces;

public interface IGuidanceEditorialAccess
{
    bool CanManage { get; }
}

/// <summary>Merkezî katalog için platform veritabanında transaction sahipli kilit.</summary>
public interface IGuidanceCatalogLock
{
    Task<T> ExecuteAsync<T>(string contentKey, Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
