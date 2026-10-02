using Ekipzen.Application.Common.Interfaces;
using MediatR;

namespace Ekipzen.Application.Features.Leaves.Queries;

public sealed record PreviewLeaveCancellationQuery(int Id) : IRequest<LeaveCancellationPreviewDto>;
public sealed class PreviewLeaveCancellationQueryHandler(IApplicationDbContext db, ICurrentUser user)
    : IRequestHandler<PreviewLeaveCancellationQuery, LeaveCancellationPreviewDto>
{
    public Task<LeaveCancellationPreviewDto> Handle(PreviewLeaveCancellationQuery request, CancellationToken ct) =>
        LeaveOperationReview.CancellationAsync(db, user, request.Id, ct);
}
