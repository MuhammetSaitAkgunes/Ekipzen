using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record CompensationPayrollImpactDto(
    int RowId, int PeriodId, int Year, int Month, string PeriodStatus,
    string RowStatus, decimal RecordedGrossSalary);
public sealed record CompensationOffboardingImpactDto(
    int CaseId, string Status, DateOnly TerminationDate, decimal GrossSalarySnapshot);
public sealed record CompensationRemediationImpactDto(
    IReadOnlyList<CompensationPayrollImpactDto> PotentialPayrollRows,
    IReadOnlyList<CompensationOffboardingImpactDto> OffboardingCases);
public sealed record CompensationRemediationCaseDto(
    int Id, int Version, string Status, int PackageVersion, string IssueDescription,
    DateOnly? ProposedEffectiveFrom, CompensationPackageValuesDto ProposedValues,
    CompensationRemediationImpactDto ImpactSnapshot, string OpenedByUserId,
    DateTime OpenedAtUtc, string? ReviewedByUserId, DateTime? ReviewedAtUtc,
    string? ExpertReviewReference, string? ReviewNotes);
public sealed record CompensationRemediationContextDto(
    EmployeeCompensationPackageDto Package, CompensationRemediationImpactDto CurrentImpact,
    bool CanOpen, CompensationRemediationCaseDto? Case,
    IReadOnlyList<CompensationRemediationCaseDto> History);

public sealed record GetCompensationRemediationContextQuery(int EmployeeId, int PackageId)
    : IRequest<CompensationRemediationContextDto>;

public sealed class GetCompensationRemediationContextQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCompensationRemediationContextQuery, CompensationRemediationContextDto>
{
    public async Task<CompensationRemediationContextDto> Handle(
        GetCompensationRemediationContextQuery request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        var impact = await CompensationRemediationRules.ImpactAsync(
            context, request.EmployeeId, package, cancellationToken);
        var cases = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .Where(candidate => candidate.CompensationPackageId == package.Id)
            .OrderByDescending(candidate => candidate.Id)
            .ToListAsync(cancellationToken);
        var item = cases.FirstOrDefault();
        return new(package.ToDto(), impact,
            (item?.Status is null or "approved") && CompensationRemediationRules.HasImpact(impact),
            item?.ToDto(), cases.Select(candidate => candidate.ToDto()).ToList());
    }
}

public sealed record SaveCompensationRemediationCaseCommand(
    int EmployeeId, int PackageId, int? CaseId, int ExpectedPackageVersion,
    int? ExpectedCaseVersion, string IssueDescription, DateOnly? ProposedEffectiveFrom,
    decimal MonthlyGrossSalary, decimal RegularPremium, decimal RoadAllowance,
    decimal MealAllowance, decimal OtherRegularBenefits, string SourceReference)
    : IRequest<CompensationRemediationCaseDto>;

public sealed class SaveCompensationRemediationCaseCommandValidator
    : AbstractValidator<SaveCompensationRemediationCaseCommand>
{
    public SaveCompensationRemediationCaseCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.PackageId).GreaterThan(0);
        RuleFor(item => item.ExpectedPackageVersion).GreaterThan(0);
        RuleFor(item => item.IssueDescription).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.MonthlyGrossSalary).GreaterThan(0m);
        RuleFor(item => item.RegularPremium).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.RoadAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.MealAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.OtherRegularBenefits).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.SourceReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ExpectedCaseVersion).GreaterThan(0).When(item => item.CaseId is not null);
    }
}

public sealed class SaveCompensationRemediationCaseCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<SaveCompensationRemediationCaseCommand, CompensationRemediationCaseDto>
{
    public Task<CompensationRemediationCaseDto> Handle(
        SaveCompensationRemediationCaseCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"employee-compensation:{request.EmployeeId}",
            token => SaveAsync(request, token), cancellationToken);

