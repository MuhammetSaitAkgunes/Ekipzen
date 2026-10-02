using Ekipzen.Application.Features.Guidance;
using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Recruitment.Queries;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Recruitment;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Recruitment.Commands;

// --- pozisyon oluştur ---

public sealed record CreatePositionCommand(string Title, int? DepartmentId, int OpenCount = 1, int? GuidanceContentRevisionId = null)
    : IRequest<PositionDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "position-create";
}

public sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.OpenCount).InclusiveBetween(1, 100);
    }
}

public sealed class CreatePositionCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<CreatePositionCommand, PositionDto>
{
    public Task<PositionDto> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("recruitment-position-create", async ct =>
    {
        if (request.DepartmentId is not null &&
            !await context.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
        {
            throw new NotFoundException("Departman", request.DepartmentId);
        }

        var position = new Position
        {
            Title = request.Title.Trim(),
            DepartmentId = request.DepartmentId,
            OpenCount = request.OpenCount,
            IsOpen = true,
        };

        context.Positions.Add(position);
        await context.SaveChangesAsync(ct);

        var departmentName = request.DepartmentId is null
            ? null
            : await context.Departments
                .Where(d => d.Id == request.DepartmentId)
                .Select(d => d.Name)
                .SingleAsync(ct);

        await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "position", position.Id,
            ["Pozisyon adı", "Departman", "Açık kadro"], ct);
        return new PositionDto(
            position.Id, position.Title, position.DepartmentId, departmentName,
            position.IsOpen, position.OpenCount, 0, position.Version);
    }, cancellationToken);
}

// --- aday oluştur ---

public sealed record CandidateExperienceInput(
    string Title, string Company, string? Period = null, string? Description = null);

public sealed record CreateCandidateCommand(
    string Name,
    string AppliedRole,
    int? PositionId = null,
    int? Score = null,
    string? Location = null,
    int ExperienceYears = 0,
    string? Summary = null,
    IReadOnlyList<string>? Skills = null,
    IReadOnlyList<CandidateExperienceInput>? Experiences = null, int? GuidanceContentRevisionId = null)
    : IRequest<CandidateDetailDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "candidate-create";
}

public sealed class CreateCandidateCommandValidator : AbstractValidator<CreateCandidateCommand>
{
    public CreateCandidateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AppliedRole).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
        RuleFor(x => x.ExperienceYears).InclusiveBetween(0, 60);
        RuleFor(x => x.Summary).MaximumLength(2000);
    }
}

public sealed class CreateCandidateCommandHandler(
    IApplicationDbContext context, ISender sender, IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<CreateCandidateCommand, CandidateDetailDto>
{
    public Task<CandidateDetailDto> Handle(
        CreateCandidateCommand request, CancellationToken cancellationToken)
        => request.PositionId is { } positionId
            ? databaseLock.ExecuteExclusiveAsync($"recruitment-position:{positionId}",
                token => CreateAsync(request, token), cancellationToken)
            : databaseLock.ExecuteExclusiveAsync("recruitment-candidate-create", token => CreateAsync(request, token), cancellationToken);

    private async Task<CandidateDetailDto> CreateAsync(
        CreateCandidateCommand request, CancellationToken cancellationToken)
    {
        if (request.PositionId is { } positionId)
        {
            var position = await context.Positions.AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == positionId, cancellationToken)
                ?? throw new NotFoundException("Pozisyon", positionId);
            if (!position.IsOpen || position.OpenCount < 1)
                throw new ConflictException("Pozisyon kapalı veya açık kadrosu yok; aday bağlantısı kurulamaz.");
        }

        var candidate = new Candidate
        {
            Name = request.Name.Trim(),
            AppliedRole = request.AppliedRole.Trim(),
            PositionId = request.PositionId,
            Status = CandidateStatus.New,
            Score = request.Score,
            AppliedAtUtc = DateTime.UtcNow,
            Location = request.Location,
            ExperienceYears = request.ExperienceYears,
            Summary = request.Summary,
        };

        foreach (var skill in request.Skills ?? [])
        {
            candidate.Skills.Add(new CandidateSkill { Name = skill.Trim() });
        }

        foreach (var xp in request.Experiences ?? [])
        {
            candidate.Experiences.Add(new CandidateExperience
            {
                Title = xp.Title.Trim(),
                Company = xp.Company.Trim(),
                Period = xp.Period,
                Description = xp.Description,
            });
        }

        candidate.History.Add(new CandidateHistory
        {
            Event = "Başvuru alındı",
            OccurredAtUtc = DateTime.UtcNow,
        });

        context.Candidates.Add(candidate);
        await context.SaveChangesAsync(cancellationToken);

        await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "candidate", candidate.Id,
            ["Başvuru bilgileri", "Pozisyon", "Puan / değerlendirilmedi"], cancellationToken);
        return await sender.Send(new GetCandidateQuery(candidate.Id), cancellationToken);
    }
}

