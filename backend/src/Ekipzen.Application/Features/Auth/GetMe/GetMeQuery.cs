using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Auth.GetMe;

/// <summary>Oturum açmış kullanıcının profili (frontend /me sözleşmesi).</summary>
public sealed record GetMeQuery : IRequest<UserDto>;

public sealed class GetMeQueryHandler(IIdentityService identityService, ICurrentUser currentUser)
    : IRequestHandler<GetMeQuery, UserDto>
{
    public async Task<UserDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum bulunamadı.");

        return await identityService.GetUserAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("Kullanıcı kaydı bulunamadı.");
    }
}
