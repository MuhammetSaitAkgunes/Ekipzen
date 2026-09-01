using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Employees.Employment;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Offboarding;

public sealed record EmployeeOffboardingCaseDto(
    int Id,
    int EmployeeId,
    string Status,
    int Version,
    DateOnly TerminationDate,
    string ExitCode,
    string ExitReason,
    string TerminationReason,
    string NoticeHandling,
    string ContractTypeSnapshot,
    int? SuccessorManagerId,
    int? EmploymentPeriodId,
    int? CompensationPackageId,
    decimal MonthlyGrossSalarySnapshot,
    decimal RegularPremiumSnapshot,
    decimal RoadAllowanceSnapshot,
    decimal MealAllowanceSnapshot,
    decimal OtherRegularBenefitsSnapshot,
    decimal UnusedLeaveDays,
    decimal UnusedLeavePayment,
    decimal SeverancePayment,
    decimal NoticePayment,
    decimal TotalPayment,
    string SettlementSource,
    string SettlementReference,
    string? SettlementNotes,
    DateTime? CompletedAtUtc,
    DateTime? ReversedAtUtc,
    string? ReversalReason,
    bool IdentityProvisioningPending,
    string? IdentityProvisioningLastError);

internal static class EmployeeOffboardingMappings
{
    public static EmployeeOffboardingCaseDto ToDto(
        this EmployeeOffboardingCase item,
        bool identityProvisioningPending = false,
        string? identityProvisioningLastError = null) => new(
        item.Id,
        item.EmployeeId,
        item.Status switch
        {
            EmployeeOffboardingStatus.Draft => "draft",
            EmployeeOffboardingStatus.Completed => "completed",
            EmployeeOffboardingStatus.Reversed => "reversed",
            _ => item.Status.ToString().ToLowerInvariant(),
        },
        item.Version,
        item.TerminationDate,
        item.ExitCode,
        item.ExitReason,
        item.TerminationReason.ToDto(),
        item.NoticeHandling.ToDto(),
        item.ContractTypeSnapshot.ToDto(),
        item.SuccessorManagerId,
        item.EmploymentPeriodId,
        item.CompensationPackageId,
        item.MonthlyGrossSalarySnapshot,
        item.RegularPremiumSnapshot,
        item.RoadAllowanceSnapshot,
        item.MealAllowanceSnapshot,
        item.OtherRegularBenefitsSnapshot,
        item.UnusedLeaveDays,
        item.UnusedLeavePayment,
        item.SeverancePayment,
        item.NoticePayment,
        item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment,
        "external-expert",
        item.SettlementReference,
        item.SettlementNotes,
        item.CompletedAtUtc,
        item.ReversedAtUtc,
        item.ReversalReason,
        identityProvisioningPending,
        identityProvisioningLastError);
}

public sealed record SaveEmployeeOffboardingDraftCommand(
    int EmployeeId,
    int? ExpectedVersion,
    DateOnly TerminationDate,
    string ExitCode,
    string ExitReason,
    string TerminationReason,
    string NoticeHandling,
    int? SuccessorManagerId,
    decimal UnusedLeaveDays,
    decimal UnusedLeavePayment,
    decimal SeverancePayment,
    decimal NoticePayment,
    string SettlementReference,
    string? SettlementNotes)
    : IRequest<EmployeeOffboardingCaseDto>;

public sealed class SaveEmployeeOffboardingDraftCommandValidator
    : AbstractValidator<SaveEmployeeOffboardingDraftCommand>
{
    public SaveEmployeeOffboardingDraftCommandValidator()
    {
        RuleFor(item => item.ExpectedVersion).GreaterThan(0).When(item => item.ExpectedVersion is not null);
        RuleFor(item => item.ExitCode).NotEmpty().MaximumLength(64);
        RuleFor(item => item.ExitReason).NotEmpty().MaximumLength(1000);
        RuleFor(item => item.TerminationReason).Must(value => value is
            "employee-resignation" or "employer-termination" or "employer-termination-for-cause"
            or "mutual-agreement" or "fixed-term-expiry" or "retirement"
            or "military-service" or "marriage" or "death" or "other");
        RuleFor(item => item.NoticeHandling).Must(value => value is
            "not-applicable" or "worked" or "payable-to-employee"
            or "payable-to-employer" or "waived");
        RuleFor(item => item.SuccessorManagerId).GreaterThan(0).When(item => item.SuccessorManagerId is not null);
        RuleFor(item => item.UnusedLeaveDays).GreaterThanOrEqualTo(0);
        RuleFor(item => item.UnusedLeavePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.SeverancePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticePayment).GreaterThanOrEqualTo(0);
        RuleFor(item => item.SettlementReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.SettlementNotes).MaximumLength(2000);
    }
}

