using FluentValidation;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record RehireEmployeeCommand(
    int EmployeeId,
    DateOnly RehireDate,
    string ContractType, int? GuidanceContentRevisionId = null, string? Reason = null)
    : IRequest<EmployeeEmploymentFoundationDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "employee-rehire";
}

public sealed class RehireEmployeeCommandValidator : AbstractValidator<RehireEmployeeCommand>
{
    public RehireEmployeeCommandValidator()
    {
        RuleFor(item => item.EmployeeId).GreaterThan(0);
        RuleFor(item => item.RehireDate).NotEmpty();
        RuleFor(item => item.Reason).MaximumLength(500);
        RuleFor(item => item.Reason).NotEmpty().When(item => item.GuidanceContentRevisionId is not null);
        RuleFor(item => item.ContractType)
            .Must(value => value is "indefinite" or "fixed-term")
            .WithMessage("Sözleşme türü 'indefinite' veya 'fixed-term' olmalı.");
    }
}

public sealed class RehireEmployeeCommandHandler(
    IApplicationDbContext context,
    IDatabaseLock databaseLock,
    IIdentityProvisioningProcessor provisioningProcessor, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<RehireEmployeeCommand, EmployeeEmploymentFoundationDto>
{
    public async Task<EmployeeEmploymentFoundationDto> Handle(
        RehireEmployeeCommand request, CancellationToken cancellationToken)
    {
        var result = await databaseLock.ExecuteExclusiveAsync(
            $"employee-rehire:{request.EmployeeId}",
            token => RehireAsync(request, token),
            cancellationToken);

        await provisioningProcessor.ProcessNowAsync(result.ProvisioningOperationId, cancellationToken);
        return result.Foundation;
    }

    private async Task<RehireResult> RehireAsync(
        RehireEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(item => item.EmploymentPeriods)
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        if (employee.Status != EmployeeStatus.Passive)
        {
            throw new ConflictException("Yalnız pasif personel yeniden işe alınabilir.");
        }
        if (employee.EmploymentPeriods.Any(period => period.EndDate == null))
        {
            throw new ConflictException("Personelin zaten açık bir istihdam dönemi var.");
        }

        var latestEndDate = employee.EmploymentPeriods
            .Where(period => period.EndDate is not null)
            .Max(period => period.EndDate);
        if (request.RehireDate <= latestEndDate)
        {
            throw new ConflictException("Yeniden işe alma tarihi önceki istihdam döneminden sonra olmalıdır.");
        }

        var previousContract = employee.ContractType;
        employee.Status = EmployeeStatus.Active;
        employee.TerminationDate = null;
        employee.HireDate = request.RehireDate;
        employee.ContractType = EmployeeEmploymentMappings.ParseContractType(request.ContractType);
        var newPeriod = new EmployeeEmploymentPeriod
        {
            StartDate = request.RehireDate,
            ContractType = employee.ContractType,
            AnnualLeaveEntitlementDays = 24,
            LeaveAccrualManagedFrom = request.RehireDate,
        };
        employee.EmploymentPeriods.Add(newPeriod);

        var provisioning = new IdentityProvisioningOperation
        {
            EmployeeId = employee.Id,
            Type = IdentityProvisioningOperationType.EnableEmployeeAccess,
            Status = IdentityProvisioningStatus.Pending,
            NextAttemptAtUtc = DateTime.UtcNow,
        };
        context.IdentityProvisioningOperations.Add(provisioning);
        await context.SaveChangesAsync(cancellationToken);

        var fields = new List<string> { "İş / Durum", "İş / İşe giriş", "İş / Yeni çalışma dönemi", "Erişim / Eşitleme isteği" };
        if (previousContract != employee.ContractType) fields.Add("İş / Sözleşme");
        PersonnelEmploymentEvidence.Add(context, user, guidance, employee.Id, request.GuidanceOperationKey,
            fields.ToArray(), newPeriod.Id, reason: request.Reason);
        await context.SaveChangesAsync(cancellationToken);

        var foundation = await GetEmployeeEmploymentFoundationQueryHandler.BuildAsync(
            context, employee, cancellationToken);
        return new RehireResult(foundation, provisioning.Id);
    }

    private sealed record RehireResult(
        EmployeeEmploymentFoundationDto Foundation,
        int ProvisioningOperationId);
}
