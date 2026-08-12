using IKPro.Domain.Constants;
using IKPro.Domain.Entities.Actions;
using IKPro.Domain.Entities.Analytics;
using IKPro.Domain.Entities.Compliance;
using IKPro.Domain.Entities.Leaves;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Entities.Payroll;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Settings;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Domain.Enums;
using IKPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Persistence;

/// <summary>
/// Migration uygular ve demo verisini tohumlar. Veri şekilleri frontend'in
/// kanonik kaynaklarından alınmıştır: <c>services/mockData.js</c> (demo kullanıcılar,
/// personel dizini), <c>components/payroll.js</c> (bordro parametreleri + vergi dilimleri).
/// Idempotenttir: dolu tabloları atlar.
/// </summary>
public class AppDbContextInitializer(
    ILogger<AppDbContextInitializer> logger,
    AppDbContext context,
    IPlatformDbContext platform,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ICurrentTenant currentTenant,
    IConfiguration configuration,
    ITenantDirectory directory,
    ITenantScopeFactory tenantScopeFactory)
{
    public async Task InitialiseAsync()
    {
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı migration uygulanırken hata oluştu.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            // Kiracı KAPSAMDAN gelir; bu sınıf artık kendi kendine impersone etmez.
            // Program.cs bu initializer'ı ITenantScopeFactory ile açılmış, varsayılan
            // kiracıya sabitlenmiş bir kapsamdan çözer — böylece AppDbContext'in
            // bağlantısı ve interceptor damgalaması ilk andan itibaren doğrudur.
            //
            // Bu satır bir TEL TUZAĞIDIR: biri ileride initializer'ı kök kapsamdan
            // çözerse seed veriyi SESSİZCE yanlış (ya da hiçbir) kiracıya yazmak
            // yerine burada, hiçbir şey yazmadan patlar. Bkz. SeedKapsamiTests.
            var kiraciId = currentTenant.TenantIdOrThrow();
            logger.LogInformation("Demo verisi {TenantId} kiracısına tohumlanıyor.", kiraciId);
            await SeedRolesAsync();
            await SeedOrganizationAsync();
            await SeedDemoUsersAsync();
            await SeedLeaveCatalogAsync();
            await SeedHolidaysAsync();
            await SeedLeaveBalancesAsync();
            await SeedPayrollSettingsAsync();
            await SeedAnalyticsAsync();
            await SeedComplianceAsync();
            await SeedActionsAsync();
            await SeedSettingsAsync();
            await SeedSecondDemoTenantAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed verisi yazılırken hata oluştu.");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private async Task SeedOrganizationAsync()
    {
        if (await context.Departments.AnyAsync()) return;

        var yazilim = new Department { Name = "Yazılım", Code = "YAZ" };
        var insanKaynaklari = new Department { Name = "İnsan Kaynakları", Code = "IK" };
        var tasarim = new Department { Name = "Tasarım", Code = "TSR" };
        var satis = new Department { Name = "Satış", Code = "STS" };
        context.Departments.AddRange(yazilim, insanKaynaklari, tasarim, satis);

        // personnelDirectory (mockData.js) + demo manager. TC'ler kurgusal demo değerleridir.
        var ece = new Employee
        {
            FirstName = "Ece",
            LastName = "Arslan",
            Title = "Yazılım Ekip Lideri",
            NationalId = "98765432109",
            Department = yazilim,
            Status = EmployeeStatus.Active,
            HireDate = new DateOnly(2020, 5, 15),
            Profile = new EmployeeProfile(),
        };
        var ahmet = new Employee
        {
            FirstName = "Ahmet",
            LastName = "Yılmaz",
            Title = "Senior Developer",
            NationalId = "12345678901",
            Department = yazilim,
            Manager = ece,
            Status = EmployeeStatus.Active,
            HireDate = new DateOnly(2022, 3, 12),
            Profile = new EmployeeProfile(),
        };
        var ayse = new Employee
        {
            FirstName = "Ayşe",
            LastName = "Demir",
            Title = "İK Uzmanı",
            NationalId = "23456789012",
            Department = insanKaynaklari,
            Status = EmployeeStatus.Active,
            HireDate = new DateOnly(2021, 9, 4),
            Profile = new EmployeeProfile(),
        };
        var selin = new Employee
        {
            FirstName = "Selin",
            LastName = "Koç",
            Title = "UI Designer",
            NationalId = "34567890123",
            Department = tasarim,
            Manager = ece,
            Status = EmployeeStatus.Passive,
            HireDate = new DateOnly(2023, 1, 17),
            Profile = new EmployeeProfile(),
        };
        context.Employees.AddRange(ece, ahmet, ayse, selin);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Multi-tenancy'yi çalışan demo'da görünür kılmak için ikinci, bağımsız bir kiracı
    /// (Globex Bilişim). Üç rolle giriş yapılabilir; verisi varsayılan kiracıdan tamamen
    /// izoledir. İdempotent: kiracı zaten varsa atlanır.
    /// </summary>
    private async Task SeedSecondDemoTenantAsync()
    {
        const string slug = "globex";
        const string adminEmail = "globex-admin@globex.local";
        if (await platform.Tenants.AnyAsync(t => t.Slug == slug)) return;

        // İkinci koruma: platform DB'si (IKProPlatform) taze/boş doğduğunda yukarıdaki
        // slug kontrolü tutmaz (platformda "globex" yok) ama uygulama DB'si (IKProDb)
        // eski bir çalıştırmadan kalmışsa Identity'de globex-admin ZATEN vardır. Bu
        // durumda userManager.CreateAsync var olan kullanıcıya çarpar → istisna →
        // açılış çöker. Admin e-postası zaten varsa seed'i atla; doğru yükseltme
        // prosedürü iki veritabanını da BİRLİKTE düşürmektir (bkz.
        // docs/rehberler/07-veritabani-ve-migrationlar.md, "Yükseltme notu").
        //
        // DİKKAT — burada IIdentityService.EmailExistsAsync KULLANILAMAZ: o metot
        // e-postayı önce yönlendirme DİZİNİNDEN çözer, dizin ise platform
        // veritabanındadır. Tam da bu korumanın hedeflediği senaryoda (platform taze,
        // uygulama DB'si eski) dizin BOŞTUR; EmailExistsAsync false döner ve koruma
        // kör kalır. Sapmanın tespit edilmek istendiği yer kullanıcı deposudur,
        // dolayısıyla kontrol doğrudan orada yapılır.
        var normalizeAdmin = adminEmail.ToUpperInvariant();
        if (await context.Set<ApplicationUser>().AnyAsync(u => u.NormalizedEmail == normalizeAdmin))
        {
            logger.LogWarning(
                "Globex demo kiracısı platform veritabanında yok ama {AdminEmail} uygulama " +
                "veritabanında zaten var; seed atlandı. Bu genelde IKProPlatform düşürülüp " +
                "IKProDb düşürülmediğinde oluşur — ikisini birlikte düşürüp yeniden başlatın.",
                adminEmail);
            return;
        }

        var tenant = new Tenant
        {
            Name = "Globex Bilişim A.Ş.",
            Slug = slug,
            Status = TenantStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
        };
        platform.Tenants.Add(tenant);
        await platform.SaveChangesAsync(default);

        // Bu adımdan sonraki TÜM yazımlar Globex'e ait — ama ambient `context` yukarıdaki
        // varsayılan-kiracı seed adımlarıyla (SeedOrganizationAsync…SeedSettingsAsync) PAYLAŞILAN
        // aynı DbContext'tir. Ambient `currentTenant`'ı burada yeniden impersone edip ambient
        // `context`'i kullanmaya devam etmek (Faz 1b'de tek DB olduğundan) çalışırdı, ama TAM
        // OLARAK Task 5'in kaçınmak istediği örüntü olurdu: "önce impersone et, sonra context'i
        // KULLANMAYA DEVAM ET" sırasına güvenmek. Bunun yerine — TenantPurger'ın taze purgeContext
        // açması gibi — Globex için TAMAMEN BAĞIMSIZ, kendi ICurrentTenant/AppDbContext/UserManager/
        // ITenantDirectory'sini taşıyan bir kapsam açılır; kapsam DÖNMEDEN önce impersone edilmiş
        // olduğundan sıra hatası yapısal olarak imkânsızdır (bkz. ITenantScopeFactory dokümanı).
        using var kapsam = tenantScopeFactory.Create(tenant.Id);
        var globexContext = kapsam.Services.GetRequiredService<AppDbContext>();
        var globexUserManager = kapsam.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var globexDirectory = kapsam.Services.GetRequiredService<ITenantDirectory>();

        var urun = new Department { Name = "Ürün" };
        var operasyon = new Department { Name = "Operasyon" };
        globexContext.Departments.AddRange(urun, operasyon);
        await globexContext.SaveChangesAsync();

        var deniz = new Employee
        {
            FirstName = "Deniz", LastName = "Kaya", Title = "Operasyon Yöneticisi",
            DepartmentId = operasyon.Id, HireDate = new DateOnly(2024, 3, 1),
            Status = EmployeeStatus.Active, Profile = new EmployeeProfile(),
        };
        globexContext.Employees.Add(deniz);
        await globexContext.SaveChangesAsync();

        var cem = new Employee
        {
            FirstName = "Cem", LastName = "Öz", Title = "Operasyon Uzmanı",
            DepartmentId = operasyon.Id, HireDate = new DateOnly(2025, 6, 1),
            Status = EmployeeStatus.Active, ManagerId = deniz.Id, Profile = new EmployeeProfile(),
        };
        globexContext.Employees.Add(cem);
        await globexContext.SaveChangesAsync();

        var year = DateTime.UtcNow.Year;
        globexContext.LeaveBalances.AddRange(
            new LeaveBalance { EmployeeId = deniz.Id, Year = year, EntitledDays = 24, CarriedOverDays = 0 },
            new LeaveBalance { EmployeeId = cem.Id, Year = year, EntitledDays = 24, CarriedOverDays = 0 });

        globexContext.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Globex Bilişim A.Ş.", Website = "www.globex.local",
            SystemEmail = "info@globex.local", Phone = "+90 216 000 00 00",
            HeadquartersAddress = "Ataşehir/İstanbul",
        });
        globexContext.NotificationSettings.Add(new NotificationSettings
        {
            NewPersonnelEmail = true, LeaveRequestEmail = true,
            WeeklyReportEmail = false, TwoFactorSmsEnabled = false,
        });
        globexContext.Subscriptions.Add(new Subscription
        {
            Plan = "STANDART", PlanName = "Globex Başlangıç", BillingCycle = "Aylık",
            Price = 900m, RenewalDate = new DateOnly(year, 12, 1),
            PaymentMethodMasked = "•••• •••• •••• 1000",
        });
        await globexContext.SaveChangesAsync();

        var demoPassword = configuration["Seed:DemoPassword"] ?? "demo123";
        var users = new (ApplicationUser User, string Role, Employee? Employee)[]
        {
            (new ApplicationUser
            {
                UserName = adminEmail, Email = adminEmail,
                EmailConfirmed = true, DisplayName = "Globex Yöneticisi", Initials = "GY",
                TenantId = tenant.Id,
            }, Roles.HrAdmin, null),
            (new ApplicationUser
            {
                UserName = "deniz.kaya@globex.local", Email = "deniz.kaya@globex.local",
                EmailConfirmed = true, DisplayName = "Deniz Kaya", Initials = "DK",
                TenantId = tenant.Id,
            }, Roles.Manager, deniz),
            (new ApplicationUser
            {
                UserName = "cem.oz@globex.local", Email = "cem.oz@globex.local",
                EmailConfirmed = true, DisplayName = "Cem Öz", Initials = "CÖ",
                TenantId = tenant.Id,
            }, Roles.Employee, cem),
        };

        foreach (var (user, role, employee) in users)
        {
            user.EmployeeId = employee?.Id;
            var result = await globexUserManager.CreateAsync(user, demoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Globex demo kullanıcı oluşturulamadı ({user.Email}): " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // Demo kullanıcılar da normal kullanıcı oluşturma yollarıyla aynı kuralı izler:
            // dizine yazılmadan hiçbir hesap "var" sayılmaz (Faz 1b login'i buradan çözecek).
            await globexDirectory.ReserveAsync(user.Email!, tenant.Id, default);

            await globexUserManager.AddToRoleAsync(user, role);
            if (employee is not null)
            {
                employee.UserId = user.Id;
            }
        }

        await globexContext.SaveChangesAsync();
    }

    private async Task SeedDemoUsersAsync()
    {
        if (await userManager.Users.AnyAsync()) return;

        var tenantId = currentTenant.TenantIdOrThrow();

        // Frontend login formu bu şifreyi öndolu getirir (auth.js); üretimde geçersizdir.
        var demoPassword = configuration["Seed:DemoPassword"] ?? "demo123";

        var ece = await context.Employees.FirstAsync(e => e.FirstName == "Ece" && e.LastName == "Arslan");
        var ahmet = await context.Employees.FirstAsync(e => e.FirstName == "Ahmet" && e.LastName == "Yılmaz");

        // demoUsers (mockData.js) ile birebir: e-posta, görünen ad, rol, baş harfler.
        var users = new (ApplicationUser User, string Role, Employee? Employee)[]
        {
            (new ApplicationUser
            {
                UserName = "ik@hrmaster.local",
                Email = "ik@hrmaster.local",
                EmailConfirmed = true,
                DisplayName = "İK Yöneticisi",
                Initials = "İK",
            }, Roles.HrAdmin, null),
            (new ApplicationUser
            {
                UserName = "ece.arslan@hrmaster.local",
                Email = "ece.arslan@hrmaster.local",
                EmailConfirmed = true,
                DisplayName = "Ece Arslan",
                Initials = "EA",
            }, Roles.Manager, ece),
            (new ApplicationUser
            {
                UserName = "ahmet.yilmaz@hrmaster.local",
                Email = "ahmet.yilmaz@hrmaster.local",
                EmailConfirmed = true,
                DisplayName = "Ahmet Yılmaz",
                Initials = "AY",
            }, Roles.Employee, ahmet),
        };

        foreach (var (user, role, employee) in users)
        {
            user.EmployeeId = employee?.Id;
            user.TenantId = tenantId;

            var result = await userManager.CreateAsync(user, demoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Demo kullanıcı oluşturulamadı ({user.Email}): " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // Demo kullanıcılar da normal kullanıcı oluşturma yollarıyla aynı kuralı izler:
            // dizine yazılmadan hiçbir hesap "var" sayılmaz (Faz 1b login'i buradan çözecek).
            await directory.ReserveAsync(user.Email!, tenantId, default);

            await userManager.AddToRoleAsync(user, role);

            if (employee is not null)
            {
                employee.UserId = user.Id;
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedLeaveCatalogAsync()
    {
        if (await context.LeaveTypes.AnyAsync()) return;

        // Enum kataloğu (plan Ek A): Yıllık | Mazeret | Raporlu | Uzaktan.
        context.LeaveTypes.AddRange(
            new LeaveType { Name = "Yıllık İzin", Code = "Yıllık", DeductsFromAnnualBalance = true, RequiresApproval = true },
            new LeaveType { Name = "Mazeret İzni", Code = "Mazeret", DeductsFromAnnualBalance = false, RequiresApproval = true },
            new LeaveType { Name = "Raporlu", Code = "Raporlu", DeductsFromAnnualBalance = false, RequiresApproval = false },
            new LeaveType { Name = "Uzaktan Çalışma", Code = "Uzaktan", DeductsFromAnnualBalance = false, RequiresApproval = true });

        await context.SaveChangesAsync();
    }

    private async Task SeedHolidaysAsync()
    {
        if (await context.Holidays.AnyAsync()) return;

        // 2026 resmi tatilleri (tam günler; yarım gün arifeler hariç).
        context.Holidays.AddRange(
            new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Yılbaşı" },
            new Holiday { Date = new DateOnly(2026, 3, 20), Name = "Ramazan Bayramı 1. Gün" },
            new Holiday { Date = new DateOnly(2026, 3, 21), Name = "Ramazan Bayramı 2. Gün" },
            new Holiday { Date = new DateOnly(2026, 3, 22), Name = "Ramazan Bayramı 3. Gün" },
            new Holiday { Date = new DateOnly(2026, 4, 23), Name = "Ulusal Egemenlik ve Çocuk Bayramı" },
            new Holiday { Date = new DateOnly(2026, 5, 1), Name = "Emek ve Dayanışma Günü" },
            new Holiday { Date = new DateOnly(2026, 5, 19), Name = "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
            new Holiday { Date = new DateOnly(2026, 5, 27), Name = "Kurban Bayramı 1. Gün" },
            new Holiday { Date = new DateOnly(2026, 5, 28), Name = "Kurban Bayramı 2. Gün" },
            new Holiday { Date = new DateOnly(2026, 5, 29), Name = "Kurban Bayramı 3. Gün" },
            new Holiday { Date = new DateOnly(2026, 5, 30), Name = "Kurban Bayramı 4. Gün" },
            new Holiday { Date = new DateOnly(2026, 7, 15), Name = "Demokrasi ve Millî Birlik Günü" },
            new Holiday { Date = new DateOnly(2026, 8, 30), Name = "Zafer Bayramı" },
            new Holiday { Date = new DateOnly(2026, 10, 29), Name = "Cumhuriyet Bayramı" });

        await context.SaveChangesAsync();
    }

    private async Task SeedLeaveBalancesAsync()
    {
        if (await context.LeaveBalances.AnyAsync()) return;

        // leaves.js bakiye kartı: hak ediş 24 gün (tüm personel, içinde bulunulan yıl).
        var year = DateTime.UtcNow.Year;
        var employeeIds = await context.Employees.Select(e => e.Id).ToListAsync();

        context.LeaveBalances.AddRange(employeeIds.Select(id => new LeaveBalance
        {
            EmployeeId = id,
            Year = year,
            EntitledDays = 24,
            CarriedOverDays = 0,
        }));

        await context.SaveChangesAsync();
    }

    private async Task SeedPayrollSettingsAsync()
    {
        if (await context.PayrollSettings.AnyAsync()) return;

        // payrollDefaultSettings + taxBrackets (components/payroll.js) ile birebir.
        var settings = new PayrollSettings
        {
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsDefault = true,
            OvertimeMultiplier = 1.5m,
            MonthlyWorkingHours = 225m,
            DefaultWorkedDays = 30,
            SgkEmployeeRate = 14m,
            UnemploymentEmployeeRate = 1m,
            SgkEmployerRate = 20.5m,
            UnemploymentEmployerRate = 2m,
            StampTaxRate = 0.759m,
            SgkBaseMin = 33030m,
            SgkBaseMax = 297270m,
            // İstisna = asgari ücret üzerinden hesaplanan gelir vergisi:
            // (33.030 − %15 SGK) × %15 = 28.075,50 × 0,15 = 4.211,325 → 4.211,33.
            // Yuvarlanmış 4.211 kullanıldığında asgari ücretliden 0,325 TL gelir
            // vergisi kesiliyor ve net, resmî 28.075,50 yerine 28.075,175 çıkıyordu.
            MonthlyMinWageIncomeTaxExemption = 4211.33m,
            MonthlyMinWageStampTaxExemption = 250.7m, // 33.030 × binde 7,59 = 250,6977
            MinWageGross = 33030m,
            TaxBrackets =
            {
                new IncomeTaxBracket { Order = 1, Limit = 190000m, Base = 0m, BaseTax = 0m, Rate = 0.15m },
                new IncomeTaxBracket { Order = 2, Limit = 400000m, Base = 190000m, BaseTax = 28500m, Rate = 0.20m },
                new IncomeTaxBracket { Order = 3, Limit = 1500000m, Base = 400000m, BaseTax = 70500m, Rate = 0.27m },
                new IncomeTaxBracket { Order = 4, Limit = 5300000m, Base = 1500000m, BaseTax = 367500m, Rate = 0.35m },
                new IncomeTaxBracket { Order = 5, Limit = null, Base = 5300000m, BaseTax = 1697500m, Rate = 0.40m },
            },
        };

        context.PayrollSettings.Add(settings);
        await context.SaveChangesAsync();
    }

    private async Task SeedAnalyticsAsync()
    {
        if (await context.EmployeeMetricSnapshots.AnyAsync()) return;

        var employees = await context.Employees.ToListAsync();
        var ahmet = employees.First(e => e.FirstName == "Ahmet");
        var selin = employees.First(e => e.FirstName == "Selin");
        var ece = employees.First(e => e.FirstName == "Ece");
        var ayse = employees.First(e => e.FirstName == "Ayşe");

        // dashboard.js getDashboardMetrics() employees dizisiyle hizalı güncel değerler.
        // (absence, lateness, overtime, unusedLeave, pulse, performance, roleCriticality)
        var profiles = new (Employee Employee, int[] Values, string Trend, string Action)[]
        {
            (ahmet, [18, 22, 74, 82, 52, 68, 92],
                "Son 30 günde fazla mesai +%18",
                "1:1 görüşme ve izin planı başlat"),
            (selin, [8, 12, 66, 71, 61, 74, 76],
                "İzin erteleme ve teslim baskısı yükseldi",
                "Sprint kapasitesi ve izin penceresi netleştir"),
            (ece, [6, 9, 34, 42, 78, 84, 58],
                "Stabil bağlılık ve dengeli kapasite",
                "Standart takip yeterli"),
            (ayse, [10, 12, 40, 50, 66, 80, 60],
                "Dengeli kapasite, izlemede",
                "Standart takip yeterli"),
        };

        // Son 12 ay: geriye gidildikçe yük göstergeleri hafifçe azalır (risk trendi yükselen seri).
        var currentPeriod = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        foreach (var (employee, values, trend, action) in profiles)
        {
            for (var monthsBack = 11; monthsBack >= 0; monthsBack--)
            {
                var drift = monthsBack * 2; // en eski dönemde -22 puana kadar
                context.EmployeeMetricSnapshots.Add(new EmployeeMetricSnapshot
                {
                    Employee = employee,
                    PeriodDate = currentPeriod.AddMonths(-monthsBack),
                    AbsencePct = Math.Clamp(values[0] - drift / 2, 0, 100),
                    LatenessPct = Math.Clamp(values[1] - drift / 2, 0, 100),
                    OvertimePct = Math.Clamp(values[2] - drift, 0, 100),
                    UnusedLeavePct = Math.Clamp(values[3] - drift, 0, 100),
                    Pulse = Math.Clamp(values[4] + drift / 2, 0, 100),
                    Performance = values[5],
                    RoleCriticality = values[6],
                    TrendNote = monthsBack == 0 ? trend : null,
                    RecommendedAction = monthsBack == 0 ? action : null,
                });
            }
        }

        // employeeVoiceMetrics.departments (dashboard.js): son ölçüm + önceki dönem.
        // Yazılım ve Tasarım'da nabız önceki döneme göre düşük → decliningTeams = 2.
        var departments = await context.Departments.ToListAsync();
        var voice = new (string Code, int Pulse, int PreviousPulse, int ENps, int Participation, string Mood, string Driver)[]
        {
            ("YAZ", 58, 65, 2, 82, "Baskı yüksek", "Teslim takvimi ve fazla mesai"),
            ("STS", 61, 61, 5, 74, "Kırılgan", "Hedef baskısı ve vardiya düzeni"),
            ("TSR", 69, 72, 14, 78, "Dengeli", "Teslim baskısı izleniyor"),
            ("IK", 78, 76, 24, 88, "Pozitif", "Net iletişim ve dengeli kapasite"),
        };

        foreach (var v in voice)
        {
            var department = departments.First(d => d.Code == v.Code);
            context.EngagementMetrics.AddRange(
                new EngagementMetric
                {
                    Department = department,
                    PeriodDate = currentPeriod.AddMonths(-1),
                    PulseScore = v.PreviousPulse,
                    ENps = v.ENps,
                    ParticipationRate = v.Participation,
                    Mood = v.Mood,
                    Driver = v.Driver,
                },
                new EngagementMetric
                {
                    Department = department,
                    PeriodDate = currentPeriod,
                    PulseScore = v.Pulse,
                    ENps = v.ENps,
                    ParticipationRate = v.Participation,
                    Mood = v.Mood,
                    Driver = v.Driver,
                });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedComplianceAsync()
    {
        if (await context.ComplianceDocuments.AnyAsync()) return;

        var employees = await context.Employees.ToListAsync();
        var ahmet = employees.First(e => e.FirstName == "Ahmet");
        var selin = employees.First(e => e.FirstName == "Selin");
        var ece = employees.First(e => e.FirstName == "Ece");
        var ayse = employees.First(e => e.FirstName == "Ayşe");

        // complianceMetrics.records (dashboard.js) seed karşılığı.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        context.ComplianceDocuments.AddRange(
            new ComplianceDocument
            {
                Employee = ece,
                DocumentName = "KVKK açık rıza eki",
                OwnerName = "İK Operasyon",
                DueDate = today,
                Status = ComplianceStatus.Missing,
                Level = RiskLevel.High,
            },
            new ComplianceDocument
            {
                Employee = ahmet,
                DocumentName = "İSG yenileme formu",
                OwnerName = "İK Operasyon",
                DueDate = today.AddDays(3),
                Status = ComplianceStatus.DueSoon,
                Level = RiskLevel.Medium,
            },
            new ComplianceDocument
            {
                Employee = ahmet,
                DocumentName = "Görev tanımı onayı",
                OwnerName = "Ece Arslan",
                DueDate = today.AddDays(5),
                Status = ComplianceStatus.InReview,
                Level = RiskLevel.Medium,
            },
            new ComplianceDocument
            {
                Employee = selin,
                DocumentName = "Yan hak bilgilendirme formu",
                OwnerName = "İK Operasyon",
                DueDate = today.AddDays(9),
                Status = ComplianceStatus.DueSoon,
                Level = RiskLevel.Low,
            },
            new ComplianceDocument
            {
                Employee = ayse,
                DocumentName = "Personel dosyası kontrolü",
                OwnerName = "İK Operasyon",
                DueDate = null,
                Status = ComplianceStatus.Completed,
                Level = RiskLevel.Low,
            });

        await context.SaveChangesAsync();
    }

    private async Task SeedActionsAsync()
    {
        if (await context.GlobalActions.AnyAsync()) return;

        // dashboard.js actions dizisinden temsilci kayıtlar (Faz 10 CRUD bunları yönetir).
        context.GlobalActions.AddRange(
            new GlobalAction
            {
                Title = "Yazılım ekibinde tükenmişlik sinyali yükseldi",
                Source = "Risk Merkezi",
                SourceRoute = "burnout-risk",
                Owner = "Ece Arslan",
                Due = "Bugün",
                Priority = ActionPriority.High,
                Status = ActionStatus.Open,
                RecommendedAction = "Ekip lideriyle kapasite görüşmesi planla.",
            },
            new GlobalAction
            {
                Title = "2 kritik rolde ayrılma riski var",
                Source = "Risk Merkezi",
                SourceRoute = "attrition-risk",
                Owner = "İK Yöneticisi",
                Due = "Bugün",
                Priority = ActionPriority.High,
                Status = ActionStatus.Open,
                RecommendedAction = "1:1 görüşme ve yedekleme planı başlat.",
            },
            new GlobalAction
            {
                Title = "Yönetici yükü iki ekipte kritik eşiğe yaklaştı",
                Source = "Risk Merkezi",
                SourceRoute = "manager-load",
                Owner = "Ece Arslan",
                Due = "Bu hafta",
                Priority = ActionPriority.Medium,
                Status = ActionStatus.Week,
                RecommendedAction = "Aksiyonları delege et ve haftalık takip ritmi kur.",
            },
            new GlobalAction
            {
                Title = "KVKK ve İSG evraklarında kritik son tarih var",
                Source = "Uyum",
                SourceRoute = "compliance-risk",
                Owner = "İK Operasyon",
                Due = "Bugün",
                Priority = ActionPriority.High,
                Status = ActionStatus.Open,
                RecommendedAction = "Eksik evrak sahiplerini netleştir ve gün sonu kontrolü yap.",
            },
            new GlobalAction
            {
                Title = "İşe alım hunisinde teklif kabul oranı düşüyor",
                Source = "İşe Alım",
                SourceRoute = "recruitment",
                Owner = "İşe Alım",
                Due = "Bu hafta",
                Priority = ActionPriority.Low,
                Status = ActionStatus.Week,
                RecommendedAction = "Teklif paketi ve aday deneyimi gözden geçirilsin.",
            });

        await context.SaveChangesAsync();
    }

    private async Task SeedSettingsAsync()
    {
        // settings.js form değerleri + plan banner'ı ile birebir (tekil kayıtlar).
        if (!await context.CompanyProfiles.AnyAsync())
        {
            context.CompanyProfiles.Add(new CompanyProfile
            {
                Name = "HR Master Teknoloji A.Ş.",
                Website = "www.hrmaster.com",
                SystemEmail = "info@hrmaster.com",
                Phone = "+90 212 555 00 00",
                HeadquartersAddress = "Maslak Mah. Büyükdere Cad. No:123 Sarıyer/İstanbul",
            });
        }

        if (!await context.NotificationSettings.AnyAsync())
        {
            context.NotificationSettings.Add(new NotificationSettings
            {
                NewPersonnelEmail = true,
                LeaveRequestEmail = true,
                WeeklyReportEmail = false,
                TwoFactorSmsEnabled = false,
            });
        }

        if (!await context.Subscriptions.AnyAsync())
        {
            context.Subscriptions.Add(new Subscription
            {
                Plan = "PRO",
                PlanName = "HR Master Kurumsal",
                BillingCycle = "Yıllık",
                Price = 12000m,
                RenewalDate = new DateOnly(2026, 10, 12),
                PaymentMethodMasked = "•••• •••• •••• 4582",
            });
        }

        await context.SaveChangesAsync();
    }
}
