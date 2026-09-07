using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Settings;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Setup;

// Bu durum yalnız başlangıç kayıtlarını ölçer; mevzuat/pilot uygunluk belgesi değildir.
public sealed record TenantSetupStatusDto(
    int TemplateYear,
    bool CompanyProfileExists,
    int MissingLeaveTypeCount,
    int MissingFullDayHolidayCount,
    bool PayrollSettingsExists,
    bool HasEffectiveApprovedPayrollSettings)
{
    public bool NeedsInitialization => !CompanyProfileExists || MissingLeaveTypeCount > 0
        || MissingFullDayHolidayCount > 0 || !PayrollSettingsExists;
}

public sealed record GetTenantSetupStatusQuery : IRequest<TenantSetupStatusDto>;
public sealed record InitializeTenantSetupCommand : IRequest<TenantSetupStatusDto>;

public sealed class GetTenantSetupStatusQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTenantSetupStatusQuery, TenantSetupStatusDto>
{
    public Task<TenantSetupStatusDto> Handle(GetTenantSetupStatusQuery request, CancellationToken cancellationToken)
        => ReadAsync(context, cancellationToken);

    internal static async Task<TenantSetupStatusDto> ReadAsync(IApplicationDbContext context, CancellationToken ct)
    {
        var codes = await context.LeaveTypes.AsNoTracking().Select(x => x.Code).ToListAsync(ct);
        var dates = await context.Holidays.AsNoTracking().Select(x => x.Date).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        return new TenantSetupStatusDto(
            TenantBaseline2026.Year,
            await context.CompanyProfiles.AnyAsync(ct),
            TenantBaseline2026.CreateLeaveTypes().Count(x => !codes.Contains(x.Code, StringComparer.OrdinalIgnoreCase)),
            TenantBaseline2026.CreateFullDayHolidays().Count(x => !dates.Contains(x.Date)),
            await context.PayrollSettings.AnyAsync(ct),
            await context.PayrollSettings.AnyAsync(x => x.Status == PayrollSettingsStatus.Approved
                && x.EffectiveFrom <= today, ct));
    }
}

public sealed class InitializeTenantSetupCommandHandler(
    IApplicationDbContext context,
    IPlatformDbContext platform,
    ICurrentTenant currentTenant,
    IDatabaseLock databaseLock)
    : IRequestHandler<InitializeTenantSetupCommand, TenantSetupStatusDto>
{
    public Task<TenantSetupStatusDto> Handle(InitializeTenantSetupCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("tenant-setup", async ct =>
        {
            // Platform context'inde global kiracı filtresi yoktur; kimlik açıkça süzülür.
            var tenantId = currentTenant.TenantIdOrThrow();
            if (!await context.CompanyProfiles.AnyAsync(ct))
            {
                var name = await platform.Tenants.Where(x => x.Id == tenantId).Select(x => x.Name).SingleAsync(ct);
                context.CompanyProfiles.Add(new CompanyProfile { Name = name });
            }

            var codes = await context.LeaveTypes.Select(x => x.Code).ToListAsync(ct);
            context.LeaveTypes.AddRange(TenantBaseline2026.CreateLeaveTypes()
                .Where(x => !codes.Contains(x.Code, StringComparer.OrdinalIgnoreCase)));

            var dates = await context.Holidays.Select(x => x.Date).ToListAsync(ct);
            context.Holidays.AddRange(TenantBaseline2026.CreateFullDayHolidays()
                .Where(x => !dates.Contains(x.Date)));

            // Mevcut taslak veya onaylı setin üzerine yazma; uzman onayını taklit etme.
            if (!await context.PayrollSettings.AnyAsync(ct))
                context.PayrollSettings.Add(TenantBaseline2026.CreatePayrollDraft());

            await context.SaveChangesAsync(ct);
            return await GetTenantSetupStatusQueryHandler.ReadAsync(context, ct);
        }, cancellationToken);
}