// --- pipeline durum geçişi ---

public sealed record SetCandidateStatusCommand(int Id, string Status, int ExpectedVersion, string Reason,
    IReadOnlyList<CandidateDecisionEvidenceReference>? Evidence = null, int? GuidanceContentRevisionId = null)
    : IRequest<CandidateDetailDto>, Ekipzen.Application.Features.Guidance.IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "candidate-first-review";
}

public sealed class SetCandidateStatusCommandValidator : AbstractValidator<SetCandidateStatusCommand>
{
    public SetCandidateStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Evidence).Must(items => items is null || (items.Count <= 20 && items.All(item => item is not null)
            && items.DistinctBy(item => (item.Kind, item.Id)).Count() == items.Count))
            .WithMessage("En fazla 20 farklı dayanak kaydı seçilebilir.");
        RuleForEach(x => x.Evidence).ChildRules(item =>
        {
            item.RuleFor(x => x.Kind).Must(kind => kind is "note" or "evaluation");
            item.RuleFor(x => x.Id).GreaterThan(0);
            item.RuleFor(x => x.Version).GreaterThan(0);
        });
        RuleFor(x => x.Status)
            .Must(s => s is "Yeni" or "Mülakat" or "Teklif" or "Red")
            .WithMessage("Pipeline durumu: Yeni | Mülakat | Teklif | Red (işe alım için hire ucu kullanılır).");
    }
}

public sealed class SetCandidateStatusCommandHandler(
    IApplicationDbContext context, ISender sender, ICurrentUser currentUser, IDatabaseLock databaseLock,
    Ekipzen.Application.Features.Guidance.GuidanceOperationContext guidance)
    : IRequestHandler<SetCandidateStatusCommand, CandidateDetailDto>
{
    public Task<CandidateDetailDto> Handle(
        SetCandidateStatusCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"recruitment-candidate:{request.Id}",
            token => ChangeAsync(request, token), cancellationToken);

    private async Task<CandidateDetailDto> ChangeAsync(
        SetCandidateStatusCommand request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aday", request.Id);

        if (candidate.Version != request.ExpectedVersion)
            throw new ConflictException("Aday kaydı değişti; güncel kayıt ve dayanaklarla aşama kararını yeniden inceleyin.");

        if (candidate.Status == CandidateStatus.Hired)
        {
            throw new ConflictException("İşe alınmış adayın pipeline durumu değiştirilemez.");
        }

        var newStatus = RecruitmentMappings.ParseStatus(request.Status);
        if (guidance.IsGuided && candidate.Status != CandidateStatus.New)
            throw new ConflictException("İlk inceleme rehberi yalnız Yeni aşamasındaki aday kararına bağlanabilir.");
        if (newStatus != candidate.Status)
        {
            var userId = currentUser.UserId
                ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
            var evidence = new List<CandidateDecisionEvidenceDto>();
            foreach (var reference in request.Evidence ?? [])
            {
                var isNote = reference.Kind == "note";
                var original = await CandidateEvidenceVersions.OriginalAsync(context,
                    candidate.Id, reference.Id, isNote, cancellationToken);
                var latest = await CandidateEvidenceVersions.Revisions(context,
                        candidate.Id, reference.Id, isNote)
                    .AsNoTracking().OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
                if ((latest?.Version ?? 1) != reference.Version)
                    throw new ConflictException("Seçilen dayanak sürümü değişti; güncel dayanağı yeniden inceleyin.");
                evidence.Add(new(reference.Kind, reference.Id, reference.Version,
                    latest is null ? original : CandidateEvidenceVersions.Read(latest.AfterJson)));
            }
            context.CandidateStageDecisions.Add(new CandidateStageDecision
            {
                CandidateId = candidate.Id, FromStatus = candidate.Status, ToStatus = newStatus,
                FromVersion = candidate.Version, ToVersion = candidate.Version + 1,
                Reason = request.Reason.Trim(), EvidenceJson = JsonSerializer.Serialize(evidence),
                DecidedByUserId = userId, DecidedByName = currentUser.UserName ?? "İK",
                DecidedAtUtc = DateTime.UtcNow,
            });
            if (candidate.Status == CandidateStatus.New)
            {
                var alreadyVerified = await context.CandidateReviewEvidences
                    .AnyAsync(item => item.CandidateId == candidate.Id && item.UserId == userId, cancellationToken);
                if (!alreadyVerified)
                {
                    var reviewEvidence = new CandidateReviewEvidence
                    {
                        CandidateId = candidate.Id,
                        UserId = userId,
                        DecisionStatus = newStatus,
                    };
                    guidance.Stamp(reviewEvidence);
                    context.CandidateReviewEvidences.Add(reviewEvidence);
                }
            }

            context.CandidateHistory.Add(new Domain.Entities.Recruitment.CandidateHistory
            {
                CandidateId = candidate.Id,
                Event = $"Durum güncellendi: {candidate.Status.ToDto()} → {newStatus.ToDto()}",
                OccurredAtUtc = DateTime.UtcNow,
            });
            candidate.Status = newStatus;
            candidate.Version++;
            await context.SaveChangesAsync(cancellationToken);
        }

        return await sender.Send(new GetCandidateQuery(candidate.Id), cancellationToken);
    }
}

