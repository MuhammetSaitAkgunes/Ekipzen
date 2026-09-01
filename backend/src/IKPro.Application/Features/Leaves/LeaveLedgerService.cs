using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Leaves;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Leaves;

/// <summary>
/// İstihdam dönemi bazlı izin defterinin tek yazma/okuma kuralı. Mevzuat günü
/// hesaplamaz; dönemde snapshot olarak duran şirket politika gününü yıldönümünde yazar.
/// </summary>
public static class LeaveLedgerService
{
    public static async Task<EmployeeEmploymentPeriod> RequirePeriodAsync(
        IApplicationDbContext context,
        int employeeId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var period = await context.EmployeeEmploymentPeriods
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId
                && item.StartDate <= startDate
                && (item.EndDate == null || item.EndDate >= endDate), cancellationToken);

        return period ?? throw new ConflictException(
            "İzin tarihleri tek ve geçerli bir istihdam döneminin içinde olmalıdır.");
    }

    public static async Task<EmployeeEmploymentPeriod> RequireLatestPeriodAsync(
        IApplicationDbContext context,
        int employeeId,
        int year,
        CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var period = await context.EmployeeEmploymentPeriods
            .Where(item => item.EmployeeId == employeeId
                && item.StartDate <= yearEnd
                && (item.EndDate == null || item.EndDate >= yearStart))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return period ?? throw new ConflictException(
            "Seçilen yıl için istihdam dönemi bulunamadı.");
    }

    public static async Task<int> EnsureAccrualsAsync(
        IApplicationDbContext context,
        EmployeeEmploymentPeriod period,
        DateOnly throughDate,
        CancellationToken cancellationToken)
    {
        var managedFrom = period.LeaveAccrualManagedFrom ?? period.StartDate;
        var effectiveThrough = period.EndDate is { } endDate && endDate < throughDate
            ? endDate
            : throughDate;
        if (effectiveThrough < managedFrom || period.AnnualLeaveEntitlementDays <= 0)
        {
            return 0;
        }

        var existingServiceYears = await context.LeaveLedgerEntries
            .Where(item => item.EmploymentPeriodId == period.Id
                && item.Type == LeaveLedgerEntryType.ServiceAnniversaryEntitlement
                && item.ServiceYear != null)
            .Select(item => item.ServiceYear!.Value)
            .ToListAsync(cancellationToken);
        var existing = existingServiceYears.ToHashSet();
        var added = 0;

        for (var serviceYear = 1; ; serviceYear++)
        {
            var anniversary = period.StartDate.AddYears(serviceYear);
            if (anniversary > effectiveThrough)
            {
                break;
            }
            if (anniversary < managedFrom || existing.Contains(serviceYear))
            {
                continue;
            }

            context.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmploymentPeriodId = period.Id,
                OccurredOn = anniversary,
                Type = LeaveLedgerEntryType.ServiceAnniversaryEntitlement,
                Days = period.AnnualLeaveEntitlementDays,
                ServiceYear = serviceYear,
                SourceKey = $"service-anniversary:{period.Id}:{serviceYear}",
                SourceReference = $"{serviceYear}. hizmet yıldönümü hak edişi",
            });
            added++;
        }

        return added;
    }

    public static async Task EnsureCanReserveAsync(
        IApplicationDbContext context,
        EmployeeEmploymentPeriod period,
        DateOnly requestDate,
        int requestedDays,
        int? excludeRequestId,
        CancellationToken cancellationToken)
    {
        var ledger = await context.LeaveLedgerEntries
            .AsNoTracking()
            .Where(item => item.EmploymentPeriodId == period.Id)
            .Select(item => new DatedMovement(item.OccurredOn, item.Days, item.Days >= 0 ? 0 : 1, false))
            .ToListAsync(cancellationToken);

        var pending = await context.LeaveRequests
            .AsNoTracking()
            .Where(item => item.EmploymentPeriodId == period.Id
                && item.Status == LeaveStatus.Pending
                && item.LeaveType!.DeductsFromAnnualBalance
                && (excludeRequestId == null || item.Id != excludeRequestId))
            .Select(item => new DatedMovement(item.StartDate, -item.Days, 2, false))
            .ToListAsync(cancellationToken);

        var candidate = new DatedMovement(requestDate, -requestedDays, 3, true);
        var movements = ledger.Concat(pending).Append(candidate)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Order)
            .ToList();

        var running = 0;
        foreach (var movement in movements)
        {
            var before = running;
            running += movement.Days;
            if (running < 0)
            {
                var available = movement.IsCandidate ? Math.Max(0, before) : Math.Max(0, running - movement.Days);
                throw new ConflictException(
                    $"Yetersiz izin bakiyesi: talep {requestedDays} gün, "
                    + $"{requestDate:yyyy-MM-dd} tarihinde kullanılabilir {available} gün.");
            }
        }
    }

    public static async Task AddUsageEntriesAsync(
        IApplicationDbContext context,
        LeaveRequest request,
        IWorkingDayCalculator workingDayCalculator,
        bool reversal,
        CancellationToken cancellationToken)
    {
        for (var year = request.StartDate.Year; year <= request.EndDate.Year; year++)
        {
            var segmentStart = request.StartDate > new DateOnly(year, 1, 1)
                ? request.StartDate
                : new DateOnly(year, 1, 1);
            var segmentEnd = request.EndDate < new DateOnly(year, 12, 31)
                ? request.EndDate
                : new DateOnly(year, 12, 31);
            var days = await workingDayCalculator.GetWorkingDaysAsync(
                segmentStart, segmentEnd, cancellationToken);
            if (days == 0)
            {
                continue;
            }

            context.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmploymentPeriodId = request.EmploymentPeriodId,
                LeaveRequestId = request.Id,
                OccurredOn = segmentStart,
                Type = reversal ? LeaveLedgerEntryType.UsageReversal : LeaveLedgerEntryType.Usage,
                Days = reversal ? days : -days,
                SourceKey = $"leave-request:{request.Id}:{(reversal ? "reversal" : "usage")}:{year}",
                SourceReference = $"İzin talebi #{request.Id} {(reversal ? "kullanım iadesi" : "kullanımı")}",
            });
        }
    }

    public static async Task<LeaveBalanceDto> GetBalanceAsync(
        IApplicationDbContext context,
        EmployeeEmploymentPeriod period,
        int year,
        DateOnly throughDate,
        CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var effectiveThrough = throughDate > new DateOnly(year, 12, 31)
            ? new DateOnly(year, 12, 31)
            : throughDate;
        var entries = await context.LeaveLedgerEntries
            .AsNoTracking()
            .Where(item => item.EmploymentPeriodId == period.Id && item.OccurredOn <= effectiveThrough)
            .ToListAsync(cancellationToken);

        var carried = entries.Where(item => item.OccurredOn < yearStart).Sum(item => item.Days);
        var inYear = entries.Where(item => item.OccurredOn >= yearStart).ToList();
        var entitled = inYear
            .Where(item => item.Days > 0 && item.Type is not LeaveLedgerEntryType.UsageReversal)
            .Sum(item => item.Days);
        var used = -inYear.Where(item => item.Type == LeaveLedgerEntryType.Usage).Sum(item => item.Days)
            - inYear.Where(item => item.Type == LeaveLedgerEntryType.UsageReversal).Sum(item => item.Days);
        var pending = await context.LeaveRequests
            .AsNoTracking()
            .Where(item => item.EmploymentPeriodId == period.Id
                && item.Status == LeaveStatus.Pending
                && item.LeaveType!.DeductsFromAnnualBalance
                && item.StartDate.Year == year)
            .SumAsync(item => item.Days, cancellationToken);
        var remaining = entries.Sum(item => item.Days);

        var nextAccrual = NextAccrualDate(period, effectiveThrough);
        return new LeaveBalanceDto(
            year,
            entitled,
            carried,
            Math.Max(0, used),
            pending,
            remaining,
            effectiveThrough,
            nextAccrual,
            period.Id);
    }

    public static async Task<IReadOnlyList<LeaveLedgerEntryDto>> GetEntriesAsync(
        IApplicationDbContext context,
        int employmentPeriodId,
        int? year,
        CancellationToken cancellationToken)
    {
        var query = context.LeaveLedgerEntries
            .AsNoTracking()
            .Where(item => item.EmploymentPeriodId == employmentPeriodId);
        if (year is not null)
        {
            var start = new DateOnly(year.Value, 1, 1);
            var end = new DateOnly(year.Value, 12, 31);
            query = query.Where(item => item.OccurredOn >= start && item.OccurredOn <= end);
        }

        return await query
            .OrderByDescending(item => item.OccurredOn)
            .ThenByDescending(item => item.Id)
            .Select(item => new LeaveLedgerEntryDto(
                item.Id,
                item.EmploymentPeriodId,
                item.LeaveRequestId,
                item.OccurredOn,
                item.Type == LeaveLedgerEntryType.LegacyOpening ? "legacy-opening"
                    : item.Type == LeaveLedgerEntryType.ServiceAnniversaryEntitlement ? "service-anniversary"
                    : item.Type == LeaveLedgerEntryType.Usage ? "usage"
                    : item.Type == LeaveLedgerEntryType.UsageReversal ? "usage-reversal"
                    : "adjustment",
                item.Days,
                item.ServiceYear,
                item.SourceReference))
            .ToListAsync(cancellationToken);
    }

    private static DateOnly? NextAccrualDate(EmployeeEmploymentPeriod period, DateOnly after)
    {
        var serviceYear = Math.Max(1, after.Year - period.StartDate.Year);
        var candidate = period.StartDate.AddYears(serviceYear);
        if (candidate <= after)
        {
            candidate = period.StartDate.AddYears(++serviceYear);
        }
        if (period.EndDate is { } endDate && candidate > endDate)
        {
            return null;
        }
        return candidate;
    }

    private sealed record DatedMovement(DateOnly Date, int Days, int Order, bool IsCandidate);
}