public sealed class SaveEmployeeOffboardingDraftCommandHandler(
    IApplicationDbContext context,
    IDatabaseLock databaseLock)
    : IRequestHandler<SaveEmployeeOffboardingDraftCommand, EmployeeOffboardingCaseDto>
{
    public Task<EmployeeOffboardingCaseDto> Handle(
        SaveEmployeeOffboardingDraftCommand request,
        CancellationToken cancellationToken) =>
        databaseLock.ExecuteExclusiveAsync(
            $"employee-offboarding:{request.EmployeeId}",
            token => SaveAsync(request, token),
            cancellationToken);

    private async Task<EmployeeOffboardingCaseDto> SaveAsync(
        SaveEmployeeOffboardingDraftCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        if (employee.Status != EmployeeStatus.Active)
        {
            throw new ConflictException("Yalnız aktif personel için işten çıkış taslağı hazırlanabilir.");
        }

        if (request.TerminationDate < employee.HireDate)
        {
            throw new ArgumentException("İşten ayrılma tarihi işe giriş tarihinden önce olamaz.");
        }

        var employmentPeriod = await context.EmployeeEmploymentPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(period => period.EmployeeId == employee.Id && period.EndDate == null,
                cancellationToken)
            ?? throw new ConflictException("Personelin açık istihdam dönemi bulunamadı; taslak kaydedilemez.");
        if (request.TerminationDate < employmentPeriod.StartDate)
        {
            throw new ConflictException("İşten ayrılma tarihi açık istihdam döneminden önce olamaz.");
        }

        var item = await context.EmployeeOffboardingCases
            .FirstOrDefaultAsync(candidate =>
                candidate.EmployeeId == request.EmployeeId
                && candidate.Status != EmployeeOffboardingStatus.Reversed,
                cancellationToken);

        if (item?.Status == EmployeeOffboardingStatus.Completed)
        {
            throw new ConflictException("Personelin tamamlanmış işten çıkış dosyası geri alınmadan yeni taslak açılamaz.");
        }

        if (item is null)
        {
            if (request.ExpectedVersion is not null)
            {
                throw new ConflictException("İşten çıkış taslağı artık mevcut değil; ekranı yenileyin.");
            }

            item = new EmployeeOffboardingCase
            {
                EmployeeId = request.EmployeeId,
                Status = EmployeeOffboardingStatus.Draft,
                Version = 1,
            };
            context.EmployeeOffboardingCases.Add(item);
        }
        else
        {
            if (request.ExpectedVersion != item.Version)
            {
                throw new ConflictException("İşten çıkış taslağı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
            }

            item.Version++;
        }

        item.TerminationDate = request.TerminationDate;
        item.ExitCode = request.ExitCode.Trim();
        item.ExitReason = request.ExitReason.Trim();
        item.TerminationReason = EmployeeEmploymentMappings.ParseTerminationReason(request.TerminationReason);
        item.NoticeHandling = EmployeeEmploymentMappings.ParseNoticeHandling(request.NoticeHandling);
        item.ContractTypeSnapshot = employee.ContractType;
        item.EmploymentPeriodId = employmentPeriod.Id;
        item.SuccessorManagerId = request.SuccessorManagerId;
        item.UnusedLeaveDays = request.UnusedLeaveDays;
        item.UnusedLeavePayment = request.UnusedLeavePayment;
        item.SeverancePayment = request.SeverancePayment;
        item.NoticePayment = request.NoticePayment;
        var compensation = await context.EmployeeCompensationPackages
            .AsNoTracking()
            .Where(package => package.EmploymentPeriodId == employmentPeriod.Id
                && package.EffectiveFrom <= request.TerminationDate
                && (package.EffectiveTo == null || package.EffectiveTo >= request.TerminationDate))
            .OrderByDescending(package => package.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        item.CompensationPackageId = compensation?.Id;
        item.MonthlyGrossSalarySnapshot = compensation?.MonthlyGrossSalary ?? 0m;
        item.RegularPremiumSnapshot = compensation?.RegularPremium ?? 0m;
        item.RoadAllowanceSnapshot = compensation?.RoadAllowance ?? 0m;
        item.MealAllowanceSnapshot = compensation?.MealAllowance ?? 0m;
        item.OtherRegularBenefitsSnapshot = compensation?.OtherRegularBenefits ?? 0m;
        item.SettlementSource = OffboardingSettlementSource.ExternalExpert;
        item.SettlementReference = request.SettlementReference.Trim();
        item.SettlementNotes = string.IsNullOrWhiteSpace(request.SettlementNotes)
            ? null
            : request.SettlementNotes.Trim();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İşten çıkış taslağı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        return item.ToDto();
    }
}

public sealed record CompleteEmployeeOffboardingCommand(
    int EmployeeId,
    int CaseId,
    int ExpectedVersion)
    : IRequest<EmployeeOffboardingCaseDto>;

public sealed class CompleteEmployeeOffboardingCommandValidator
    : AbstractValidator<CompleteEmployeeOffboardingCommand>
{
    public CompleteEmployeeOffboardingCommandValidator()
    {
        RuleFor(item => item.CaseId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class CompleteEmployeeOffboardingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<CompleteEmployeeOffboardingCommand, EmployeeOffboardingCaseDto>
{
    public async Task<EmployeeOffboardingCaseDto> Handle(
        CompleteEmployeeOffboardingCommand request,
        CancellationToken cancellationToken)
    {
        var result = await databaseLock.ExecuteExclusiveAsync(
            $"employee-offboarding:{request.EmployeeId}",
            token => CompleteAsync(request, token),
            cancellationToken);

        await ProcessNowAsync(result.Operations, cancellationToken);
        return result.Case;
    }

    private async Task<OffboardingMutationResult> CompleteAsync(
        CompleteEmployeeOffboardingCommand request,
        CancellationToken cancellationToken)
    {
        var item = await context.EmployeeOffboardingCases
            .Include(candidate => candidate.Employee)
                .ThenInclude(employee => employee!.Profile)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.CaseId && candidate.EmployeeId == request.EmployeeId,
                cancellationToken)
            ?? throw new NotFoundException("İşten çıkış dosyası", request.CaseId);

        if (item.Status != EmployeeOffboardingStatus.Draft)
        {
            throw new ConflictException("Yalnız taslak işten çıkış dosyası tamamlanabilir.");
        }

        if (item.Version != request.ExpectedVersion)
        {
            throw new ConflictException("İşten çıkış taslağı değişti; ekranı yenileyin.");
        }

        var employee = item.Employee!;
        if (employee.Status != EmployeeStatus.Active)
        {
            throw new ConflictException("Personel artık aktif değil; işten çıkış tamamlanamaz.");
        }

        if (item.TerminationDate < employee.HireDate)
        {
            throw new ArgumentException("İşten ayrılma tarihi işe giriş tarihinden önce olamaz.");
        }

        var employmentPeriod = await context.EmployeeEmploymentPeriods
            .SingleOrDefaultAsync(period => period.EmployeeId == employee.Id && period.EndDate == null,
                cancellationToken)
            ?? throw new ConflictException("Personelin açık istihdam dönemi bulunamadı; çıkış tamamlanamaz.");
        if (item.TerminationDate < employmentPeriod.StartDate)
        {
            throw new ConflictException("İşten ayrılma tarihi açık istihdam döneminden önce olamaz.");
        }

        if (item.EmploymentPeriodId is not null && item.EmploymentPeriodId != employmentPeriod.Id)
        {
            throw new ConflictException("İstihdam dönemi taslak kaydedildikten sonra değişti; taslağı yenileyin.");
        }

        if (item.ContractTypeSnapshot != employmentPeriod.ContractType
            || item.ContractTypeSnapshot != employee.ContractType)
        {
            throw new ConflictException("Sözleşme türü taslak kaydedildikten sonra değişti; taslağı yenileyin.");
        }

        var compensation = await context.EmployeeCompensationPackages
            .Where(package => package.EmploymentPeriodId == employmentPeriod.Id
                && package.EffectiveFrom <= item.TerminationDate
                && (package.EffectiveTo == null || package.EffectiveTo >= item.TerminationDate))
            .OrderByDescending(package => package.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (compensation?.Id != item.CompensationPackageId
            || (compensation is not null
                && (compensation.MonthlyGrossSalary != item.MonthlyGrossSalarySnapshot
                    || compensation.RegularPremium != item.RegularPremiumSnapshot
                    || compensation.RoadAllowance != item.RoadAllowanceSnapshot
                    || compensation.MealAllowance != item.MealAllowanceSnapshot
                    || compensation.OtherRegularBenefits != item.OtherRegularBenefitsSnapshot)))
        {
            throw new ConflictException("Ücret paketi taslak kaydedildikten sonra değişti; taslağı yenileyin.");
        }

        employmentPeriod.EndDate = item.TerminationDate;
        item.EmploymentPeriodId = employmentPeriod.Id;
        if (compensation is not null)
        {
            item.CompensationPackagePreviousEffectiveTo = compensation.EffectiveTo;
            compensation.EffectiveTo = item.TerminationDate;
        }

        var directReports = await context.Employees
            .Where(candidate => candidate.ManagerId == employee.Id)
            .ToListAsync(cancellationToken);

        var successor = await PrepareTeamTransferAsync(
            item, employee, directReports, cancellationToken);

        if (employee.Profile is null)
        {
            employee.Profile = new EmployeeProfile { EmployeeId = employee.Id };
        }

        item.PreviousExitCode = employee.Profile.ExitCode;
        employee.Profile.ExitCode = item.ExitCode;
        employee.Status = EmployeeStatus.Passive;
        employee.TerminationDate = item.TerminationDate;

        item.Status = EmployeeOffboardingStatus.Completed;
        item.CompletedAtUtc = DateTime.UtcNow;
        item.CompletedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("İşten çıkış işlemi kullanıcı kimliği gerektirir.");
        item.Version++;

        var operations = new List<IdentityProvisioningOperation>();
        if (successor is not null)
        {
            operations.Add(EnqueueIdentity(
                item, successor.Id, IdentityProvisioningOperationType.GrantManagerRole));
        }

        operations.Add(EnqueueIdentity(
            item, employee.Id, IdentityProvisioningOperationType.RevokeManagerRole));
        operations.Add(EnqueueIdentity(
            item, employee.Id, IdentityProvisioningOperationType.DisableEmployeeAccess));
        context.IdentityProvisioningOperations.AddRange(operations);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İşten çıkış dosyası başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        return new OffboardingMutationResult(item.ToDto(identityProvisioningPending: true), operations);
    }

    private async Task<Employee?> PrepareTeamTransferAsync(
        EmployeeOffboardingCase item,
        Employee employee,
        IReadOnlyList<Employee> directReports,
        CancellationToken cancellationToken)
    {
        if (directReports.Count == 0)
        {
            if (item.SuccessorManagerId is not null)
            {
                throw new ConflictException("Ekibi olmayan personel için halef belirtilemez.");
            }

            return null;
        }

        if (item.SuccessorManagerId is null)
        {
            throw new ConflictException("Ekibi bulunan yönetici için aktif bir halef seçilmelidir.");
        }

        if (item.SuccessorManagerId == employee.Id)
        {
            throw new ConflictException("Ayrılan yönetici kendi halefi olamaz.");
        }

        var successor = await context.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == item.SuccessorManagerId, cancellationToken)
            ?? throw new NotFoundException("Halef yönetici", item.SuccessorManagerId);

        if (successor.Status != EmployeeStatus.Active)
        {
            throw new ConflictException("Halef yönetici aktif olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(successor.UserId))
        {
            throw new ConflictException("Halef yöneticinin bağlı bir giriş hesabı olmalıdır.");
        }

        var directReportIds = directReports.Select(report => report.Id).ToHashSet();
        if (directReportIds.Contains(successor.Id))
        {
            if (employee.ManagerId == successor.Id)
            {
                throw new ConflictException("Mevcut yönetici hiyerarşisi döngü içeriyor.");
            }

            item.TeamTransfers.Add(new EmployeeOffboardingTeamTransfer
            {
                EmployeeId = successor.Id,
                PreviousManagerId = successor.ManagerId,
                AssignedManagerId = employee.ManagerId,
            });
            successor.ManagerId = employee.ManagerId;
        }
        else
        {
            var hierarchy = await context.Employees
                .AsNoTracking()
                .Select(candidate => new { candidate.Id, candidate.ManagerId })
                .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.ManagerId, cancellationToken);
            var visited = new HashSet<int>();
            var cursor = successor.ManagerId;

            while (cursor is int managerId && visited.Add(managerId))
            {
                if (managerId == employee.Id)
                {
                    throw new ConflictException("Alt ekipten halef yalnız doğrudan raporlar arasından seçilebilir.");
                }

                cursor = hierarchy.GetValueOrDefault(managerId);
            }
        }

        foreach (var report in directReports.Where(report => report.Id != successor.Id))
        {
            item.TeamTransfers.Add(new EmployeeOffboardingTeamTransfer
            {
                EmployeeId = report.Id,
                PreviousManagerId = report.ManagerId,
                AssignedManagerId = successor.Id,
            });
            report.ManagerId = successor.Id;
        }

        return successor;
    }

    private async Task ProcessNowAsync(
        IEnumerable<IdentityProvisioningOperation> operations,
        CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await provisioningProcessor.ProcessNowAsync(operation.Id, cancellationToken);
        }
    }

    private static IdentityProvisioningOperation EnqueueIdentity(
        EmployeeOffboardingCase item,
        int employeeId,
        IdentityProvisioningOperationType type) => new()
    {
        EmployeeId = employeeId,
        OffboardingCase = item,
        Type = type,
        Status = IdentityProvisioningStatus.Pending,
        NextAttemptAtUtc = DateTime.UtcNow,
    };
}

public sealed record ReverseEmployeeOffboardingCommand(
    int EmployeeId,
    int CaseId,
    int ExpectedVersion,
    string Reason)
    : IRequest<EmployeeOffboardingCaseDto>;

public sealed class ReverseEmployeeOffboardingCommandValidator
    : AbstractValidator<ReverseEmployeeOffboardingCommand>
{
    public ReverseEmployeeOffboardingCommandValidator()
    {
        RuleFor(item => item.CaseId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReverseEmployeeOffboardingCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<ReverseEmployeeOffboardingCommand, EmployeeOffboardingCaseDto>
{
    public async Task<EmployeeOffboardingCaseDto> Handle(
        ReverseEmployeeOffboardingCommand request,
        CancellationToken cancellationToken)
    {
        var result = await databaseLock.ExecuteExclusiveAsync(
            $"employee-offboarding:{request.EmployeeId}",
            token => ReverseAsync(request, token),
            cancellationToken);

        foreach (var operation in result.Operations)
        {
            await provisioningProcessor.ProcessNowAsync(operation.Id, cancellationToken);
        }

        return result.Case;
    }

    private async Task<OffboardingMutationResult> ReverseAsync(
        ReverseEmployeeOffboardingCommand request,
        CancellationToken cancellationToken)
    {
        var item = await context.EmployeeOffboardingCases
            .Include(candidate => candidate.Employee)
                .ThenInclude(employee => employee!.Profile)
            .Include(candidate => candidate.TeamTransfers)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.CaseId && candidate.EmployeeId == request.EmployeeId,
                cancellationToken)
            ?? throw new NotFoundException("İşten çıkış dosyası", request.CaseId);

        if (item.Status != EmployeeOffboardingStatus.Completed)
        {
            throw new ConflictException("Yalnız tamamlanmış işten çıkış dosyası geri alınabilir.");
        }

        if (item.Version != request.ExpectedVersion)
        {
            throw new ConflictException("İşten çıkış dosyası değişti; ekranı yenileyin.");
        }

        var employee = item.Employee!;
        if (employee.Status != EmployeeStatus.Passive
            || employee.TerminationDate != item.TerminationDate)
        {
            throw new ConflictException("Personel durumu işten çıkış snapshot'ıyla uyuşmuyor; otomatik geri alma güvenli değil.");
        }

        if (item.EmploymentPeriodId is null)
        {
            throw new ConflictException("İşten çıkış dosyasında istihdam dönemi snapshot'ı yok; otomatik geri alma güvenli değil.");
        }

        var employmentPeriod = await context.EmployeeEmploymentPeriods
            .SingleOrDefaultAsync(period => period.Id == item.EmploymentPeriodId
                && period.EmployeeId == employee.Id, cancellationToken)
            ?? throw new ConflictException("İşten çıkışın kapattığı istihdam dönemi artık bulunamıyor.");
        if (employmentPeriod.EndDate != item.TerminationDate)
        {
            throw new ConflictException("İstihdam dönemi işten çıkıştan sonra değişmiş; otomatik geri alma güvenli değil.");
        }

        EmployeeCompensationPackage? compensation = null;
        if (item.CompensationPackageId is int compensationPackageId)
        {
            compensation = await context.EmployeeCompensationPackages
                .SingleOrDefaultAsync(package => package.Id == compensationPackageId
                    && package.EmploymentPeriodId == employmentPeriod.Id, cancellationToken)
                ?? throw new ConflictException("İşten çıkışın kapattığı ücret paketi artık bulunamıyor.");
            if (compensation.EffectiveTo != item.TerminationDate)
            {
                throw new ConflictException("Ücret paketi işten çıkıştan sonra değişmiş; otomatik geri alma güvenli değil.");
            }
        }

        var transferIds = item.TeamTransfers.Select(transfer => transfer.EmployeeId).ToArray();
        var transferEmployees = await context.Employees
            .Where(candidate => transferIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);

        foreach (var transfer in item.TeamTransfers)
        {
            if (!transferEmployees.TryGetValue(transfer.EmployeeId, out var transferred)
                || transferred.ManagerId != transfer.AssignedManagerId)
            {
                throw new ConflictException(
                    "Organizasyon işten çıkıştan sonra değişmiş; ekip bağları otomatik geri alınamaz.");
            }
        }

        foreach (var transfer in item.TeamTransfers)
        {
            transferEmployees[transfer.EmployeeId].ManagerId = transfer.PreviousManagerId;
        }

        employmentPeriod.EndDate = null;
        if (compensation is not null)
        {
            compensation.EffectiveTo = item.CompensationPackagePreviousEffectiveTo;
        }
        employee.Status = EmployeeStatus.Active;
        employee.TerminationDate = null;
        if (employee.Profile is not null)
        {
            employee.Profile.ExitCode = item.PreviousExitCode;
        }

        item.Status = EmployeeOffboardingStatus.Reversed;
        item.ReversedAtUtc = DateTime.UtcNow;
        item.ReversedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Geri alma işlemi kullanıcı kimliği gerektirir.");
        item.ReversalReason = request.Reason.Trim();
        item.Version++;

        var organization = await context.Employees
            .AsNoTracking()
            .Select(candidate => new OrganizationSnapshot(
                candidate.Id, candidate.ManagerId, candidate.Status))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);

        organization[employee.Id] = new OrganizationSnapshot(
            employee.Id, employee.ManagerId, EmployeeStatus.Active);
        foreach (var transfer in item.TeamTransfers)
        {
            var current = organization[transfer.EmployeeId];
            organization[transfer.EmployeeId] = current with { ManagerId = transfer.PreviousManagerId };
        }

        var impactedManagerIds = item.TeamTransfers
            .Where(transfer => transfer.AssignedManagerId is not null)
            .Select(transfer => transfer.AssignedManagerId!.Value)
            .Append(employee.Id)
            .Distinct()
            .ToArray();

        var operations = new List<IdentityProvisioningOperation>();
        foreach (var managerId in impactedManagerIds)
        {
            var shouldBeManager = organization.TryGetValue(managerId, out var manager)
                && manager.Status == EmployeeStatus.Active
                && organization.Values.Any(candidate =>
                    candidate.Status == EmployeeStatus.Active && candidate.ManagerId == managerId);
            operations.Add(EnqueueIdentity(
                item,
                managerId,
                shouldBeManager
                    ? IdentityProvisioningOperationType.GrantManagerRole
                    : IdentityProvisioningOperationType.RevokeManagerRole));
        }

        operations.Add(EnqueueIdentity(
            item, employee.Id, IdentityProvisioningOperationType.EnableEmployeeAccess));
        context.IdentityProvisioningOperations.AddRange(operations);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İşten çıkış dosyası başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        return new OffboardingMutationResult(item.ToDto(identityProvisioningPending: true), operations);
    }

    private static IdentityProvisioningOperation EnqueueIdentity(
        EmployeeOffboardingCase item,
        int employeeId,
        IdentityProvisioningOperationType type) => new()
    {
        EmployeeId = employeeId,
        OffboardingCase = item,
        Type = type,
        Status = IdentityProvisioningStatus.Pending,
        NextAttemptAtUtc = DateTime.UtcNow,
    };

    private sealed record OrganizationSnapshot(
        int Id,
        int? ManagerId,
        EmployeeStatus Status);
}

internal sealed record OffboardingMutationResult(
    EmployeeOffboardingCaseDto Case,
    IReadOnlyList<IdentityProvisioningOperation> Operations);
