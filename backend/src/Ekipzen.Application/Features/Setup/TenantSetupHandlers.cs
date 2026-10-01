using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Settings;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Setup;

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
public sealed record InitializeTenantSetupCommand(int? GuidanceContentRevisionId = null, string? ExpectedPreviewToken = null) : IRequest<TenantSetupStatusDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "tenant-setup"; }

public sealed class InitializeTenantSetupCommandValidator : AbstractValidator<InitializeTenantSetupCommand>
{ public InitializeTenantSetupCommandValidator() => RuleFor(x => x.ExpectedPreviewToken).NotEmpty().MaximumLength(64); }

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
    IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<InitializeTenantSetupCommand, TenantSetupStatusDto>
{
    public Task<TenantSetupStatusDto> Handle(InitializeTenantSetupCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("tenant-setup", async ct =>
        {
            var preview = await GetTenantSetupPreviewQueryHandler.ReadAsync(context, platform, currentTenant, ct);
            if (!preview.Status.NeedsInitialization) return preview.Status;
            if (request.ExpectedPreviewToken != preview.Token) throw new ConflictException("Başlangıç kayıtları değişti. Önizlemeyi yeniden okuyup kontrol edin.");
            // Platform context'inde global kiracı filtresi yoktur; kimlik açıkça süzülür.
            var tenantId = currentTenant.TenantIdOrThrow();
            var addedCompany = !await context.CompanyProfiles.AnyAsync(ct);
            if (addedCompany)
            {
                var name = await platform.Tenants.Where(x => x.Id == tenantId).Select(x => x.Name).SingleAsync(ct);
                context.CompanyProfiles.Add(new CompanyProfile { Name = name });
            }

            var codes = await context.LeaveTypes.Select(x => x.Code).ToListAsync(ct);
            var addedLeaves = TenantBaseline2026.CreateLeaveTypes().Where(x => !codes.Contains(x.Code, StringComparer.OrdinalIgnoreCase)).ToList();
            context.LeaveTypes.AddRange(addedLeaves);

            var dates = await context.Holidays.Select(x => x.Date).ToListAsync(ct);
            var addedHolidays = TenantBaseline2026.CreateFullDayHolidays().Where(x => !dates.Contains(x.Date)).ToList();
            context.Holidays.AddRange(addedHolidays);

            // Mevcut taslak veya onaylı setin üzerine yazma; uzman onayını taklit etme.
            var addedPayroll = !await context.PayrollSettings.AnyAsync(ct);
            if (addedPayroll)
                context.PayrollSettings.Add(TenantBaseline2026.CreatePayrollDraft());

            if (guidance.IsGuided && (addedCompany || addedPayroll || addedLeaves.Count > 0 || addedHolidays.Count > 0))
            {
                var evidence = new GuidanceSetupEvidence { UserId = PersonnelEntryGuide.RequireUserId(user),
                    TemplateYear = TenantBaseline2026.Year, AddedCompanyProfile = addedCompany, AddedPayrollSettings = addedPayroll,
                    AddedLeaveTypeCount = addedLeaves.Count, AddedHolidayCount = addedHolidays.Count };
                guidance.Stamp(evidence);
                context.GuidanceSetupEvidences.Add(evidence);
            }
            await context.SaveChangesAsync(ct);
            return await GetTenantSetupStatusQueryHandler.ReadAsync(context, ct);
        }, cancellationToken);
}
