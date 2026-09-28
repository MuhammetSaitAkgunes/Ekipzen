using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Attendance.Commands;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace Ekipzen.Application.Features.Attendance.Queries;

/// <summary>Manuel kaydı yazmadan mevcut kaynaklarla hesaplar; kayıt için rezervasyon oluşturmaz.</summary>
public sealed record PreviewAttendanceEntryQuery(
    int EmployeeId, int? EntryId, AttendanceEntryModel Model, string? ExpectedRevision = null)
    : IRequest<AttendanceEntryPreviewDto>;

public sealed record AttendanceEntryPreviewDto(
    int WorkedMinutes,
    int OvertimeMinutes,
    string Status,
    string EffectiveType,
    bool CanSave,
    string? BlockReason,
    IReadOnlyList<string> Warnings);

public sealed class PreviewAttendanceEntryQueryValidator : AbstractValidator<PreviewAttendanceEntryQuery>
{
    public PreviewAttendanceEntryQueryValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EntryId).GreaterThan(0).When(x => x.EntryId is not null);
        RuleFor(x => x.ExpectedRevision).NotEmpty().When(x => x.EntryId is not null);
        RuleFor(x => x.Model).NotNull().SetValidator(new AttendanceEntryModelValidator());
    }
}

public sealed class PreviewAttendanceEntryQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<PreviewAttendanceEntryQuery, AttendanceEntryPreviewDto>
{
    public async Task<AttendanceEntryPreviewDto> Handle(
        PreviewAttendanceEntryQuery request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, request.EmployeeId, cancellationToken);

        string? revisionBlock = null;
        if (request.EntryId is { } entryId)
        {
            var existing = await context.AttendanceRecords.AsNoTracking().SingleOrDefaultAsync(
                row => row.Id == entryId && row.EmployeeId == request.EmployeeId,
                cancellationToken) ?? throw new NotFoundException("Puantaj kaydı", entryId);
            if (existing.Revision() != request.ExpectedRevision)
            {
                revisionBlock = "Puantaj kaydı başka biri tarafından değiştirildi. Güncel satırı yeniden yükleyip farkları kontrol edin.";
            }
        }

        var model = request.Model;
        var (worked, overtime, status) = AttendanceCalculator.Compute(
            model.CheckIn, model.CheckOut, model.BreakMinutes);
        var effectiveType = string.IsNullOrEmpty(model.Type)
            ? (overtime > 0 ? "Mesai" : "Tam")
            : model.Type;
        var warnings = new List<string>();
        if (model.CheckIn is null || model.CheckOut is null)
        {
            warnings.Add("Giriş veya çıkış saati eksik: çalışılan süre 0 hesaplanır. Gerçek kaydı doğrulayın.");
        }
        if (overtime > 0)
        {
            warnings.Add("Standart 480 dakikayı aşan süre fazla mesai alanına yansır; yasal değerlendirmeyi ayrıca yapın.");
        }
        if (model.Type == "Rapor" && worked > 0)
        {
            warnings.Add("Rapor tipiyle çalışma saati birlikte girilmiş; kaynak belge ve gün tipini kontrol edin.");
        }

        string? blockReason = revisionBlock;
        if (blockReason is null)
        {
            try
            {
                await EmployeeEmploymentGuard.EnsureDateIsWithinEmploymentAsync(
                    context, request.EmployeeId, model.WorkDate, cancellationToken);
            }
            catch (ConflictException exception)
            {
                blockReason = exception.Message;
            }
        }

        if (blockReason is null && await context.AttendanceRecords.AsNoTracking().AnyAsync(
            row => row.EmployeeId == request.EmployeeId && row.WorkDate == model.WorkDate &&
                (request.EntryId == null || row.Id != request.EntryId), cancellationToken))
        {
            blockReason = request.EntryId is null
                ? "Bu personelin bu güne ait puantaj kaydı zaten var; satırı düzenleyin."
                : "Hedef günde bu personelin başka bir kaydı var.";
        }

        return new AttendanceEntryPreviewDto(worked, overtime,
            AttendanceMappings.ToTimesheetStatus(status, overtime), effectiveType,
            blockReason is null, blockReason, warnings);
    }
}
