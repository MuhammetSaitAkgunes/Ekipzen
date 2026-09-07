using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Enums;
using IKPro.Domain.Services;

namespace IKPro.Application.Features.Payroll.Periods;

/// <summary>Bordro satırının puantaj, sınıflandırma ve parasal onay kapıları.</summary>
public static class PayrollRowReadiness
{
    public static async Task RefreshTimesheetCompletenessAsync(
        IApplicationDbContext context,
        PayrollPeriod period,
        IReadOnlyCollection<PayrollEmployee> rows,
        CancellationToken cancellationToken)
    {
        var employmentPeriods = rows.ToDictionary(
            row => row.EmployeeId,
            row => row.Employee is { } employee
                ? new PayrollEmploymentPeriod(employee.HireDate, employee.TerminationDate)
                : throw new InvalidOperationException(
                    "Puantaj kontrolü için çalışan bilgisi yüklenmelidir."));
        var coverage = await PayrollTimesheetCoverage.CalculateAsync(
            context, employmentPeriods, period.Year, period.Month, cancellationToken);

        foreach (var row in rows)
            row.TimesheetComplete = coverage.GetValueOrDefault(row.EmployeeId);
    }

    public static bool IsReady(PayrollEmployee row, PayrollCalculation calculation) =>
        GetBlockingReasons(row, calculation).Count == 0;

    public static void EnsureApprovable(PayrollEmployee row, PayrollCalculation calculation)
    {
        var reasons = GetBlockingReasons(row, calculation);
        if (reasons.Count > 0)
        {
            throw new ConflictException(
                "Bordro satırı kanuni kontrol kapılarını geçemedi: " + string.Join("; ", reasons));
        }
    }

    public static IReadOnlyList<string> GetBlockingReasons(
        PayrollEmployee row,
        PayrollCalculation calculation)
    {
        var reasons = new List<string>();

        if (row.StatutoryProfile != PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA)
            reasons.Add("standart yurt içi özel sektör 4/a profili doğrulanmadı");
        if (!row.IbanComplete) reasons.Add("IBAN eksik");
        if (!row.TimesheetComplete) reasons.Add("puantaj eksik");
        if (!row.SgkPremiumDaysConfirmed) reasons.Add("SGK prim günü doğrulanmadı");
        if (row.OvertimeHours > 0 && !row.OvertimeTreatmentConfirmed)
            reasons.Add("fazla çalışma türü, çalışan onayı veya yıllık 270 saat kontrolü doğrulanmadı");
        if (row.GrossSalary <= 0m) reasons.Add("brüt maaş sıfır");
        if (row.Period?.Settings is { } settings
            && row.WorkedDays == settings.DefaultWorkedDays
            && row.GrossSalary < settings.MinWageGross)
            reasons.Add("aylık brüt ücret yürürlükteki asgari ücretin altında");
        if (row.DisabilityReliefType == PayrollDisabilityReliefType.Unspecified)
            reasons.Add("engellilik indirimi durumu seçilmedi");
        if (row.DisabilityReliefType is PayrollDisabilityReliefType.FirstDegree
                or PayrollDisabilityReliefType.SecondDegree
                or PayrollDisabilityReliefType.ThirdDegree
            && string.IsNullOrWhiteSpace(row.DisabilityReliefReference))
            reasons.Add("engellilik indirimi uygunluk belgesi/referansı eksik");
        if (!row.OtherIncomeTaxBaseDeductionsConfirmedNone)
            reasons.Add("diğer gelir vergisi matrah indirimlerinin bulunmadığı doğrulanmadı");
        if (!row.CumulativeIncomeTaxBaseConfirmed)
            reasons.Add("kümülatif gelir vergisi matrahı başlangıcı doğrulanmadı");
        if (row.PreviousEmployerIncomeTaxBase > 0m
            && string.IsNullOrWhiteSpace(row.PreviousEmployerIncomeTaxBaseReference))
            reasons.Add("önceki işveren vergi matrahı talep/kabul referansı eksik");
        if (calculation.Warnings.Contains("Engellilik indirimi mevzuat parametresi eksik"))
            reasons.Add("engellilik indirimi mevzuat parametresi eksik");
        if (row.MealAllowance > 0m && row.MealTreatment == PayrollMealTreatment.Unspecified)
            reasons.Add("yemek yardımının vergi/SGK sınıfı seçilmedi");
        if (row.MealAllowance > 0m
            && row.MealTreatment == PayrollMealTreatment.CashExemptWhereNoMealProvided
            && row.MealDays <= 0)
            reasons.Add("yemek istisnası fiili çalışma günü eksik");
        if (row.BenefitPay > 0m && !row.BenefitPayFullyTaxableConfirmed)
            reasons.Add("diğer ödemenin tam prime/vergiye tabi olduğu doğrulanmadı");
        if (row.EmployerSgkDiscount != EmployerSgkDiscountType.None
            && string.IsNullOrWhiteSpace(row.EmployerSgkDiscountReference))
            reasons.Add("SGK işveren indirimi uygunluk referansı eksik");
        if (row.Period is not null && row.Period.Settings?.Status != PayrollSettingsStatus.Approved)
            reasons.Add("bordro mevzuat seti onaylı değil");
        if (calculation.NetPay <= 0m) reasons.Add("net ödeme sıfır veya negatif");
        if (calculation.Warnings.Contains("SGK prim günü olmayan ek ödeme dağıtım bekliyor"))
            reasons.Add("prim günü olmayan ek ödemenin SGK ay dağıtımı bekliyor");

        return reasons;
    }
}
