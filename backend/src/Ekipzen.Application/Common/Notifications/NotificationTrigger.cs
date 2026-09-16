using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Common.Notifications;

/// <summary>
/// Bildirim tetikleyicileri (plan Faz 11): ayarlar ekranındaki toggle'lara uyar.
/// Toggle kapalıysa hiç e-posta üretilmez. Alıcı, şirket profili sistem e-postasıdır
/// (İK operasyon kutusu); gönderim hatası iş akışını asla bozmaz.
/// </summary>
public interface INotificationTrigger
{
    Task NewPersonnelCreatedAsync(Employee employee, CancellationToken cancellationToken);
    Task LeaveRequestCreatedAsync(LeaveRequest request, CancellationToken cancellationToken);
}

public sealed class NotificationTrigger(IApplicationDbContext context, IEmailSender emailSender)
    : INotificationTrigger
{
    private const string FallbackRecipient = "ik@hrmaster.local";

    public async Task NewPersonnelCreatedAsync(Employee employee, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await context.NotificationSettings
                .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.NewPersonnelEmail)
            {
                return;
            }

            await emailSender.SendAsync(new EmailMessage(
                await RecipientAsync(cancellationToken),
                "Yeni personel kaydı",
                $"{employee.FullName} ({employee.Title}) sisteme eklendi."),
                cancellationToken);
        }
        catch
        {
            // Bildirim gönderimi asıl işlemi (personel kaydı) geri döndürmemeli.
        }
    }

    public async Task LeaveRequestCreatedAsync(LeaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await context.NotificationSettings
                .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.LeaveRequestEmail)
            {
                return;
            }

            var employeeName = await context.Employees
                .Where(e => e.Id == request.EmployeeId)
                .Select(e => e.FirstName + " " + e.LastName)
                .FirstOrDefaultAsync(cancellationToken) ?? "Personel";

            await emailSender.SendAsync(new EmailMessage(
                await RecipientAsync(cancellationToken),
                "Yeni izin talebi",
                $"{employeeName}, {request.StartDate:dd.MM.yyyy} - {request.EndDate:dd.MM.yyyy} " +
                $"aralığı için {request.Days} günlük izin talebi oluşturdu."),
                cancellationToken);
        }
        catch
        {
            // Bildirim gönderimi asıl işlemi (izin talebi) geri döndürmemeli.
        }
    }

    private async Task<string> RecipientAsync(CancellationToken cancellationToken)
        => await context.CompanyProfiles
               .AsNoTracking()
               .Select(p => p.SystemEmail)
               .FirstOrDefaultAsync(cancellationToken)
           ?? FallbackRecipient;
}
