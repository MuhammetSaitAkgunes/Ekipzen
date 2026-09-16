using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Settings;

/// <summary>Açık mevzuat taslaklarını sayfa yenilemesi ve kesintilerden sonra geri getirir.</summary>
public sealed record GetPayrollSettingsDraftsQuery
    : IRequest<IReadOnlyList<PayrollSettingsDto>>;

public sealed class GetPayrollSettingsDraftsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollSettingsDraftsQuery, IReadOnlyList<PayrollSettingsDto>>
{
    public async Task<IReadOnlyList<PayrollSettingsDto>> Handle(
        GetPayrollSettingsDraftsQuery request,
        CancellationToken cancellationToken)
    {
        var drafts = await context.PayrollSettings
            .AsNoTracking()
            .Include(settings => settings.TaxBrackets)
            .Where(settings => settings.Status == PayrollSettingsStatus.Draft)
            .OrderByDescending(settings => settings.EffectiveFrom)
            .ThenByDescending(settings => settings.Version)
            .ToListAsync(cancellationToken);

        return drafts.Select(PayrollSettingsResolver.ToDto).ToList();
    }
}
