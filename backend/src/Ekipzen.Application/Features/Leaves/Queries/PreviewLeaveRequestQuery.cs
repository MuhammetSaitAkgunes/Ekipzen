using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Queries;

/// <summary>İstek göndermeden önce mevcut kayıtlarla yapılan salt-okur değerlendirme.</summary>
public sealed record LeaveRequestPreviewDto(
    int WorkingDays,
    bool DeductsFromAnnualBalance,
    bool RequiresApproval,
    bool CanSubmit,
    string? BlockReason,
    DateTime PreviewedAtUtc);

public sealed record PreviewLeaveRequestQuery(
    int LeaveTypeId, DateOnly StartDate, DateOnly EndDate, int? SubstituteEmployeeId = null)
    : IRequest<LeaveRequestPreviewDto>;

public sealed class PreviewLeaveRequestQueryValidator : AbstractValidator<PreviewLeaveRequestQuery>
{
    public PreviewLeaveRequestQueryValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
    }
}

public sealed class PreviewLeaveRequestQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<PreviewLeaveRequestQuery, LeaveRequestPreviewDto>
{
    public async Task<LeaveRequestPreviewDto> Handle(
        PreviewLeaveRequestQuery request, CancellationToken ct)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);
        var leaveType = await context.LeaveTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.LeaveTypeId, ct)
            ?? throw new NotFoundException("İzin tipi", request.LeaveTypeId);

        if (request.SubstituteEmployeeId == employeeId)
        {
            return Block(0, "İzindeki personel kendi vekili olamaz.");
        }
        if (request.SubstituteEmployeeId is not null &&
            !await context.Employees.AsNoTracking().AnyAsync(
                item => item.Id == request.SubstituteEmployeeId &&
                    item.Status == EmployeeStatus.Active, ct))
        {
            throw new NotFoundException("Aktif vekil personel", request.SubstituteEmployeeId);
        }

        var days = await workingDayCalculator.GetWorkingDaysAsync(request.StartDate, request.EndDate, ct);
        if (days == 0)
        {
            return Block(days, "Seçilen aralıkta iş günü bulunmuyor (hafta sonu/resmi tatil).");
        }

        Ekipzen.Domain.Entities.Organization.EmployeeEmploymentPeriod period;
        try
        {
            period = await LeaveLedgerService.RequirePeriodAsync(
                context, employeeId, request.StartDate, request.EndDate, ct);
        }
        catch (ConflictException exception)
        {
            return Block(days, exception.Message);
        }

        var hasOverlap = await context.LeaveRequests.AsNoTracking().AnyAsync(item =>
            item.EmployeeId == employeeId &&
            (item.Status == LeaveStatus.Pending || item.Status == LeaveStatus.Approved) &&
            item.StartDate <= request.EndDate &&
            item.EndDate >= request.StartDate, ct);
        if (hasOverlap)
        {
            return Block(days, "Bu tarih aralığıyla çakışan bekleyen/onaylı bir talebiniz var.");
        }

        if (leaveType.DeductsFromAnnualBalance)
        {
            var unrecordedAccruals = await LeaveLedgerService.GetUnrecordedAccrualsAsync(
                context, period, request.StartDate, ct);
            var conflict = await LeaveLedgerService.GetReservationConflictAsync(
                context, period, request.StartDate, days, null, unrecordedAccruals, ct);
            if (conflict is not null)
            {
                return Block(days, conflict);
            }
        }

        return new(days, leaveType.DeductsFromAnnualBalance, leaveType.RequiresApproval,
            true, null, DateTime.UtcNow);

        LeaveRequestPreviewDto Block(int workingDays, string reason) =>
            new(workingDays, leaveType.DeductsFromAnnualBalance, leaveType.RequiresApproval,
                false, reason, DateTime.UtcNow);
    }
}
