using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Auth.DataExport;

public sealed record AccountInfo(string UserId, string Name, string Email, IReadOnlyList<string> Roles);

public sealed record EmployeeInfo(
    string FullName, string Title, string DepartmentName, DateOnly HireDate,
    DateOnly? TerminationDate, string Status,
    DateOnly? BirthDate, string? Gender, string? MaritalStatus, string? MobilePhone,
    string? PersonalEmail, string? HomeAddress, string? Iban, string? BankName);

public sealed record LeaveRequestExportItem(string LeaveTypeName, DateOnly StartDate, DateOnly EndDate, int Days, string Status);
public sealed record LeaveLedgerExportItem(
    DateOnly OccurredOn, string Type, int Days, string SourceReference);
public sealed record AttendanceExportItem(DateOnly WorkDate, TimeOnly? CheckIn, TimeOnly? CheckOut, int WorkedMinutes, string Status);
public sealed record ComplianceDocumentExportItem(string DocumentName, string Status, DateOnly? DueDate);
public sealed record PayslipExportItem(string PeriodName, string ApprovalStatus);
public sealed record GuidanceBookmarkExportItem(string ProcessKey, string StepKey, int ContentVersion);
public sealed record GuidanceHelpPreferenceExportItem(string ProcessKey, string Mode, int Revision);
public sealed record GuidanceContentReadingExportItem(
    int ContentRevisionId, string ContentKey, string ProcessKey, int ContentVersion, string Title, DateTime FirstReadAtUtc);
public sealed record GuidancePublishedPracticeExportItem(int ContentRevisionId, string ContentKey, string ProcessKey,
    string StepKey, int ContentVersion, string Title, int AttemptCount, int LastSelectedChoice, bool LastCorrect, DateTime LastAnsweredAtUtc);
public sealed record EmployeeCreationEvidenceExportItem(int EmployeeId, DateTime CreatedAtUtc);
public sealed record CandidateReviewEvidenceExportItem(int CandidateId, string DecisionStatus, DateTime CreatedAtUtc);
public sealed record GuidanceDecisionProgressExportItem(
    string ProcessKey, string StepKey, int ContentVersion, int AttemptCount,
    int LastSelectedChoice, bool LastCorrect, DateTime LastAnsweredAtUtc);

public sealed record MyDataExportDto(
    AccountInfo Account,
    EmployeeInfo? Employee,
    IReadOnlyList<LeaveRequestExportItem> LeaveRequests,
    IReadOnlyList<LeaveLedgerExportItem> LeaveLedger,
    IReadOnlyList<AttendanceExportItem> AttendanceRecords,
    IReadOnlyList<ComplianceDocumentExportItem> ComplianceDocuments,
    IReadOnlyList<PayslipExportItem> Payslips,
    IReadOnlyList<GuidanceBookmarkExportItem> GuidanceBookmarks,
    IReadOnlyList<EmployeeCreationEvidenceExportItem> EmployeeCreationEvidences,
    IReadOnlyList<CandidateReviewEvidenceExportItem> CandidateReviewEvidences,
    IReadOnlyList<GuidanceDecisionProgressExportItem> GuidanceDecisionProgresses,
    IReadOnlyList<GuidanceContentReadingExportItem> GuidanceContentReadings,
    IReadOnlyList<GuidancePublishedPracticeExportItem> GuidancePublishedPractices,
    IReadOnlyList<GuidanceHelpPreferenceExportItem> GuidanceHelpPreferences,
    DateTime ExportedAtUtc);

/// <summary>
/// KVKK taşınabilirlik: oturum açmış kullanıcının kendi verisini JSON paketi olarak
/// döndürür. Çalışan verileri EmployeeId, rehber verileri UserId ile süzülür.
/// Tenant izolasyonu EF global filtresinden otomatik gelir (çifte güvence).
/// </summary>
public sealed record GetMyDataExportQuery : IRequest<(byte[] Content, string FileName)>;

