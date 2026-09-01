using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Employees.Employment;
using IKPro.Application.Features.Employees.GetEmployee;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Upsert;

public sealed record UpdateEmployeeCommand(int Id, EmployeeUpsertModel Model) : IRequest<EmployeeDetailDto>;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new EmployeeUpsertModelValidator());
    }
}

public sealed class UpdateEmployeeCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(e => e.Profile)
            .Include(e => e.EmploymentPeriods)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel", request.Id);

        var model = request.Model;
        var previousManagerId = employee.ManagerId;
        var requestedContractType = EmployeeEmploymentMappings.ParseContractType(model.ContractType);
        var openPeriod = employee.EmploymentPeriods.SingleOrDefault(period => period.EndDate == null);
        var requestedStatus = EmployeeMappings.ParseStatus(model.Status);
        if (requestedStatus != employee.Status)
        {
            throw new ConflictException(
                "Personel durumu genel güncelleme ile değiştirilemez; ayrılış tarihi ve erişim eşitlemesi için durum işlemini kullanın.");
        }

        if (employee.Status == Domain.Enums.EmployeeStatus.Passive
            && openPeriod is null
            && (model.HireDate != employee.HireDate || requestedContractType != employee.ContractType))
        {
            throw new ConflictException(
                "Kapanmış istihdam döneminin işe giriş tarihi veya sözleşme türü personel kartından değiştirilemez.");
        }

        await EmployeeUpsertGuards.EnsureReferencesAsync(context, model, employee.Id, cancellationToken);

        var roleOperations = new List<IdentityProvisioningOperation>();
        if (model.ManagerId != previousManagerId)
        {
            if (model.ManagerId is int nextManagerId)
            {
                roleOperations.Add(ManagerRoleProvisioning.Enqueue(context, nextManagerId, isManager: true));
            }

            if (previousManagerId is int oldManagerId)
            {
                var oldManagerKeepsTeam = await context.Employees
                    .AsNoTracking()
                    .AnyAsync(candidate => candidate.ManagerId == oldManagerId
                        && candidate.Id != employee.Id, cancellationToken);
                if (!oldManagerKeepsTeam)
                {
                    roleOperations.Add(ManagerRoleProvisioning.Enqueue(context, oldManagerId, isManager: false));
                }
            }
        }

        employee.FirstName = model.FirstName.Trim();
        employee.LastName = model.LastName.Trim();
        employee.Title = model.Title.Trim();
        employee.NationalId = model.NationalId;
        employee.DepartmentId = model.DepartmentId;
        employee.ManagerId = model.ManagerId;
        if (model.HireDate != employee.HireDate
            && openPeriod is not null
            && await context.EmployeeCompensationPackages.AnyAsync(
                item => item.EmploymentPeriodId == openPeriod.Id
                    && item.EffectiveFrom < model.HireDate,
                cancellationToken))
        {
            throw new ConflictException(
                "İşe giriş tarihi, kayıtlı ücret geçmişinin sonrasına taşınamaz.");
        }

        employee.HireDate = model.HireDate;
        employee.ContractType = requestedContractType;
        if (openPeriod is null && employee.Status == Domain.Enums.EmployeeStatus.Active)
        {
            employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
            {
                StartDate = employee.HireDate,
                ContractType = employee.ContractType,
            });
        }
        else if (openPeriod is not null)
        {
            openPeriod.StartDate = employee.HireDate;
            openPeriod.ContractType = employee.ContractType;
        }

        employee.Profile = EmployeeUpsertGuards.MapProfile(
            employee.Profile ?? new EmployeeProfile { EmployeeId = employee.Id }, model.Profile);

        await context.SaveChangesAsync(cancellationToken);

        await ManagerRoleProvisioning.ProcessNowAsync(provisioningProcessor, roleOperations, cancellationToken);

        return await sender.Send(new GetEmployeeQuery(employee.Id), cancellationToken);
    }
}
