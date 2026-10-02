using System.Security.Cryptography;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Services;
using MediatR;

namespace Ekipzen.Application.Features.Payroll.Periods;

public sealed record PayrollRowApprovalPreviewDto(PayrollRowDto Row, IReadOnlyList<string> BlockingReasons)
{ public bool CanApprove => BlockingReasons.Count == 0; }
public sealed record GetPayrollRowApprovalPreviewQuery(int PeriodId, int RowId) : IRequest<PayrollRowApprovalPreviewDto>;
public sealed class GetPayrollRowApprovalPreviewQueryHandler(IApplicationDbContext db, IPayrollEngine engine, IDatabaseLock gate)
    : IRequestHandler<GetPayrollRowApprovalPreviewQuery, PayrollRowApprovalPreviewDto>
{
    public Task<PayrollRowApprovalPreviewDto> Handle(GetPayrollRowApprovalPreviewQuery request, CancellationToken ct) =>
        gate.ExecuteExclusiveAsync(PayrollRowGuards.LockResource(request.PeriodId), async lockedCt => {
            var row = await PayrollRowGuards.GetMutableRowAsync(db, request.PeriodId, request.RowId, lockedCt);
            await PayrollRowReview.RefreshAsync(db, row, lockedCt);
            var parameters = row.Period!.Settings!.ToEngineParameters();
            var calculation = engine.Calculate(row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore), parameters);
            var reasons = PayrollRowReadiness.GetBlockingReasons(row, calculation).ToList();
            if (row.ApprovalStatus == Ekipzen.Domain.Enums.PayrollApprovalStatus.Approved) reasons.Add("Satır zaten onaylı.");
            return new PayrollRowApprovalPreviewDto(PayrollPeriodProjector.ToRow(row, engine, parameters, row.Period.MinimumWageIncomeTaxBaseBefore), reasons);
        }, ct);
}
public static class PayrollRowReview
{
    public static string Revision(PayrollRowDto dto, PayrollEmployee row) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { Data = dto with { Revision = null }, Created = row.CreatedAtUtc.Ticks, Updated = row.UpdatedAtUtc?.Ticks,
            PeriodStatus = row.Period?.Status, SettingsVersion = row.Period?.Settings?.Version,
            row.CurrentTimesheetRevision, row.ReviewedTimesheetRevision })));
    public static void EnsureRevision(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected)) throw new ConflictException("Bordro satırını yeniden okuyup sürümüyle gönderin.");
        if (expected != actual) throw new ConflictException("Bordro satırı veya hesap dayanağı değişti. Güncel satırı okuyup yeniden kontrol edin.");
    }
    public static async Task RefreshAsync(IApplicationDbContext db, PayrollEmployee row, CancellationToken ct)
    {
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(db, row.Period!, [row], ct);
        await PayrollCumulativeTaxBase.RefreshAsync(db, [row], row.Period!.Year, row.Period.Month, ct);
    }
}
