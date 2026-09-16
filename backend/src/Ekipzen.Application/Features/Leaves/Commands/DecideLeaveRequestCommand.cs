using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Commands;

public sealed record DecideLeaveRequestCommand(int Id, bool Approve, string? DecisionNote = null)
    : IRequest<LeaveRequestDto>;

public sealed class DecideLeaveRequestCommandValidator : AbstractValidator<DecideLeaveRequestCommand>
{
    public DecideLeaveRequestCommandValidator()
    {
        RuleFor(x => x.DecisionNote).MaximumLength(1000);
    }
}

public sealed class DecideLeaveRequestCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IWorkingDayCalculator workingDayCalculator,
    IDatabaseLock databaseLock)
    : IRequestHandler<DecideLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(
        DecideLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await context.LeaveRequests
            .Where(item => item.Id == request.Id)
            .Select(item => (int?)item.EmployeeId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İzin talebi", request.Id);

        return await databaseLock.ExecuteExclusiveAsync(
            $"leave-ledger:{employeeId}",
            token => DecideAsync(request, token),
            cancellationToken);
    }

    private async Task<LeaveRequestDto> DecideAsync(
        DecideLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await context.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.EmploymentPeriod)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İzin talebi", request.Id);

        var inScope = await context.Employees
            .ScopeFor(currentUser)
            .AnyAsync(e => e.Id == leaveRequest.EmployeeId, cancellationToken);
        if (!inScope)
        {
            throw new ForbiddenAccessException("Bu talep sizin onay kapsamınızda değil.");
        }

        var isHrAdmin = currentUser.Roles.Contains(Roles.HrAdmin);
        if (!isHrAdmin && leaveRequest.EmployeeId == currentUser.EmployeeId)
        {
            throw new ForbiddenAccessException("Kendi izin talebinizi karara bağlayamazsınız.");
        }
        if (leaveRequest.Status != LeaveStatus.Pending)
        {
            throw new ConflictException("Yalnız bekleyen talepler karara bağlanabilir.");
        }

        if (request.Approve && (leaveRequest.LeaveType?.DeductsFromAnnualBalance ?? false))
        {
            var period = leaveRequest.EmploymentPeriod
                ?? throw new ConflictException("İzin talebinin istihdam dönemi bulunamıyor.");
            if (await LeaveLedgerService.EnsureAccrualsAsync(
                    context, period, leaveRequest.StartDate, cancellationToken) > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            await LeaveLedgerService.EnsureCanReserveAsync(
                context, period, leaveRequest.StartDate, leaveRequest.Days,
                excludeRequestId: leaveRequest.Id, cancellationToken);
        }

        leaveRequest.Status = request.Approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        leaveRequest.DecisionByUserId = currentUser.UserId;
        leaveRequest.DecisionAtUtc = DateTime.UtcNow;
        leaveRequest.DecisionNote = request.DecisionNote;
        await context.SaveChangesAsync(cancellationToken);

        if (request.Approve && (leaveRequest.LeaveType?.DeductsFromAnnualBalance ?? false))
        {
            await LeaveLedgerService.AddUsageEntriesAsync(
                context, leaveRequest, workingDayCalculator, reversal: false, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.Id == leaveRequest.Id)
            .Select(Queries.LeaveQueryProjections.Projection)
            .SingleAsync(cancellationToken);
    }
}