    private async Task<CompensationRemediationCaseDto> SaveAsync(
        SaveCompensationRemediationCaseCommand request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        if (package.Version != request.ExpectedPackageVersion)
            throw new ConflictException("Ücret paketi değişti; inceleme dosyasını güncel kayıttan açın.");

        var impact = await CompensationRemediationRules.ImpactAsync(
            context, request.EmployeeId, package, cancellationToken);
        if (!CompensationRemediationRules.HasImpact(impact))
            throw new ConflictException("Bu paket için bordro veya ayrılış etkisi bulunmadı; doğrudan düzeltme durumunu kontrol edin.");

        var proposed = new CompensationPackageValuesDto(
            request.MonthlyGrossSalary, request.RegularPremium, request.RoadAllowance,
            request.MealAllowance, request.OtherRegularBenefits, request.SourceReference.Trim());
        var existing = await context.EmployeeCompensationRemediationCases
            .Where(item => item.CompensationPackageId == package.Id)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        EmployeeCompensationRemediationCase item;
        if (request.CaseId is null)
        {
            if (existing?.Status == "draft")
                throw new ConflictException("Bu ücret paketi için açık inceleme taslağı zaten var.");
            item = new EmployeeCompensationRemediationCase
            {
                EmployeeId = request.EmployeeId,
                CompensationPackageId = package.Id,
                OpenedByUserId = currentUser.UserId
                    ?? throw new InvalidOperationException("İnceleme dosyası için oturum sahibi gerekli."),
                OpenedAtUtc = DateTime.UtcNow,
            };
            context.EmployeeCompensationRemediationCases.Add(item);
        }
        else
        {
            if (existing is null || existing.Id != request.CaseId)
                throw new NotFoundException("Ücret telafi incelemesi", request.CaseId.Value);
            if (existing.Status != "draft")
                throw new ConflictException("Onaylanan inceleme dosyası değiştirilemez.");
            if (existing.Version != request.ExpectedCaseVersion)
                throw new ConflictException("İnceleme dosyası değişti; güncel halini açın.");
            item = existing;
            item.Version++;
        }

        item.PackageVersion = package.Version;
        item.IssueDescription = request.IssueDescription.Trim();
        item.ProposedEffectiveFrom = request.ProposedEffectiveFrom;
        item.ProposedValuesJson = JsonSerializer.Serialize(proposed);
        item.ImpactSnapshotJson = JsonSerializer.Serialize(impact);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İnceleme dosyası başka bir işlemde değişti; yeniden açın.");
        }
        return item.ToDto();
    }
}

public sealed record ApproveCompensationRemediationCaseCommand(
    int EmployeeId, int PackageId, int CaseId, int ExpectedCaseVersion,
    string ExpertReviewReference, string ReviewNotes, bool ConfirmExpertReview)
    : IRequest<CompensationRemediationCaseDto>;

public sealed class ApproveCompensationRemediationCaseCommandValidator
    : AbstractValidator<ApproveCompensationRemediationCaseCommand>
{
    public ApproveCompensationRemediationCaseCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.PackageId).GreaterThan(0);
        RuleFor(item => item.CaseId).GreaterThan(0);
        RuleFor(item => item.ExpectedCaseVersion).GreaterThan(0);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ReviewNotes).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.ConfirmExpertReview).Equal(true);
    }
}

public sealed class ApproveCompensationRemediationCaseCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<ApproveCompensationRemediationCaseCommand, CompensationRemediationCaseDto>
{
    public Task<CompensationRemediationCaseDto> Handle(
        ApproveCompensationRemediationCaseCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"employee-compensation:{request.EmployeeId}",
            token => ApproveAsync(request, token), cancellationToken);

