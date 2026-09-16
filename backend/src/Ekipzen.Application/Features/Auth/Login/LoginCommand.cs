using FluentValidation;
using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(IIdentityService identityService)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        => identityService.LoginAsync(request.Email, request.Password, cancellationToken);
}
