using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Common.Notifications;
using IKPro.Application.Features.Employees.GetEmployee;
using IKPro.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Upsert;

public sealed record CreateEmployeeCommand(EmployeeUpsertModel Model) : IRequest<EmployeeDetailDto>;

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
    IApplicationDbContext context, ISender sender, INotificationTrigger notificationTrigger)
    : IRequestHandler<CreateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;

        await EmployeeUpsertGuards.EnsureReferencesAsync(context, model, employeeId: null, cancellationToken);

        var employee = new Employee
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Title = model.Title.Trim(),
            NationalId = model.NationalId,
            DepartmentId = model.DepartmentId,
            ManagerId = model.ManagerId,
            HireDate = model.HireDate,
            Status = EmployeeMappings.ParseStatus(model.Status),
            Profile = EmployeeUpsertGuards.MapProfile(new EmployeeProfile(), model.Profile),
        };

        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);

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

            if (!await context.Employees.AnyAsync(e => e.Id == model.ManagerId, cancellationToken))
            {
                throw new NotFoundException("Yönetici", model.ManagerId);
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
