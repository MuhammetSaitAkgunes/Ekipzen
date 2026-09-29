using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

public sealed record FileDownload(Stream Content, string FileName, string ContentType);

/// <summary>Evrak indirme (rol kapsamına tabi).</summary>
public sealed record DownloadEmployeeDocumentQuery(int EmployeeId, int DocumentId) : IRequest<FileDownload>;

public sealed class DownloadEmployeeDocumentQueryHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IFileStorage fileStorage)
    : IRequestHandler<DownloadEmployeeDocumentQuery, FileDownload>
{
    public async Task<FileDownload> Handle(
        DownloadEmployeeDocumentQuery request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(context, currentUser, request.EmployeeId, cancellationToken);

        var document = await context.EmployeeDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == request.DocumentId && d.EmployeeId == request.EmployeeId
                    && d.WithdrawnAtUtc == null, cancellationToken)
            ?? throw new NotFoundException("Evrak", request.DocumentId);

        var stream = await fileStorage.OpenReadAsync(document.FilePath, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;
        return new FileDownload(stream, document.FileName, contentType);
    }
}
