using Ekipzen.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> b)
    {
        b.Property(c => c.Name).IsRequired().HasMaxLength(200);
        b.Property(c => c.Website).HasMaxLength(200);
        b.Property(c => c.SystemEmail).HasMaxLength(200);
        b.Property(c => c.Phone).HasMaxLength(32);
        b.Property(c => c.HeadquartersAddress).HasMaxLength(500);
        b.Property(c => c.LogoPath).HasMaxLength(500);
    }
}

