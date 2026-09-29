using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Compliance.Queries;
using Ekipzen.Application.Features.Leaves.Queries;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Application.Features.Recruitment.Queries;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

/// <summary>
/// Mevcut iş kayıtlarından her sorguda yeniden üretilen görev. Key aynı kayıt için sabittir;
/// DueDate yalnız kaynakta gerçekten son tarih olduğunda doludur.
/// </summary>
public sealed record DailyGuidanceTaskDto(
    string Key,
    string Kind,
    int RecordId,
    string Title,
    string Reason,
    string NextAction,
    DateOnly? DueDate,
    string DateNote,
    string TargetPath);

public sealed record GetDailyGuidanceTasksQuery : IRequest<IReadOnlyList<DailyGuidanceTaskDto>>;

public sealed class GetDailyGuidanceTasksQueryHandler(
    ISender sender, ICurrentUser currentUser, IApplicationDbContext context)
    : IRequestHandler<GetDailyGuidanceTasksQuery, IReadOnlyList<DailyGuidanceTaskDto>>
{
    public async Task<IReadOnlyList<DailyGuidanceTaskDto>> Handle(
        GetDailyGuidanceTasksQuery request, CancellationToken ct)
    {
        var pendingLeaves = await sender.Send(new GetPendingLeaveRequestsQuery(), ct);
        var tasks = pendingLeaves.Select(leave => new DailyGuidanceTaskDto(
            $"leave-approval:{leave.Id}",
            "leave-approval",
            leave.Id,
            $"{leave.EmployeeName} izin talebini incele",
            $"{leave.LeaveTypeName} talebi onay bekliyor.",
            "Talep ayrıntılarını inceleyip onay veya ret kararı verin.",
            null,
            $"İzin başlangıcı: {leave.StartDate:dd.MM.yyyy}. Karar için kayıtlı son tarih yok.",
            $"/inbox?tab=approvals&requestId={leave.Id}"
        )).ToList();

        // Yönetici belgeyi görebilir ancak durumunu değiştiremez; görev yalnız
        // kaydı düzeltebilen İK yöneticisine sunulur.
        if (currentUser.Roles.Contains(Roles.HrAdmin))
        {
            var documents = await sender.Send(new GetComplianceDocumentsQuery(), ct);
            tasks.AddRange(documents
                .Where(document => document.Status is "Eksik" or "Süresi Yaklaşıyor")
                .Select(document => new DailyGuidanceTaskDto(
                    $"compliance-document:{document.Id}",
                    "compliance-document",
                    document.Id,
                    $"{document.Employee} · {document.Document}",
                    $"Belge kaydı \"{document.Status}\" durumunda.",
                    "Kaydı ve sorumluyu inceleyin; gerçek duruma göre güncelleyin.",
                    document.DueDate,
                    document.DueDate is null ? "Son tarih kayıtlı değil; öncelik tarihine göre değerlendirilemiyor." : "",
                    $"/risk/compliance?documentId={document.Id}"
                )));

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var missingCompensation = await context.Employees.AsNoTracking()
                .Where(employee =>
                    employee.Status == EmployeeStatus.Active &&
                    employee.HireDate <= today &&
                    employee.EmploymentPeriods.Any(period =>
                        period.EndDate == null && period.StartDate <= today &&
                        !period.CompensationPackages.Any(package =>
                            package.EffectiveFrom <= today &&
                            (package.EffectiveTo == null || package.EffectiveTo >= today))))
                .Select(employee => new {
                    employee.Id, employee.FirstName, employee.LastName, employee.HireDate
                })
                .ToListAsync(ct);
            tasks.AddRange(missingCompensation.Select(employee => new DailyGuidanceTaskDto(
                $"employee-compensation:{employee.Id}",
                "employee-compensation",
                employee.Id,
                $"{employee.FirstName} {employee.LastName} ücret hazırlığını kontrol et",
                "Açık çalışma dönemi için bugün yürürlükte ücret paketi yok. Bu durumda açılan bordro döneminin ilk ücret girdisi sıfır oluşabilir.",
                "Tutarı, yürürlük tarihini ve kaynak referansını doğrulayın. Dayanak netleşmeden ücret girmeyin; sonra paketi kaydedin.",
                null,
                $"İşe giriş: {employee.HireDate:dd.MM.yyyy}. Bu tarih karar son tarihi değildir; kayıtlı son tarih yok.",
                $"/personnel/{employee.Id}?edit=1&guide=1&tab=tab-mali"
            )));

            var draftOffboardings = await context.EmployeeOffboardingCases.AsNoTracking()
                .Where(offboarding => offboarding.Status == EmployeeOffboardingStatus.Draft &&
                    offboarding.Employee!.Status == EmployeeStatus.Active)
                .Select(offboarding => new {
                    offboarding.Id, offboarding.EmployeeId, offboarding.TerminationDate,
                    offboarding.Employee!.FirstName, offboarding.Employee.LastName
                })
                .ToListAsync(ct);
            tasks.AddRange(draftOffboardings.Select(offboarding => new DailyGuidanceTaskDto(
                $"offboarding-case:{offboarding.Id}",
                "offboarding-case",
                offboarding.Id,
                $"{offboarding.FirstName} {offboarding.LastName} ayrılış taslağını incele",
                "İşten ayrılış dosyası taslak durumunda; personel henüz pasife alınmadı.",
                "Tarihi, fesih nedenini, ekip devrini ve uzman/harici hesap dayanağını doğrulayın. Hata varsa taslağı düzeltin; uygunsa süreci tamamlayın.",
                null,
                $"Taslakta kayıtlı ayrılış tarihi: {offboarding.TerminationDate:dd.MM.yyyy}. Bu tarih karar son tarihi değildir; kayıtlı son tarih yok.",
                $"/personnel/{offboarding.EmployeeId}?offboarding=1"
            )));

            var currentSettlements = await context.EmployeeOffboardingSettlementRevisions
                .AsNoTracking()
                .Where(revision => revision.Status == "approved"
                    && revision.OffboardingCase!.Status == EmployeeOffboardingStatus.Completed
                    && revision.OffboardingCase.Employee!.Status == EmployeeStatus.Passive
                    && !context.EmployeeOffboardingSettlementRevisions.Any(newer =>
                        newer.OffboardingCaseId == revision.OffboardingCaseId
                        && newer.Status == "approved"
                        && newer.RevisionNumber > revision.RevisionNumber))
                .Select(revision => new {
                    revision.Id, revision.RevisionNumber, revision.OffboardingCaseId,
                    revision.OffboardingCase!.EmployeeId,
                    revision.OffboardingCase.Employee!.FirstName,
                    revision.OffboardingCase.Employee.LastName,
                })
                .ToListAsync(ct);
            var currentIds = currentSettlements.Select(revision => revision.Id).ToList();
            var completedExternalKinds = await context.EmployeeOffboardingExternalActionRecords
                .AsNoTracking()
                .Where(record => currentIds.Contains(record.SettlementRevisionId))
                .Select(record => new { record.Id, record.SettlementRevisionId,
                    record.Kind, record.Outcome })
                .ToListAsync(ct);
            var completedKeys = completedExternalKinds
                .GroupBy(record => new { record.SettlementRevisionId, record.Kind })
                .Select(group => group.MaxBy(record => record.Id)!)
                .Where(record => record.Outcome != "pending")
                .Select(record => $"{record.SettlementRevisionId}:{record.Kind}")
                .ToHashSet();
            foreach (var settlement in currentSettlements)
            {
                foreach (var kind in new[] { "payment", "notification" })
                {
                    if (completedKeys.Contains($"{settlement.Id}:{kind}")) continue;
                    var label = kind == "payment" ? "ödeme" : "bildirim";
                    tasks.Add(new DailyGuidanceTaskDto(
                        $"offboarding-external-{kind}:{settlement.Id}",
                        $"offboarding-external-{kind}", settlement.Id,
                        $"{settlement.FirstName} {settlement.LastName} ayrılış {label} kanıtını incele",
                        $"Ayrılışın geçerli parasal sürüm #{settlement.RevisionNumber} hesabında dış {label} sonucu kayıtlı değil.",
                        $"Mali bilgilerden ilgili ücret paketi ve ayrılış sürümünü açın; dış {label} sonucunu sorumlu ve dayanak referansıyla doğrulayın.",
                        null,
                        "Bu dış işlem için uygulamada güvenilir son tarih kayıtlı değil.",
                        $"/personnel/{settlement.EmployeeId}?tab=tab-mali"));
                }
            }

            var currentPayrollRevisions = await context.PayrollApprovedRowRevisions.AsNoTracking()
                .Where(revision => revision.PayrollRow!.ApprovalStatus == PayrollApprovalStatus.Approved
                    && (revision.PayrollRow.Period!.Status == PayrollPeriodStatus.Approved
                        || revision.PayrollRow.Period.Status == PayrollPeriodStatus.Closed)
                    && !context.PayrollApprovedRowRevisions.Any(newer =>
                        newer.PayrollRowId == revision.PayrollRowId
                        && newer.RevisionNumber > revision.RevisionNumber))
                .Select(revision => new {
                    revision.Id, revision.RevisionNumber, revision.PayrollRowId,
                    revision.PayrollRow!.EmployeeId,
                    revision.PayrollRow.Employee!.FirstName,
                    revision.PayrollRow.Employee.LastName,
                    revision.PayrollRow.Period!.Year,
                    revision.PayrollRow.Period.Month,
                }).ToListAsync(ct);
            var payrollRevisionIds = currentPayrollRevisions.Select(revision => revision.Id).ToList();
            var payrollExternalRecords = await context.PayrollApprovedRowExternalActionRecords
                .AsNoTracking()
                .Where(record => payrollRevisionIds.Contains(record.RevisionId))
                .Select(record => new { record.Id, record.RevisionId, record.Kind, record.Outcome })
                .ToListAsync(ct);
            var closedPayrollKinds = payrollExternalRecords
                .GroupBy(record => new { record.RevisionId, record.Kind })
                .Select(group => group.MaxBy(record => record.Id)!)
                .Where(record => record.Outcome is "completed" or "not-required")
                .Select(record => $"{record.RevisionId}:{record.Kind}")
                .ToHashSet();
            foreach (var revision in currentPayrollRevisions)
            {
                foreach (var kind in new[] { "payment", "notification" })
                {
                    if (closedPayrollKinds.Contains($"{revision.Id}:{kind}")) continue;
                    var label = kind == "payment" ? "ödeme" : "bildirim";
                    tasks.Add(new DailyGuidanceTaskDto(
                        $"payroll-revision-external-{kind}:{revision.Id}",
                        $"payroll-revision-external-{kind}", revision.Id,
                        $"{revision.FirstName} {revision.LastName} bordro düzeltmesi {label} kanıtını incele",
                        $"{revision.Year}/{revision.Month:00} bordrosunun parasal sürüm #{revision.RevisionNumber} için dış {label} sonucu henüz kanıtla kapatılmadı.",
                        $"Personelin mali bilgilerindeki bordro etki dosyasını açın; dış {label} sonucunu sorumlu ve belge referansıyla kaydedin.",
                        null,
                        "Bu dış işlem için uygulamada güvenilir son tarih kayıtlı değil.",
                        $"/personnel/{revision.EmployeeId}?tab=tab-mali"));
                }
            }

            var periods = await sender.Send(new GetPayrollPeriodsQuery(), ct);
            tasks.AddRange(periods
                .Where(period => period.Status is "draft" or "control")
                .Select(period => new DailyGuidanceTaskDto(
                    $"payroll-period:{period.Id}",
                    "payroll-period",
                    period.Id,
                    $"{period.Name} bordro dönemini incele",
                    $"Dönem {(period.Status == "draft" ? "taslak" : "kontrol")} durumunda; henüz onaylanmadı.",
                    "Satırları ve uyarıları inceleyin; eksikleri giderip uygun satırları ve dönemi onaylayın.",
                    null,
                    "Bu dönem için kayıtlı son tarih yok; dönem ayı karar son tarihi değildir.",
                    $"/payroll?periodId={period.Id}"
                )));

            var newCandidates = await sender.Send(new GetCandidatesQuery(Status: "Yeni"), ct);
            tasks.AddRange(newCandidates.Select(candidate => new DailyGuidanceTaskDto(
                $"candidate-review:{candidate.Id}",
                "candidate-review",
                candidate.Id,
                $"{candidate.Name} başvurusunu incele",
                $"{candidate.AppliedRole} başvurusu Yeni aşamasında; ilk değerlendirme bekliyor.",
                "Adayın bilgilerini ve varsa bağlı pozisyonu inceleyin; değerlendirme sonucuna göre aşamasını güncelleyin.",
                null,
                "Başvuru tarihi karar son tarihi değildir; kayıtlı son tarih yok.",
                $"/recruitment?candidateId={candidate.Id}"
            )));
        }

        return tasks
            .OrderBy(task => task.Kind switch
            {
                "compliance-document" => 0,
                "leave-approval" => 1,
                "offboarding-case" => 2,
                "offboarding-external-payment" or "offboarding-external-notification"
                    or "payroll-revision-external-payment" or "payroll-revision-external-notification" => 3,
                "employee-compensation" => 4,
                "payroll-period" => 5,
                _ => 6,
            })
            .ThenBy(task => task.DueDate ?? DateOnly.MaxValue)
            .ThenBy(task => task.RecordId)
            .ToList();
    }
}
