using System.Security.Cryptography;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Setup;

public sealed record SetupLeaveTypeDto(string Code, string Name, bool DeductsFromAnnualBalance, bool RequiresApproval);
public sealed record SetupHolidayDto(DateOnly Date, string Name);
public sealed record TenantSetupPreviewDto(TenantSetupStatusDto Status, string? CompanyName,
    IReadOnlyList<SetupLeaveTypeDto> LeaveTypes, IReadOnlyList<SetupHolidayDto> Holidays,
    PayrollSettingsDto? PayrollDraft, string Token);
public sealed record GetTenantSetupPreviewQuery : IRequest<TenantSetupPreviewDto>;
public sealed class GetTenantSetupPreviewQueryHandler(IApplicationDbContext db, IPlatformDbContext platform, ICurrentTenant tenant)
    : IRequestHandler<GetTenantSetupPreviewQuery, TenantSetupPreviewDto>
{
    public Task<TenantSetupPreviewDto> Handle(GetTenantSetupPreviewQuery request, CancellationToken ct) => ReadAsync(db, platform, tenant, ct);
    internal static async Task<TenantSetupPreviewDto> ReadAsync(IApplicationDbContext db, IPlatformDbContext platform, ICurrentTenant tenant, CancellationToken ct)
    {
        var status = await GetTenantSetupStatusQueryHandler.ReadAsync(db, ct);
        var codes = await db.LeaveTypes.AsNoTracking().Select(x => x.Code).ToListAsync(ct);
        var dates = await db.Holidays.AsNoTracking().Select(x => x.Date).ToListAsync(ct);
        var leaves = TenantBaseline2026.CreateLeaveTypes().Where(x => !codes.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
            .Select(x => new SetupLeaveTypeDto(x.Code!, x.Name, x.DeductsFromAnnualBalance, x.RequiresApproval)).ToArray();
        var holidays = TenantBaseline2026.CreateFullDayHolidays().Where(x => !dates.Contains(x.Date))
            .Select(x => new SetupHolidayDto(x.Date, x.Name)).ToArray();
        var tenantId = tenant.TenantIdOrThrow();
        var name = status.CompanyProfileExists ? null : await platform.Tenants.Where(x => x.Id == tenantId).Select(x => x.Name).SingleAsync(ct);
        var draft = status.PayrollSettingsExists ? null : PayrollSettingsResolver.ToDto(TenantBaseline2026.CreatePayrollDraft());
        var token = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { tenantId, status, name, leaves, holidays, draft })));
        return new TenantSetupPreviewDto(status, name, leaves, holidays, draft, token);
    }
}
