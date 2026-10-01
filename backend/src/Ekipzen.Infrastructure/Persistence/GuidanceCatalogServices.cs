using System.Data;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace Ekipzen.Infrastructure.Persistence;

// Sunucu operatörü ekip kiracısını ve tek tek kullanıcı kimliklerini tanımlar.
// Müşterinin hr-admin rolü veya istemciden gönderilen bir bayrak yayın yetkisi vermez.
public sealed class GuidanceEditorialAccess(
    IConfiguration configuration, ICurrentUser user, ICurrentTenant tenant) : IGuidanceEditorialAccess
{
    public bool CanManage => user.IsAuthenticated && user.Roles.Contains(Roles.HrAdmin)
        && int.TryParse(configuration["GuidanceEditorial:TenantId"], out var teamTenantId)
        && teamTenantId > 0 && tenant.TenantId == teamTenantId
        && user.UserId is not null
        && configuration.GetSection("GuidanceEditorial:UserIds").GetChildren()
            .Any(item => string.Equals(item.Value, user.UserId, StringComparison.Ordinal));
}

public sealed class SqlServerGuidanceCatalogLock(PlatformDbContext context) : IGuidanceCatalogLock
{
    public async Task<T> ExecuteAsync<T>(
        string contentKey, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SET NOCOUNT ON;
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.DbType = DbType.String;
        parameter.Value = "guidance-catalog:" + contentKey;
        command.Parameters.Add(parameter);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0)
            throw new TimeoutException("Rehber içerik kilidi alınamadı.");
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
