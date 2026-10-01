using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Notifications;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.GetEmployee;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Upsert;

public sealed record CreateEmployeeCommand(EmployeeUpsertModel Model) : IRequest<EmployeeDetailDto>, IGuidanceOperationCommand
{
    public int? GuidanceContentRevisionId => Model.GuidanceContentRevisionId;
    public string GuidanceOperationKey => "employee-create";
}

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new EmployeeUpsertModelValidator());

        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Status)
                .Equal("active")
                .WithMessage("Yeni personel aktif oluşturulmalı; pasife alma için durum işlemini kullanın.");
        });
    }
}

public sealed class CreateEmployeeCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    ICurrentUser currentUser,
    INotificationTrigger notificationTrigger,
    IIdentityProvisioningProcessor provisioningProcessor,
    GuidanceOperationContext guidance)
    : IRequestHandler<CreateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;

        await EmployeeUpsertGuards.EnsureReferencesAsync(context, model, employeeId: null, cancellationToken);

        var roleOperations = new List<IdentityProvisioningOperation>();
        if (model.ManagerId is int managerId)
        {
            roleOperations.Add(ManagerRoleProvisioning.Enqueue(context, managerId, isManager: true));
        }

        var employee = new Employee
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Title = model.Title.Trim(),
            NationalId = model.NationalId,
            DepartmentId = model.DepartmentId,
            ManagerId = model.ManagerId,
            HireDate = model.HireDate,
            ContractType = EmployeeEmploymentMappings.ParseContractType(model.ContractType),
            Status = EmployeeMappings.ParseStatus(model.Status),
            Profile = EmployeeUpsertGuards.MapProfile(new EmployeeProfile(), model.Profile),
        };
        employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
        {
            StartDate = employee.HireDate,
            ContractType = employee.ContractType,
        });

        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        context.Employees.Add(employee);
        var creationEvidence = new EmployeeCreationEvidence
        {
            UserId = userId,
            Employee = employee,
        };
        guidance.Stamp(creationEvidence);
        context.EmployeeCreationEvidences.Add(creationEvidence);
        await context.SaveChangesAsync(cancellationToken);

        await ManagerRoleProvisioning.ProcessNowAsync(provisioningProcessor, roleOperations, cancellationToken);
        // Faz 11: "Yeni Personel Kaydı" bildirimi (ayar toggle'ına uyar).
        await notificationTrigger.NewPersonnelCreatedAsync(employee, cancellationToken);

        return await sender.Send(new GetEmployeeQuery(employee.Id), cancellationToken);
    }
}

/// <summary>Create/Update ortak referans kontrolleri ve profil eşlemesi.</summary>
public static class EmployeeUpsertGuards
{
    public static async Task EnsureReferencesAsync(
        IApplicationDbContext context, EmployeeUpsertModel model, int? employeeId, CancellationToken cancellationToken)
    {
        if (!await context.Departments.AnyAsync(d => d.Id == model.DepartmentId, cancellationToken))
        {
            throw new NotFoundException("Departman", model.DepartmentId);
        }

        if (model.ManagerId is not null)
        {
            if (model.ManagerId == employeeId)
            {
                throw new ConflictException("Personel kendi yöneticisi olamaz.");
            }

            var manager = await context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == model.ManagerId, cancellationToken)
                ?? throw new NotFoundException("Yönetici", model.ManagerId);

            if (manager.Status != Domain.Enums.EmployeeStatus.Active)
            {
                throw new ConflictException("Yönetici aktif olmalıdır.");
            }

            if (string.IsNullOrWhiteSpace(manager.UserId))
            {
                throw new ConflictException("Yöneticinin bağlı bir giriş hesabı olmalıdır.");
            }

            if (employeeId is int currentEmployeeId)
            {
                var hierarchy = await context.Employees
                    .AsNoTracking()
                    .Select(candidate => new { candidate.Id, candidate.ManagerId })
                    .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.ManagerId, cancellationToken);
                var visited = new HashSet<int>();
                int? cursor = manager.Id;

                while (cursor is int managerId && visited.Add(managerId))
                {
                    if (managerId == currentEmployeeId)
                    {
                        throw new ConflictException("Yönetici ataması organizasyon hiyerarşisinde döngü oluşturamaz.");
                    }

                    cursor = hierarchy.GetValueOrDefault(managerId);
                }
            }
        }

        if (model.NationalId is not null)
        {
            var nationalIdTaken = await context.Employees.AnyAsync(
                e => e.NationalId == model.NationalId && e.Id != employeeId, cancellationToken);
            if (nationalIdTaken)
            {
                throw new ConflictException("Bu TC Kimlik No ile kayıtlı başka bir personel var.");
            }
        }
    }

    public static EmployeeProfile MapProfile(EmployeeProfile profile, EmployeeProfileUpsertModel? model)
    {
        if (model is null)
        {
            return profile;
        }

        profile.BirthDate = model.BirthDate;
        profile.Gender = model.Gender;
        profile.MaritalStatus = model.MaritalStatus;
        profile.BloodType = model.BloodType;

        profile.MobilePhone = model.MobilePhone;
        profile.PersonalEmail = model.PersonalEmail;
        profile.HomeAddress = model.HomeAddress;
        profile.EmergencyContactName = model.EmergencyContactName;
        profile.EmergencyContactRelation = model.EmergencyContactRelation;
        profile.EmergencyContactPhone = model.EmergencyContactPhone;

        profile.EmploymentType = EmployeeMappings.ParseEmploymentType(model.EmploymentType);
        profile.RehireEligibility = model.RehireEligibility;
        profile.ExitCode = model.ExitCode;

        profile.Iban = model.Iban?.Replace(" ", "");
        profile.BankName = model.BankName;
        profile.SalaryType = model.SalaryType;
        profile.PensionStatus = model.PensionStatus;
        profile.MealCard = model.MealCard;

        profile.TshirtSize = model.TshirtSize;
        profile.PantsSize = model.PantsSize;
        profile.CoatSize = model.CoatSize;
        profile.ShoeSize = model.ShoeSize;
        profile.CanWorkAtHeight = model.CanWorkAtHeight;
        profile.CanWorkNightShift = model.CanWorkNightShift;
        profile.CanLiftHeavyLoads = model.CanLiftHeavyLoads;
        profile.HealthNotes = model.HealthNotes;

        return profile;
    }
}
