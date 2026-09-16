namespace Ekipzen.Application.Common.Interfaces;

/// <summary>Gönderilecek e-posta (bildirim tetikleyicilerinin ürettiği yük).</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// E-posta gönderim soyutlaması (plan Faz 11). Geliştirme/demo ortamında dosya
/// outbox'ına yazan implementasyon kullanılır; üretimde SMTP ile değiştirilir.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
