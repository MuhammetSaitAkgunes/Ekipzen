using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record EmployeeEmploymentPeriodDto(
    int Id,
    DateOnly StartDate,
    DateOnly? EndDate,
    string ContractType);

public sealed record EmployeeCompensationPackageDto(
    int Id,
    int Version,
    int EmploymentPeriodId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal MonthlyGrossSalary,
    decimal RegularPremium,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal OtherRegularBenefits,
    decimal MonthlyRecurringTotal,
    string Currency,
    string SourceReference);

public sealed record EmployeeEmploymentFoundationDto(
    int EmployeeId,
    string ContractType,
    IReadOnlyList<EmployeeEmploymentPeriodDto> EmploymentPeriods,
    IReadOnlyList<EmployeeCompensationPackageDto> CompensationPackages);

public sealed record GetEmployeeEmploymentFoundationQuery(int EmployeeId)
    : IRequest<EmployeeEmploymentFoundationDto>;

public sealed class GetEmployeeEmploymentFoundationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetEmployeeEmploymentFoundationQuery, EmployeeEmploymentFoundationDto>
{
    public async Task<EmployeeEmploymentFoundationDto> Handle(
        GetEmployeeEmploymentFoundationQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        return await BuildAsync(context, employee, cancellationToken);
    }

    internal static async Task<EmployeeEmploymentFoundationDto> BuildAsync(
        IApplicationDbContext context,
        Employee employee,
        CancellationToken cancellationToken)
    {
        var periods = await context.EmployeeEmploymentPeriods
            .AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id)
            .OrderByDescending(item => item.StartDate)
            .Select(item => new EmployeeEmploymentPeriodDto(
                item.Id,
                item.StartDate,
                item.EndDate,
                item.ContractType.ToDto()))
            .ToListAsync(cancellationToken);

        var packages = await context.EmployeeCompensationPackages
            .AsNoTracking()
            .Where(item => item.EmploymentPeriod!.EmployeeId == employee.Id)
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return new EmployeeEmploymentFoundationDto(
            employee.Id,
            employee.ContractType.ToDto(),
            periods,
            packages.Select(item => item.ToDto()).ToList());
    }
}

public sealed record SaveEmployeeCompensationPackageCommand(
    int EmployeeId,
    DateOnly EffectiveFrom,
    decimal MonthlyGrossSalary,
    decimal RegularPremium,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal OtherRegularBenefits,
    string SourceReference)
    : IRequest<EmployeeEmploymentFoundationDto>;

public sealed class SaveEmployeeCompensationPackageCommandValidator
    : AbstractValidator<SaveEmployeeCompensationPackageCommand>
{
    public SaveEmployeeCompensationPackageCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.EffectiveFrom).NotEmpty();
        RuleFor(item => item.MonthlyGrossSalary).GreaterThan(0m);
        RuleFor(item => item.RegularPremium).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.RoadAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.MealAllowance).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.OtherRegularBenefits).GreaterThanOrEqualTo(0m);
        RuleFor(item => item.SourceReference).NotEmpty().MaximumLength(256);
    }
}

