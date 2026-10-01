using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record CompensationDateCorrectionDto(
    DateOnly OldEffectiveFrom, DateOnly NewEffectiveFrom,
    DateOnly? OldPreviousEffectiveTo, DateOnly? NewPreviousEffectiveTo,
    int FromVersion, int ToVersion, string Reason, string ActorUserId,
    DateTime CorrectedAtUtc);
public sealed record CompensationDateCorrectionPreviewDto(
    EmployeeCompensationPackageDto Package, EmployeeCompensationPackageDto? PreviousPackage,
    DateOnly ProposedEffectiveFrom, DateOnly? ProposedPreviousEffectiveTo,
    bool CanApply, IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<CompensationDateCorrectionDto> History);

public sealed record PreviewCompensationDateCorrectionQuery(
    int EmployeeId, int PackageId, DateOnly ProposedEffectiveFrom)
    : IRequest<CompensationDateCorrectionPreviewDto>;

public sealed class PreviewCompensationDateCorrectionQueryHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewCompensationDateCorrectionQuery, CompensationDateCorrectionPreviewDto>
{
    public Task<CompensationDateCorrectionPreviewDto> Handle(
        PreviewCompensationDateCorrectionQuery request, CancellationToken cancellationToken)
        => CompensationDateCorrectionRules.PreviewAsync(context, request.EmployeeId,
            request.PackageId, request.ProposedEffectiveFrom, cancellationToken);
}

public sealed record CorrectEmployeeCompensationDateCommand(
    int EmployeeId, int PackageId, DateOnly ProposedEffectiveFrom,
    int ExpectedPackageVersion, int? ExpectedPreviousPackageId,
    int? ExpectedPreviousPackageVersion, string Reason, int? GuidanceContentRevisionId = null)
    : IRequest<EmployeeEmploymentFoundationDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "employee-compensation-date";
}

public sealed class CorrectEmployeeCompensationDateCommandValidator
    : AbstractValidator<CorrectEmployeeCompensationDateCommand>
{
    public CorrectEmployeeCompensationDateCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.PackageId).GreaterThan(0);
        RuleFor(item => item.ProposedEffectiveFrom).NotEmpty();
        RuleFor(item => item.ExpectedPackageVersion).GreaterThan(0);
        RuleFor(item => item.ExpectedPreviousPackageVersion).GreaterThan(0)
            .When(item => item.ExpectedPreviousPackageId is not null);
        RuleFor(item => item.ExpectedPreviousPackageId).GreaterThan(0)
            .When(item => item.ExpectedPreviousPackageVersion is not null);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CorrectEmployeeCompensationDateCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<CorrectEmployeeCompensationDateCommand, EmployeeEmploymentFoundationDto>
{
    public Task<EmployeeEmploymentFoundationDto> Handle(
        CorrectEmployeeCompensationDateCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"employee-compensation:{request.EmployeeId}",
            token => CorrectAsync(request, token), cancellationToken);

    private async Task<EmployeeEmploymentFoundationDto> CorrectAsync(
        CorrectEmployeeCompensationDateCommand request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        var previous = await CompensationDateCorrectionRules.PreviousAsync(
            context, package, cancellationToken);
        if (package.Version != request.ExpectedPackageVersion
            || previous?.Id != request.ExpectedPreviousPackageId
            || previous?.Version != request.ExpectedPreviousPackageVersion)
        {
            throw new ConflictException("Ücret paketi veya önceki paket değişti; tarih önizlemesini yenileyin.");
        }

        var preview = await CompensationDateCorrectionRules.PreviewAsync(context,
            request.EmployeeId, request.PackageId, request.ProposedEffectiveFrom,
            cancellationToken, package, previous);
        if (!preview.CanApply)
            throw new ConflictException(string.Join(" ", preview.BlockingReasons));

        var oldStart = package.EffectiveFrom;
        var oldVersion = package.Version;
        var oldPreviousEnd = previous?.EffectiveTo;
        package.EffectiveFrom = request.ProposedEffectiveFrom;
        package.Version++;
        if (previous is not null)
        {
            previous.EffectiveTo = request.ProposedEffectiveFrom.AddDays(-1);
            previous.Version++;
        }
        context.EmployeeCompensationDateCorrections.Add(new EmployeeCompensationDateCorrection
        {
            CompensationPackageId = package.Id,
            PreviousPackageId = previous?.Id,
            OldEffectiveFrom = oldStart,
            NewEffectiveFrom = request.ProposedEffectiveFrom,
            OldPreviousEffectiveTo = oldPreviousEnd,
            NewPreviousEffectiveTo = previous?.EffectiveTo,
            FromVersion = oldVersion,
            ToVersion = package.Version,
            Reason = request.Reason.Trim(),
            ActorUserId = currentUser.UserId
                ?? throw new InvalidOperationException("Tarih düzeltmesi için oturum sahibi gerekli."),
            CorrectedAtUtc = DateTime.UtcNow,
        });
        try
        {
            PersonnelEmploymentEvidence.Add(context, currentUser, guidance, request.EmployeeId, request.GuidanceOperationKey,
                previous is null ? ["Ücret / Yürürlük başlangıcı"] : ["Ücret / Yürürlük başlangıcı", "Ücret / Önceki paket bitişi"],
                package.EmploymentPeriodId, package.Id, request.Reason, previous?.Id, oldVersion, package.Version);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Ücret paketi başka bir işlemde değişti; tarih önizlemesini yenileyin.");
        }
        var employee = await context.Employees.AsNoTracking()
            .SingleAsync(item => item.Id == request.EmployeeId, cancellationToken);
        return await GetEmployeeEmploymentFoundationQueryHandler.BuildAsync(
            context, employee, cancellationToken);
    }
}

