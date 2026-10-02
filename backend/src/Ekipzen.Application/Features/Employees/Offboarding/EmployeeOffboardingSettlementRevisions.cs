using Ekipzen.Application.Features.Guidance;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Offboarding;

public sealed record OffboardingSettlementValuesDto(
    decimal MonthlyGrossSalarySnapshot, decimal RegularPremiumSnapshot,
    decimal RoadAllowanceSnapshot, decimal MealAllowanceSnapshot,
    decimal OtherRegularBenefitsSnapshot, decimal UnusedLeaveDays,
    decimal UnusedLeavePayment, decimal SeverancePayment, decimal NoticePayment,
    decimal TotalPayment, string SettlementSource, string SettlementReference,
    string? SettlementNotes);

public sealed record OffboardingSettlementRevisionDto(
    int Id, int RevisionNumber, int Version, string Status, int RemediationCaseId,
    int? BasedOnApprovedRevisionId, OffboardingSettlementValuesDto Values,
    string Reason, string OpenedByUserId, DateTime OpenedAtUtc,
    string? ApprovedByUserId, DateTime? ApprovedAtUtc,
    string? ExpertReviewReference, string? ReviewNotes);

public sealed record OffboardingSettlementRevisionContextDto(
    int OffboardingCaseId, int OffboardingCaseVersion, int RemediationCaseId,
    OffboardingSettlementValuesDto Original, OffboardingSettlementValuesDto Effective,
    OffboardingSettlementRevisionDto? Draft,
    IReadOnlyList<OffboardingSettlementRevisionDto> History,
    bool CanOpen, IReadOnlyList<string> BlockingReasons);

public sealed record GetOffboardingSettlementRevisionContextQuery(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId)
    : IRequest<OffboardingSettlementRevisionContextDto>;

public sealed class GetOffboardingSettlementRevisionContextQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOffboardingSettlementRevisionContextQuery,
        OffboardingSettlementRevisionContextDto>
{
    public async Task<OffboardingSettlementRevisionContextDto> Handle(
        GetOffboardingSettlementRevisionContextQuery request, CancellationToken cancellationToken)
        => (await OffboardingSettlementRevisionRules.LoadAsync(context, request.EmployeeId,
            request.PackageId, request.RemediationCaseId, request.OffboardingCaseId,
            cancellationToken)).ToDto();
}

public sealed record OpenOffboardingSettlementRevisionCommand(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId,
    int ExpectedOffboardingVersion, int? GuidanceContentRevisionId = null)
    : IRequest<OffboardingSettlementRevisionDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "offboarding-revision-open"; }

public sealed class OpenOffboardingSettlementRevisionCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<OpenOffboardingSettlementRevisionCommand, OffboardingSettlementRevisionDto>
{
    public Task<OffboardingSettlementRevisionDto> Handle(
        OpenOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync([
                $"employee-compensation:{request.EmployeeId}",
                $"employee-offboarding:{request.EmployeeId}"],
            token => OpenAsync(request, token), cancellationToken);

    private async Task<OffboardingSettlementRevisionDto> OpenAsync(
        OpenOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
    {
        var state = await OffboardingSettlementRevisionRules.LoadAsync(context,
            request.EmployeeId, request.PackageId, request.RemediationCaseId,
            request.OffboardingCaseId, cancellationToken);
        state.EnsureEligible();
        if (state.Case.Version != request.ExpectedOffboardingVersion)
            throw new ConflictException("Ayrılış dosyası değişti; güncel kaydı yeniden açın.");
        if (state.Draft is not null)
            throw new ConflictException("Bu ayrılış hesabının açık parasal düzeltme taslağı zaten var.");

        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            state.Remediation.ProposedValuesJson)!;
        var effective = state.Current is null
            ? OffboardingSettlementRevisionRules.Values(state.Case)
            : OffboardingSettlementRevisionRules.Values(state.Current);
        var revision = new EmployeeOffboardingSettlementRevision
        {
            OffboardingCaseId = state.Case.Id,
            RemediationCaseId = state.Remediation.Id,
            RevisionNumber = state.Revisions.Count == 0
                ? 1 : state.Revisions.Max(item => item.RevisionNumber) + 1,
            BasedOnApprovedRevisionId = state.Current?.Id,
            MonthlyGrossSalarySnapshot = values.MonthlyGrossSalary,
            RegularPremiumSnapshot = values.RegularPremium,
            RoadAllowanceSnapshot = values.RoadAllowance,
            MealAllowanceSnapshot = values.MealAllowance,
            OtherRegularBenefitsSnapshot = values.OtherRegularBenefits,
            UnusedLeaveDays = effective.UnusedLeaveDays,
            UnusedLeavePayment = effective.UnusedLeavePayment,
            SeverancePayment = effective.SeverancePayment,
            NoticePayment = effective.NoticePayment,
            Reason = state.Remediation.IssueDescription,
            SettlementReference = effective.SettlementReference,
            SettlementNotes = effective.SettlementNotes,
            OpenedByUserId = currentUser.UserId
                ?? throw new ForbiddenAccessException("Parasal düzeltme kullanıcı kimliği gerektirir."),
            OpenedAtUtc = DateTime.UtcNow,
        };
        context.EmployeeOffboardingSettlementRevisions.Add(revision);
        await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser, request.GuidanceOperationKey,
                "offboarding-revision", revision.Id, ["Parasal düzeltme taslağı açıldı"], cancellationToken);
        return OffboardingSettlementRevisionRules.ToDto(revision);
    }
}

