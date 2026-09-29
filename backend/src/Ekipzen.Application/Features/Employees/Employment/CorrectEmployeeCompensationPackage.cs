using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record CompensationPackageValuesDto(
    decimal MonthlyGrossSalary,
    decimal RegularPremium,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal OtherRegularBenefits,
    string SourceReference);

public sealed record CompensationPackageCorrectionDto(
    int FromVersion,
    int ToVersion,
    CompensationPackageValuesDto Before,
    CompensationPackageValuesDto After,
    string Reason,
    string ActorUserId,
    DateTime CorrectedAtUtc);

public sealed record CompensationPackageCorrectionContextDto(
    EmployeeCompensationPackageDto Package,
    bool CanCorrect,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<CompensationPackageCorrectionDto> History);

public sealed record GetCompensationPackageCorrectionContextQuery(int EmployeeId, int PackageId)
    : IRequest<CompensationPackageCorrectionContextDto>;

public sealed class GetCompensationPackageCorrectionContextQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCompensationPackageCorrectionContextQuery, CompensationPackageCorrectionContextDto>
{
    public async Task<CompensationPackageCorrectionContextDto> Handle(
        GetCompensationPackageCorrectionContextQuery request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        var reasons = await CompensationPackageCorrectionRules.BlockingReasonsAsync(
            context, request.EmployeeId, package, cancellationToken);
        var corrections = await context.EmployeeCompensationPackageCorrections.AsNoTracking()
            .Where(item => item.CompensationPackageId == package.Id)
            .OrderByDescending(item => item.ToVersion)
            .ToListAsync(cancellationToken);

        return new CompensationPackageCorrectionContextDto(
            package.ToDto(), reasons.Count == 0, reasons,
            corrections.Select(item => new CompensationPackageCorrectionDto(
                item.FromVersion, item.ToVersion,
                JsonSerializer.Deserialize<CompensationPackageValuesDto>(item.BeforeJson)!,
                JsonSerializer.Deserialize<CompensationPackageValuesDto>(item.AfterJson)!,
                item.Reason, item.ActorUserId, item.CorrectedAtUtc)).ToList());
    }
}

public sealed record CorrectEmployeeCompensationPackageCommand(
    int EmployeeId,
    int PackageId,
    int ExpectedVersion,
    decimal MonthlyGrossSalary,
    decimal RegularPremium,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal OtherRegularBenefits,
    string SourceReference,
    string Reason) : IRequest<EmployeeEmploymentFoundationDto>;

