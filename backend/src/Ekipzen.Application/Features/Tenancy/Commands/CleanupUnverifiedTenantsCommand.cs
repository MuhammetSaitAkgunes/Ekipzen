using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Doğrulanmamış (hiç etkinleşmemiş) self-servis kiracıları temizler: pasif + verilen
/// günden eski + hiçbir kullanıcısı şifre belirlememiş (davet hiç kabul edilmemiş) kiracılar.
/// Askıya alınmış (şifreli kullanıcısı olan) kiracılar korunur. Cron ile tetiklenebilir.
/// </summary>
public sealed record CleanupUnverifiedTenantsCommand(int OlderThanDays) : IRequest<CleanupUnverifiedResult>;

public sealed record CleanupUnverifiedResult(int PurgedCount);

public sealed class CleanupUnverifiedTenantsCommandHandler(ITenantPurger purger)
    : IRequestHandler<CleanupUnverifiedTenantsCommand, CleanupUnverifiedResult>
{
    public async Task<CleanupUnverifiedResult> Handle(
        CleanupUnverifiedTenantsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Abs(request.OlderThanDays));
        var count = await purger.PurgeUnverifiedAsync(cutoff, cancellationToken);
        return new CleanupUnverifiedResult(count);
    }
}
