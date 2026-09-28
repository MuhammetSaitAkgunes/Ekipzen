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
                "employee-compensation" => 3,
                "payroll-period" => 4,
                _ => 5,
            })
            .ThenBy(task => task.DueDate ?? DateOnly.MaxValue)
            .ThenBy(task => task.RecordId)
            .ToList();
    }
}
