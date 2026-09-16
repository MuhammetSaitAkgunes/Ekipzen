using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.SetStatus;

public sealed record SetEmployeeStatusCommand(
    int Id,
    string Status,
    DateOnly? TerminationDate = null,
    int? SuccessorManagerId = null,
    DateOnly? RehireDate = null)
    : IRequest;

public sealed class SetEmployeeStatusCommandValidator : AbstractValidator<SetEmployeeStatusCommand>
{
    public SetEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is "active" or "passive")
            .WithMessage("Durum 'active' veya 'passive' olmalı.");

        RuleFor(x => x.TerminationDate)
            .NotNull()
            .When(x => x.Status == "passive")
            .WithMessage("Pasife alınan personel için işten ayrılma tarihi zorunludur.");

        RuleFor(x => x.SuccessorManagerId)
            .GreaterThan(0)
            .When(x => x.SuccessorManagerId is not null)
            .WithMessage("Halef yönetici kimliği pozitif olmalıdır.");
    }
}

/// <summary>
/// Eski durum ucu yalnız süreç kaydı bulunmayan legacy pasif personeli yeniden
/// aktifleştirebilir. Yeni pasifleştirme ve tamamlanmış çıkışın geri alınması
/// kalıcı offboarding dosyası üzerinden yürür; bu uç süreç kapısını aşamaz.
/// </summary>
public sealed class SetEmployeeStatusCommandHandler(
    IApplicationDbContext context,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<SetEmployeeStatusCommand>
{
    public async Task Handle(SetEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(item => item.EmploymentPeriods)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel", request.Id);

        var status = EmployeeMappings.ParseStatus(request.Status);
        if (status == EmployeeStatus.Passive)
        {
            throw new ConflictException(
                "İşten çıkarma için finansal kapanış içeren işten çıkış dosyası kullanılmalıdır.");
        }

        if (request.SuccessorManagerId is not null)
        {
            throw new ConflictException("Halef yalnız işten çıkış dosyasında belirtilebilir.");
        }

        if (employee.Status == EmployeeStatus.Active)
        {
            return;
        }

        var completedCaseExists = await context.EmployeeOffboardingCases
            .AnyAsync(item => item.EmployeeId == employee.Id
                && item.Status == EmployeeOffboardingStatus.Completed,
                cancellationToken);
        if (completedCaseExists)
        {
            throw new ConflictException(
                "Tamamlanmış işten çıkış yalnız kontrollü geri alma işlemiyle açılabilir.");
        }

        var openPeriod = employee.EmploymentPeriods.SingleOrDefault(period => period.EndDate == null);
        if (openPeriod is null)
        {
            if (request.RehireDate is null)
            {
                throw new ConflictException("Yeniden işe alma tarihi zorunludur.");
            }

            var lastEndDate = employee.EmploymentPeriods
                .Where(period => period.EndDate is not null)
                .Max(period => period.EndDate);
            if (request.RehireDate <= lastEndDate)
            {
                throw new ConflictException("Yeniden işe alma tarihi önceki istihdam döneminden sonra olmalıdır.");
            }

            employee.HireDate = request.RehireDate.Value;
            employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
            {
                StartDate = request.RehireDate.Value,
                ContractType = employee.ContractType,
            });
        }

        employee.Status = EmployeeStatus.Active;
        employee.TerminationDate = null;

        var accessOperation = new IdentityProvisioningOperation
        {
            EmployeeId = employee.Id,
            Type = IdentityProvisioningOperationType.EnableEmployeeAccess,
            Status = IdentityProvisioningStatus.Pending,
            NextAttemptAtUtc = DateTime.UtcNow,
        };
        context.IdentityProvisioningOperations.Add(accessOperation);
        await context.SaveChangesAsync(cancellationToken);

        await provisioningProcessor.ProcessNowAsync(accessOperation.Id, cancellationToken);
    }
}