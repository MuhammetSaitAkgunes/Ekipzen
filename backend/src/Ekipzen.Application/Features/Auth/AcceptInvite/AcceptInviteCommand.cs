using FluentValidation;
using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Auth.AcceptInvite;

/// <summary>
/// Davet token'ıyla ilk şifreyi belirler ve hesabı etkinleştirir. Anonim uçtur
/// (kullanıcının henüz şifresi/oturumu yoktur); token kimlik kanıtıdır.
/// </summary>
public sealed record AcceptInviteCommand(string Email, string Token, string NewPassword) : IRequest;

public sealed class AcceptInviteCommandValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public sealed class AcceptInviteCommandHandler(IIdentityService identityService)
    : IRequestHandler<AcceptInviteCommand>
{
    public Task Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
        => identityService.AcceptInviteAsync(
            request.Email, request.Token, request.NewPassword, cancellationToken);
}
