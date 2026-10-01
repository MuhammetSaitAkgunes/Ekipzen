using FluentValidation;
using System.Text.Json;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.GetEmployee;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Upsert;

public sealed record UpdateEmployeeCommand(int Id, EmployeeUpsertModel Model) : IRequest<EmployeeDetailDto>, IGuidanceOperationCommand
{
    public int? GuidanceContentRevisionId => Model.GuidanceContentRevisionId;
    public string GuidanceOperationKey => "employee-update";
}

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new EmployeeUpsertModelValidator());
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model.CorrectionReason).MaximumLength(500);
            RuleFor(x => x.Model.ExpectedRecordToken).Length(64).When(x => x.Model.ExpectedRecordToken is not null);
            When(x => x.Model.GuidanceContentRevisionId is not null, () =>
            {
                RuleFor(x => x.Model.ExpectedRecordToken).NotEmpty();
                RuleFor(x => x.Model.CorrectionReason).NotEmpty().WithMessage("Rehberli düzeltmede gerekçe yazın.");
            });
        });
    }
}

public sealed class UpdateEmployeeCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IIdentityProvisioningProcessor provisioningProcessor, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var roleOperations = await databaseLock.ExecuteExclusiveAsync("employee-organization", async token =>
        {
            var employee = await context.Employees
                .Include(e => e.Profile)
                .Include(e => e.EmploymentPeriods)
                .FirstOrDefaultAsync(e => e.Id == request.Id, token)
                ?? throw new NotFoundException("Personel", request.Id);

            var model = request.Model;
            if (model.ExpectedRecordToken is not null && model.ExpectedRecordToken != EmployeeRecordChanges.Token(employee))
                throw new ConflictException("Personel kartı bu sırada değişti; formunuz korunur. Güncel kartı inceleyip düzeltmeyi yeniden hazırlayın.");
            var before = EmployeeRecordChanges.Capture(employee);
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

            await EmployeeUpsertGuards.EnsureReferencesAsync(context, model, employee.Id, token);

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
                            && candidate.Id != employee.Id, token);
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
                    token))
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

            var fields = EmployeeRecordChanges.Changed(before, employee);
            if (fields.Length > 0)
            {
                var evidence = new GuidancePersonnelEvidence
                {
                    UserId = user.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi."),
                    EmployeeId = employee.Id, OperationKey = "employee-update",
                    ChangedFieldsJson = JsonSerializer.Serialize(fields), Reason = model.CorrectionReason?.Trim(),
                };
                guidance.Stamp(evidence);
                context.GuidancePersonnelEvidences.Add(evidence);
            }
            await context.SaveChangesAsync(token);
            return roleOperations;


        }, cancellationToken);

        await ManagerRoleProvisioning.ProcessNowAsync(provisioningProcessor, roleOperations, cancellationToken);

        return await sender.Send(new GetEmployeeQuery(request.Id), cancellationToken);
    }
}