// --- mülakat notu ---

public sealed record AddInterviewNoteCommand(int CandidateId, string NoteType, string Text, int? GuidanceContentRevisionId = null)
    : IRequest<InterviewNoteDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "candidate-note-create";
}

public sealed class AddInterviewNoteCommandValidator : AbstractValidator<AddInterviewNoteCommand>
{
    public AddInterviewNoteCommandValidator()
    {
        RuleFor(x => x.NoteType)
            .Must(t => t is "Teknik Mülakat" or "İK Görüşmesi")
            .WithMessage("Not türü: Teknik Mülakat | İK Görüşmesi.");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public sealed class AddInterviewNoteCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<AddInterviewNoteCommand, InterviewNoteDto>
{
    public Task<InterviewNoteDto> Handle(
        AddInterviewNoteCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"recruitment-candidate:{request.CandidateId}",
            token => AddAsync(request, token), cancellationToken);

    private async Task<InterviewNoteDto> AddAsync(
        AddInterviewNoteCommand request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .SingleOrDefaultAsync(c => c.Id == request.CandidateId, cancellationToken)
            ?? throw new NotFoundException("Aday", request.CandidateId);
        if (candidate.Status == CandidateStatus.Hired)
            throw new ConflictException("İşe alınmış adayın başvuru geçmişi kilitli; yeni mülakat notu eklenemez.");
        candidate.Version++;

        var note = new InterviewNote
        {
            CandidateId = request.CandidateId,
            AuthorUserId = currentUser.UserId,
            AuthorName = currentUser.UserName ?? "İK",
            NoteType = request.NoteType,
            Text = request.Text.Trim(),
        };

        guidance.Stamp(note);
        context.InterviewNotes.Add(note);
        context.CandidateHistory.Add(new Domain.Entities.Recruitment.CandidateHistory
        {
            CandidateId = request.CandidateId,
            Event = $"{request.NoteType} notu eklendi",
            OccurredAtUtc = DateTime.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);

        return new InterviewNoteDto(note.Id, note.AuthorName, note.NoteType, note.Text, note.CreatedAtUtc);
    }
}

// --- değerlendirme ---

public sealed record AddCandidateEvaluationCommand(int CandidateId, string Criterion, int Score, int MaxScore = 5, int? GuidanceContentRevisionId = null)
    : IRequest<CandidateEvaluationDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "candidate-evaluation-create";
}

public sealed class AddCandidateEvaluationCommandValidator : AbstractValidator<AddCandidateEvaluationCommand>
{
    public AddCandidateEvaluationCommandValidator()
    {
        RuleFor(x => x.Criterion).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MaxScore).InclusiveBetween(1, 10);
        RuleFor(x => x.Score)
            .Must((m, score) => score >= 0 && score <= m.MaxScore)
            .WithMessage("Puan 0 ile azami puan arasında olmalı.");
    }
}

public sealed class AddCandidateEvaluationCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock, ICurrentUser currentUser, GuidanceOperationContext guidance)
    : IRequestHandler<AddCandidateEvaluationCommand, CandidateEvaluationDto>
{
    public Task<CandidateEvaluationDto> Handle(
        AddCandidateEvaluationCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"recruitment-candidate:{request.CandidateId}",
            token => AddAsync(request, token), cancellationToken);

    private async Task<CandidateEvaluationDto> AddAsync(
        AddCandidateEvaluationCommand request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .SingleOrDefaultAsync(c => c.Id == request.CandidateId, cancellationToken)
            ?? throw new NotFoundException("Aday", request.CandidateId);
        if (candidate.Status == CandidateStatus.Hired)
            throw new ConflictException("İşe alınmış adayın başvuru geçmişi kilitli; yeni değerlendirme eklenemez.");
        candidate.Version++;

        var evaluation = new CandidateEvaluation
        {
            CandidateId = request.CandidateId,
            Criterion = request.Criterion.Trim(),
            Score = request.Score,
            MaxScore = request.MaxScore,
        };

        if (guidance.IsGuided)
        {
            evaluation.GuidanceUserId = currentUser.UserId ?? throw new ForbiddenAccessException("Değerlendirme için oturum sahibi gerekli.");
            evaluation.GuidanceOccurredAtUtc = DateTime.UtcNow;
            guidance.Stamp(evaluation);
        }
        context.CandidateEvaluations.Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);

        return new CandidateEvaluationDto(evaluation.Id, evaluation.Criterion, evaluation.Score, evaluation.MaxScore);
    }
}

