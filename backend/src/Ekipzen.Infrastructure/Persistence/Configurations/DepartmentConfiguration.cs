using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(d => d.Name).IsRequired().HasMaxLength(128);
        b.Property(d => d.Code).HasMaxLength(32);
        // Tekillik KİRACI İÇİNDE geçerlidir. TenantId olmadan ilk müşterinin
        // "Yazılım" departmanı, ikinci müşterinin aynı adı kullanmasını engellerdi.
        b.HasIndex(d => new { d.TenantId, d.Name }).IsUnique();
    }
}