public sealed record SaveOffboardingSettlementRevisionCommand(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId,
    int RevisionId, int ExpectedRevisionVersion,
    decimal UnusedLeaveDays, decimal UnusedLeavePayment,
    decimal SeverancePayment, decimal NoticePayment,
    string Reason, string SettlementReference, string? SettlementNotes, int? GuidanceContentRevisionId = null)
    : IRequest<OffboardingSettlementRevisionDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "offboarding-revision-save"; }

public sealed class SaveOffboardingSettlementRevisionCommandValidator
    : AbstractValidator<SaveOffboardingSettlementRevisionCommand>
{
    public SaveOffboardingSettlementRevisionCommandValidator()
    {
        RuleFor(item => item.ExpectedRevisionVersion).GreaterThan(0);
        RuleFor(item => item.UnusedLeaveDays).GreaterThanOrEqualTo(0);
        RuleFor(item => item.UnusedLeavePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.SeverancePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(item => item.SettlementReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.SettlementNotes).MaximumLength(2000);
    }
}

public sealed class SaveOffboardingSettlementRevisionCommandHandler(
    IApplicationDbContext context, IDatabaseLock databaseLock, ICurrentUser guidanceUser, GuidanceOperationContext guidance)
    : IRequestHandler<SaveOffboardingSettlementRevisionCommand, OffboardingSettlementRevisionDto>
{
    public Task<OffboardingSettlementRevisionDto> Handle(
        SaveOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync([
                $"employee-compensation:{request.EmployeeId}",
                $"employee-offboarding:{request.EmployeeId}"],
            token => SaveAsync(request, token), cancellationToken);

    private async Task<OffboardingSettlementRevisionDto> SaveAsync(
        SaveOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
    {
        var state = await OffboardingSettlementRevisionRules.LoadAsync(context,
            request.EmployeeId, request.PackageId, request.RemediationCaseId,
            request.OffboardingCaseId, cancellationToken);
        state.EnsureEligible();
        var draft = state.Draft;
        if (draft is null || draft.Id != request.RevisionId)
            throw new NotFoundException("Parasal düzeltme taslağı", request.RevisionId);
        if (draft.Version != request.ExpectedRevisionVersion)
            throw new ConflictException("Parasal düzeltme taslağı değişti; güncel kaydı açın.");
        draft.UnusedLeaveDays = request.UnusedLeaveDays;
        draft.UnusedLeavePayment = request.UnusedLeavePayment;
        draft.SeverancePayment = request.SeverancePayment;
        draft.NoticePayment = request.NoticePayment;
        draft.Reason = request.Reason.Trim();
        draft.SettlementReference = request.SettlementReference.Trim();
        draft.SettlementNotes = string.IsNullOrWhiteSpace(request.SettlementNotes)
            ? null : request.SettlementNotes.Trim();
        draft.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, guidanceUser, request.GuidanceOperationKey,
                "offboarding-revision", draft.Id, ["Parasal düzeltme taslağı güncellendi"], cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Parasal düzeltme taslağı değişti; yeniden açın.");
        }
        return OffboardingSettlementRevisionRules.ToDto(draft);
    }
}

public sealed record ApproveOffboardingSettlementRevisionCommand(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId,
    int RevisionId, int ExpectedRevisionVersion,
    string ExpertReviewReference, string ReviewNotes,
    bool ConfirmFinancialReview, bool ConfirmExternalReconciliation, int? GuidanceContentRevisionId = null)
    : IRequest<OffboardingSettlementRevisionDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "offboarding-revision-approve"; }

public sealed class ApproveOffboardingSettlementRevisionCommandValidator
    : AbstractValidator<ApproveOffboardingSettlementRevisionCommand>
{
    public ApproveOffboardingSettlementRevisionCommandValidator()
    {
        RuleFor(item => item.ExpectedRevisionVersion).GreaterThan(0);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ReviewNotes).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.ConfirmFinancialReview).Equal(true);
        RuleFor(item => item.ConfirmExternalReconciliation).Equal(true);
    }
}

