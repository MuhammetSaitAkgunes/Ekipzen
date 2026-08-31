using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Offboarding;

public sealed record ManagerSuccessorDto(
    int Id,
    string Name,
    string Title,
    bool IsDirectReport);

public sealed record EmployeeOffboardingContextDto(
    int EmployeeId,
    string EmployeeName,
    int DirectReportCount,
    int PendingLeaveRequestCount,
    IReadOnlyList<ManagerSuccessorDto> Successors);

public sealed record GetEmployeeOffboardingContextQuery(int EmployeeId)
    : IRequest<EmployeeOffboardingContextDto>;

public sealed class GetEmployeeOffboardingContextQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetEmployeeOffboardingContextQuery, EmployeeOffboardingContextDto>
{
    public async Task<EmployeeOffboardingContextDto> Handle(
        GetEmployeeOffboardingContextQuery request, CancellationToken cancellationToken)
    {
        var employees = await context.Employees
            .AsNoTracking()
            .Select(employee => new
            {
                employee.Id,
                employee.FirstName,
                employee.LastName,
                employee.Title,
                employee.Status,
                employee.ManagerId,
                employee.UserId,
            })
            .ToListAsync(cancellationToken);

        var departing = employees.FirstOrDefault(employee => employee.Id == request.EmployeeId)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        var directReportIds = employees
            .Where(employee => employee.ManagerId == departing.Id)
            .Select(employee => employee.Id)
            .ToHashSet();

        // Doğrudan rapor terfi edebilir; daha derin alt ekip seçilirse devir
        // hiyerarşide döngü yaratabileceği için aday listesinden çıkarılır.
        var descendantIds = new HashSet<int>(directReportIds);
        var frontier = directReportIds.ToHashSet();
        while (frontier.Count > 0)
        {
            var next = employees
                .Where(employee => employee.ManagerId is int managerId
                    && frontier.Contains(managerId)
                    && descendantIds.Add(employee.Id))
                .Select(employee => employee.Id)
                .ToHashSet();
            frontier = next;
        }

        var successors = employees
            .Where(employee => employee.Id != departing.Id
                && employee.Status == EmployeeStatus.Active
                && !string.IsNullOrWhiteSpace(employee.UserId)
                && (directReportIds.Contains(employee.Id) || !descendantIds.Contains(employee.Id)))
            .Select(employee => new ManagerSuccessorDto(
                employee.Id,
                $"{employee.FirstName} {employee.LastName}".Trim(),
                employee.Title,
                directReportIds.Contains(employee.Id)))
            .OrderByDescending(employee => employee.IsDirectReport)
            .ThenBy(employee => employee.Name)
            .ToList();

        var pendingLeaveRequestCount = directReportIds.Count == 0
            ? 0
            : await context.LeaveRequests
                .AsNoTracking()
                .CountAsync(request => request.Status == LeaveStatus.Pending
                    && directReportIds.Contains(request.EmployeeId), cancellationToken);

        return new EmployeeOffboardingContextDto(
            departing.Id,
            $"{departing.FirstName} {departing.LastName}".Trim(),
            directReportIds.Count,
            pendingLeaveRequestCount,
            successors);
    }
}
