using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Domain.ReadModels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Summary;

/// <summary>Dönem özeti (SQL view): onaylı sonuç toplamları + onay ilerlemesi.</summary>
public sealed record GetPayrollPeriodSummaryQuery(int PeriodId) : IRequest<PayrollPeriodSummary>;

public sealed class GetPayrollPeriodSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollPeriodSummaryQuery, PayrollPeriodSummary>
{
    public async Task<PayrollPeriodSummary> Handle(
        GetPayrollPeriodSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!await context.PayrollPeriods.AnyAsync(p => p.Id == request.PeriodId, cancellationToken))
        {
            throw new NotFoundException("Bordro dönemi", request.PeriodId);
        }

        var summary = await context.PayrollPeriodSummaries
            .FirstOrDefaultAsync(s => s.PayrollPeriodId == request.PeriodId, cancellationToken)
            ?? new PayrollPeriodSummary { PayrollPeriodId = request.PeriodId };
        var rowIds = await context.PayrollEmployees.AsNoTracking()
            .Where(row => row.PayrollPeriodId == request.PeriodId)
            .Select(row => row.Id).ToArrayAsync(cancellationToken);
        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(
            context, rowIds, cancellationToken);
        foreach (var revision in revisions.Values)
        {
            var values = ApprovedPayrollRevisionRules.ToDto(revision);
            summary.TotalGross += values.After.GrossEarnings - values.Before.GrossEarnings;
            summary.TotalNet += values.After.NetPay - values.Before.NetPay;
            summary.TotalDeductions += values.After.TotalDeductions - values.Before.TotalDeductions;
            summary.TotalEmployerCost += values.After.EmployerCost - values.Before.EmployerCost;
        }
        return summary;
    }
}
