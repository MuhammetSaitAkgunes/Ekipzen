using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>
/// Kurumsal departman. Frontend'de personel `dept` string'i olarak geçer; burada
/// FK ile normalize edilir (Yazılım, İnsan Kaynakları, Tasarım, Satış, Operasyon, Finans).
/// </summary>
public class Department : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
