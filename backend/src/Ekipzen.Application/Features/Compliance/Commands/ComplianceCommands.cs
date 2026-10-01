using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Guidance;
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
    string Level = "medium", int? GuidanceContentRevisionId = null) : IRequest<ComplianceDocumentDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "compliance-create"; }

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

public sealed class CreateComplianceDocumentCommandHandler(IApplicationDbContext context, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock)
    : IRequestHandler<CreateComplianceDocumentCommand, ComplianceDocumentDto>
{
    public Task<ComplianceDocumentDto> Handle(CreateComplianceDocumentCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"compliance-create:{request.EmployeeId}", ct => ExecuteAsync(request, ct), cancellationToken);

    private async Task<ComplianceDocumentDto> ExecuteAsync(CreateComplianceDocumentCommand request, CancellationToken cancellationToken)
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
        ComplianceOperationEvidence.Record(context, user, guidance, document, request.GuidanceOperationKey, "created");
        await context.SaveChangesAsync(cancellationToken);

        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- belge güncelle (ad, son tarih, seviye) ---

public sealed record UpdateComplianceDocumentCommand(
    int Id,
    string DocumentName,
    DateOnly? DueDate,
    string Level, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null, string? OwnerName = null, bool UpdateOwner = false) : IRequest<ComplianceDocumentDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "compliance-update"; }

public sealed class UpdateComplianceDocumentCommandValidator
    : AbstractValidator<UpdateComplianceDocumentCommand>
{
    public UpdateComplianceDocumentCommandValidator()
    {
        RuleFor(x => x.ExpectedRevision).NotEmpty().MaximumLength(64);
        RuleFor(x => x.OwnerName).MaximumLength(128);
        RuleFor(x => x.DocumentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Level)
            .Must(ComplianceMappings.IsLevel)
            .WithMessage("Risk seviyesi: high | medium | low.");
    }
}

public sealed class UpdateComplianceDocumentCommandHandler(IApplicationDbContext context, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock)
    : IRequestHandler<UpdateComplianceDocumentCommand, ComplianceDocumentDto>
{
    public Task<ComplianceDocumentDto> Handle(UpdateComplianceDocumentCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"compliance-document:{request.Id}", ct => ExecuteAsync(request, ct), cancellationToken);

    private async Task<ComplianceDocumentDto> ExecuteAsync(UpdateComplianceDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        ComplianceMappings.EnsureRevision(document, request.ExpectedRevision);
        var owner = request.UpdateOwner ? (string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim()) : document.OwnerName;
        if (document.DocumentName == request.DocumentName.Trim() && document.DueDate == request.DueDate
            && document.Level == ComplianceMappings.ParseLevel(request.Level) && document.OwnerName == owner)
            return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
        if (document.Status != ComplianceStatus.Completed && await context.ComplianceDocuments.AnyAsync(x => x.Id != document.Id && x.EmployeeId == document.EmployeeId && x.DocumentName == request.DocumentName.Trim() && x.Status != ComplianceStatus.Completed, cancellationToken))
            throw new ConflictException("Aynı personel için bu adla açık bir belge kaydı zaten var.");
        document.OwnerName = owner;
        document.DocumentName = request.DocumentName.Trim();
        document.DueDate = request.DueDate;
        document.Level = ComplianceMappings.ParseLevel(request.Level);

        ComplianceOperationEvidence.Record(context, user, guidance, document, request.GuidanceOperationKey, "updated");
        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- durum iş akışı ---

public sealed record SetComplianceStatusCommand(int Id, string Status, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null)
    : IRequest<ComplianceDocumentDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "compliance-status"; }

public sealed class SetComplianceStatusCommandValidator
    : AbstractValidator<SetComplianceStatusCommand>
{
    public SetComplianceStatusCommandValidator()
    {
        RuleFor(x => x.ExpectedRevision).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status)
            .Must(ComplianceMappings.IsStatus)
            .WithMessage("Belge durumu: Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı.");
    }
}

public sealed class SetComplianceStatusCommandHandler(IApplicationDbContext context, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock)
    : IRequestHandler<SetComplianceStatusCommand, ComplianceDocumentDto>
{
    public Task<ComplianceDocumentDto> Handle(SetComplianceStatusCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"compliance-document:{request.Id}", ct => ExecuteAsync(request, ct), cancellationToken);

    private async Task<ComplianceDocumentDto> ExecuteAsync(SetComplianceStatusCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        ComplianceMappings.EnsureRevision(document, request.ExpectedRevision);
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

        ComplianceOperationEvidence.Record(context, user, guidance, document, request.GuidanceOperationKey, document.Status.ToString());
        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

// --- sorumlu atama ---

public sealed record AssignComplianceOwnerCommand(int Id, string OwnerName, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null)
    : IRequest<ComplianceDocumentDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "compliance-owner"; }

public sealed class AssignComplianceOwnerCommandValidator
    : AbstractValidator<AssignComplianceOwnerCommand>
{
    public AssignComplianceOwnerCommandValidator()
    {
        RuleFor(x => x.ExpectedRevision).NotEmpty().MaximumLength(64);
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(128);
    }
}

public sealed class AssignComplianceOwnerCommandHandler(IApplicationDbContext context, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock)
    : IRequestHandler<AssignComplianceOwnerCommand, ComplianceDocumentDto>
{
    public Task<ComplianceDocumentDto> Handle(AssignComplianceOwnerCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"compliance-document:{request.Id}", ct => ExecuteAsync(request, ct), cancellationToken);

    private async Task<ComplianceDocumentDto> ExecuteAsync(AssignComplianceOwnerCommand request, CancellationToken cancellationToken)
    {
        var document = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Uyum belgesi", request.Id);

        ComplianceMappings.EnsureRevision(document, request.ExpectedRevision);
        if (document.OwnerName == request.OwnerName.Trim())
            return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
        document.OwnerName = request.OwnerName.Trim();

        ComplianceOperationEvidence.Record(context, user, guidance, document, request.GuidanceOperationKey, "owner-assigned");
        await context.SaveChangesAsync(cancellationToken);
        return document.ToDto(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }
}

internal static class ComplianceOperationEvidence
{
    public static void Record(IApplicationDbContext context, ICurrentUser user, GuidanceOperationContext guidance,
        ComplianceDocument document, string operation, string outcome)
    {
        if (!guidance.IsGuided) return;
        var evidence = new GuidanceComplianceEvidence { UserId = PersonnelEntryGuide.RequireUserId(user), ComplianceDocument = document,
            OperationKey = operation, Outcome = outcome };
        guidance.Stamp(evidence);
        context.GuidanceComplianceEvidences.Add(evidence);
    }
}
