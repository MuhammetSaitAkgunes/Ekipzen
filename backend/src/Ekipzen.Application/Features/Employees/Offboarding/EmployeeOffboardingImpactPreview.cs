using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Offboarding;

public sealed record OffboardingTeamImpactDto(int EmployeeId, string Name, int? PreviousManagerId,
    string? PreviousManagerName, int? NextManagerId, string? NextManagerName);
public sealed record OffboardingAccessImpactDto(int EmployeeId, string Name, string Operation);
public sealed record EmployeeOffboardingImpactPreviewDto(string Action, int CaseId, int CaseVersion,
    string EmployeeName, DateOnly TerminationDate, int? EmploymentPeriodId, int? CompensationPackageId,
    DateOnly? NextCompensationEndDate, decimal TotalSettlement, string SettlementReference,
    IReadOnlyList<OffboardingTeamImpactDto> TeamTransfers, int PendingLeaveRequestCount,
    IReadOnlyList<OffboardingAccessImpactDto> AccessOperations, IReadOnlyList<string> BlockingReasons, string Token)
{ public bool CanApply => BlockingReasons.Count == 0; }
public sealed record GetEmployeeOffboardingImpactPreviewQuery(int EmployeeId, int CaseId, bool Reverse)
    : IRequest<EmployeeOffboardingImpactPreviewDto>;
public sealed class GetEmployeeOffboardingImpactPreviewQueryHandler(IApplicationDbContext context, IDatabaseLock databaseLock)
    : IRequestHandler<GetEmployeeOffboardingImpactPreviewQuery, EmployeeOffboardingImpactPreviewDto>
{
    public Task<EmployeeOffboardingImpactPreviewDto> Handle(GetEmployeeOffboardingImpactPreviewQuery request, CancellationToken ct)
        => databaseLock.ExecuteExclusiveAsync($"employee-offboarding:{request.EmployeeId}",
            token => OffboardingImpactPreview.ReadAsync(context, request.EmployeeId, request.CaseId, request.Reverse, token), ct);
}

internal static class OffboardingImpactPreview
{
    private sealed record Person(int Id, string FirstName, string LastName, int? ManagerId, EmployeeStatus Status,
        string? UserId, DateOnly HireDate, DateOnly? TerminationDate, EmploymentContractType ContractType, string? ExitCode)
    { public string Name => FirstName + " " + LastName; }

    public static async Task EnsureAsync(IApplicationDbContext context, int employeeId, int caseId, bool reverse,
        string? expectedToken, CancellationToken ct)
    {
        var preview = await ReadAsync(context, employeeId, caseId, reverse, ct);
        if (!string.Equals(expectedToken, preview.Token, StringComparison.Ordinal))
            throw new ConflictException("Ayrılışın etkilediği kayıtlar değişti; etki önizlemesini yenileyip yeniden kontrol edin.");
        if (!preview.CanApply) throw new ConflictException(string.Join(" ", preview.BlockingReasons));
    }

