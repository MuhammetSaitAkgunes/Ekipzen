using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Compliance.Queries;
using Ekipzen.Application.Features.Leaves.Queries;
using Ekipzen.Domain.Constants;
using MediatR;

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

public sealed class GetDailyGuidanceTasksQueryHandler(ISender sender, ICurrentUser currentUser)
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
        }

        return tasks
            .OrderBy(task => task.Kind == "compliance-document" ? 0 : 1)
            .ThenBy(task => task.DueDate ?? DateOnly.MaxValue)
            .ThenBy(task => task.RecordId)
            .ToList();
    }
}