// --- işe al → Employee dönüşümü ---

public sealed record HireCandidateCommand(
    int CandidateId,
    int DepartmentId,
    string Email,
    string ContractType,
    string? Title = null,
    DateOnly? HireDate = null) : IRequest<HireResultDto>;

public sealed record HireResultDto(
    int CandidateId, int EmployeeId, string EmployeeName, IdentityProvisioningDto Provisioning);

public sealed class HireCandidateCommandValidator : AbstractValidator<HireCandidateCommand>
{
    public HireCandidateCommandValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContractType)
            .Must(value => value is "indefinite" or "fixed-term")
            .WithMessage("Sözleşme türü 'indefinite' veya 'fixed-term' olmalı.");
        RuleFor(x => x.Title).MaximumLength(128);
    }
}

public sealed class HireCandidateCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IIdentityProvisioningProcessor provisioningProcessor,
    IDatabaseLock databaseLock)
    : IRequestHandler<HireCandidateCommand, HireResultDto>
{
    public async Task<HireResultDto> Handle(HireCandidateCommand request, CancellationToken cancellationToken)
    {
        var positionId = await context.Candidates.AsNoTracking()
            .Where(candidate => candidate.Id == request.CandidateId)
            .Select(candidate => candidate.PositionId)
            .SingleOrDefaultAsync(cancellationToken);
        var resources = positionId is { } id
            ? new[] { $"recruitment-candidate:{request.CandidateId}", $"recruitment-position:{id}" }
            : new[] { $"recruitment-candidate:{request.CandidateId}" };
        var persisted = await databaseLock.ExecuteExclusiveAsync(resources,
            token => HireLockedAsync(request, positionId, token), cancellationToken);
        await provisioningProcessor.ProcessNowAsync(persisted.ProvisioningId, cancellationToken);
        var currentProvisioning = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .SingleAsync(operation => operation.Id == persisted.ProvisioningId, cancellationToken);
        return new HireResultDto(persisted.CandidateId, persisted.EmployeeId,
            persisted.EmployeeName, currentProvisioning.ToDto());
    }

    private sealed record HirePersisted(int CandidateId, int EmployeeId,
        string EmployeeName, int ProvisioningId);

    private async Task<HirePersisted> HireLockedAsync(
        HireCandidateCommand request, int? expectedPositionId, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .Include(c => c.Position)
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId, cancellationToken)
            ?? throw new NotFoundException("Aday", request.CandidateId);

        if (candidate.PositionId != expectedPositionId)
            throw new ConflictException("Adayın pozisyon bağlantısı değişti; yeniden inceleyin.");
        if (candidate.Status is CandidateStatus.Hired)
        {
            throw new ConflictException("Aday zaten işe alınmış.");
        }

        if (candidate.Status is CandidateStatus.Rejected)
        {
            throw new ConflictException("Reddedilmiş aday işe alınamaz; önce pipeline durumunu güncelleyin.");
        }

        if (candidate.Position is not null
            && (!candidate.Position.IsOpen || candidate.Position.OpenCount < 1))
            throw new ConflictException("Bağlı pozisyon kapalı veya açık kadrosu yok; işe alma tamamlanamaz.");

        if (!await context.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken))
        {
            throw new NotFoundException("Departman", request.DepartmentId);
        }

        // Login e-postasını önce doğrula — yazımlar başlamadan çakışmayı yakala (orphan önlenir).
        if (await identityService.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new ConflictException($"'{request.Email}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        var nameParts = candidate.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = nameParts.Length > 1 ? string.Join(' ', nameParts[..^1]) : nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[^1] : string.Empty;

        var hireDate = request.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var employee = new Domain.Entities.Organization.Employee
        {
            FirstName = firstName,
            LastName = lastName,
            Title = request.Title ?? candidate.AppliedRole,
            DepartmentId = request.DepartmentId,
            HireDate = hireDate,
            ContractType = EmployeeEmploymentMappings.ParseContractType(request.ContractType),
            Status = EmployeeStatus.Active,
            Profile = new Domain.Entities.Organization.EmployeeProfile(),
        };
        employee.EmploymentPeriods.Add(new Domain.Entities.Organization.EmployeeEmploymentPeriod
        {
            StartDate = hireDate,
            ContractType = employee.ContractType,
        });
        context.Employees.Add(employee);

        var provisioning = new IdentityProvisioningOperation
        {
            Type = IdentityProvisioningOperationType.EmployeeLogin,
            Status = IdentityProvisioningStatus.Pending,
            Employee = employee,
            Candidate = candidate,
            Email = request.Email.Trim(),
            DisplayName = candidate.Name,
            NextAttemptAtUtc = DateTime.UtcNow,
        };
        context.IdentityProvisioningOperations.Add(provisioning);

        candidate.Status = CandidateStatus.Hired;
        candidate.Version++;
        candidate.History.Add(new CandidateHistory
        {
            Event = "İşe alındı — personel kaydı oluşturuldu",
            OccurredAtUtc = DateTime.UtcNow,
        });

        // Pozisyon kontenjanı düşer; kontenjan biterse ilan kapanır.
        if (candidate.Position is not null)
        {
            candidate.Position.OpenCount = Math.Max(0, candidate.Position.OpenCount - 1);
            candidate.Position.IsOpen = candidate.Position.OpenCount > 0;
            candidate.Position.Version++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return new HirePersisted(candidate.Id, employee.Id, employee.FullName, provisioning.Id);
    }
}

public sealed record RetryCandidateProvisioningCommand(int CandidateId, string? Email = null)
    : IRequest<IdentityProvisioningDto>;

public sealed class RetryCandidateProvisioningCommandValidator
    : AbstractValidator<RetryCandidateProvisioningCommand>
{
    public RetryCandidateProvisioningCommandValidator()
    {
        RuleFor(command => command.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}

public sealed class RetryCandidateProvisioningCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<RetryCandidateProvisioningCommand, IdentityProvisioningDto>
{
    public async Task<IdentityProvisioningDto> Handle(
        RetryCandidateProvisioningCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await context.IdentityProvisioningOperations
            .Where(item => item.CandidateId == request.CandidateId
                && item.Type == IdentityProvisioningOperationType.EmployeeLogin)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Aday login provizyonu", request.CandidateId);

        if (operation.Status == IdentityProvisioningStatus.Completed)
        {
            throw new ConflictException("Aday login provizyonu zaten tamamlanmış.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !string.Equals(operation.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var email = request.Email.Trim();
            if (await identityService.EmailExistsAsync(email, cancellationToken))
            {
                throw new ConflictException($"'{email}' e-postasıyla kayıtlı bir hesap zaten var.");
            }

            operation.Email = email;
        }

        operation.Status = IdentityProvisioningStatus.Pending;
        operation.NextAttemptAtUtc = DateTime.UtcNow;
        operation.LeaseUntilUtc = null;
        operation.LastError = null;
        await context.SaveChangesAsync(cancellationToken);

        await provisioningProcessor.ProcessNowAsync(operation.Id, cancellationToken);
        var current = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .SingleAsync(item => item.Id == operation.Id, cancellationToken);
        return current.ToDto();
    }
}
