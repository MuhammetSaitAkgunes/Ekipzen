using FluentValidation;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Guidance;
using Microsoft.Extensions.Logging;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

/// <summary>Özlük evrakı yükler (nüfus cüzdanı, ikametgah, adli sicil vb.).</summary>
// Not: İstemcinin bildirdiği Content-Type bilinçli olarak alınmaz — MIME,
// doğrulanmış uzantıdan türetilir (bkz. EmployeeDocumentTypes).
public sealed record UploadEmployeeDocumentCommand(
    int EmployeeId,
    string DocumentType,
    Stream Content,
    string FileName,
    long Length, int? GuidanceContentRevisionId = null) : IRequest<EmployeeDocumentDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "employee-document-upload";
}

public sealed class UploadEmployeeDocumentCommandValidator : AbstractValidator<UploadEmployeeDocumentCommand>
{
    private const long MaxBytes = 10 * 1024 * 1024;

    public UploadEmployeeDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(64);

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("Dosya boş olamaz.")
            .LessThanOrEqualTo(MaxBytes).WithMessage("Evrak en fazla 10 MB olabilir.");

        RuleFor(x => x.FileName)
            .Must(EmployeeDocumentTypes.IsAllowed)
            .WithMessage($"İzin verilen dosya türleri: {string.Join(", ", EmployeeDocumentTypes.AllowedExtensions)}");
    }
}

public sealed class UploadEmployeeDocumentCommandHandler(IApplicationDbContext context, IFileStorage fileStorage, ICurrentUser user, GuidanceOperationContext guidance, IDatabaseLock databaseLock, ILogger<UploadEmployeeDocumentCommandHandler> logger)
    : IRequestHandler<UploadEmployeeDocumentCommand, EmployeeDocumentDto>
{
    public async Task<EmployeeDocumentDto> Handle(
        UploadEmployeeDocumentCommand request, CancellationToken cancellationToken)
    {
        StoredFile? stored = null;
        try
        {
            return await databaseLock.ExecuteExclusiveAsync($"employee-documents:{request.EmployeeId}", async token =>
            {
                var employeeExists = await context.Employees
                    .AnyAsync(e => e.Id == request.EmployeeId, token);
                if (!employeeExists)
                {
                    throw new NotFoundException("Personel", request.EmployeeId);
                }

                stored = await fileStorage.SaveAsync(
                    request.Content, request.FileName, $"documents-emp-{request.EmployeeId}", token);

                var document = new EmployeeDocument
                {
                    EmployeeId = request.EmployeeId,
                    DocumentType = request.DocumentType.Trim(),
                    FileName = request.FileName,
                    FilePath = stored.Path,
                    ContentType = EmployeeDocumentTypes.ResolveContentType(request.FileName),
                    SizeBytes = stored.SizeBytes,
                };

                context.EmployeeDocuments.Add(document);
                await context.SaveChangesAsync(token);

                var evidence = new GuidancePersonnelEvidence
                {
                    UserId = user.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi."),
                    EmployeeId = request.EmployeeId, DocumentId = document.Id, OperationKey = request.GuidanceOperationKey,
                    ChangedFieldsJson = "[\"Evrak / Yükleme\"]",
                };
                guidance.Stamp(evidence);
                context.GuidancePersonnelEvidences.Add(evidence);
                await context.SaveChangesAsync(token);
                return new EmployeeDocumentDto(
                    document.Id, document.DocumentType, document.FileName,
                    document.ContentType, document.SizeBytes, document.CreatedAtUtc);
            }, cancellationToken);
        }
        catch
        {
            // Yalnız bu başarısız isteğin yeni dosyası temizlenir; eski evraklara dokunulmaz.
            if (stored is not null)
            {
                try { await fileStorage.DeleteAsync(stored.Path, CancellationToken.None); }
                catch (Exception cleanup) { logger.LogWarning(cleanup, "Başarısız evrak yüklemesinin yeni dosyası temizlenemedi; depo uzlaştırması gerekli."); }
            }
            throw;
        }
    }
}
