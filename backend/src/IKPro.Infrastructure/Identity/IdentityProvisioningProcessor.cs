using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Identity;

/// <summary>
/// Kalıcı Identity işlerini lease ile sahiplenir. ExecuteUpdate tabanlı claim,
/// aynı tenant işini birden çok uygulama örneğinin eşzamanlı çalıştırmasını önler.
/// </summary>
public sealed class IdentityProvisioningProcessor(
    IApplicationDbContext context,
    IIdentityService identityService,
    ILogger<IdentityProvisioningProcessor> logger) : IIdentityProvisioningProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<IdentityProvisioningStatus> ProcessNowAsync(
        int operationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var claimed = await context.IdentityProvisioningOperations
            .Where(operation => operation.Id == operationId
                && operation.Status == IdentityProvisioningStatus.Pending
                && operation.NextAttemptAtUtc <= now
                && (operation.LeaseUntilUtc == null || operation.LeaseUntilUtc < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.LeaseUntilUtc, now.Add(LeaseDuration))
                .SetProperty(operation => operation.LastAttemptAtUtc, now)
                .SetProperty(operation => operation.AttemptCount, operation => operation.AttemptCount + 1),
                cancellationToken);

        if (claimed == 0)
        {
            var currentStatus = await context.IdentityProvisioningOperations
                .Where(operation => operation.Id == operationId)
                .Select(operation => (IdentityProvisioningStatus?)operation.Status)
                .SingleOrDefaultAsync(cancellationToken);

            return currentStatus
                ?? throw new NotFoundException("Identity provizyon işi", operationId);
        }

        var operation = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .SingleAsync(item => item.Id == operationId, cancellationToken);

        try
        {
            switch (operation.Type)
            {
                case IdentityProvisioningOperationType.EmployeeLogin:
                    await ProcessEmployeeLoginAsync(operation, cancellationToken);
                    break;
                case IdentityProvisioningOperationType.GrantManagerRole:
                    await identityService.SetEmployeeManagerRoleAsync(operation.EmployeeId, true, cancellationToken);
                    break;
                case IdentityProvisioningOperationType.RevokeManagerRole:
                    await identityService.SetEmployeeManagerRoleAsync(operation.EmployeeId, false, cancellationToken);
                    break;
                case IdentityProvisioningOperationType.EnableEmployeeAccess:
                    await identityService.SetEmployeeAccessAsync(operation.EmployeeId, true, cancellationToken);
                    break;
                case IdentityProvisioningOperationType.DisableEmployeeAccess:
                    await identityService.SetEmployeeAccessAsync(operation.EmployeeId, false, cancellationToken);
                    break;
                default:
                    throw new ValidationException(
                        $"Desteklenmeyen provizyon işi: {operation.Type}.");
            }

            var completedAt = DateTime.UtcNow;
            await context.IdentityProvisioningOperations
                .Where(item => item.Id == operation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, IdentityProvisioningStatus.Completed)
                    .SetProperty(item => item.CompletedAtUtc, completedAt)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.LastError, (string?)null), cancellationToken);

            return IdentityProvisioningStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var permanent = exception is ConflictException or ValidationException;
            var nextAttempt = permanent
                ? operation.NextAttemptAtUtc
                : DateTime.UtcNow.Add(BackoffFor(operation.AttemptCount));
            var error = exception.Message.Length <= 2000
                ? exception.Message
                : exception.Message[..2000];

            await context.IdentityProvisioningOperations
                .Where(item => item.Id == operation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, permanent
                        ? IdentityProvisioningStatus.Failed
                        : IdentityProvisioningStatus.Pending)
                    .SetProperty(item => item.NextAttemptAtUtc, nextAttempt)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.LastError, error), cancellationToken);

            logger.LogWarning(exception,
                "Identity provizyon işi tamamlanamadı. Operation={OperationId}, Attempt={Attempt}, Permanent={Permanent}",
                operation.Id, operation.AttemptCount, permanent);

            return permanent
                ? IdentityProvisioningStatus.Failed
                : IdentityProvisioningStatus.Pending;
        }
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var operationIds = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .Where(operation => operation.Status == IdentityProvisioningStatus.Pending
                && operation.NextAttemptAtUtc <= now
                && (operation.LeaseUntilUtc == null || operation.LeaseUntilUtc < now))
            .OrderBy(operation => operation.NextAttemptAtUtc)
            .ThenBy(operation => operation.Id)
            .Select(operation => operation.Id)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var completed = 0;
        foreach (var operationId in operationIds)
        {
            if (await ProcessNowAsync(operationId, cancellationToken)
                == IdentityProvisioningStatus.Completed)
            {
                completed++;
            }
        }

        return completed;
    }

    private async Task ProcessEmployeeLoginAsync(
        Domain.Entities.Identity.IdentityProvisioningOperation operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operation.Email)
            || string.IsNullOrWhiteSpace(operation.DisplayName))
        {
            throw new ValidationException("Personel login provizyonunda e-posta ve görünen ad zorunludur.");
        }

        var userId = await identityService.CreateEmployeeLoginAsync(
            operation.EmployeeId, operation.DisplayName, operation.Email, cancellationToken);

        await context.Employees
            .Where(employee => employee.Id == operation.EmployeeId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(employee => employee.UserId, userId), cancellationToken);
    }

    private static TimeSpan BackoffFor(int attemptCount)
    {
        var minutes = Math.Min(60, Math.Pow(2, Math.Clamp(attemptCount - 1, 0, 6)));
        return TimeSpan.FromMinutes(minutes);
    }
}
