using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Compliance;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Compliance.Commands;

// --- belge oluştur ---

public sealed record CreateComplianceDocumentCommand(
    int EmployeeId,
    string DocumentName,
    string? OwnerName = null,
    DateOnly? DueDate = null,
    string Status = "Eksik",
    string Level = "medium") : IRequest<ComplianceDocumentDto>;

public sealed class CreateComplianceDocumentCommandValidator
    : AbstractValidator<CreateComplianceDocumentCommand>
{
    public CreateComplianceDocumentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.DocumentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerName).MaximumLength(128);
        RuleFor(x => x.Status)
            .Must(ComplianceMappings.IsStatus)
            .WithMessage("Belge durumu: Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı.");
        RuleFor(x => x.Level)
            .Must(ComplianceMappings.IsLevel)
            .WithMessage("Risk seviyesi: high | medium | low.");
    }
}

public sealed class CreateComplianceDocumentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateComplianceDocumentCommand, ComplianceDocumentDto>
{
    public async Task<ComplianceDocumentDto> Handle(
        CreateComplianceDocumentCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        // Aynı personel için aynı adlı açık (tamamlanmamış) belge mükerrer açılamaz.
        var documentName = request.DocumentName.Trim();
        var duplicateExists = await context.ComplianceDocuments.AnyAsync(
            d => d.EmployeeId == request.EmployeeId &&
                 d.DocumentName == documentName &&
                 d.Status != ComplianceStatus.Completed,
            cancellationToken);
        if (duplicateExists)
        {
            throw new ConflictException(
                $"'{documentName}' belgesi bu personel için zaten açık durumda.");
        }

        var document = new ComplianceDocument
        {
            Employee = employee,
            EmployeeId = employee.Id,
            DocumentName = documentName,
            OwnerName = request.OwnerName?.Trim(),
            DueDate = request.DueDate,
            Status = ComplianceMappings.ParseStatus(request.Status),
            Level = ComplianceMappings.ParseLevel(request.Level),
        };

        context.ComplianceDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken);

        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- belge güncelle (ad, son tarih, seviye) ---

public sealed record UpdateComplianceDocumentCommand(
    int Id,
    string DocumentName,
    DateOnly? DueDate,
    string Level) : IRequest<ComplianceDocumentDto>;

public sealed class UpdateComplianceDocumentCommandValidator
    : AbstractValidator<UpdateComplianceDocumentCommand>
{
    public UpdateComplianceDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Level)
            .Must(ComplianceMappings.IsLevel)
            .WithMessage("Risk seviyesi: high | medium | low.");
    }
}

public sealed class UpdateComplianceDocumentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateComplianceDocumentCommand, ComplianceDocumentDto>
{
    public async Task<ComplianceDocumentDto> Handle(
        UpdateComplianceDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        document.DocumentName = request.DocumentName.Trim();
        document.DueDate = request.DueDate;
        document.Level = ComplianceMappings.ParseLevel(request.Level);

        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- durum iş akışı ---

public sealed record SetComplianceStatusCommand(int Id, string Status)
    : IRequest<ComplianceDocumentDto>;

public sealed class SetComplianceStatusCommandValidator
    : AbstractValidator<SetComplianceStatusCommand>
{
    public SetComplianceStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(ComplianceMappings.IsStatus)
            .WithMessage("Belge durumu: Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı.");
    }
}

public sealed class SetComplianceStatusCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SetComplianceStatusCommand, ComplianceDocumentDto>
{
    public async Task<ComplianceDocumentDto> Handle(
        SetComplianceStatusCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        var target = ComplianceMappings.ParseStatus(request.Status);
        if (document.Status == target)
        {
            throw new ConflictException($"Belge zaten '{target.ToLabel()}' durumunda.");
        }

        // Tamamlanan belge risk taşımaz; yeniden açılırsa seviye korunur.
        document.Status = target;
        if (target == ComplianceStatus.Completed)
        {
            document.Level = RiskLevel.Low;
        }

        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- sorumlu atama ---

public sealed record AssignComplianceOwnerCommand(int Id, string OwnerName)
    : IRequest<ComplianceDocumentDto>;

public sealed class AssignComplianceOwnerCommandValidator
    : AbstractValidator<AssignComplianceOwnerCommand>
{
    public AssignComplianceOwnerCommandValidator()
    {
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(128);
    }
}

public sealed class AssignComplianceOwnerCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AssignComplianceOwnerCommand, ComplianceDocumentDto>
{
    public async Task<ComplianceDocumentDto> Handle(
        AssignComplianceOwnerCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        document.OwnerName = request.OwnerName.Trim();

        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}
