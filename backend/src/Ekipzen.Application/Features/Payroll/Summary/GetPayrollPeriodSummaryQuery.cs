using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
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

        return await context.PayrollPeriodSummaries
            .FirstOrDefaultAsync(s => s.PayrollPeriodId == request.PeriodId, cancellationToken)
            ?? new PayrollPeriodSummary { PayrollPeriodId = request.PeriodId };
    }
}
