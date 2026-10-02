using System.Globalization;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Leaves;

public static class LeaveOperationReview
{
    public static string CreateToken(int employeeId, int periodId, LeaveType type,
        DateOnly start, DateOnly end, int days, int? substituteId) => PreparationOperationEvidence.RecordToken(
        employeeId.ToString(CultureInfo.InvariantCulture), periodId.ToString(CultureInfo.InvariantCulture),
        type.Id.ToString(CultureInfo.InvariantCulture), type.Name, type.Code,
        type.DeductsFromAnnualBalance.ToString(), type.RequiresApproval.ToString(),
        start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        days.ToString(CultureInfo.InvariantCulture), substituteId?.ToString(CultureInfo.InvariantCulture));

    public static async Task<LeaveCancellationPreviewDto> CancellationAsync(
        IApplicationDbContext db, ICurrentUser user, int id, CancellationToken ct)
    {
        var employeeId = LeaveGuards.RequireEmployeeId(user);
        var request = await db.LeaveRequests.AsNoTracking().Include(x => x.LeaveType)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("İzin talebi", id);
        if (request.EmployeeId != employeeId)
            throw new ForbiddenAccessException("Yalnız kendi talebinizi iptal edebilirsiniz.");
        var entries = await db.LeaveLedgerEntries.AsNoTracking().Where(x => x.LeaveRequestId == id)
            .OrderBy(x => x.Id).ToListAsync(ct);
        var canCancel = request.Status is LeaveStatus.Pending or LeaveStatus.Approved;
        var refund = request.Status == LeaveStatus.Approved
            ? -entries.Where(x => x.Type == LeaveLedgerEntryType.Usage).Sum(x => x.Days) : 0;
        var reserved = request.Status == LeaveStatus.Pending && request.LeaveType!.DeductsFromAnnualBalance ? request.Days : 0;
        var token = PreparationOperationEvidence.RecordToken(request.Id.ToString(CultureInfo.InvariantCulture),
            request.EmployeeId.ToString(CultureInfo.InvariantCulture), request.EmploymentPeriodId.ToString(CultureInfo.InvariantCulture),
            request.Status.ToString(), request.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), request.Days.ToString(CultureInfo.InvariantCulture),
            request.LeaveTypeId.ToString(CultureInfo.InvariantCulture), request.LeaveType!.Name,
            request.LeaveType.DeductsFromAnnualBalance.ToString(),
            string.Join("|", entries.Select(x => $"{x.Id}:{x.Type}:{x.Days}:{x.OccurredOn:yyyy-MM-dd}:{x.SourceKey}")));
        return new(id, request.LeaveType.Name, request.StartDate, request.EndDate, request.Days,
            request.Status.ToDto(), canCancel, reserved, refund, token);
    }
}
