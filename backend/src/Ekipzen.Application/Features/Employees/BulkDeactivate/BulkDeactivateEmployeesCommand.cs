using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using MediatR;

namespace Ekipzen.Application.Features.Employees.BulkDeactivate;

/// <summary>
/// Finansal kapanış ve geri alma snapshot'ı kişi bazında zorunlu olduğundan toplu
/// pasifleştirme korunmuş API sözleşmesiyle artık işten çıkış yapmaz.
/// </summary>
public sealed record BulkDeactivateEmployeesCommand(
    IReadOnlyList<int> Ids,
    DateOnly? TerminationDate) : IRequest<int>;

public sealed class BulkDeactivateEmployeesCommandValidator : AbstractValidator<BulkDeactivateEmployeesCommand>
{
    public BulkDeactivateEmployeesCommandValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("En az bir personel seçilmeli.");
        RuleFor(x => x.TerminationDate).NotNull()
            .WithMessage("İşten ayrılma tarihi zorunludur.");
    }
}

public sealed class BulkDeactivateEmployeesCommandHandler
    : IRequestHandler<BulkDeactivateEmployeesCommand, int>
{
    public Task<int> Handle(
        BulkDeactivateEmployeesCommand request,
        CancellationToken cancellationToken)
        => throw new ConflictException(
            "Toplu pasifleştirme finansal kapanışı atlayamaz. Her personel için tekil işten çıkış dosyası tamamlanmalıdır.");
}