using Ekipzen.Application.Common.Interfaces;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ekipzen.Infrastructure.Email;

/// <summary>
/// Üretim e-posta göndericisi: MailKit ile gerçek SMTP. Yalnız <c>Email:Mode=smtp</c>
/// iken kaydedilir (bkz. DependencyInjection); dev/test outbox stub'ında kalır.
/// Kuyruk/retry kapsam dışıdır — gönderim hatası çağırana (davet akışı) yükselir.
/// </summary>
public sealed class MailKitSmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<MailKitSmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        var security = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
        await client.ConnectAsync(smtp.Host, smtp.Port, security, cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtp.User))
        {
            await client.AuthenticateAsync(smtp.User, smtp.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("E-posta SMTP ile gönderildi: {To} — {Subject}", message.To, message.Subject);
    }
}
