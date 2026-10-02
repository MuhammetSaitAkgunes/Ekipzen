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

/// <summary>Personel fotoğrafı yükler (personnel.js: JPG/PNG, maksimum 2 MB).</summary>
public sealed record UploadEmployeePhotoCommand(
    int EmployeeId,
    Stream Content,
    string FileName,
    long Length, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest<string>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "employee-photo-upload";
}

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

public sealed class UploadEmployeePhotoCommandHandler(IApplicationDbContext context, IFileStorage storage,
    IDatabaseLock gate, ICurrentUser user, GuidanceOperationContext guidance, ILogger<UploadEmployeePhotoCommandHandler> logger)
    : IRequestHandler<UploadEmployeePhotoCommand, string>
{
    public static string RecordToken(int id, string? path) => PreparationOperationEvidence.RecordToken("employee-photo", id.ToString(), path);
    public Task<string> Handle(UploadEmployeePhotoCommand request, CancellationToken ct)
        => GuidanceImageReplacement.ExecuteAsync(storage, gate, "employee-organization", request.Content, request.FileName, "photos", async (upload, token) =>
        {
            var employee = await context.Employees.Include(x => x.Profile).SingleOrDefaultAsync(x => x.Id == request.EmployeeId, token)
                ?? throw new NotFoundException("Personel", request.EmployeeId);
            PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, RecordToken(employee.Id, employee.Profile?.PhotoPath), guidance.IsGuided);
            var stored = await upload(token);
            var profile = employee.Profile ??= new EmployeeProfile { EmployeeId = employee.Id };
            var previous = profile.PhotoPath;
            profile.PhotoPath = stored.Path;
            await context.SaveChangesAsync(token);
            if (guidance.IsGuided)
            {
                var evidence = new GuidancePersonnelEvidence { UserId = user.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi."),
                    EmployeeId = employee.Id, OperationKey = request.GuidanceOperationKey, ChangedFieldsJson = "[\"Kimlik / Fotoğraf\"]" };
                guidance.Stamp(evidence); context.GuidancePersonnelEvidences.Add(evidence);
                await context.SaveChangesAsync(token);
            }
            return previous;
        }, logger, ct);
}
