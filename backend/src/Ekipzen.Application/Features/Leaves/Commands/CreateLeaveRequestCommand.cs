using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Notifications;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves.Commands;

/// <summary>
/// İzin talebi oluşturur. Gün sayısı SQL iş-günü fonksiyonuyla hesaplanır;
/// çakışan talep ve yetersiz bakiye 409 döner. Düşümlü onaylı talep
/// istihdam dönemi ledger'ına takvim yılı dilimleri halinde yazılır.
/// </summary>
public sealed record CreateLeaveRequestCommand(
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Description = null,
    int? SubstituteEmployeeId = null,
    string? ExpectedRecordToken = null,
    int? GuidanceContentRevisionId = null) : IRequest<LeaveRequestDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "leave-request-create";
}

public sealed class CreateLeaveRequestCommandValidator : AbstractValidator<CreateLeaveRequestCommand>
{
    public CreateLeaveRequestCommandValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateLeaveRequestCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IWorkingDayCalculator workingDayCalculator,
    INotificationTrigger notificationTrigger,
    IDatabaseLock databaseLock,
    GuidanceOperationContext guidance)
    : IRequestHandler<CreateLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(
        CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(currentUser);
        var result = await databaseLock.ExecuteExclusiveAsync(
            $"leave-ledger:{employeeId}",
            token => CreateAsync(request, employeeId, token),
            cancellationToken);

        await notificationTrigger.LeaveRequestCreatedAsync(result.Request, cancellationToken);
        return result.Dto;
    }

    private async Task<CreateResult> CreateAsync(
        CreateLeaveRequestCommand request,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var leaveType = await context.LeaveTypes
            .FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException("İzin tipi", request.LeaveTypeId);

        if (request.SubstituteEmployeeId == employeeId)
        {
            throw new ConflictException("İzindeki personel kendi vekili olamaz.");
        }
        if (request.SubstituteEmployeeId is not null
            && !await context.Employees.AnyAsync(e => e.Id == request.SubstituteEmployeeId
                && e.Status == Domain.Enums.EmployeeStatus.Active, cancellationToken))
        {
            throw new NotFoundException("Aktif vekil personel", request.SubstituteEmployeeId);
        }

        var period = await LeaveLedgerService.RequirePeriodAsync(
            context, employeeId, request.StartDate, request.EndDate, cancellationToken);
        var days = await workingDayCalculator.GetWorkingDaysAsync(
            request.StartDate, request.EndDate, cancellationToken);
        if (days == 0)
        {
            throw new ConflictException("Seçilen aralıkta iş günü bulunmuyor (hafta sonu/resmi tatil).");
        }

        PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken,
            LeaveOperationReview.CreateToken(employeeId, period.Id, leaveType, request.StartDate,
                request.EndDate, days, request.SubstituteEmployeeId), guidance.IsGuided);

        var hasOverlap = await context.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved)
            && r.StartDate <= request.EndDate
            && r.EndDate >= request.StartDate, cancellationToken);
        if (hasOverlap)
        {
            throw new ConflictException("Bu tarih aralığıyla çakışan bekleyen/onaylı bir talebiniz var.");
        }

        if (leaveType.DeductsFromAnnualBalance)
        {
            if (await LeaveLedgerService.EnsureAccrualsAsync(
                    context, period, request.StartDate, cancellationToken) > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            await LeaveLedgerService.EnsureCanReserveAsync(
                context, period, request.StartDate, days, excludeRequestId: null, cancellationToken);
        }

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            EmploymentPeriodId = period.Id,
            LeaveTypeId = leaveType.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            Description = request.Description,
            SubstituteEmployeeId = request.SubstituteEmployeeId,
            Status = leaveType.RequiresApproval ? LeaveStatus.Pending : LeaveStatus.Approved,
        };

        context.LeaveRequests.Add(leaveRequest);
        await context.SaveChangesAsync(cancellationToken);
        if (leaveRequest.Status == LeaveStatus.Approved && leaveType.DeductsFromAnnualBalance)
        {
            await LeaveLedgerService.AddUsageEntriesAsync(
                context, leaveRequest, workingDayCalculator, reversal: false, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser,
            request.GuidanceOperationKey, "leave-request", leaveRequest.Id,
            [leaveRequest.Status == LeaveStatus.Pending ? "Onay bekleyen talep" : "Onaylı talep", "Tür ve tarihler"], cancellationToken);

        var dto = await context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.Id == leaveRequest.Id)
            .Select(Queries.LeaveQueryProjections.Projection)
            .SingleAsync(cancellationToken);
        return new CreateResult(leaveRequest, dto);
    }

    private sealed record CreateResult(LeaveRequest Request, LeaveRequestDto Dto);
}