public sealed class ApproveOffboardingSettlementRevisionCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<ApproveOffboardingSettlementRevisionCommand,
        OffboardingSettlementRevisionDto>
{
    public Task<OffboardingSettlementRevisionDto> Handle(
        ApproveOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync([
                $"employee-compensation:{request.EmployeeId}",
                $"employee-offboarding:{request.EmployeeId}"],
            token => ApproveAsync(request, token), cancellationToken);

    private async Task<OffboardingSettlementRevisionDto> ApproveAsync(
        ApproveOffboardingSettlementRevisionCommand request, CancellationToken cancellationToken)
    {
        var state = await OffboardingSettlementRevisionRules.LoadAsync(context,
            request.EmployeeId, request.PackageId, request.RemediationCaseId,
            request.OffboardingCaseId, cancellationToken);
        state.EnsureEligible();
        var draft = state.Draft;
        if (draft is null || draft.Id != request.RevisionId)
            throw new NotFoundException("Parasal düzeltme taslağı", request.RevisionId);
        if (draft.Version != request.ExpectedRevisionVersion)
            throw new ConflictException("Parasal düzeltme taslağı değişti; güncel kaydı açın.");
        if (draft.BasedOnApprovedRevisionId != state.Current?.Id)
            throw new ConflictException("Geçerli parasal hesap değişti; yeni taslak açın.");
        var effective = state.Current is null
            ? OffboardingSettlementRevisionRules.Values(state.Case)
            : OffboardingSettlementRevisionRules.Values(state.Current);
        var proposed = OffboardingSettlementRevisionRules.Values(draft);
        if (OffboardingSettlementRevisionRules.SameAmounts(effective, proposed))
            throw new ConflictException("Parasal tutarlar değişmedi; aynı hesap yeni sürüm olarak onaylanamaz.");

        draft.Status = "approved";
        draft.Version++;
        draft.ExpertReviewReference = request.ExpertReviewReference.Trim();
        draft.ReviewNotes = request.ReviewNotes.Trim();
        draft.ApprovedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Parasal düzeltme onayı kullanıcı kimliği gerektirir.");
        draft.ApprovedAtUtc = DateTime.UtcNow;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser, request.GuidanceOperationKey,
                "offboarding-revision", draft.Id, ["Yeni geçerli parasal sürüm onaylandı"], cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Parasal düzeltme başka bir işlemde onaylandı; yeniden açın.");
        }
        return OffboardingSettlementRevisionRules.ToDto(draft);
    }
}

internal sealed record OffboardingSettlementRevisionState(
    EmployeeOffboardingCase Case, EmployeeCompensationRemediationCase Remediation,
    IReadOnlyList<EmployeeOffboardingSettlementRevision> Revisions,
    EmployeeOffboardingSettlementRevision? Current,
    EmployeeOffboardingSettlementRevision? Draft,
    IReadOnlyList<string> BlockingReasons)
{
    public void EnsureEligible()
    {
        if (BlockingReasons.Count > 0)
            throw new ConflictException(string.Join(" ", BlockingReasons));
    }

    public OffboardingSettlementRevisionContextDto ToDto()
        => new(Case.Id, Case.Version, Remediation.Id,
            OffboardingSettlementRevisionRules.Values(Case),
            Current is null ? OffboardingSettlementRevisionRules.Values(Case)
                : OffboardingSettlementRevisionRules.Values(Current),
            Draft is null ? null : OffboardingSettlementRevisionRules.ToDto(Draft),
            Revisions.Where(item => item.Status == "approved")
                .OrderByDescending(item => item.RevisionNumber)
                .Select(OffboardingSettlementRevisionRules.ToDto).ToList(),
            BlockingReasons.Count == 0 && Draft is null,
            Draft is null ? BlockingReasons : [.. BlockingReasons,
                "Açık parasal düzeltme taslağını tamamlayın."]);
}

