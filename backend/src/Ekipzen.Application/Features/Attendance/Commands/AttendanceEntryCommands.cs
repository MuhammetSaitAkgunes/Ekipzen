using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Files;
using Ekipzen.Domain.Entities.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Attendance.Commands;

/// <summary>Manuel giriş/düzenleme ortak gövdesi. Worked/overtime/status sunucuda hesaplanır.</summary>
public sealed record AttendanceEntryModel(
    DateOnly WorkDate,
    TimeOnly? CheckIn = null,
    TimeOnly? CheckOut = null,
    int BreakMinutes = 60,
    string? Type = null,
    string? Note = null);

public sealed class AttendanceEntryModelValidator : AbstractValidator<AttendanceEntryModel>
{
    public AttendanceEntryModelValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.BreakMinutes).InclusiveBetween(0, 480);
        RuleFor(x => x.Note).MaximumLength(500);

        RuleFor(x => x.CheckOut)
            .Must((m, checkOut) => checkOut is null || m.CheckIn is null || checkOut > m.CheckIn)
            .WithMessage("Çıkış saati girişten sonra olmalı.");

        RuleFor(x => x.Type)
            .Must(t => t is null or "" or "Tam" or "Mesai" or "Rapor")
            .WithMessage("Puantaj tipi: Tam | Mesai | Rapor.");
    }
}

// --- manuel giriş ---

public sealed record CreateAttendanceEntryCommand(int EmployeeId, AttendanceEntryModel Model)
    : IRequest<TimesheetRowDto>;

public sealed class CreateAttendanceEntryCommandValidator : AbstractValidator<CreateAttendanceEntryCommand>
{
    public CreateAttendanceEntryCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.Model).NotNull().SetValidator(new AttendanceEntryModelValidator());
    }
}

public sealed class CreateAttendanceEntryCommandHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<CreateAttendanceEntryCommand, TimesheetRowDto>
{
    public async Task<TimesheetRowDto> Handle(
        CreateAttendanceEntryCommand request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, request.EmployeeId, cancellationToken);

        await EmployeeEmploymentGuard.EnsureDateIsWithinEmploymentAsync(
            context, request.EmployeeId, request.Model.WorkDate, cancellationToken);

        var exists = await context.AttendanceRecords.AnyAsync(
            a => a.EmployeeId == request.EmployeeId && a.WorkDate == request.Model.WorkDate,
            cancellationToken);
        if (exists)
        {
            throw new ConflictException(
                "Bu personelin bu güne ait puantaj kaydı zaten var; satırı düzenleyin.");
        }

        var record = new AttendanceRecord { EmployeeId = request.EmployeeId };
        Apply(record, request.Model);

        context.AttendanceRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return ToRow(record);
    }

    internal static void Apply(AttendanceRecord record, AttendanceEntryModel model)
    {
        var (worked, overtime, status) = AttendanceCalculator.Compute(
            model.CheckIn, model.CheckOut, model.BreakMinutes);

        record.WorkDate = model.WorkDate;
        record.CheckIn = model.CheckIn;
        record.CheckOut = model.CheckOut;
        record.BreakMinutes = model.BreakMinutes;
        record.WorkedMinutes = worked;
        record.OvertimeMinutes = overtime;
        record.Status = status;
        record.Note = model.Note;

        // Tip verilmediyse türet: fazla mesai varsa Mesai, yoksa Tam.
        record.Type = string.IsNullOrEmpty(model.Type)
            ? (overtime > 0 ? Domain.Enums.TimesheetType.Overtime : Domain.Enums.TimesheetType.Full)
            : AttendanceMappings.ParseType(model.Type);
    }

    internal static TimesheetRowDto ToRow(AttendanceRecord a) => new(
        a.Id,
        a.WorkDate,
        a.Type.ToDto(),
        a.CheckIn,
        a.CheckOut,
        a.BreakMinutes,
        a.WorkedMinutes,
        a.OvertimeMinutes,
        AttendanceMappings.ToTimesheetStatus(a.Status, a.OvertimeMinutes),
        a.Note,
        a.Revision());
}

// --- satır düzenleme ---

public sealed record UpdateAttendanceEntryCommand(int Id, AttendanceEntryModel Model, string ExpectedRevision)
    : IRequest<TimesheetRowDto>;

public sealed class UpdateAttendanceEntryCommandValidator : AbstractValidator<UpdateAttendanceEntryCommand>
{
    public UpdateAttendanceEntryCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new AttendanceEntryModelValidator());
        RuleFor(x => x.ExpectedRevision).NotEmpty();
    }
}

public sealed class UpdateAttendanceEntryCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<UpdateAttendanceEntryCommand, TimesheetRowDto>
{
    public Task<TimesheetRowDto> Handle(
        UpdateAttendanceEntryCommand request, CancellationToken cancellationToken) =>
        databaseLock.ExecuteExclusiveAsync($"attendance-entry:{request.Id}",
            token => UpdateAsync(request, token), cancellationToken);

    private async Task<TimesheetRowDto> UpdateAsync(
        UpdateAttendanceEntryCommand request, CancellationToken cancellationToken)
    {
        var record = await context.AttendanceRecords
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Puantaj kaydı", request.Id);

        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, record.EmployeeId, cancellationToken);
        if (record.Revision() != request.ExpectedRevision)
        {
            throw new ConflictException(
                "Puantaj kaydı başka biri tarafından değiştirildi. Güncel satırı yeniden yükleyip farkları kontrol edin.");
        }

        await EmployeeEmploymentGuard.EnsureDateIsWithinEmploymentAsync(
            context, record.EmployeeId, request.Model.WorkDate, cancellationToken);

        // Tarih değişiyorsa gün başına tek kayıt kuralı korunur.
        if (record.WorkDate != request.Model.WorkDate)
        {
            var clash = await context.AttendanceRecords.AnyAsync(
                a => a.EmployeeId == record.EmployeeId
                     && a.WorkDate == request.Model.WorkDate
                     && a.Id != record.Id,
                cancellationToken);
            if (clash)
            {
                throw new ConflictException("Hedef günde bu personelin başka bir kaydı var.");
            }
        }

        CreateAttendanceEntryCommandHandler.Apply(record, request.Model);
        await context.SaveChangesAsync(cancellationToken);

        return CreateAttendanceEntryCommandHandler.ToRow(record);
    }
}
