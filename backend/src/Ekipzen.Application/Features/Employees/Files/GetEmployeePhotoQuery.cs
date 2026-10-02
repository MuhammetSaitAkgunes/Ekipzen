using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

public sealed record GetEmployeePhotoQuery(int EmployeeId) : IRequest<(Stream Content, string ContentType)>;

public sealed class GetEmployeePhotoQueryHandler(IApplicationDbContext db, ICurrentUser user, IFileStorage storage)
    : IRequestHandler<GetEmployeePhotoQuery, (Stream Content, string ContentType)>
{
    public async Task<(Stream Content, string ContentType)> Handle(GetEmployeePhotoQuery request, CancellationToken ct)
    {
        if (!user.Roles.Contains(Roles.HrAdmin)) throw new ForbiddenAccessException("Personel fotoğrafını yalnız İK yöneticisi görüntüleyebilir.");
        // Tenant veritabanı ve dosya alanı mevcut isteğin şirketinden çözülür.
        var path = await db.Employees.AsNoTracking().Where(x => x.Id == request.EmployeeId)
            .Select(x => x.Profile == null ? null : x.Profile.PhotoPath).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Personel fotoğrafı", request.EmployeeId);
        var stream = await storage.OpenReadAsync(path, ct);
        return (stream, Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
    }
}