    private async Task<CompensationRemediationCaseDto> ApproveAsync(
        ApproveCompensationRemediationCaseCommand request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        var item = await context.EmployeeCompensationRemediationCases
            .SingleOrDefaultAsync(candidate => candidate.Id == request.CaseId
                && candidate.EmployeeId == request.EmployeeId
                && candidate.CompensationPackageId == package.Id, cancellationToken)
            ?? throw new NotFoundException("Ücret telafi incelemesi", request.CaseId);
        if (item.Status != "draft")
            throw new ConflictException("İnceleme dosyası zaten onaylanmış.");
        if (item.Version != request.ExpectedCaseVersion)
            throw new ConflictException("İnceleme dosyası değişti; güncel halini açın.");
        if (package.Version != item.PackageVersion)
            throw new ConflictException("Ücret paketi değişti; inceleme dosyasını güncelleyin.");
        var currentImpact = await CompensationRemediationRules.ImpactAsync(
            context, request.EmployeeId, package, cancellationToken);
        if (JsonSerializer.Serialize(currentImpact) != item.ImpactSnapshotJson)
            throw new ConflictException("Etkilenen bordro veya ayrılış kayıtları değişti; taslağı güncelleyin.");

        item.Status = "approved";
        item.Version++;
        item.ExpertReviewReference = request.ExpertReviewReference.Trim();
        item.ReviewNotes = request.ReviewNotes.Trim();
        item.ReviewedByUserId = currentUser.UserId
            ?? throw new InvalidOperationException("İnceleme onayı için oturum sahibi gerekli.");
        item.ReviewedAtUtc = DateTime.UtcNow;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İnceleme dosyası başka bir işlemde onaylandı; yeniden açın.");
        }
        return item.ToDto();
    }
}

internal static class CompensationRemediationRules
{
    public static bool HasImpact(CompensationRemediationImpactDto impact)
        => impact.PotentialPayrollRows.Count > 0 || impact.OffboardingCases.Count > 0;

    public static async Task<CompensationRemediationImpactDto> ImpactAsync(
        IApplicationDbContext context, int employeeId, EmployeeCompensationPackage package,
        CancellationToken cancellationToken)
    {
        // Bordro satırı kaynak paket ID'sini saklamaz; tarih aralığı yalnız olası ilişkiyi gösterir.
        var payroll = await context.PayrollEmployees.AsNoTracking()
            .Where(row => row.EmployeeId == employeeId)
            .Select(row => new { row.Id, row.PayrollPeriodId, row.Period!.Year,
                row.Period.Month, PeriodStatus = row.Period.Status,
                RowStatus = row.ApprovalStatus, row.GrossSalary })
            .ToListAsync(cancellationToken);
        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(context,
            payroll.Select(row => row.Id).ToArray(), cancellationToken);
        var potential = payroll.Where(row =>
                new DateOnly(row.Year, row.Month, 1) >= package.EffectiveFrom
                && (package.EffectiveTo is null
                    || new DateOnly(row.Year, row.Month, 1) <= package.EffectiveTo))
            .OrderBy(row => row.Year).ThenBy(row => row.Month).ThenBy(row => row.Id)
            .Select(row => new CompensationPayrollImpactDto(
                row.Id, row.PayrollPeriodId, row.Year, row.Month,
                row.PeriodStatus.ToString().ToLowerInvariant(),
                row.RowStatus.ToString().ToLowerInvariant(),
                revisions.TryGetValue(row.Id, out var revision)
                    ? ApprovedPayrollRevisionRules.EffectiveRow(revision).GrossSalary
                    : row.GrossSalary))
            .ToList();
        var offboarding = await context.EmployeeOffboardingCases.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.CompensationPackageId == package.Id)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Status, item.TerminationDate,
                item.MonthlyGrossSalarySnapshot })
            .ToListAsync(cancellationToken);
        return new(potential, offboarding.Select(item => new CompensationOffboardingImpactDto(
            item.Id, item.Status.ToString().ToLowerInvariant(), item.TerminationDate,
            item.MonthlyGrossSalarySnapshot)).ToList());
    }

    public static CompensationRemediationCaseDto ToDto(this EmployeeCompensationRemediationCase item)
        => new(item.Id, item.Version, item.Status, item.PackageVersion,
            item.IssueDescription, item.ProposedEffectiveFrom,
            JsonSerializer.Deserialize<CompensationPackageValuesDto>(item.ProposedValuesJson)!,
            JsonSerializer.Deserialize<CompensationRemediationImpactDto>(item.ImpactSnapshotJson)!,
            item.OpenedByUserId, item.OpenedAtUtc, item.ReviewedByUserId, item.ReviewedAtUtc,
            item.ExpertReviewReference, item.ReviewNotes);
}