internal static class CompensationDateCorrectionRules
{
    public static Task<EmployeeCompensationPackage?> PreviousAsync(
        IApplicationDbContext context, EmployeeCompensationPackage package,
        CancellationToken cancellationToken)
        => context.EmployeeCompensationPackages
            .Where(item => item.EmploymentPeriodId == package.EmploymentPeriodId
                && item.EffectiveFrom < package.EffectiveFrom)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<CompensationDateCorrectionPreviewDto> PreviewAsync(
        IApplicationDbContext context, int employeeId, int packageId,
        DateOnly proposed, CancellationToken cancellationToken,
        EmployeeCompensationPackage? loadedPackage = null,
        EmployeeCompensationPackage? loadedPrevious = null)
    {
        var package = loadedPackage ?? await CompensationPackageCorrectionRules.LoadAsync(
            context, employeeId, packageId, cancellationToken);
        var previous = loadedPrevious ?? await PreviousAsync(context, package, cancellationToken);
        var reasons = new List<string>();
        if (proposed == default || proposed == package.EffectiveFrom)
            reasons.Add("Farklı ve geçerli bir yürürlük tarihi seçin.");
        if (package.EmploymentPeriod?.EndDate is not null || package.EffectiveTo is not null)
            reasons.Add("Tarih düzeltmesi yalnız açık çalışma döneminin son paketinde yapılabilir.");
        if (await context.EmployeeCompensationPackages.AsNoTracking().AnyAsync(item =>
                item.EmploymentPeriodId == package.EmploymentPeriodId
                && item.EffectiveFrom > package.EffectiveFrom, cancellationToken))
            reasons.Add("Bu paket son paket değil; önce tarih sırasını kontrol edin.");
        if (package.EmploymentPeriod is not null && proposed < package.EmploymentPeriod.StartDate)
            reasons.Add("Önerilen tarih çalışma dönemi başlangıcından önce olamaz.");
        if (previous is not null && proposed <= previous.EffectiveFrom)
            reasons.Add("Önerilen tarih önceki paketin başlangıcından sonra olmalıdır.");
        if (previous is not null && previous.EffectiveTo != package.EffectiveFrom.AddDays(-1))
            reasons.Add("Paketlerin mevcut yürürlük sınırları tutarsız; uzman incelemesi gerekir.");

        var months = await context.PayrollEmployees.AsNoTracking()
            .Where(row => row.EmployeeId == employeeId)
            .Select(row => new { row.Period!.Year, row.Period.Month })
            .ToListAsync(cancellationToken);
        if (months.Any(month =>
            new DateOnly(month.Year, month.Month, 1) >= package.EffectiveFrom
            && (package.EffectiveTo is null
                || new DateOnly(month.Year, month.Month, 1) <= package.EffectiveTo)))
            reasons.Add("Son paket bordro satırında kullanılmış olabilir; yürürlük tarihi doğrudan düzeltilemez.");
        if (await context.EmployeeOffboardingCases.AsNoTracking().AnyAsync(item =>
                item.EmployeeId == employeeId && item.CompensationPackageId == package.Id,
                cancellationToken))
            reasons.Add("Son paket ayrılış vakasında kullanılmış; tarih doğrudan düzeltilemez.");
        if (await context.EmployeeCompensationRemediationCases.AsNoTracking().AnyAsync(item =>
                item.CompensationPackageId == package.Id
                || (previous != null && item.CompensationPackageId == previous.Id),
                cancellationToken))
            reasons.Add("Bu aralıkta telafi inceleme dosyası var; uzman süreciyle ilerleyin.");

        if (proposed != default && proposed != package.EffectiveFrom)
        {
            var affectedStart = proposed < package.EffectiveFrom ? proposed : package.EffectiveFrom;
            var affectedEnd = (proposed > package.EffectiveFrom
                ? proposed : package.EffectiveFrom).AddDays(-1);
            if (months.Any(month =>
                new DateOnly(month.Year, month.Month, 1) >= affectedStart
                && new DateOnly(month.Year, month.Month, 1) <= affectedEnd))
                reasons.Add("Değişecek yürürlük aralığında bordro satırı var; tarih doğrudan düzeltilemez.");
            if (await context.EmployeeOffboardingCases.AsNoTracking().AnyAsync(item =>
                    item.EmployeeId == employeeId && item.TerminationDate >= affectedStart
                    && item.TerminationDate <= affectedEnd, cancellationToken))
                reasons.Add("Değişecek yürürlük aralığında ayrılış vakası var; tarih doğrudan düzeltilemez.");
        }

        var history = await context.EmployeeCompensationDateCorrections.AsNoTracking()
            .Where(item => item.CompensationPackageId == package.Id)
            .OrderByDescending(item => item.ToVersion)
            .Select(item => new CompensationDateCorrectionDto(
                item.OldEffectiveFrom, item.NewEffectiveFrom,
                item.OldPreviousEffectiveTo, item.NewPreviousEffectiveTo,
                item.FromVersion, item.ToVersion, item.Reason,
                item.ActorUserId, item.CorrectedAtUtc))
            .ToListAsync(cancellationToken);
        return new(package.ToDto(), previous?.ToDto(), proposed,
            previous is null || proposed == default ? null : proposed.AddDays(-1),
            reasons.Count == 0, reasons, history);
    }
}
