using Ekipzen.Application.Features.Setup;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Actions;
using Ekipzen.Domain.Entities.Analytics;
using Ekipzen.Domain.Entities.Compliance;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Settings;
using Ekipzen.Domain.Entities.Tenancy;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Infrastructure.Persistence;

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
    ICurrentTenant currentTenant,
    IConfiguration configuration,
    ITenantDirectory directory)
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

    /// <summary>
    /// Varsayılan demo kiracısının zengin verisini TOHUMLAR — çağıran kapsam o
    /// kiracıya sabitlenmiş olmalıdır.
    ///
    /// Kiracının KENDİSİNİ oluşturmaz: kiracı, gerçek provizyon yolundan
    /// (<c>TenantOnboarding</c>) doğar ve bu sınıf yalnız içini doldurur. Bkz.
    /// <c>DemoTenantSeeder</c>.
    /// </summary>
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

            // Roller BURADA tohumlanmaz: onlar şema hazırlığının parçasıdır ve
            // provizyonda ITenantSchemaInitializer tarafından yazılır. Burada da
            // yapmak, aynı kuralı iki yerde tutmak olurdu — biri değişince diğeri
            // sessizce eskir.
            await SeedOrganizationAsync();
            await SeedDemoUsersAsync();
            await SeedLeaveCatalogAsync();
            await SeedHolidaysAsync();
            await SeedLeaveLedgerAsync();
            await SeedPayrollSettingsAsync();
            await SeedAnalyticsAsync();
            await SeedComplianceAsync();
            await SeedActionsAsync();
            await SeedSettingsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed verisi yazılırken hata oluştu.");
            throw;
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
        foreach (var employee in new[] { ece, ahmet, ayse, selin })
        {
            employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
            {
                StartDate = employee.HireDate,
                EndDate = employee.TerminationDate,
                ContractType = employee.ContractType,
            });
        }
        context.Employees.AddRange(ece, ahmet, ayse, selin);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// İkinci demo kiracısının (Globex Bilişim) verisini tohumlar — çağıran kapsam
    /// o kiracıya sabitlenmiş olmalıdır.
    ///
    /// Multi-tenancy'yi çalışan demo'da GÖRÜNÜR kılmak için var: üç rolle giriş
    /// yapılabilen, verisi varsayılan kiracıdan tamamen izole ikinci bir şirket.
    /// Kiracının kendisini ve hr-admin'ini burası oluşturmaz — ikisi de gerçek
    /// provizyon yolundan gelir (bkz. <c>DemoTenantSeeder</c>).
    /// </summary>
    public async Task SeedGlobexTenantAsync()
    {
        var tenantId = currentTenant.TenantIdOrThrow();
        if (await context.Departments.AnyAsync()) return;

        var demoPassword = configuration["Seed:DemoPassword"] ?? "demo123";
        await DemoSifresiVerAsync("globex-admin@globex.local", demoPassword);

        var urun = new Department { Name = "Ürün" };
        var operasyon = new Department { Name = "Operasyon" };
        context.Departments.AddRange(urun, operasyon);
        await context.SaveChangesAsync();

        var deniz = new Employee
        {
            FirstName = "Deniz", LastName = "Kaya", Title = "Operasyon Yöneticisi",
            DepartmentId = operasyon.Id, HireDate = new DateOnly(2024, 3, 1),
            Status = EmployeeStatus.Active, Profile = new EmployeeProfile(),
        };
        deniz.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
        {
            StartDate = deniz.HireDate,
            ContractType = deniz.ContractType,
        });
        context.Employees.Add(deniz);
        await context.SaveChangesAsync();

        var cem = new Employee
        {
            FirstName = "Cem", LastName = "Öz", Title = "Operasyon Uzmanı",
            DepartmentId = operasyon.Id, HireDate = new DateOnly(2025, 6, 1),
            Status = EmployeeStatus.Active, ManagerId = deniz.Id, Profile = new EmployeeProfile(),
        };
        cem.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
        {
            StartDate = cem.HireDate,
            ContractType = cem.ContractType,
        });
        context.Employees.Add(cem);
        await context.SaveChangesAsync();

        var year = DateTime.UtcNow.Year;
        var managedFrom = new DateOnly(year + 1, 1, 1);
        var denizPeriod = deniz.EmploymentPeriods.Single();
        var cemPeriod = cem.EmploymentPeriods.Single();
        denizPeriod.LeaveAccrualManagedFrom = managedFrom;
        cemPeriod.LeaveAccrualManagedFrom = managedFrom;
        context.LeaveLedgerEntries.AddRange(
            new LeaveLedgerEntry
            {
                EmploymentPeriodId = denizPeriod.Id,
                OccurredOn = new DateOnly(year, 1, 1),
                Type = LeaveLedgerEntryType.LegacyOpening,
                Days = 24,
                SourceKey = $"demo-opening:{denizPeriod.Id}:{year}",
                SourceReference = "Demo geçiş bakiyesi",
            },
            new LeaveLedgerEntry
            {
                EmploymentPeriodId = cemPeriod.Id,
                OccurredOn = new DateOnly(year, 1, 1),
                Type = LeaveLedgerEntryType.LegacyOpening,
                Days = 24,
                SourceKey = $"demo-opening:{cemPeriod.Id}:{year}",
                SourceReference = "Demo geçiş bakiyesi",
            });

        context.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Globex Bilişim A.Ş.", Website = "www.globex.local",
            SystemEmail = "info@globex.local", Phone = "+90 216 000 00 00",
            HeadquartersAddress = "Ataşehir/İstanbul",
        });
        context.NotificationSettings.Add(new NotificationSettings
        {
            NewPersonnelEmail = true, LeaveRequestEmail = true,
            WeeklyReportEmail = false, TwoFactorSmsEnabled = false,
        });
        await context.SaveChangesAsync();

        await AbonelikTohumlaAsync(tenantId, new Subscription
        {
            Plan = "STANDART", PlanName = "Globex Başlangıç", BillingCycle = "Aylık",
            Price = 900m, RenewalDate = new DateOnly(year, 12, 1),
            PaymentMethodMasked = "•••• •••• •••• 1000",
        });
        var users = new (ApplicationUser User, string Role, Employee? Employee)[]
        {
            (new ApplicationUser
            {
                UserName = "deniz.kaya@globex.local", Email = "deniz.kaya@globex.local",
                EmailConfirmed = true, DisplayName = "Deniz Kaya", Initials = "DK",
                TenantId = tenantId,
            }, Roles.Manager, deniz),
            (new ApplicationUser
            {
                UserName = "cem.oz@globex.local", Email = "cem.oz@globex.local",
                EmailConfirmed = true, DisplayName = "Cem Öz", Initials = "CÖ",
                TenantId = tenantId,
            }, Roles.Employee, cem),
        };

        foreach (var (user, role, employee) in users)
        {
            user.EmployeeId = employee?.Id;
            var result = await userManager.CreateAsync(user, demoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Globex demo kullanıcısı oluşturulamadı ({user.Email}): " +
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

    /// <summary>
    /// Provizyonun ŞİFRESİZ oluşturduğu demo admin'ine şifre verir.
    ///
    /// Üründe bu adımın karşılığı davet e-postasıyla <c>accept-invite</c>'tır;
    /// demo kurulumunda geliştiricinin outbox dosyasını açması beklenmez. Hesabın
    /// KENDİSİ yine de gerçek akışın ürünüdür — seed onu taklit etmez, yalnız
    /// giriş yapılabilir hale getirir.
    /// </summary>
    private async Task DemoSifresiVerAsync(string email, string sifre)
    {
        var kullanici = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException(
                $"Demo admin '{email}' bulunamadı — provizyon adımı çalışmamış olabilir.");

        if (await userManager.HasPasswordAsync(kullanici)) return;

        var sonuc = await userManager.AddPasswordAsync(kullanici, sifre);
        if (!sonuc.Succeeded)
        {
            throw new InvalidOperationException(
                $"Demo admin şifresi belirlenemedi ({email}): " +
                string.Join(", ", sonuc.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        var tenantId = currentTenant.TenantIdOrThrow();

        // Frontend login formu bu şifreyi öndolu getirir (auth.js); üretimde geçersizdir.
        var demoPassword = configuration["Seed:DemoPassword"] ?? "demo123";

        // hr-admin'i PROVİZYON oluşturdu (şifresiz + davet). Demo kurulumunda
        // geliştiricinin davet e-postasını açması beklenmez; şifreyi burada
        // veriyoruz. Kullanıcıyı yeniden oluşturmuyoruz — o, gerçek kayıt akışının
        // ürünüdür ve seed'in onu taklit etmesi akışı test edilmemiş bırakırdı.
        await DemoSifresiVerAsync("ik@hrmaster.local", demoPassword);

        // Geri kalan iki demo hesabı (yönetici + personel) burada oluşur: üründe
        // onların karşılığı işe alım akışıdır, demo verisinde ise personel kayıtları
        // doğrudan tohumlandığı için hesapları da doğrudan bağlıyoruz.
        if (await userManager.Users.CountAsync() > 1) return;

        var ece = await context.Employees.FirstAsync(e => e.FirstName == "Ece" && e.LastName == "Arslan");
        var ahmet = await context.Employees.FirstAsync(e => e.FirstName == "Ahmet" && e.LastName == "Yılmaz");

        // demoUsers (mockData.js) ile birebir: e-posta, görünen ad, rol, baş harfler.
        var users = new (ApplicationUser User, string Role, Employee? Employee)[]
        {
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
        context.LeaveTypes.AddRange(TenantBaseline2026.CreateLeaveTypes());

        await context.SaveChangesAsync();
    }

    private async Task SeedHolidaysAsync()
    {
        if (await context.Holidays.AnyAsync()) return;

        // 2026 resmi tatilleri (tam günler; yarım gün arifeler hariç).
        context.Holidays.AddRange(TenantBaseline2026.CreateFullDayHolidays());

        await context.SaveChangesAsync();
    }

    private async Task SeedLeaveLedgerAsync()
    {
        if (await context.LeaveLedgerEntries.AnyAsync()) return;

        // Mevcut demo politikasını geçiş açılışı olarak koru. Bundan sonraki
        // krediler takvim yılında değil hizmet yıldönümünde oluşur.
        var year = DateTime.UtcNow.Year;
        var managedFrom = new DateOnly(year + 1, 1, 1);
        var periods = await context.EmployeeEmploymentPeriods.ToListAsync();
        foreach (var period in periods)
        {
            period.LeaveAccrualManagedFrom = managedFrom;
            context.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmploymentPeriodId = period.Id,
                OccurredOn = new DateOnly(year, 1, 1),
                Type = LeaveLedgerEntryType.LegacyOpening,
                Days = 24,
                SourceKey = $"demo-opening:{period.Id}:{year}",
                SourceReference = "Demo geçiş bakiyesi",
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedPayrollSettingsAsync()
    {
        if (await context.PayrollSettings.AnyAsync()) return;

        // 2026 resmî SGK, GİB ve ÇSGB parametreleri.
        var settings = TenantBaseline2026.CreatePayrollDraft();
        // Yalnız geliştirme/demo kurulumu; müşteri kurulumunda onay verilmez.
        settings.Version = 2;
        settings.Status = PayrollSettingsStatus.Approved;
        settings.ReviewNotes = "2026 standart yurt içi özel sektör 4/a profili";
        settings.ApprovedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        settings.ApprovedByUserId = "system:official-2026-seed";

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

        await context.SaveChangesAsync();

        await AbonelikTohumlaAsync(currentTenant.TenantIdOrThrow(), new Subscription
        {
            Plan = "PRO",
            PlanName = "HR Master Kurumsal",
            BillingCycle = "Yıllık",
            Price = 12000m,
            RenewalDate = new DateOnly(2026, 10, 12),
            PaymentMethodMasked = "•••• •••• •••• 4582",
        });
    }

    /// <summary>
    /// Aboneliği PLATFORM veritabanına yazar.
    ///
    /// Kiracı context'inden farklı olarak burada <c>AuditableEntityInterceptor</c>
    /// çalışmaz: <c>TenantId</c> ve <c>CreatedAtUtc</c> ELLE doldurulmalıdır.
    /// Unutulursa satır kiracısız (TenantId = 0) yazılır ve hiçbir kiracıya
    /// görünmez — sessiz bir arıza, o yüzden tek bir yerde toplandı.
    /// </summary>
    private async Task AbonelikTohumlaAsync(int tenantId, Subscription abonelik)
    {
        if (await platform.Subscriptions.AnyAsync(s => s.TenantId == tenantId)) return;

        abonelik.TenantId = tenantId;
        abonelik.CreatedAtUtc = DateTime.UtcNow;
        platform.Subscriptions.Add(abonelik);
        await platform.SaveChangesAsync(default);
    }
}