public sealed class CorrectEmployeeCompensationPackageCommandValidator
    : AbstractValidator<CorrectEmployeeCompensationPackageCommand>
{
    public CorrectEmployeeCompensationPackageCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.PackageId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
        RuleFor(item => item.MonthlyGrossSalary).GreaterThan(0m);
        RuleFor(item => item.RegularPremium).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.RoadAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.MealAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.OtherRegularBenefits).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.SourceReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CorrectEmployeeCompensationPackageCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<CorrectEmployeeCompensationPackageCommand, EmployeeEmploymentFoundationDto>
{
    public Task<EmployeeEmploymentFoundationDto> Handle(
        CorrectEmployeeCompensationPackageCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            $"employee-compensation:{request.EmployeeId}",
            token => CorrectAsync(request, token), cancellationToken);

    private async Task<EmployeeEmploymentFoundationDto> CorrectAsync(
        CorrectEmployeeCompensationPackageCommand request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        if (package.Version != request.ExpectedVersion)
        {
            throw new ConflictException("Ücret paketi bu sırada değişti; güncel kaydı yeniden açın.");
        }

        var reasons = await CompensationPackageCorrectionRules.BlockingReasonsAsync(
            context, request.EmployeeId, package, cancellationToken);
        if (reasons.Count > 0)
        {
            throw new ConflictException(string.Join(" ", reasons));
        }

        var before = package.ToValuesDto();
        var after = new CompensationPackageValuesDto(
            request.MonthlyGrossSalary, request.RegularPremium, request.RoadAllowance,
            request.MealAllowance, request.OtherRegularBenefits, request.SourceReference.Trim());
        if (before == after)
        {
            throw new ConflictException("Ücret paketinde değişiklik yok.");
        }

        var actorId = currentUser.UserId
            ?? throw new InvalidOperationException("Ücret düzeltmesi için oturum sahibi gerekli.");
        var oldVersion = package.Version;
        package.MonthlyGrossSalary = after.MonthlyGrossSalary;
        package.RegularPremium = after.RegularPremium;
        package.RoadAllowance = after.RoadAllowance;
        package.MealAllowance = after.MealAllowance;
        package.OtherRegularBenefits = after.OtherRegularBenefits;
        package.SourceReference = after.SourceReference;
        package.Version++;
        context.EmployeeCompensationPackageCorrections.Add(new EmployeeCompensationPackageCorrection
        {
            CompensationPackageId = package.Id,
            FromVersion = oldVersion,
            ToVersion = package.Version,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            Reason = request.Reason.Trim(),
            ActorUserId = actorId,
            CorrectedAtUtc = DateTime.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
        var employee = await context.Employees.AsNoTracking()
            .SingleAsync(item => item.Id == request.EmployeeId, cancellationToken);
        return await GetEmployeeEmploymentFoundationQueryHandler.BuildAsync(context, employee, cancellationToken);
    }
}

internal static class CompensationPackageCorrectionRules
{
    public static async Task<EmployeeCompensationPackage> LoadAsync(
        IApplicationDbContext context, int employeeId, int packageId, CancellationToken cancellationToken)
    {
        if (!await context.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken))
        {
            throw new NotFoundException("Personel", employeeId);
        }

        return await context.EmployeeCompensationPackages
            .Include(item => item.EmploymentPeriod)
            .SingleOrDefaultAsync(item => item.Id == packageId
                && item.EmploymentPeriod!.EmployeeId == employeeId, cancellationToken)
            ?? throw new NotFoundException("Ücret paketi", packageId);
    }

    public static async Task<List<string>> BlockingReasonsAsync(
        IApplicationDbContext context, int employeeId,
        EmployeeCompensationPackage package, CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        if (package.EmploymentPeriod?.EndDate is not null)
        {
            reasons.Add("Ücret paketi kapalı istihdam dönemine ait; doğrudan düzeltilemez.");
        }

        var payrollMonths = await context.PayrollEmployees.AsNoTracking()
            .Where(row => row.EmployeeId == employeeId)
            .Select(row => new { row.Period!.Year, row.Period.Month })
            .ToListAsync(cancellationToken);
        if (payrollMonths.Any(month =>
            new DateOnly(month.Year, month.Month, 1) >= package.EffectiveFrom
            && (package.EffectiveTo is null
                || new DateOnly(month.Year, month.Month, 1) <= package.EffectiveTo)))
        {
            reasons.Add("Bu yürürlük aralığında çalışanın bordro satırı var; paket doğrudan düzeltilemez.");
        }

        if (await context.EmployeeOffboardingCases.AsNoTracking().AnyAsync(item =>
                item.EmployeeId == employeeId && item.CompensationPackageId == package.Id,
                cancellationToken))
        {
            reasons.Add("Ücret paketi işten ayrılış geçmişinde kullanılmış; doğrudan düzeltilemez.");
        }
        else if (await context.EmployeeOffboardingCases.AsNoTracking().AnyAsync(item =>
                item.EmployeeId == employeeId && item.Status == EmployeeOffboardingStatus.Draft
                && item.TerminationDate >= package.EffectiveFrom
                && (package.EffectiveTo == null || item.TerminationDate <= package.EffectiveTo),
                cancellationToken))
        {
            reasons.Add("Bu ücret aralığında açık işten ayrılış taslağı var; önce taslağı tamamlayın veya yeniden değerlendirin.");
        }

        return reasons;
    }

    public static CompensationPackageValuesDto ToValuesDto(this EmployeeCompensationPackage package)
        => new(package.MonthlyGrossSalary, package.RegularPremium, package.RoadAllowance,
            package.MealAllowance, package.OtherRegularBenefits, package.SourceReference);
}
