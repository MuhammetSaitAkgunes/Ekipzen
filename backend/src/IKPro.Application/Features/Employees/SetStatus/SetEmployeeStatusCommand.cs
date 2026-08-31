using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.SetStatus;

public sealed record SetEmployeeStatusCommand(
    int Id,
    string Status,
    DateOnly? TerminationDate = null,
    int? SuccessorManagerId = null)
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

public sealed class SetEmployeeStatusCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<SetEmployeeStatusCommand>
{
    public async Task Handle(SetEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel", request.Id);

        var status = EmployeeMappings.ParseStatus(request.Status);
        if (status == EmployeeStatus.Passive && request.TerminationDate < employee.HireDate)
        {
            throw new ArgumentException("İşten ayrılma tarihi işe giriş tarihinden önce olamaz.");
        }

        var roleOperations = new List<IdentityProvisioningOperation>();
        if (status == EmployeeStatus.Passive)
        {
            var directReports = await context.Employees
                .Where(candidate => candidate.ManagerId == employee.Id)
                .ToListAsync(cancellationToken);

            var successor = await PrepareHandoverAsync(
                employee, directReports, request.SuccessorManagerId, cancellationToken);
            if (successor is not null)
            {
                roleOperations.Add(CreateRoleOperation(
                    successor.Id, IdentityProvisioningOperationType.GrantManagerRole));
            }

            roleOperations.Add(CreateRoleOperation(
                employee.Id, IdentityProvisioningOperationType.RevokeManagerRole));
            context.IdentityProvisioningOperations.AddRange(roleOperations);
        }
        else if (request.SuccessorManagerId is not null)
        {
            throw new ConflictException("Halef yalnız pasife alma işleminde belirtilebilir.");
        }

        employee.Status = status;
        employee.TerminationDate = status == EmployeeStatus.Passive
            ? request.TerminationDate
            : null;
        await context.SaveChangesAsync(cancellationToken);

        // Organizasyon devri ve kalıcı rol niyeti aynı DB commit'indedir. Identity
        // dış etkileri idempotent processor/worker ile tamamlanır.
        foreach (var operation in roleOperations)
        {
            await provisioningProcessor.ProcessNowAsync(operation.Id, cancellationToken);
        }

        // Önce personel kaydı commit edildiği için bu dış adım aksasa bile HTTP
        // erişim kapısı pasif personeli anında reddeder.
        await identityService.SetEmployeeAccessAsync(
            employee.Id,
            employee.Status == EmployeeStatus.Active,
            cancellationToken);
    }

    private async Task<Employee?> PrepareHandoverAsync(
        Employee employee,
        IReadOnlyList<Employee> directReports,
        int? successorManagerId,
        CancellationToken cancellationToken)
    {
        if (directReports.Count == 0)
        {
            if (successorManagerId is not null)
            {
                throw new ConflictException("Ekibi olmayan personel için halef belirtilemez.");
            }

            return null;
        }

        if (successorManagerId is null)
        {
            throw new ConflictException("Ekibi bulunan yönetici için aktif bir halef seçilmelidir.");
        }

        if (successorManagerId == employee.Id)
        {
            throw new ConflictException("Ayrılan yönetici kendi halefi olamaz.");
        }

        var successor = await context.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == successorManagerId, cancellationToken)
            ?? throw new NotFoundException("Halef yönetici", successorManagerId);

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

            // Doğrudan rapor terfi ederse ayrılan yöneticinin üstüne bağlanır;
            // aksi halde kendi kendisinin yöneticisi olurdu.
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

        foreach (var report in directReports)
        {
            if (report.Id != successor.Id)
            {
                report.ManagerId = successor.Id;
            }
        }

        return successor;
    }

    private static IdentityProvisioningOperation CreateRoleOperation(
        int employeeId, IdentityProvisioningOperationType type) => new()
    {
        EmployeeId = employeeId,
        Type = type,
        Status = IdentityProvisioningStatus.Pending,
        NextAttemptAtUtc = DateTime.UtcNow,
    };
}
