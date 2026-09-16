using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Commands;

/// <summary>
/// Talep sahibi bekleyen veya onaylı talebini iptal eder. Onaylı, bakiyeden
/// düşen talep ledger'da silinmez; eş hareketlerle geri alınır.
/// </summary>
public sealed record CancelLeaveRequestCommand(int Id) : IRequest;

public sealed class CancelLeaveRequestCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IWorkingDayCalculator workingDayCalculator,
    IDatabaseLock databaseLock)
    : IRequestHandler<CancelLeaveRequestCommand>
{
    public async Task Handle(CancelLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);
        await databaseLock.ExecuteExclusiveAsync(
            $"leave-ledger:{employeeId}",
            async token =>
            {
                await CancelAsync(request, employeeId, token);
                return true;
            },
            cancellationToken);
    }

    private async Task CancelAsync(
        CancelLeaveRequestCommand request,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var leaveRequest = await context.LeaveRequests
            .Include(item => item.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İzin talebi", request.Id);

        if (leaveRequest.EmployeeId != employeeId)
        {
            throw new ForbiddenAccessException("Yalnız kendi talebinizi iptal edebilirsiniz.");
        }
        if (leaveRequest.Status is not (LeaveStatus.Pending or LeaveStatus.Approved))
        {
            throw new ConflictException("Yalnız bekleyen veya onaylı talepler iptal edilebilir.");
        }

        if (leaveRequest.Status == LeaveStatus.Approved
            && (leaveRequest.LeaveType?.DeductsFromAnnualBalance ?? false))
        {
            await LeaveLedgerService.AddUsageEntriesAsync(
                context, leaveRequest, workingDayCalculator, reversal: true, cancellationToken);
        }

        leaveRequest.Status = LeaveStatus.Cancelled;
        await context.SaveChangesAsync(cancellationToken);
    }
}
