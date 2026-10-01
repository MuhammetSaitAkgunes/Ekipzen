using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Entities.Payroll;

namespace Ekipzen.Application.Features.Guidance;

public static class FinancialOperationEvidence
{
    public static void RecordPeriod(IApplicationDbContext db, ICurrentUser user, GuidanceOperationContext guidance, PayrollPeriod period, string operation)
    {
        if (!guidance.IsGuided) return;
        var evidence = new GuidancePayrollPeriodEvidence { UserId = PersonnelEntryGuide.RequireUserId(user), PayrollPeriod = period, OperationKey = operation };
        guidance.Stamp(evidence); db.GuidancePayrollPeriodEvidences.Add(evidence);
    }
    public static void RecordOffboarding(IApplicationDbContext db, ICurrentUser user, GuidanceOperationContext guidance, EmployeeOffboardingCase item, string operation)
    {
        if (!guidance.IsGuided) return;
        var evidence = new GuidanceOffboardingEvidence { UserId = PersonnelEntryGuide.RequireUserId(user), OffboardingCase = item, CaseVersion = item.Version, OperationKey = operation };
        guidance.Stamp(evidence); db.GuidanceOffboardingEvidences.Add(evidence);
    }
}
