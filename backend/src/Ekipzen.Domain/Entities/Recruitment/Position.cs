using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>
/// Açık pozisyon / rol. İşe alım funnel'ının ve aday başvurularının hedefi.
/// </summary>
public class Position : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsOpen { get; set; } = true;
    public int OpenCount { get; set; } = 1;

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
