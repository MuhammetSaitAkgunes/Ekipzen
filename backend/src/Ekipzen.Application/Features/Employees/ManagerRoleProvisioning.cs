using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Employees;

/// <summary>
/// Organizasyon hiyerarşisiyle Identity rolleri arasındaki kalıcı teslimat kaydını
/// aynı iş verisi commit'ine ekler ve ilk denemeyi tetikler.
/// </summary>
internal static class ManagerRoleProvisioning
{
    public static IdentityProvisioningOperation Enqueue(
        IApplicationDbContext context, int employeeId, bool isManager)
    {
        var operation = new IdentityProvisioningOperation
        {
            EmployeeId = employeeId,
            Type = isManager
                ? IdentityProvisioningOperationType.GrantManagerRole
                : IdentityProvisioningOperationType.RevokeManagerRole,
            Status = IdentityProvisioningStatus.Pending,
            NextAttemptAtUtc = DateTime.UtcNow,
        };
        context.IdentityProvisioningOperations.Add(operation);
        return operation;
    }

    public static async Task ProcessNowAsync(
        IIdentityProvisioningProcessor processor,
        IEnumerable<IdentityProvisioningOperation> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await processor.ProcessNowAsync(operation.Id, cancellationToken);
        }
    }
}
