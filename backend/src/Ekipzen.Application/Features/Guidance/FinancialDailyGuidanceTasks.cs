using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

internal static class FinancialDailyGuidanceTasks
{
    public static async Task AddAsync(IApplicationDbContext db, List<DailyGuidanceTaskDto> tasks, CancellationToken ct)
    {
        var payroll = await db.PayrollEmployees.AsNoTracking()
            .Where(x => x.ApprovalStatus == PayrollApprovalStatus.Approved && x.Result != null
                && (x.Period!.Status == PayrollPeriodStatus.Approved || x.Period.Status == PayrollPeriodStatus.Closed)
                && !db.PayrollApprovedRowRevisions.Any(r => r.PayrollRowId == x.Id))
            .Select(x => new { x.Id, x.PayrollPeriodId, Name = x.Employee!.FirstName + " " + x.Employee.LastName }).ToListAsync(ct);
        var cases = await db.EmployeeOffboardingCases.AsNoTracking()
            .Where(x => x.Status == EmployeeOffboardingStatus.Completed && x.Employee!.Status == EmployeeStatus.Passive
                && x.Employee.TerminationDate == x.TerminationDate
                && !db.EmployeeOffboardingSettlementRevisions.Any(r => r.OffboardingCaseId == x.Id && r.Status == "approved"))
            .Select(x => new { x.Id, x.EmployeeId, Name = x.Employee!.FirstName + " " + x.Employee.LastName }).ToListAsync(ct);
        var rowIds = payroll.Select(x => x.Id).ToList();
        var caseIds = cases.Select(x => x.Id).ToList();
        var records = await db.FinancialExternalActionRecords.AsNoTracking()
            .Where(x => x.PayrollRowId.HasValue && rowIds.Contains(x.PayrollRowId.Value)
                || x.OffboardingCaseId.HasValue && caseIds.Contains(x.OffboardingCaseId.Value))
            .Select(x => new { x.Id, x.PayrollRowId, x.OffboardingCaseId, x.Kind, x.Outcome }).ToListAsync(ct);
        var closed = records.GroupBy(x => new { x.PayrollRowId, x.OffboardingCaseId, x.Kind })
            .Select(g => g.MaxBy(x => x.Id)!).Where(x => x.Outcome is "completed" or "not-required")
            .Select(x => $"{(x.PayrollRowId.HasValue ? "payroll" : "offboarding")}:{x.PayrollRowId ?? x.OffboardingCaseId}:{x.Kind}").ToHashSet();
        foreach (var item in payroll)
            Add("payroll", item.Id, item.Name, $"/payroll?periodId={item.PayrollPeriodId}");
        foreach (var item in cases)
            Add("offboarding", item.Id, item.Name, $"/personnel/{item.EmployeeId}?tab=tab-mali");

        var pending = await db.PayrollEmployees.AsNoTracking()
            .Where(x => x.ReviewedTimesheetRevision != null && x.ApprovalStatus != PayrollApprovalStatus.Approved
                && x.Period!.Status != PayrollPeriodStatus.Approved && x.Period.Status != PayrollPeriodStatus.Closed)
            .Select(x => new { x.Id, x.EmployeeId, x.PayrollPeriodId, x.ReviewedTimesheetRevision,
                x.Period!.Year, x.Period.Month, Name = x.Employee!.FirstName + " " + x.Employee.LastName }).ToListAsync(ct);
        foreach (var group in pending.GroupBy(x => new { x.Year, x.Month }))
        {
            var current = await PayrollTimesheetSource.ReadAsync(db, group.Select(x => x.EmployeeId).Distinct().ToArray(),
                group.Key.Year, group.Key.Month, ct);
            foreach (var row in group.Where(x => current.GetValueOrDefault(x.EmployeeId) != x.ReviewedTimesheetRevision))
                tasks.Add(new($"payroll-timesheet-review:{row.Id}", "payroll-timesheet-review", row.Id,
                    $"{row.Name} puantaj ve bordro girdilerini yeniden uzlaştır",
                    "Satır incelendikten sonra puantaj, onaylı izin, takvim veya istihdam kaynağı değişti.",
                    "Aylık puantajı ve bordro satırını karşılaştırın; gün ve fazla mesai girdilerini doğrulayarak kaydedin. Toplu kontrol tek başına bu işi kapatmaz.",
                    null, "Bordro ayı bir yasal son tarih değildir.", $"/payroll?periodId={row.PayrollPeriodId}"));
        }

        void Add(string source, int id, string name, string path)
        {
            foreach (var kind in new[] { "payment", "notification" })
            {
                if (closed.Contains($"{source}:{id}:{kind}")) continue;
                var label = kind == "payment" ? "ödeme" : "bildirim";
                tasks.Add(new($"{source}-original-external-{kind}:{id}", $"{source}-original-external-{kind}", id,
                    $"{name} ilk hesap {label} kanıtını doğrula",
                    $"İlk onaylı hesapta dış {label} sonucu kanıtla kapatılmadı; uygulama onayı dış işlemi gerçekleştirmez.",
                    "İlgili hesabın dış işlem panelinde sorumluyu ve belge referansını kaydedin. Sorumlu atamak işi kapatmaz.",
                    null, "Dış işlem için güvenilir son tarih kaydı yok.", path));
            }
        }
    }
}
