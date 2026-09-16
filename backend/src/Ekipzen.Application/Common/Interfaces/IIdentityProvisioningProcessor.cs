using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Common.Interfaces;

public interface IIdentityProvisioningProcessor
{
    Task<IdentityProvisioningStatus> ProcessNowAsync(
        int operationId,
        CancellationToken cancellationToken);

    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken);
}