public sealed class SaveEmployeeCompensationPackageCommandHandler(
    IApplicationDbContext context,
    IDatabaseLock databaseLock)
    : IRequestHandler<SaveEmployeeCompensationPackageCommand, EmployeeEmploymentFoundationDto>
{
    public Task<EmployeeEmploymentFoundationDto> Handle(
        SaveEmployeeCompensationPackageCommand request,
        CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            $"employee-compensation:{request.EmployeeId}",
            token => SaveAsync(request, token),
            cancellationToken);

    private async Task<EmployeeEmploymentFoundationDto> SaveAsync(
        SaveEmployeeCompensationPackageCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        var employmentPeriod = await context.EmployeeEmploymentPeriods
            .SingleOrDefaultAsync(period => period.EmployeeId == employee.Id && period.EndDate == null,
                cancellationToken)
            ?? throw new ConflictException("Ücret paketi yalnız açık bir istihdam dönemine eklenebilir.");

        if (request.EffectiveFrom < employmentPeriod.StartDate)
        {
            throw new ConflictException("Ücret paketinin başlangıcı açık istihdam döneminden önce olamaz.");
        }

        var packages = await context.EmployeeCompensationPackages
            .Where(item => item.EmploymentPeriodId == employmentPeriod.Id)
            .OrderBy(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        if (packages.Any(item => item.EffectiveFrom == request.EffectiveFrom))
        {
            throw new ConflictException("Bu başlangıç tarihi için bir ücret paketi zaten var.");
        }

        var previous = packages.LastOrDefault(item => item.EffectiveFrom < request.EffectiveFrom);
        var next = packages.FirstOrDefault(item => item.EffectiveFrom > request.EffectiveFrom);
        if (previous is not null)
        {
            previous.EffectiveTo = request.EffectiveFrom.AddDays(-1);
            previous.Version++;
        }

        context.EmployeeCompensationPackages.Add(new EmployeeCompensationPackage
        {
            EmploymentPeriodId = employmentPeriod.Id,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = next?.EffectiveFrom.AddDays(-1),
            MonthlyGrossSalary = request.MonthlyGrossSalary,
            RegularPremium = request.RegularPremium,
            RoadAllowance = request.RoadAllowance,
            MealAllowance = request.MealAllowance,
            OtherRegularBenefits = request.OtherRegularBenefits,
            Currency = "TRY",
            SourceReference = request.SourceReference.Trim(),
        });

        await context.SaveChangesAsync(cancellationToken);
        return await GetEmployeeEmploymentFoundationQueryHandler.BuildAsync(
            context, employee, cancellationToken);
    }
}

public static class EmployeeEmploymentMappings
{
    public static string ToDto(this EmploymentContractType value) => value switch
    {
        EmploymentContractType.Indefinite => "indefinite",
        EmploymentContractType.FixedTerm => "fixed-term",
        _ => value.ToString(),
    };

    public static EmploymentContractType ParseContractType(string? value) => value switch
    {
        null or "" or "indefinite" => EmploymentContractType.Indefinite,
        "fixed-term" => EmploymentContractType.FixedTerm,
        _ => throw new ArgumentException($"Geçersiz sözleşme türü: {value}."),
    };

    public static string ToDto(this TerminationReason value) => value switch
    {
        TerminationReason.EmployeeResignation => "employee-resignation",
        TerminationReason.EmployerTermination => "employer-termination",
        TerminationReason.EmployerTerminationForCause => "employer-termination-for-cause",
        TerminationReason.MutualAgreement => "mutual-agreement",
        TerminationReason.FixedTermExpiry => "fixed-term-expiry",
        TerminationReason.Retirement => "retirement",
        TerminationReason.MilitaryService => "military-service",
        TerminationReason.Marriage => "marriage",
        TerminationReason.Death => "death",
        TerminationReason.Other => "other",
        _ => value.ToString(),
    };

    public static TerminationReason ParseTerminationReason(string? value) => value switch
    {
        "employee-resignation" => TerminationReason.EmployeeResignation,
        "employer-termination" => TerminationReason.EmployerTermination,
        "employer-termination-for-cause" => TerminationReason.EmployerTerminationForCause,
        "mutual-agreement" => TerminationReason.MutualAgreement,
        "fixed-term-expiry" => TerminationReason.FixedTermExpiry,
        "retirement" => TerminationReason.Retirement,
        "military-service" => TerminationReason.MilitaryService,
        "marriage" => TerminationReason.Marriage,
        "death" => TerminationReason.Death,
        null or "" or "other" => TerminationReason.Other,
        _ => throw new ArgumentException($"Geçersiz fesih nedeni: {value}."),
    };

    public static string ToDto(this NoticeHandling value) => value switch
    {
        NoticeHandling.NotApplicable => "not-applicable",
        NoticeHandling.Worked => "worked",
        NoticeHandling.PayableToEmployee => "payable-to-employee",
        NoticeHandling.PayableToEmployer => "payable-to-employer",
        NoticeHandling.Waived => "waived",
        _ => value.ToString(),
    };

    public static NoticeHandling ParseNoticeHandling(string? value) => value switch
    {
        null or "" or "not-applicable" => NoticeHandling.NotApplicable,
        "worked" => NoticeHandling.Worked,
        "payable-to-employee" => NoticeHandling.PayableToEmployee,
        "payable-to-employer" => NoticeHandling.PayableToEmployer,
        "waived" => NoticeHandling.Waived,
        _ => throw new ArgumentException($"Geçersiz ihbar uygulaması: {value}."),
    };

    public static EmployeeCompensationPackageDto ToDto(this EmployeeCompensationPackage item)
        => new(
            item.Id,
            item.Version,
            item.EmploymentPeriodId,
            item.EffectiveFrom,
            item.EffectiveTo,
            item.MonthlyGrossSalary,
            item.RegularPremium,
            item.RoadAllowance,
            item.MealAllowance,
            item.OtherRegularBenefits,
            item.MonthlyGrossSalary + item.RegularPremium + item.RoadAllowance
                + item.MealAllowance + item.OtherRegularBenefits,
            item.Currency,
            item.SourceReference);
}
