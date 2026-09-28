using FluentValidation;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Files;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Attendance.Queries;

// --- canlı yoklama panosu ---

/// <summary>
/// Günün yoklama panosu: kapsamdaki AKTİF personel + varsa gün kaydı.
/// Kaydı olmayan personel 'absent' kartı olarak döner (attendance.js dailyMoves).
/// </summary>
public sealed record GetLiveBoardQuery(DateOnly? Date = null) : IRequest<IReadOnlyList<LiveBoardCardDto>>;

public sealed class GetLiveBoardQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetLiveBoardQuery, IReadOnlyList<LiveBoardCardDto>>
{
    public async Task<IReadOnlyList<LiveBoardCardDto>> Handle(
        GetLiveBoardQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await context.Employees
            .AsNoTracking()
            .ScopeFor(currentUser)
            .Where(e => e.Status == EmployeeStatus.Active)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                Department = e.Department!.Name,
                Record = context.AttendanceRecords
                    .Where(a => a.EmployeeId == e.Id && a.WorkDate == date)
                    .Select(a => new { a.CheckIn, a.Status })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new LiveBoardCardDto(
                r.Id,
                $"{r.FirstName} {r.LastName}",
                string.Concat(r.FirstName[..1], r.LastName[..1]).ToUpperInvariant(),
                r.Department,
                r.Record?.CheckIn,
                (r.Record?.Status ?? AttendanceStatus.Absent).ToDto()))
            .ToList();
    }
}

// --- aylık puantaj (tek çalışan) ---

public sealed record GetTimesheetQuery(int EmployeeId, int Year, int Month) : IRequest<TimesheetDto>;

public sealed class GetTimesheetQueryValidator : AbstractValidator<GetTimesheetQuery>
{
    public GetTimesheetQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class GetTimesheetQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetTimesheetQuery, TimesheetDto>
{
    public async Task<TimesheetDto> Handle(GetTimesheetQuery request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, request.EmployeeId, cancellationToken);

        var employeeName = await context.Employees
            .Where(e => e.Id == request.EmployeeId)
            .Select(e => e.FirstName + " " + e.LastName)
            .SingleAsync(cancellationToken);

        var records = await context.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.EmployeeId == request.EmployeeId
                        && a.WorkDate.Year == request.Year
                        && a.WorkDate.Month == request.Month)
            .OrderBy(a => a.WorkDate)
            .ToListAsync(cancellationToken);

        var rows = records
            .Select(a => new TimesheetRowDto(
                a.Id,
                a.WorkDate,
                a.Type.ToDto(),
                a.CheckIn,
                a.CheckOut,
                a.BreakMinutes,
                a.WorkedMinutes,
                a.OvertimeMinutes,
                AttendanceMappings.ToTimesheetStatus(a.Status, a.OvertimeMinutes),
                a.Note,
                a.Revision()))
            .ToList();

        return new TimesheetDto(
            request.EmployeeId,
            employeeName,
            request.Year,
            request.Month,
            rows,
            records.Sum(a => a.WorkedMinutes),
            records.Sum(a => a.OvertimeMinutes));
    }
}

// --- aylık özet (SQL view; kapsam dahilindeki tüm personel) ---

public sealed record GetMonthlyAttendanceSummaryQuery(int Year, int Month)
    : IRequest<IReadOnlyList<AttendanceSummaryDto>>;

public sealed class GetMonthlyAttendanceSummaryQueryValidator
    : AbstractValidator<GetMonthlyAttendanceSummaryQuery>
{
    public GetMonthlyAttendanceSummaryQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class GetMonthlyAttendanceSummaryQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMonthlyAttendanceSummaryQuery, IReadOnlyList<AttendanceSummaryDto>>
{
    public async Task<IReadOnlyList<AttendanceSummaryDto>> Handle(
        GetMonthlyAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        // Keyless view + Join çevirisi EF'te sorunlu: view ayrı sorgulanır,
        // kapsamdaki personelle bellek içinde birleştirilir (aylık satır sayısı küçük).
        var summaries = await context.MonthlyAttendanceSummaries
            .Where(s => s.Year == request.Year && s.Month == request.Month)
            .ToListAsync(cancellationToken);

        var summaryIds = summaries.Select(s => s.EmployeeId).ToList();
        var employees = await context.Employees
            .AsNoTracking()
            .ScopeFor(currentUser)
            .Where(e => summaryIds.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, Department = e.Department!.Name })
            .ToListAsync(cancellationToken);

        return employees
            .Join(summaries, e => e.Id, s => s.EmployeeId, (e, s) => new AttendanceSummaryDto(
                e.Id,
                e.Name,
                e.Department,
                s.TotalDays,
                s.PresentDays,
                s.AbsentDays,
                s.LateDays,
                s.TotalWorkedMinutes,
                s.TotalOvertimeMinutes))
            .OrderBy(s => s.EmployeeName)
            .ToList();
    }
}
