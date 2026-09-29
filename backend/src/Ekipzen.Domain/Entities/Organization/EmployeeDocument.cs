using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>
/// Personele bağlı yüklenen evrak (nüfus cüzdanı, ikametgah, adli sicil, özlük evrakları).
/// Dosya IFileStorage ile diskte tutulur; burada yalnız metadata + yol.
/// </summary>
public class EmployeeDocument : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    // Geri çekme metadata'sı denetim izidir; dosya fiziksel silinince de korunur.
    public DateTime? WithdrawnAtUtc { get; set; }
    public string? WithdrawnByUserId { get; set; }
    public string? WithdrawalReason { get; set; }
}
