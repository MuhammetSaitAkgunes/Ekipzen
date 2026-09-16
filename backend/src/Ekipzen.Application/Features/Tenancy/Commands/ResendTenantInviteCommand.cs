using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracıdaki, henüz şifre belirlememiş kullanıcıya davet e-postasını yeniden
/// gönderir. Platform seviyesi bir kurtarma aracıdır.
///
/// <b>Neden var:</b> provizyon altı adımlıdır ve e-posta 5. adımdadır. O adım
/// (SMTP arızası, yanlış adres, spam kutusu) başarısız olduğunda geriye kullanıcısı
/// oluşmuş ama girişi imkânsız bir kiracı kalıyordu. Operatörün elindeki tek çare
/// kiracıyı silip baştan kurmaktı — bir e-posta gitmedi diye VERİ SİLMEK. Bu uç
/// o boşluğu kapatır.
/// </summary>
public sealed record ResendTenantInviteCommand(int TenantId, string Email) : IRequest;

public sealed class ResendTenantInviteCommandValidator : AbstractValidator<ResendTenantInviteCommand>
{
    public ResendTenantInviteCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class ResendTenantInviteCommandHandler(
    IPlatformDbContext platform, IIdentityService identityService)
    : IRequestHandler<ResendTenantInviteCommand>
{
    public async Task Handle(ResendTenantInviteCommand request, CancellationToken cancellationToken)
    {
        // Kiracının VARLIĞI önce doğrulanır: olmayan bir kiracı için kapsam açmak,
        // bağlantı çözümlemesinde anlamsız bir hataya dönüşür — operatör "sunucu
        // hatası" görür, oysa cevap "böyle bir kiracı yok"tur.
        var tenant = await platform.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Kiracı", request.TenantId);

        await identityService.ResendInviteAsync(tenant.Id, request.Email.Trim(), cancellationToken);
    }
}