    public static async Task<EmployeeOffboardingImpactPreviewDto> ReadAsync(IApplicationDbContext context,
        int employeeId, int caseId, bool reverse, CancellationToken ct)
    {
        var item = await context.EmployeeOffboardingCases.AsNoTracking().Include(x => x.TeamTransfers)
            .SingleOrDefaultAsync(x => x.Id == caseId && x.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("İşten çıkış dosyası", caseId);
        var organization = await context.Employees.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new Person(x.Id, x.FirstName, x.LastName, x.ManagerId, x.Status, x.UserId,
                x.HireDate, x.TerminationDate, x.ContractType, x.Profile == null ? null : x.Profile.ExitCode)).ToListAsync(ct);
        var people = organization.ToDictionary(x => x.Id);
        var employee = people.GetValueOrDefault(employeeId) ?? throw new NotFoundException("Personel", employeeId);
        var employment = reverse
            ? await context.EmployeeEmploymentPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.EmploymentPeriodId && x.EmployeeId == employeeId, ct)
            : await context.EmployeeEmploymentPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EndDate == null, ct);
        var compensation = reverse
            ? await context.EmployeeCompensationPackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.CompensationPackageId && x.EmploymentPeriodId == item.EmploymentPeriodId, ct)
            : employment == null ? null : await context.EmployeeCompensationPackages.AsNoTracking().Where(x => x.EmploymentPeriodId == employment.Id
                && x.EffectiveFrom <= item.TerminationDate && (x.EffectiveTo == null || x.EffectiveTo >= item.TerminationDate))
                .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var hasSettlementRevisions = await context.EmployeeOffboardingSettlementRevisions.AnyAsync(x => x.OffboardingCaseId == caseId, ct);
        var reasons = new List<string>();
        var transfers = new List<OffboardingTeamImpactDto>();
        var access = new List<OffboardingAccessImpactDto>();
        void Transfer(int id, int? from, int? to) => transfers.Add(new(id, people[id].Name, from,
            from is int f ? people.GetValueOrDefault(f)?.Name : null, to, to is int t ? people.GetValueOrDefault(t)?.Name : null));
        void Access(int id, string action) => access.Add(new(id, people.GetValueOrDefault(id)?.Name ?? $"Personel #{id}", action));

        if (!reverse)
        {
            if (item.Status != EmployeeOffboardingStatus.Draft) reasons.Add("Yalnız taslak dosya tamamlanabilir.");
            if (employee.Status != EmployeeStatus.Active) reasons.Add("Personel artık aktif değil.");
            if (item.TerminationDate < employee.HireDate) reasons.Add("Ayrılış tarihi işe girişten önce olamaz.");
            if (employment is null) reasons.Add("Açık istihdam dönemi bulunamadı.");
            else
            {
                if (item.TerminationDate < employment.StartDate) reasons.Add("Ayrılış tarihi açık istihdam döneminden önce olamaz.");
                if (item.EmploymentPeriodId != null && item.EmploymentPeriodId != employment.Id) reasons.Add("İstihdam dönemi değişti; taslağı yenileyin.");
                if (item.ContractTypeSnapshot != employment.ContractType || item.ContractTypeSnapshot != employee.ContractType)
                    reasons.Add("Sözleşme türü değişti; taslağı yenileyin.");
            }
            if (compensation?.Id != item.CompensationPackageId || (compensation != null &&
                (compensation.MonthlyGrossSalary != item.MonthlyGrossSalarySnapshot || compensation.RegularPremium != item.RegularPremiumSnapshot
                || compensation.RoadAllowance != item.RoadAllowanceSnapshot || compensation.MealAllowance != item.MealAllowanceSnapshot
                || compensation.OtherRegularBenefits != item.OtherRegularBenefitsSnapshot))) reasons.Add("Ücret paketi değişti; taslağı yenileyin.");
            var reports = organization.Where(x => x.ManagerId == employeeId).ToArray();
            var successor = item.SuccessorManagerId is int s ? people.GetValueOrDefault(s) : null;
            if (reports.Length == 0 && item.SuccessorManagerId != null) reasons.Add("Ekibi olmayan personel için halef belirtilemez.");
            if (reports.Length > 0)
            {
                if (successor is null || successor.Id == employeeId || successor.Status != EmployeeStatus.Active || string.IsNullOrWhiteSpace(successor.UserId))
                    reasons.Add("Ekip için aktif, giriş hesabı bağlı ve farklı bir halef seçin.");
                else
                {
                    var directSuccessor = reports.Any(x => x.Id == successor.Id);
                    if (directSuccessor)
                    {
                        if (employee.ManagerId == successor.Id) reasons.Add("Mevcut yönetici hiyerarşisi döngü içeriyor.");
                        Transfer(successor.Id, successor.ManagerId, employee.ManagerId);
                    }
                    else
                    {
                        var visited = new HashSet<int>();
                        var cursor = successor.ManagerId;
                        while (cursor is int id && visited.Add(id))
                        {
                            if (id == employeeId) { reasons.Add("Alt ekipten halef yalnız doğrudan raporlar arasından seçilebilir."); break; }
                            cursor = people.GetValueOrDefault(id)?.ManagerId;
                        }
                    }
                    foreach (var report in reports.Where(x => x.Id != successor.Id)) Transfer(report.Id, report.ManagerId, successor.Id);
                    Access(successor.Id, "Yönetici rolü verilecek");
                }
            }
            Access(employeeId, "Yönetici rolü kaldırılacak");
            Access(employeeId, "Çalışan erişimi kapatılacak");
        }
        else
        {
            if (item.Status != EmployeeOffboardingStatus.Completed) reasons.Add("Yalnız tamamlanmış dosya geri alınabilir.");
            if (hasSettlementRevisions) reasons.Add("Parasal düzeltme geçmişi var; otomatik geri alma kilitli. Uzman incelemesi gerekir.");
            if (employee.Status != EmployeeStatus.Passive || employee.TerminationDate != item.TerminationDate) reasons.Add("Personel durumu ayrılış kaydıyla uyuşmuyor.");
            if (employment is null || employment.EndDate != item.TerminationDate) reasons.Add("Kapatılan istihdam dönemi değişmiş veya bulunamıyor.");
            if (item.CompensationPackageId != null && (compensation == null || compensation.EffectiveTo != item.TerminationDate)) reasons.Add("Kapatılan ücret paketi değişmiş veya bulunamıyor.");
            foreach (var transfer in item.TeamTransfers.OrderBy(x => x.EmployeeId))
            {
                if (!people.TryGetValue(transfer.EmployeeId, out var person) || person.ManagerId != transfer.AssignedManagerId)
                    reasons.Add($"Personel #{transfer.EmployeeId} ekip bağı değişmiş veya kayıt bulunamıyor.");
                if (person != null) Transfer(person.Id, person.ManagerId, transfer.PreviousManagerId);
            }
            var restored = organization.ToDictionary(x => x.Id);
            restored[employeeId] = employee with { Status = EmployeeStatus.Active };
            foreach (var t in transfers) restored[t.EmployeeId] = restored[t.EmployeeId] with { ManagerId = t.NextManagerId };
            foreach (var id in item.TeamTransfers.Where(x => x.AssignedManagerId != null).Select(x => x.AssignedManagerId!.Value).Append(employeeId).Distinct().Order())
                Access(id, restored.TryGetValue(id, out var manager) && manager.Status == EmployeeStatus.Active
                    && restored.Values.Any(x => x.Status == EmployeeStatus.Active && x.ManagerId == id)
                    ? "Yönetici rolü verilecek" : "Yönetici rolü kaldırılacak");
            Access(employeeId, "Çalışan erişimi açılacak");
        }
        var reportIds = reverse ? transfers.Select(x => x.EmployeeId).ToArray()
            : organization.Where(x => x.ManagerId == employeeId).Select(x => x.Id).ToArray();
        var pendingLeaves = await context.LeaveRequests.AsNoTracking().Where(x => reportIds.Contains(x.EmployeeId) && x.Status == LeaveStatus.Pending)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.EmployeeId, x.StartDate, x.EndDate }).ToListAsync(ct);
        var preview = new EmployeeOffboardingImpactPreviewDto(reverse ? "reverse" : "complete", caseId, item.Version,
            employee.Name, item.TerminationDate, employment?.Id, compensation?.Id,
            reverse ? item.CompensationPackagePreviousEffectiveTo : item.TerminationDate,
            item.ToDto().TotalPayment, item.SettlementReference, transfers, pendingLeaves.Count, access, reasons, "");
        // Only a hash leaves the server; account identifiers and organization outside the affected team are not exposed.
        var source = new { Preview = preview, Case = item.ToDto(), Organization = organization, PendingLeaves = pendingLeaves,
            HasSettlementRevisions = hasSettlementRevisions,
            Employment = employment == null ? null : new { employment.Id, employment.StartDate, employment.EndDate, employment.ContractType },
            Compensation = compensation == null ? null : new { compensation.Id, compensation.Version, compensation.EffectiveFrom, compensation.EffectiveTo,
                compensation.MonthlyGrossSalary, compensation.RegularPremium, compensation.RoadAllowance, compensation.MealAllowance, compensation.OtherRegularBenefits, compensation.SourceReference } };
        return preview with { Token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(source)))) };
    }
}
