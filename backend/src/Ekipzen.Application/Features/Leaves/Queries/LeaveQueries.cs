using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Queries;

// --- izin tipleri ---

public sealed record GetLeaveTypesQuery : IRequest<IReadOnlyList<LeaveTypeDto>>;

public sealed class GetLeaveTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>
{
    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(
        GetLeaveTypesQuery request, CancellationToken cancellationToken)
        => await context.LeaveTypes
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new LeaveTypeDto(t.Id, t.Name, t.Code, t.DeductsFromAnnualBalance, t.RequiresApproval))
            .ToListAsync(cancellationToken);
}

// --- bakiyem (SQL view) ---

public sealed record GetMyLeaveBalanceQuery(int? Year = null) : IRequest<LeaveBalanceDto>;

public sealed class GetMyLeaveBalanceQueryHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<GetMyLeaveBalanceQuery, LeaveBalanceDto>
{
    public Task<LeaveBalanceDto> Handle(
        GetMyLeaveBalanceQuery request, CancellationToken cancellationToken)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);
        var year = request.Year ?? DateTime.UtcNow.Year;
        return databaseLock.ExecuteExclusiveAsync(
            $"leave-ledger:{employeeId}",
            token => GetAsync(employeeId, year, token),
            cancellationToken);
    }

    private async Task<LeaveBalanceDto> GetAsync(
        int employeeId, int year, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var throughDate = year < today.Year
            ? new DateOnly(year, 12, 31)
            : year == today.Year ? today : new DateOnly(year, 1, 1);
        var period = await LeaveLedgerService.RequireLatestPeriodAsync(
            context, employeeId, year, cancellationToken);
        if (await LeaveLedgerService.EnsureAccrualsAsync(
                context, period, throughDate, cancellationToken) > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await LeaveLedgerService.GetBalanceAsync(
            context, period, year, throughDate, cancellationToken);
    }
}

public sealed record GetMyLeaveLedgerQuery(int? Year = null)
    : IRequest<IReadOnlyList<LeaveLedgerEntryDto>>;

public sealed class GetMyLeaveLedgerQueryHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<GetMyLeaveLedgerQuery, IReadOnlyList<LeaveLedgerEntryDto>>
{
    public Task<IReadOnlyList<LeaveLedgerEntryDto>> Handle(
        GetMyLeaveLedgerQuery request, CancellationToken cancellationToken)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);
        var year = request.Year ?? DateTime.UtcNow.Year;
        return databaseLock.ExecuteExclusiveAsync(
            $"leave-ledger:{employeeId}",
            token => GetAsync(employeeId, year, token),
            cancellationToken);
    }

    private async Task<IReadOnlyList<LeaveLedgerEntryDto>> GetAsync(
        int employeeId, int year, CancellationToken cancellationToken)
    {
        var period = await LeaveLedgerService.RequireLatestPeriodAsync(
            context, employeeId, year, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var throughDate = year < today.Year ? new DateOnly(year, 12, 31) : today;
        if (await LeaveLedgerService.EnsureAccrualsAsync(
                context, period, throughDate, cancellationToken) > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        return await LeaveLedgerService.GetEntriesAsync(
            context, period.Id, year, cancellationToken);
    }
}

// --- taleplerim ---

public sealed record GetMyLeaveRequestsQuery(int? Year = null) : IRequest<IReadOnlyList<LeaveRequestDto>>;

public sealed class GetMyLeaveRequestsQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetMyLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);

        var query = context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId);

        if (request.Year is not null)
        {
            query = query.Where(r => r.StartDate.Year == request.Year);
        }

        return await query
            .OrderByDescending(r => r.StartDate)
            .Select(LeaveQueryProjections.Projection)
            .ToListAsync(cancellationToken);
    }
}

// --- onay kuyruğu (manager: ekibi, hr-admin: herkes; kendi talebi hariç) ---

public sealed record GetPendingLeaveRequestsQuery : IRequest<IReadOnlyList<LeaveRequestDto>>;

public sealed class GetPendingLeaveRequestsQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPendingLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetPendingLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var scopedEmployeeIds = context.Employees.ScopeFor(currentUser).Select(e => e.Id);

        var query = context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.Status == LeaveStatus.Pending && scopedEmployeeIds.Contains(r.EmployeeId));

        if (!currentUser.Roles.Contains(Roles.HrAdmin) && currentUser.EmployeeId is not null)
        {
            query = query.Where(r => r.EmployeeId != currentUser.EmployeeId);
        }

        return await query
            .OrderBy(r => r.StartDate)
            .Select(LeaveQueryProjections.Projection)
            .ToListAsync(cancellationToken);
    }
}

// --- takım yokluk widget'ı ---

public sealed record GetTeamLeavesQuery(int WindowDays = 14) : IRequest<IReadOnlyList<TeamLeaveDto>>;

public sealed class GetTeamLeavesQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetTeamLeavesQuery, IReadOnlyList<TeamLeaveDto>>
{
    public async Task<IReadOnlyList<TeamLeaveDto>> Handle(
        GetTeamLeavesQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowEnd = today.AddDays(request.WindowDays);

        IQueryable<int> teamIds;
        if (currentUser.Roles.Contains(Roles.HrAdmin))
        {
            teamIds = context.Employees.Select(e => e.Id);
        }
        else if (currentUser.Roles.Contains(Roles.Manager))
        {
            var selfId = currentUser.EmployeeId ?? -1;
            teamIds = context.Employees
                .Where(e => e.ManagerId == selfId || e.Id == selfId)
                .Select(e => e.Id);
        }
        else
        {
            // Çalışan: aynı yöneticiye bağlı takım arkadaşları + kendisi.
            var selfId = LeaveGuards.RequireEmployeeId(currentUser);
            teamIds = context.Employees
                .Where(e => e.Id == selfId ||
                            (e.ManagerId != null &&
                             e.ManagerId == context.Employees
                                 .Where(s => s.Id == selfId)
                                 .Select(s => s.ManagerId)
                                 .FirstOrDefault()))
                .Select(e => e.Id);
        }

        return await context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.Status == LeaveStatus.Approved
                        && teamIds.Contains(r.EmployeeId)
                        && r.EndDate >= today
                        && r.StartDate <= windowEnd)
            .OrderBy(r => r.StartDate)
            .Select(r => new TeamLeaveDto(
                r.EmployeeId,
                r.Employee!.FirstName + " " + r.Employee.LastName,
                string.Concat(
                    r.Employee.FirstName.Substring(0, 1),
                    r.Employee.LastName.Substring(0, 1)).ToUpper(),
                r.LeaveType!.Name,
                r.StartDate,
                r.EndDate))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Ortak LeaveRequest → DTO projeksiyonu (Expression: EF sorgu içinde çevrilebilir).</summary>
public static class LeaveQueryProjections
{
    public static readonly System.Linq.Expressions.Expression<
        Func<Domain.Entities.Leaves.LeaveRequest, LeaveRequestDto>> Projection = r => new LeaveRequestDto(
            r.Id,
            r.EmployeeId,
            r.Employee!.FirstName + " " + r.Employee.LastName,
            r.LeaveTypeId,
            r.LeaveType!.Name,
            r.StartDate,
            r.EndDate,
            r.Days,
            r.Status == LeaveStatus.Pending ? "pending"
                : r.Status == LeaveStatus.Approved ? "approved"
                : r.Status == LeaveStatus.Rejected ? "rejected"
                : "cancelled",
            r.Description,
            r.SubstituteEmployeeId,
            r.DecisionNote,
            r.DecisionAtUtc,
            r.CreatedAtUtc);
}