public sealed class GetMyDataExportQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyDataExportQuery, (byte[] Content, string FileName)>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<(byte[] Content, string FileName)> Handle(
        GetMyDataExportQuery request, CancellationToken cancellationToken)
    {
        var account = new AccountInfo(
            currentUser.UserId ?? "", currentUser.UserName ?? "", currentUser.Email ?? "", currentUser.Roles);

        var helpPreferences = await context.GuidanceHelpPreferences.AsNoTracking()
            .Where(x => x.UserId == currentUser.UserId)
            .Select(x => new GuidanceHelpPreferenceExportItem(x.ProcessKey, x.Mode, x.Revision)).ToListAsync(cancellationToken);

        EmployeeInfo? employeeInfo = null;
        IReadOnlyList<LeaveRequestExportItem> leaveRequests = Array.Empty<LeaveRequestExportItem>();
        IReadOnlyList<LeaveLedgerExportItem> leaveLedger = Array.Empty<LeaveLedgerExportItem>();
        IReadOnlyList<AttendanceExportItem> attendance = Array.Empty<AttendanceExportItem>();
        IReadOnlyList<ComplianceDocumentExportItem> compliance = Array.Empty<ComplianceDocumentExportItem>();
        IReadOnlyList<PayslipExportItem> payslips = Array.Empty<PayslipExportItem>();

        var userId = currentUser.UserId ?? string.Empty;
        var publishedPractices = await context.GuidancePublishedPracticeProgresses.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new GuidancePublishedPracticeExportItem(item.ContentRevisionId, item.ContentKey,
                item.ProcessKey, item.StepKey, item.ContentVersion, item.Title, item.AttemptCount, item.LastSelectedChoice,
                item.LastCorrect, DateTime.SpecifyKind(item.LastAnsweredAtUtc, DateTimeKind.Utc))).ToListAsync(cancellationToken);
        var guidanceReadings = await context.GuidanceContentReadings.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new GuidanceContentReadingExportItem(item.ContentRevisionId, item.ContentKey,
                item.ProcessKey, item.ContentVersion, item.Title, DateTime.SpecifyKind(item.FirstReadAtUtc, DateTimeKind.Utc)))
            .ToListAsync(cancellationToken);
        var guidanceBookmarks = await context.GuidanceBookmarks.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new GuidanceBookmarkExportItem(item.ProcessKey, item.StepKey, item.ContentVersion))
            .ToListAsync(cancellationToken);
        var employeeCreationEvidences = await context.EmployeeCreationEvidences.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new EmployeeCreationEvidenceExportItem(item.EmployeeId, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var candidateReviewEvidences = await context.CandidateReviewEvidences.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new CandidateReviewEvidenceExportItem(
                item.CandidateId, item.DecisionStatus.ToString(), item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var guidanceDecisionProgresses = await context.GuidanceDecisionProgresses.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new GuidanceDecisionProgressExportItem(
                item.ProcessKey, item.StepKey, item.ContentVersion, item.AttemptCount,
                item.LastSelectedChoice, item.LastCorrect, item.LastAnsweredAtUtc))
            .ToListAsync(cancellationToken);

        if (currentUser.EmployeeId is { } employeeId)
        {
            var employee = await context.Employees
                .Include(e => e.Department)
                .Include(e => e.Profile)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            if (employee is not null)
            {
                var p = employee.Profile;
                employeeInfo = new EmployeeInfo(
                    employee.FullName, employee.Title, employee.Department?.Name ?? "",
                    employee.HireDate, employee.TerminationDate, employee.Status.ToString(),
                    p?.BirthDate, p?.Gender, p?.MaritalStatus, p?.MobilePhone,
                    p?.PersonalEmail, p?.HomeAddress, p?.Iban, p?.BankName);
            }

            leaveRequests = await context.LeaveRequests
                .Where(r => r.EmployeeId == employeeId)
                .Select(r => new LeaveRequestExportItem(
                    r.LeaveType!.Name, r.StartDate, r.EndDate, r.Days, r.Status.ToString()))
                .ToListAsync(cancellationToken);

            leaveLedger = await context.LeaveLedgerEntries
                .Where(item => item.EmploymentPeriod!.EmployeeId == employeeId)
                .OrderBy(item => item.OccurredOn)
                .Select(item => new LeaveLedgerExportItem(
                    item.OccurredOn, item.Type.ToString(), item.Days, item.SourceReference))
                .ToListAsync(cancellationToken);

            attendance = await context.AttendanceRecords
                .Where(a => a.EmployeeId == employeeId)
                .Select(a => new AttendanceExportItem(
                    a.WorkDate, a.CheckIn, a.CheckOut, a.WorkedMinutes, a.Status.ToString()))
                .ToListAsync(cancellationToken);

            compliance = await context.ComplianceDocuments
                .Where(d => d.EmployeeId == employeeId)
                .Select(d => new ComplianceDocumentExportItem(d.DocumentName, d.Status.ToString(), d.DueDate))
                .ToListAsync(cancellationToken);

            payslips = await context.PayrollEmployees
                .Where(pe => pe.EmployeeId == employeeId)
                .Select(pe => new PayslipExportItem(pe.Period!.Name, pe.ApprovalStatus.ToString()))
                .ToListAsync(cancellationToken);
        }

        var dto = new MyDataExportDto(
            account, employeeInfo, leaveRequests, leaveLedger, attendance, compliance, payslips,
            guidanceBookmarks, employeeCreationEvidences, candidateReviewEvidences,
            guidanceDecisionProgresses, guidanceReadings, publishedPractices, helpPreferences, DateTime.UtcNow);

        var json = JsonSerializer.SerializeToUtf8Bytes(dto, SerializerOptions);
        var fileName = $"ekipzen-verilerim-{DateTime.UtcNow:yyyyMMdd}.json";
        return (json, fileName);
    }
}
