using IKPro.Application.Common.Interfaces;
using IKPro.Infrastructure.Identity;
using IKPro.Infrastructure.Persistence;
using IKPro.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IKPro.Infrastructure;

/// <summary>
/// Infrastructure katmanı servis kayıtları: DbContext + audit interceptor,
/// Identity + JWT Bearer kimlik doğrulama, token servisleri.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Açılışta doğrula: yapılandırma eksikse uygulama hiç ayağa kalkmasın
        // (eski davranış). Sonuç burada KULLANILMAZ — çözüm çalışma zamanında
        // kapsam başına ITenantConnectionResolver üzerinden yapılır (aşağıdaki
        // AddDbContext lambda'sı). Bu satır yalnız fail-fast için var: aksi halde
        // bozuk yapılandırmayla yapılan bir dağıtım temiz açılır, sağlıklı görünür
        // ve hata ancak ilk gerçek istekte 500 olarak çıkar (Development'ta fark
        // görünmez çünkü Program.cs açılışta AppDbContextInitializer'ı çözerek
        // DbContext'i zorluyor; üretimde o blok çalışmaz).
        _ = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil.");

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<ITenantConnectionResolver, Tenancy.TenantConnectionResolver>();
        // SINGLETON: yalnız IServiceScopeFactory'yi (kendisi zaten singleton) sarar,
        // durumu yok. Impersonate çağıran HTTP-dışı yollar (purge, seed, arka plan
        // servisi, test yardımcıları) kapsamı BUNUN ÜZERİNDEN açmalı — doğrudan
        // ICurrentTenant.Impersonate çağırıp sıraya güvenmek yerine (bkz. arayüz dokümanı).
        services.AddSingleton<ITenantScopeFactory, Tenancy.TenantScopeFactory>();

        // Kiracı veritabanını fiziksel olarak kuran/düşüren yetenek. Durumsuzdur
        // (her çağrı kendi bağlantısını açar), bu yüzden singleton.
        services.AddSingleton<ITenantDatabaseProvisioner, Tenancy.SqlServerTenantDatabaseProvisioner>();

        // Yeni kiracı veritabanına migration + rol tohumlaması. Kendi kapsamını
        // açtığı için (ITenantScopeFactory) durumsuzdur → singleton.
        services.AddSingleton<ITenantSchemaInitializer, Tenancy.TenantSchemaInitializer>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Bağlantı KAPSAM BAŞINA çözülür: aktif kiracı neyse ona göre.
            // Faz 1b'de sonuç herkes için aynı, ama çağrı yolu artık doğru yerden
            // geçiyor — Faz 2'de yalnız çözücünün içi değişecek.
            var cozucu = sp.GetRequiredService<ITenantConnectionResolver>();
            var kiraci = sp.GetRequiredService<ICurrentTenant>();

            options.UseSqlServer(cozucu.ResolveFor(kiraci.TenantId), sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        // Platform veritabanı: kiracı kimliği. Uygulama veritabanından AYRI bir
        // katalog; bağlantı dizesi tanımlı değilse fail-fast (sessizce uygulama
        // veritabanına düşmek, iki katmanı gizlice birleştirmek olurdu).
        var platformConnectionString = configuration.GetConnectionString("PlatformConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:PlatformConnection tanımlı değil.");

        services.AddDbContext<PlatformDbContext>(options =>
            options.UseSqlServer(platformConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Platform");
            }));

        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());
        services.AddScoped<PlatformDbInitializer>();

        // Identity çekirdeği + roller + SignInManager (lockout kontrolü için).
        // Parola politikası demo hesaplarla (demo123) uyumlu tutulur.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders(); // davet/şifre-belirleme token'ları (GeneratePasswordResetTokenAsync) için

        // JWT ayarları + Bearer kimlik doğrulama.
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt ayar bölümü eksik.");
        if (jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Secret en az 32 karakter olmalı.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Token'lar kısa claim adlarıyla üretilir; eski SOAP claim eşlemesi kapalı.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
            });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IWorkingDayCalculator, SqlWorkingDayCalculator>();
        services.AddSingleton<Domain.Services.IPayrollEngine, Domain.Services.PayrollEngine>();
        services.AddSingleton<Application.Features.Payroll.Payslips.IPayslipGenerator, Pdf.QuestPdfPayslipGenerator>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserDirectorySource, Identity.UserDirectorySource>();
        services.AddScoped<ITenantDirectory, Tenancy.TenantDirectory>();
        services.AddScoped<ITenantPurger, Persistence.TenantPurger>();
        // Kütüğün önbelleği SINGLETON'dır — okuma anında kendi kapsamını açar
        // (bkz. TenantRegistry), bu yüzden scoped platform context'i captive
        // dependency olarak yakalamaz.
        services.AddMemoryCache();
        services.AddSingleton<ITenantRegistry, Tenancy.TenantRegistry>();
        // Erişim kapısı: durum dışında bağımlılığı yok (kütükten okur), scoped
        // yeterli — captive dependency riski taşımaz.
        services.AddScoped<ITenantAccessGuard, Tenancy.TenantAccessGuard>();
        // Kiracıya bağlı olduğu için SCOPED: singleton olsaydı ilk isteğin kiracısı
        // sonsuza kadar yapışırdı (captive dependency).
        services.AddScoped<IFileStorage, Storage.LocalFileStorage>();
        // E-posta: Email:Mode=smtp → gerçek SMTP (MailKit, fail-fast doğrulamalı);
        // aksi halde (varsayılan "outbox") dosya stub'ı — dev/test davranışı değişmez.
        var emailMode = configuration["Email:Mode"] ?? "outbox";
        if (string.Equals(emailMode, "smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<Email.SmtpOptions>()
                .Bind(configuration.GetSection(Email.SmtpOptions.SectionName))
                .Validate(o => { o.Validate(); return true; })
                .ValidateOnStart();
            services.AddSingleton<IEmailSender, Email.MailKitSmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, Email.FileOutboxEmailSender>();
        }
        services.AddScoped<AppDbContextInitializer>();

        return services;
    }
}
