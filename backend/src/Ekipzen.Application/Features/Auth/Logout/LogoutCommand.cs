using FluentValidation;
using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutCommandHandler(IIdentityService identityService)
    : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        => identityService.LogoutAsync(request.RefreshToken, cancellationToken);
}
