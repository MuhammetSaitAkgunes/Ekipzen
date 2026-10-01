using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Domain.Entities.Guidance;

namespace Ekipzen.Application.Features.Guidance;

internal static class PersonnelEmploymentEvidence
{
    public static void Add(IApplicationDbContext db, ICurrentUser user, GuidanceOperationContext guidance,
        int employeeId, string operation, string[] fields, int periodId, int? packageId = null,
        string? reason = null, int? relatedPackageId = null, int? fromVersion = null, int? toVersion = null)
    {
        var evidence = new GuidancePersonnelEvidence {
            UserId = user.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi."),
            EmployeeId = employeeId, OperationKey = operation, EmploymentPeriodId = periodId,
            CompensationPackageId = packageId, ChangedFieldsJson = JsonSerializer.Serialize(fields), Reason = reason?.Trim(),
            RelatedPackageId = relatedPackageId, FromVersion = fromVersion, ToVersion = toVersion,
        };
        guidance.Stamp(evidence); db.GuidancePersonnelEvidences.Add(evidence);
    }

    public static string[] Changed(CompensationPackageValuesDto before, CompensationPackageValuesDto after)
        => new[] {
            (before.MonthlyGrossSalary != after.MonthlyGrossSalary, "Ücret / Aylık brüt maaş"),
            (before.RegularPremium != after.RegularPremium, "Ücret / Düzenli prim"),
            (before.RoadAllowance != after.RoadAllowance, "Ücret / Yol yardımı"),
            (before.MealAllowance != after.MealAllowance, "Ücret / Yemek yardımı"),
            (before.OtherRegularBenefits != after.OtherRegularBenefits, "Ücret / Diğer düzenli yan hak"),
            (before.SourceReference != after.SourceReference, "Ücret / Kaynak referansı"),
        }.Where(x => x.Item1).Select(x => x.Item2).ToArray();
}
