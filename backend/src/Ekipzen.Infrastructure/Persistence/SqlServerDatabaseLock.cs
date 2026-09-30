using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// SQL Server <c>sp_getapplock</c> tabanlı, transaction sahipli dağıtık kilit.
/// Kaynak adı tenant kimliğiyle ayrılır; aynı kiracının tüm API instance'ları
/// aynı kilidi paylaşırken farklı kiracılar birbirini bekletmez.
/// </summary>
public sealed class SqlServerDatabaseLock(AppDbContext context, ICurrentTenant currentTenant)
    : IDatabaseLock
{
    private const int LockTimeoutMilliseconds = 30_000;

    public async Task<T> ExecuteExclusiveAsync<T>(
        string resource,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
        => await ExecuteExclusiveAsync([resource], action, cancellationToken);

    public async Task<T> ExecuteExclusiveAsync<T>(
        IReadOnlyCollection<string> resources,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (resources.Count == 0)
            throw new ArgumentException("En az bir kilit kaynağı gerekli.", nameof(resources));
        var ordered = resources.Distinct(StringComparer.Ordinal)
            .OrderBy(resource => resource, StringComparer.Ordinal).ToList();
        foreach (var resource in ordered)
            ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        foreach (var resource in ordered)
            await AcquireAsync(resource, transaction, cancellationToken);

        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task AcquireAsync(
        string resource,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SET NOCOUNT ON;
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeout;
            SELECT @lockResult;
            """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.DbType = DbType.String;
        resourceParameter.Value = $"tenant:{currentTenant.TenantIdOrThrow()}:{resource}";
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@lockTimeout";
        timeoutParameter.DbType = DbType.Int32;
        timeoutParameter.Value = LockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var lockResult = Convert.ToInt32(scalar);
        if (lockResult < 0)
        {
            throw new TimeoutException(
                $"Veritabanı iş kilidi alınamadı (kaynak: {resource}, sonuç: {lockResult}).");
        }
    }
}
