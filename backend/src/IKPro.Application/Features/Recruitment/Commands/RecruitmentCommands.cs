using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Recruitment.Queries;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Recruitment;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Recruitment.Commands;

// --- pozisyon oluştur ---

public sealed record CreatePositionCommand(string Title, int? DepartmentId, int OpenCount = 1)
    : IRequest<PositionDto>;

public sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.OpenCount).InclusiveBetween(1, 100);
    }
}

public sealed class CreatePositionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreatePositionCommand, PositionDto>
{
    public async Task<PositionDto> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId is not null &&
            !await context.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken))
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
        await context.SaveChangesAsync(cancellationToken);

        var departmentName = request.DepartmentId is null
            ? null
            : await context.Departments
                .Where(d => d.Id == request.DepartmentId)
                .Select(d => d.Name)
                .SingleAsync(cancellationToken);

        return new PositionDto(
            position.Id, position.Title, position.DepartmentId, departmentName,
            position.IsOpen, position.OpenCount, 0);
    }
}

// --- aday oluştur ---

public sealed record CandidateExperienceInput(
    string Title, string Company, string? Period = null, string? Description = null);

public sealed record CreateCandidateCommand(
    string Name,
    string AppliedRole,
    int? PositionId = null,
    int Score = 0,
    string? Location = null,
    int ExperienceYears = 0,
    string? Summary = null,
    IReadOnlyList<string>? Skills = null,
    IReadOnlyList<CandidateExperienceInput>? Experiences = null) : IRequest<CandidateDetailDto>;

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

public sealed class CreateCandidateCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<CreateCandidateCommand, CandidateDetailDto>
{
    public async Task<CandidateDetailDto> Handle(
        CreateCandidateCommand request, CancellationToken cancellationToken)
    {
        if (request.PositionId is not null &&
            !await context.Positions.AnyAsync(p => p.Id == request.PositionId, cancellationToken))
        {
            throw new NotFoundException("Pozisyon", request.PositionId);
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

        return await sender.Send(new GetCandidateQuery(candidate.Id), cancellationToken);
    }
}

// --- pipeline durum geçişi ---

public sealed record SetCandidateStatusCommand(int Id, string Status) : IRequest<CandidateDetailDto>;

public sealed class SetCandidateStatusCommandValidator : AbstractValidator<SetCandidateStatusCommand>
{
    public SetCandidateStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is "Yeni" or "Mülakat" or "Teklif" or "Red")
            .WithMessage("Pipeline durumu: Yeni | Mülakat | Teklif | Red (işe alım için hire ucu kullanılır).");
    }
}

public sealed class SetCandidateStatusCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<SetCandidateStatusCommand, CandidateDetailDto>
{
    public async Task<CandidateDetailDto> Handle(
        SetCandidateStatusCommand request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aday", request.Id);

        if (candidate.Status == CandidateStatus.Hired)
        {
            throw new ConflictException("İşe alınmış adayın pipeline durumu değiştirilemez.");
        }

        var newStatus = RecruitmentMappings.ParseStatus(request.Status);
        if (newStatus != candidate.Status)
        {
            context.CandidateHistory.Add(new Domain.Entities.Recruitment.CandidateHistory
            {
                CandidateId = candidate.Id,
                Event = $"Durum güncellendi: {candidate.Status.ToDto()} → {newStatus.ToDto()}",
                OccurredAtUtc = DateTime.UtcNow,
            });
            candidate.Status = newStatus;
            await context.SaveChangesAsync(cancellationToken);
        }

        return await sender.Send(new GetCandidateQuery(candidate.Id), cancellationToken);
    }
}

// --- mülakat notu ---

public sealed record AddInterviewNoteCommand(int CandidateId, string NoteType, string Text)
    : IRequest<InterviewNoteDto>;

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

public sealed class AddInterviewNoteCommandHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<AddInterviewNoteCommand, InterviewNoteDto>
{
    public async Task<InterviewNoteDto> Handle(
        AddInterviewNoteCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Candidates.AnyAsync(c => c.Id == request.CandidateId, cancellationToken))
        {
            throw new NotFoundException("Aday", request.CandidateId);
        }

        var note = new InterviewNote
        {
            CandidateId = request.CandidateId,
            AuthorUserId = currentUser.UserId,
            AuthorName = currentUser.UserName ?? "İK",
            NoteType = request.NoteType,
            Text = request.Text.Trim(),
        };

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

public sealed record AddCandidateEvaluationCommand(int CandidateId, string Criterion, int Score, int MaxScore = 5)
    : IRequest<CandidateEvaluationDto>;

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

public sealed class AddCandidateEvaluationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AddCandidateEvaluationCommand, CandidateEvaluationDto>
{
    public async Task<CandidateEvaluationDto> Handle(
        AddCandidateEvaluationCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Candidates.AnyAsync(c => c.Id == request.CandidateId, cancellationToken))
        {
            throw new NotFoundException("Aday", request.CandidateId);
        }

        var evaluation = new CandidateEvaluation
        {
            CandidateId = request.CandidateId,
            Criterion = request.Criterion.Trim(),
            Score = request.Score,
            MaxScore = request.MaxScore,
        };

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
        RuleFor(x => x.Title).MaximumLength(128);
    }
}

public sealed class HireCandidateCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IIdentityProvisioningProcessor provisioningProcessor)
    : IRequestHandler<HireCandidateCommand, HireResultDto>
{
    public async Task<HireResultDto> Handle(HireCandidateCommand request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .Include(c => c.Position)
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId, cancellationToken)
            ?? throw new NotFoundException("Aday", request.CandidateId);

        if (candidate.Status is CandidateStatus.Hired)
        {
            throw new ConflictException("Aday zaten işe alınmış.");
        }

        if (candidate.Status is CandidateStatus.Rejected)
        {
            throw new ConflictException("Reddedilmiş aday işe alınamaz; önce pipeline durumunu güncelleyin.");
        }

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
        }

        await context.SaveChangesAsync(cancellationToken);

        await provisioningProcessor.ProcessNowAsync(provisioning.Id, cancellationToken);
        var currentProvisioning = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .SingleAsync(operation => operation.Id == provisioning.Id, cancellationToken);

        return new HireResultDto(
            candidate.Id, employee.Id, employee.FullName, currentProvisioning.ToDto());
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
