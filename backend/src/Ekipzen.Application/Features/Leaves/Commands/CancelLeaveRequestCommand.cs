using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Commands;

/// <summary>
/// Talep sahibi bekleyen veya onaylı talebini iptal eder. Onaylı, bakiyeden
/// düşen talep ledger'da silinmez; eş hareketlerle geri alınır.
/// </summary>
public sealed record CancelLeaveRequestCommand(int Id, string? ExpectedRecordToken = null,
    int? GuidanceContentRevisionId = null) : IRequest, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "leave-request-cancel";
}

public sealed class CancelLeaveRequestCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock,
    GuidanceOperationContext guidance)
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
        var preview = await LeaveOperationReview.CancellationAsync(context, currentUser, request.Id, cancellationToken);
        PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, preview.RecordToken, guidance.IsGuided);
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

        if (leaveRequest.Status == LeaveStatus.Approved)
        {
            await LeaveLedgerService.ReverseRecordedUsageAsync(context, leaveRequest, cancellationToken);
        }

        leaveRequest.Status = LeaveStatus.Cancelled;
        await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser,
            request.GuidanceOperationKey, "leave-request", leaveRequest.Id,
            ["Talep iptali", $"Kalkan rezervasyon: {preview.ReleasedReservationDays} gün", $"İade: {preview.RefundedDays} gün"], cancellationToken);
    }
}
