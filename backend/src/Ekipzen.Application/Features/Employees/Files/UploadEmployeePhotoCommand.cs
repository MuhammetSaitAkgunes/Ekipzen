using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

/// <summary>Personel fotoğrafı yükler (personnel.js: JPG/PNG, maksimum 2 MB).</summary>
public sealed record UploadEmployeePhotoCommand(
    int EmployeeId,
    Stream Content,
    string FileName,
    long Length) : IRequest<string>;

public sealed class UploadEmployeePhotoCommandValidator : AbstractValidator<UploadEmployeePhotoCommand>
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];

    public UploadEmployeePhotoCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("Dosya boş olamaz.")
            .LessThanOrEqualTo(MaxBytes).WithMessage("Fotoğraf en fazla 2 MB olabilir.");

        RuleFor(x => x.FileName)
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .WithMessage("Fotoğraf JPG veya PNG olmalı.");
    }
}

public sealed class UploadEmployeePhotoCommandHandler(IApplicationDbContext context, IFileStorage fileStorage)
    : IRequestHandler<UploadEmployeePhotoCommand, string>
{
    public async Task<string> Handle(UploadEmployeePhotoCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Personel", request.EmployeeId);

        var stored = await fileStorage.SaveAsync(request.Content, request.FileName, "photos", cancellationToken);

        var profile = employee.Profile ??= new EmployeeProfile { EmployeeId = employee.Id };
        var oldPhotoPath = profile.PhotoPath;
        profile.PhotoPath = stored.Path;

        await context.SaveChangesAsync(cancellationToken);

        if (oldPhotoPath is not null)
        {
            await fileStorage.DeleteAsync(oldPhotoPath, cancellationToken);
        }

        return stored.Path;
    }
}
