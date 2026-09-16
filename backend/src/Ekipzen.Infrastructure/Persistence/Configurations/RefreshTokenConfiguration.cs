using Ekipzen.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

// RefreshToken yapılandırması BURADA DEĞİL: RefreshToken platform veritabanında
// yaşar (bkz. RefreshToken.cs xmldoc), eşlemesi PlatformDbContext.OnModelCreating
// içindedir. Bu dosya (AppDbContext'in konfigürasyon klasörü) yalnız uygulama
// veritabanındaki tipleri yapılandırır.

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
        b.Property(u => u.Initials).HasMaxLength(8);
    }
}