internal static class OffboardingSettlementRevisionRules
{
    public static async Task<OffboardingSettlementRevisionState> LoadAsync(
        IApplicationDbContext context, int employeeId, int packageId,
        int remediationCaseId, int offboardingCaseId, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, employeeId, packageId, cancellationToken);
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == remediationCaseId
                && item.EmployeeId == employeeId && item.CompensationPackageId == packageId,
                cancellationToken)
            ?? throw new NotFoundException("Ücret telafi incelemesi", remediationCaseId);
        var offboarding = await context.EmployeeOffboardingCases
            .Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.Id == offboardingCaseId
                && item.EmployeeId == employeeId, cancellationToken)
            ?? throw new NotFoundException("İşten çıkış dosyası", offboardingCaseId);
        var revisions = await context.EmployeeOffboardingSettlementRevisions
            .Where(item => item.OffboardingCaseId == offboardingCaseId)
            .OrderBy(item => item.RevisionNumber)
            .ToListAsync(cancellationToken);
        var current = revisions.LastOrDefault(item => item.Status == "approved");
        var draft = revisions.SingleOrDefault(item => item.Status == "draft");
        var reasons = new List<string>();
        if (remediation.Status != "approved")
            reasons.Add("Ücret telafi incelemesi henüz onaylanmadı.");
        if (await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .AnyAsync(item => item.CompensationPackageId == packageId
                && item.Id > remediationCaseId, cancellationToken))
            reasons.Add("Bu dosyadan sonra yeni uzman inceleme dosyası açılmış; eski dosya yeni parasal uygulama için kullanılamaz.");
        if (remediation.PackageVersion != package.Version)
            reasons.Add("Ücret paketi inceleme sonrasında değişti.");
        if (offboarding.CompensationPackageId != packageId)
            reasons.Add("Ayrılış bu ücret paketine bağlı değil.");
        if (offboarding.Status != EmployeeOffboardingStatus.Completed)
            reasons.Add("Yalnız tamamlanmış ayrılışın parasal hesabı düzeltilebilir.");
        if (offboarding.Employee!.Status != EmployeeStatus.Passive
            || offboarding.Employee.TerminationDate != offboarding.TerminationDate)
            reasons.Add("Personel durumu veya ayrılış tarihi tamamlanan kayıtla uyuşmuyor.");
        if (remediation.ProposedEffectiveFrom is not null
            && remediation.ProposedEffectiveFrom != package.EffectiveFrom)
            reasons.Add("Önerilen yürürlük tarihi farklı; tarih etkisi ayrı incelenmelidir.");
        var impact = JsonSerializer.Deserialize<CompensationRemediationImpactDto>(
            remediation.ImpactSnapshotJson)!;
        var reviewed = impact.OffboardingCases.SingleOrDefault(item => item.CaseId == offboardingCaseId);
        if (reviewed is null)
            reasons.Add("Bu ayrılış dosyası onaylı uzman incelemesinin kapsamına alınmamış.");
        else if (reviewed.GrossSalarySnapshot != offboarding.MonthlyGrossSalarySnapshot)
            reasons.Add("Ayrılış ücret görüntüsü uzman incelemesinden sonra değişti.");
        return new(offboarding, remediation, revisions, current, draft, reasons);
    }

    public static OffboardingSettlementValuesDto Values(EmployeeOffboardingCase item)
        => new(item.MonthlyGrossSalarySnapshot, item.RegularPremiumSnapshot,
            item.RoadAllowanceSnapshot, item.MealAllowanceSnapshot,
            item.OtherRegularBenefitsSnapshot, item.UnusedLeaveDays,
            item.UnusedLeavePayment, item.SeverancePayment, item.NoticePayment,
            item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment,
            item.SettlementSource == OffboardingSettlementSource.ApprovedRuleDraft
                ? "approved-rule-draft" : "external-expert",
            item.SettlementReference, item.SettlementNotes);

    public static OffboardingSettlementValuesDto Values(EmployeeOffboardingSettlementRevision item)
        => new(item.MonthlyGrossSalarySnapshot, item.RegularPremiumSnapshot,
            item.RoadAllowanceSnapshot, item.MealAllowanceSnapshot,
            item.OtherRegularBenefitsSnapshot, item.UnusedLeaveDays,
            item.UnusedLeavePayment, item.SeverancePayment, item.NoticePayment,
            item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment,
            "external-expert", item.SettlementReference, item.SettlementNotes);

    public static bool SameAmounts(OffboardingSettlementValuesDto left,
        OffboardingSettlementValuesDto right)
        => left.MonthlyGrossSalarySnapshot == right.MonthlyGrossSalarySnapshot
            && left.RegularPremiumSnapshot == right.RegularPremiumSnapshot
            && left.RoadAllowanceSnapshot == right.RoadAllowanceSnapshot
            && left.MealAllowanceSnapshot == right.MealAllowanceSnapshot
            && left.OtherRegularBenefitsSnapshot == right.OtherRegularBenefitsSnapshot
            && left.UnusedLeaveDays == right.UnusedLeaveDays
            && left.UnusedLeavePayment == right.UnusedLeavePayment
            && left.SeverancePayment == right.SeverancePayment
            && left.NoticePayment == right.NoticePayment;

    public static OffboardingSettlementRevisionDto ToDto(EmployeeOffboardingSettlementRevision item)
        => new(item.Id, item.RevisionNumber, item.Version, item.Status,
            item.RemediationCaseId, item.BasedOnApprovedRevisionId, Values(item),
            item.Reason, item.OpenedByUserId, item.OpenedAtUtc,
            item.ApprovedByUserId, item.ApprovedAtUtc,
            item.ExpertReviewReference, item.ReviewNotes);
}
