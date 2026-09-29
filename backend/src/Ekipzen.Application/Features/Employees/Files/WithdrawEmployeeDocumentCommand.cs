using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

public sealed record WithdrawEmployeeDocumentCommand(int EmployeeId, int DocumentId, string Reason)
    : IRequest<WithdrawnEmployeeDocumentDto>;

public sealed class WithdrawEmployeeDocumentCommandValidator : AbstractValidator<WithdrawEmployeeDocumentCommand>
{
    public WithdrawEmployeeDocumentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class WithdrawEmployeeDocumentCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<WithdrawEmployeeDocumentCommand, WithdrawnEmployeeDocumentDto>
{
    public async Task<WithdrawnEmployeeDocumentDto> Handle(
        WithdrawEmployeeDocumentCommand request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, request.EmployeeId, cancellationToken);

        var document = await context.EmployeeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == request.DocumentId && d.EmployeeId == request.EmployeeId,
                cancellationToken)
            ?? throw new NotFoundException("Evrak", request.DocumentId);

        if (document.WithdrawnAtUtc is not null)
        {
            throw new ConflictException("Evrak zaten geri çekilmiş; geçmişi yenileyin.");
        }

        var actorId = currentUser.UserId
            ?? throw new InvalidOperationException("Evrak geri çekme için oturum sahibi gerekli.");
        var now = DateTime.UtcNow;
        var updated = await context.EmployeeDocuments
            .Where(d => d.Id == request.DocumentId && d.EmployeeId == request.EmployeeId
                && d.WithdrawnAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.WithdrawnAtUtc, now)
                .SetProperty(d => d.WithdrawnByUserId, actorId)
                .SetProperty(d => d.WithdrawalReason, request.Reason.Trim()), cancellationToken);
        if (updated == 0)
        {
            throw new ConflictException("Evrak bu sırada geri çekildi; geçmişi yenileyin.");
        }

        // Dosya depoda korunur. Normal liste ve indirme geri çekilenleri dışlar;
        // kalıcı silme ayrı veri saklama kararı gerektirir.
        document.WithdrawnAtUtc = now;
        document.WithdrawnByUserId = actorId;
        document.WithdrawalReason = request.Reason.Trim();
        return document.ToWithdrawnDto();
    }
}

internal static class WithdrawnEmployeeDocumentMapping
{
    public static WithdrawnEmployeeDocumentDto ToWithdrawnDto(this EmployeeDocument document)
        => new(document.Id, document.DocumentType, document.FileName, document.CreatedAtUtc,
            document.WithdrawnAtUtc!.Value, document.WithdrawnByUserId,
            document.WithdrawalReason!);
}
