using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ekipzen.Infrastructure.Email;

/// <summary>
/// Geliştirme/demo e-posta göndericisi: her mesajı {Storage:Root}/outbox altına
/// JSON dosyası olarak bırakır (SMTP yok). Üretimde IEmailSender'ın SMTP
/// implementasyonuyla değiştirilir; iş kodu soyutlamaya bağlıdır.
/// </summary>
public sealed class FileOutboxEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _outboxRoot;
    private readonly ILogger<FileOutboxEmailSender> _logger;

    public FileOutboxEmailSender(IConfiguration configuration, ILogger<FileOutboxEmailSender> logger)
    {
        var storageRoot = configuration["Storage:Root"] ?? Path.Combine("App_Data", "storage");
        _outboxRoot = Path.GetFullPath(Path.Combine(storageRoot, "outbox"));
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_outboxRoot);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        var payload = JsonSerializer.Serialize(
            new { message.To, message.Subject, message.Body, SentAtUtc = DateTime.UtcNow },
            SerializerOptions);

        await File.WriteAllTextAsync(Path.Combine(_outboxRoot, fileName), payload, cancellationToken);
        _logger.LogInformation("E-posta outbox'a yazıldı: {To} — {Subject}", message.To, message.Subject);
    }
}
