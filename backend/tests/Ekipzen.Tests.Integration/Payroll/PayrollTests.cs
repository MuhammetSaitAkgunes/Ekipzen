using FluentAssertions;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Domain.Enums;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekipzen.Tests.Integration.Payroll;

/// <summary>
/// Faz 6 uçtan uca: dönem oluşturma (puantajdan fazla mesai beslemesi), girdi →
/// check → onay → submit yaşam döngüsü, kümülatif matrah devri, dönem özeti (SQL view),
/// bordro pusulası PDF'i ve yetki korumaları. Motor değerleri parite testlerindeki
/// JS referanslarıyla aynı girdiler üzerinden doğrulanır (Ahmet = pr-001 girdileri).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PayrollTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task Settings_AreSeeded_AndVersionedByEffectiveDate()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var current = await GetAsync<PayrollSettingsDto>(admin, "/api/payroll/settings?asOf=2026-01-01");
        current.OvertimeMultiplier.Should().Be(1.5m);
        current.SgkBaseMin.Should().Be(33030m);
        current.TaxBrackets.Should().HaveCount(5);
        current.TaxBrackets[^1].Limit.Should().BeNull("son dilim sınırsız");

        // Onaysız taslak hesaplarda kullanılmaz; açık onaydan sonra yürürlüğe girer.
        var draftResponse = await admin.PutAsJsonAsync(
            "/api/payroll/settings",
            SettingsBody(current, effectiveFrom: "2027-01-01", sgkBaseMin: current.SgkBaseMin,
                overtimeMultiplier: 2.0m));
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft2027 = (await draftResponse.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
        draft2027.Status.Should().Be("draft");
        var drafts = await GetAsync<List<PayrollSettingsDto>>(admin, "/api/payroll/settings/drafts");
        drafts.Should().Contain(item => item.Id == draft2027.Id && item.Version == draft2027.Version);

        (await GetAsync<PayrollSettingsDto>(admin, "/api/payroll/settings?asOf=2027-01-01"))
            .EffectiveFrom.Should().Be(new DateOnly(2026, 1, 1));

        var approveResponse = await admin.PostAsJsonAsync(
            $"/api/payroll/settings/{draft2027.Id}/approve",
            new { id = draft2027.Id, expectedVersion = draft2027.Version });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved2027 = (await approveResponse.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
        approved2027.Status.Should().Be("approved");
        drafts = await GetAsync<List<PayrollSettingsDto>>(admin, "/api/payroll/settings/drafts");
        drafts.Should().NotContain(item => item.Id == draft2027.Id);


        (await GetAsync<PayrollSettingsDto>(admin, "/api/payroll/settings?asOf=2027-01-01"))
            .OvertimeMultiplier.Should().Be(2.0m);
        (await GetAsync<PayrollSettingsDto>(admin, "/api/payroll/settings?asOf=2026-06-01"))
            .OvertimeMultiplier.Should().Be(1.5m);

        (await admin.PutAsJsonAsync(
            "/api/payroll/settings",
            SettingsBody(approved2027, "2027-01-01", approved2027.SgkBaseMin)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "onaylı mevzuat seti yerinde değiştirilemez");

        (await admin.PutAsJsonAsync(
            "/api/payroll/settings",
            SettingsBody(current, "2029-07-15", current.SgkBaseMin)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "aylık bordro parametresi ay ortasında yürürlüğe giremez");

        var brokenBrackets = current.TaxBrackets.ToList();
        brokenBrackets[1] = brokenBrackets[1] with { BaseTax = 1m };
        (await admin.PutAsJsonAsync(
            "/api/payroll/settings",
            SettingsBody(current, "2029-08-01", current.SgkBaseMin,
                taxBrackets: brokenBrackets)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "matematiksel sürekliliği bozuk vergi dilimi reddedilmelidir");

        (await admin.PutAsJsonAsync(
            "/api/payroll/settings",
            SettingsBody(current, "2029-09-01", current.SgkBaseMin,
                sourceReference: "https://example.com/not-official")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "mevzuat kaynağı resmî gov.tr alanında olmalıdır");
        // Yönetim rolleri dışında kalanlar ve manager ayarlara erişemez.
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync("/api/payroll/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Asgari ücret ve SGK sınırları Türkiye'de yıl ORTASINDA da değişebiliyor
    /// (ör. temmuz güncellemeleri). Parametreler yıla değil yürürlük tarihine
    /// bağlanmalı: haziran dönemi eski seti, temmuz dönemi yeni seti kullanmalı.
    /// </summary>
    [Fact]
    public async Task Settings_YilOrtasiDegisim_DonemYururluktekiSetiKullanir()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ocak = await GetAsync<PayrollSettingsDto>(admin, "/api/payroll/settings?asOf=2026-01-01");

        // Temmuzdan itibaren geçerli yeni set: SGK tabanı yükseldi.
        var temmuzSeti = SettingsBody(ocak, effectiveFrom: "2028-07-01", sgkBaseMin: 40000m);
        (await SaveAndApproveSettingsAsync(admin, temmuzSeti)).Status.Should().Be("approved");

        // Parametrenin hesaba yansıması üzerinden doğrula: brüt tabanın altında
        // kaldığı için SGK matrahı o tarihte yürürlükteki tabana çekilir.
        (await PreviewSgkBaseAsync(admin, asOf: "2028-06-01"))
            .Should().Be(33030m, "haziran, temmuzdaki değişimden etkilenmemeli");

        (await PreviewSgkBaseAsync(admin, asOf: "2028-07-01"))
            .Should().Be(40000m, "temmuz döneminde yeni set yürürlükte");
    }

    /// <summary>
    /// Tohum parametreleriyle asgari ücretlinin neti, 2026 resmî tutarı (28.075,50 TL)
    /// vermeli. Motor doğru olsa bile parametre yuvarlaması neti kaydırabildiği için
    /// bu kontrol uçtan uca (seed → API → motor) yapılır.
    /// </summary>
    [Fact]
    public async Task Preview_AsgariUcret_ResmiNetiVerir()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var response = await admin.PostAsJsonAsync("/api/payroll/preview", new
        {
            grossSalary = 33030,
            statutoryProfile = "domesticPrivateSectorStandardFourA",
            disabilityReliefType = "none",
            workedDays = 30,
            overtimeHours = 0,
            premiumPay = 0,
            roadAllowance = 0,
            mealAllowance = 0,
            specialDeductions = 0,
            previousTaxBase = 0,
            asOf = "2026-01-01",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var calc = (await response.Content.ReadFromJsonAsync<PayrollCalcDto>())!;
        calc.NetPay.Should().Be(28075.50m, "2026 resmî net asgari ücret");
    }

    [Fact]
    public async Task Preview_SayisalKanuniEnumlariReddeder()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var response = await admin.PostAsJsonAsync("/api/payroll/preview", new
        {
            grossSalary = 33030,
            statutoryProfile = 1,
            disabilityReliefType = 0,
            workedDays = 30,
            overtimeHours = 0,
            premiumPay = 0,
            roadAllowance = 0,
            mealAllowance = 0,
            specialDeductions = 0,
            previousTaxBase = 0,
            asOf = "2026-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "kanuni profil değerleri yalnızca açık isimlerle kabul edilmelidir");
    }

    [Fact]
    public async Task Preview_KanuniYuksekUcretSenaryosunuHesaplar()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var response = await admin.PostAsJsonAsync("/api/payroll/preview", new
        {
            grossSalary = 85000,
            statutoryProfile = "domesticPrivateSectorStandardFourA",
            disabilityReliefType = "none",
            workedDays = 30,
            overtimeHours = 10,
            overtimeMultiplier = 2,
            premiumPay = 1500,
            roadAllowance = 800,
            mealAllowance = 1200,
            mealTreatment = "fullyTaxable",
            specialDeductions = 500,
            previousTaxBase = 185000,
            asOf = "2026-01-01",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var calc = (await response.Content.ReadFromJsonAsync<PayrollCalcDto>())!;
        // JS mock referansı 68.800,416111 idi. Asgari ücret GV istisnası tohum verisinde
        // 4.211 yerine tam değerine (4.211,33) çekilince gelir vergisi 0,33 TL azaldı ve
        // net aynı miktarda arttı. Beklenen değer bu düzeltmeyi yansıtır.
        calc.NetPay.Should().BeApproximately(68800.746111m, 0.01m, "istisna düzeltmesi sonrası");
        calc.EmployerCost.Should().Be(118868.75m, "indirimsiz %21,75 SGK işveren + %2 işsizlik oranı");
        calc.Warnings.Should().Contain("Vergi dilimi geçişi");
    }

    [Fact]
    public async Task PeriodLifecycle_EndToEnd_WithCumulativeCarryAndPayslip()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetEmployeeId = await FindEmployeeIdAsync(admin, "Ahmet");
        var employees = await GetAsync<PagedResult<EmployeeListItemDto>>(
            admin, "/api/employees?pageSize=100");

        // Mevcut çalışanların tüm beklenen iş günlerini tamamla; Ahmet'in ilk günü
        // 09:00–20:00 olduğundan 120 dk fazla mesai bordroya beslenecek.
        await SeedCompleteTimesheetsAsync(admin, employees.Items, 2026, 9, ahmetEmployeeId);

        var departmentId = employees.Items.First().DepartmentId;
        var departingEmployee = await CreateEmployeeAsync(
            admin, "Ayrılan", "Personel", departmentId, "2026-09-01");
        await SeedEmployeeTimesheetsAsync(
            admin, departingEmployee.Id, departingEmployee.Name,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15));
        await OffboardingTestHelper.CompleteAsync(
            admin, departingEmployee.Id, new DateOnly(2026, 9, 15));
        var futureEmployee = await CreateEmployeeAsync(
            admin, "Gelecek", "Çalışan", departmentId, "2026-10-15");
        var incompleteEmployee = await CreateEmployeeAsync(
            admin, "Eksik", "Puantaj", departmentId, "2026-09-30");

        // --- dönem oluştur ---
        var createResponse = await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 9 });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var period = (await createResponse.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(JsonOptions))!;

        period.Name.Should().Be("Eylül 2026");
        period.Status.Should().Be("draft");
        period.Rows.Should().NotBeEmpty("aktif personel için girdi satırları üretilir");
        period.Rows.Select(r => r.Name).Should().Contain(["Ahmet Yılmaz", "Ece Arslan", "Ayşe Demir"]);
        period.Rows.Select(r => r.Name).Should().Contain(departingEmployee.Name,
            "ay içinde ayrılan personelin son bordrosu üretilir");
        period.Rows.Select(r => r.Name).Should().NotContain("Selin Koç", "pasif personel döneme girmez");
        period.Rows.Select(r => r.Name).Should().NotContain(futureEmployee.Name,
            "dönem sonundan sonra işe başlayan çalışan bordroya giremez");

        var ahmetRow = period.Rows.Single(r => r.Name == "Ahmet Yılmaz");
        ahmetRow.OvertimeHours.Should().Be(2, "puantaj özetindeki 120 dk fazla mesai saate çevrilir");
        ahmetRow.TimesheetComplete.Should().BeTrue();
        ahmetRow.ApprovalStatus.Should().Be("Ön Hesap");
        period.Rows.Single(row => row.EmployeeId == departingEmployee.Id)
            .TimesheetComplete.Should().BeTrue(
                "ayrılış tarihinden sonraki günler puantajda beklenmez");
        var incompleteRow = period.Rows.Single(r => r.EmployeeId == incompleteEmployee.Id);
        incompleteRow.TimesheetComplete.Should().BeFalse();

        (await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 9 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "aynı dönem ikinci kez açılamaz");

        (await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "en son dönem onaylanmadan ileri dönem açılamaz");

        // İstemci eski sözleşmedeki bayrağı gönderse dahi sunucu eksik puantajı
        // yeniden hesaplar ve onayı reddeder.
        var bypassResponse = await admin.PutAsJsonAsync(
            $"/api/payroll/periods/{period.Id}/rows/{incompleteRow.Id}", new
            {
                grossSalary = 50000,
                statutoryProfile = "domesticPrivateSectorStandardFourA",
                workedDays = 30,
                sgkPremiumDaysConfirmed = true,
                overtimeHours = 0,
                disabilityReliefType = "none",
                otherIncomeTaxBaseDeductionsConfirmedNone = true,
                cumulativeIncomeTaxBaseConfirmed = true,
                ibanComplete = true,
                timesheetComplete = true,
            });
        bypassResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bypassRow = (await bypassResponse.Content.ReadFromJsonAsync<PayrollRowDto>(JsonOptions))!;
        bypassRow.TimesheetComplete.Should().BeFalse();
        bypassRow.ApprovalStatus.Should().Be("Eksik Veri");
        (await admin.PostAsync(
                $"/api/payroll/periods/{period.Id}/rows/{incompleteRow.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Eksik tek iş günü tamamlandığında sonraki kontrol sunucu bayrağını açar.
        (await admin.PostAsJsonAsync("/api/attendance", new
        {
            employeeId = incompleteEmployee.Id,
            model = new
            {
                workDate = "2026-09-30", checkIn = "09:00", checkOut = "18:00", breakMinutes = 60,
            },
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var refreshedPeriod = await GetAsync<PayrollPeriodDetailDto>(
            admin, $"/api/payroll/periods/{period.Id}");
        refreshedPeriod.Rows.Single(r => r.Id == incompleteRow.Id).TimesheetComplete.Should().BeTrue();

        // Sıfır brüt ve bunun ürettiği negatif net, puantaj tam olsa da onaylanamaz.
        var zeroResponse = await admin.PutAsJsonAsync(
            $"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}", new
            {
                grossSalary = 0, workedDays = 30, overtimeHours = 0,
                specialDeductions = 100, ibanComplete = true,
            });
        var zeroRow = (await zeroResponse.Content.ReadFromJsonAsync<PayrollRowDto>(JsonOptions))!;
        zeroRow.ApprovalStatus.Should().Be("Eksik Veri");
        zeroRow.Warnings.Should().Contain(["Brüt maaş sıfır", "Net ödeme negatif"]);
        (await admin.PostAsync($"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Önceki matrah da eski sözleşmedeki gibi gönderilse bile ilk dönemin
        // sunucu matrahı sıfır kalır.
        var updateResponse = await admin.PutAsJsonAsync(
            $"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}", new
            {
                grossSalary = 118000,
                statutoryProfile = "domesticPrivateSectorStandardFourA",
                workedDays = 30,
                sgkPremiumDaysConfirmed = true,
                overtimeHours = 12,
                overtimeTreatmentConfirmed = true,
                premiumPay = 3500,
                roadAllowance = 1200,
                mealAllowance = 1800,
                mealDays = 22,
                mealTreatment = "cashExemptWhereNoMealProvided",
                benefitPay = 3200,
                benefitPayFullyTaxableConfirmed = true,
                applyMinimumWageTaxExemption = true,
                employerSgkDiscount = "none",
                disabilityReliefType = "none",
                otherIncomeTaxBaseDeductionsConfirmedNone = true,
                previousEmployerIncomeTaxBase = 85000,
                previousEmployerIncomeTaxBaseReference = "Çalışan talebi + işveren kabulü TEST-321",
                cumulativeIncomeTaxBaseConfirmed = true,
                specialDeductions = 1800,
                previousTaxBase = 312000,
                ibanComplete = true,
            });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedRow = (await updateResponse.Content.ReadFromJsonAsync<PayrollRowDto>(JsonOptions))!;

        updatedRow.ApprovalStatus.Should().Be("Kontrol");
        updatedRow.StatutoryProfile.Should().Be(
            PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA);
        updatedRow.PreviousTaxBase.Should().Be(85000m,
            "eski sözleşmedeki previousTaxBase yok sayılır; referanslı önceki işveren açılışı kullanılır");
        updatedRow.NetPay.Should().BeGreaterThan(95000m, "referanslı açılış matrahı vergi dilimine katılır");
        updatedRow.EmployerCost.Should().Be(169283.25m);
        updatedRow.SgkExemptMealAllowance.Should().Be(1800m);
        updatedRow.AppliedEmployerSgkRate.Should().Be(21.75m);
        updatedRow.Warnings.Should().Contain("Kontrol bekliyor");

        // --- check: eksiksiz satırlar Onaya Hazır, IBAN'sızlar Eksik Veri ---
        var checkResponse = await admin.PostAsync($"/api/payroll/periods/{period.Id}/check", null);
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkedPeriod = (await checkResponse.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(JsonOptions))!;
        checkedPeriod.Status.Should().Be("control");
        checkedPeriod.Rows.Single(r => r.Id == ahmetRow.Id).ApprovalStatus.Should().Be("Onaya Hazır");

        var missingRow = checkedPeriod.Rows.FirstOrDefault(r => r.ApprovalStatus == "Eksik Veri");
        if (missingRow is not null)
        {
            (await admin.PostAsync(
                    $"/api/payroll/periods/{period.Id}/rows/{missingRow.Id}/approve", null))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "eksik verili satır onaylanamaz");
        }

        // Tüm satırlar onaylanmadan submit 409.
        (await admin.PostAsync($"/api/payroll/periods/{period.Id}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // --- kalan satırları tamamla ve tümünü onayla ---
        foreach (var row in checkedPeriod.Rows.Where(r => r.Id != ahmetRow.Id))
        {
            (await admin.PutAsJsonAsync($"/api/payroll/periods/{period.Id}/rows/{row.Id}", new
            {
                grossSalary = 50000,
                statutoryProfile = "domesticPrivateSectorStandardFourA",
                workedDays = 30,
                sgkPremiumDaysConfirmed = true,
                overtimeHours = 0,
                disabilityReliefType = "none",
                otherIncomeTaxBaseDeductionsConfirmedNone = true,
                cumulativeIncomeTaxBaseConfirmed = true,
                ibanComplete = true,
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Aynı satıra iki eşzamanlı onay geldiğinde dönem kilidi işlemleri
        // serileştirir: yalnız biri snapshot üretir, diğeri güncel durumu görür.
        var concurrentApprovals = await Task.WhenAll(
            admin.PostAsync($"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}/approve", null),
            admin.PostAsync($"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}/approve", null));
        concurrentApprovals.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        concurrentApprovals.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        foreach (var row in checkedPeriod.Rows.Where(row => row.Id != ahmetRow.Id))
        {
            (await admin.PostAsync($"/api/payroll/periods/{period.Id}/rows/{row.Id}/approve", null))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "tüm satırlar onaylansa bile dönem gönderilmeden yenisi açılamaz");

        // Eşzamanlı iki submit de aynı dönem kilidini paylaşır; yalnız ilk geçiş
        // dönemi kapatır, ikincisi kapanmış durumu görüp 409 alır.
        var concurrentSubmissions = await Task.WhenAll(
            admin.PostAsync($"/api/payroll/periods/{period.Id}/submit", null),
            admin.PostAsync($"/api/payroll/periods/{period.Id}/submit", null));
        concurrentSubmissions.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        concurrentSubmissions.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var approvedSubmission = concurrentSubmissions.Single(
            response => response.StatusCode == HttpStatusCode.OK);
        (await approvedSubmission.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(JsonOptions))!
            .Status.Should().Be("approved");

        (await admin.PutAsJsonAsync($"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}", new
        {
            grossSalary = 1,
            workedDays = 30,
            overtimeHours = 0,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict, "kapanan dönem satırı değişmez");

        // --- özet (SQL view) ---
        var summary = await GetAsync<PeriodSummaryDto>(admin, $"/api/payroll/periods/{period.Id}/summary");
        summary.EmployeeCount.Should().Be(checkedPeriod.Rows.Count);
        summary.ApprovedCount.Should().Be(checkedPeriod.Rows.Count);
        summary.TotalNet.Should().BeGreaterThan(90000m);

        // --- kümülatif matrah devri: Ekim dönemi Ahmet satırı ---
        var finalPeriod = await GetAsync<PayrollPeriodDetailDto>(admin, $"/api/payroll/periods/{period.Id}");
        var approvedAhmet = finalPeriod.Rows.Single(r => r.Id == ahmetRow.Id);

        // İki farklı ileri ay aynı anda istense bile tenant başına kilit yalnız
        // birini açar; diğeri kilit sonrasında güncel sırayı görüp 409 alır.
        var concurrentResponses = await Task.WhenAll(
            admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10 }),
            admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 11 }));
        concurrentResponses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        concurrentResponses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var nextPeriod = (await concurrentResponses
            .Single(response => response.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(JsonOptions))!;

        nextPeriod.Rows.Single(r => r.Name == "Ahmet Yılmaz").PreviousTaxBase
            .Should().BeApproximately(85000m + approvedAhmet.IncomeTaxBase, 0.01m,
                "önceki işveren açılışı ve onaylı dönem matrahı birlikte devredilir");
        nextPeriod.Rows.Select(r => r.Name).Should().Contain(futureEmployee.Name,
            "dönem içinde işe başlayan aktif çalışan bordroya girer");
        nextPeriod.Rows.Select(r => r.Name).Should().NotContain(departingEmployee.Name,
            "ayrılış ayından sonraki bordrolara pasif personel taşınmaz");

        (await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 8 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "en son dönemden geriye doğru dönem açılamaz");

        // --- pusula PDF: çalışan kendisininkini indirir, başkasınınkini indiremez ---
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");

        var payslipResponse = await employee.GetAsync(
            $"/api/payroll/periods/{period.Id}/rows/{ahmetRow.Id}/payslip");
        payslipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        payslipResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var pdfBytes = await payslipResponse.Content.ReadAsByteArrayAsync();
        pdfBytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdfBytes[..4]).Should().Be("%PDF");

        var eceRowId = checkedPeriod.Rows.Single(r => r.Name == "Ece Arslan").Id;
        (await employee.GetAsync($"/api/payroll/periods/{period.Id}/rows/{eceRowId}/payslip"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // --- /my: çalışan kendi onaylı bordrolarını görür ---
        var myPayslips = await GetAsync<List<MyPayslipDto>>(employee, "/api/payroll/my");
        myPayslips.Should().Contain(p =>
            p.PeriodName == "Eylül 2026" && Math.Abs(p.NetPay - approvedAhmet.NetPay) < 0.01m);
    }

    [Fact]
    public async Task AccessGuards_EmployeeAndManagerBlockedFromPeriodManagement()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync("/api/payroll/periods")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.GetAsync("/api/payroll/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // routes.js: /payroll rolleri hr-admin + employee; manager tamamen dışarıda.
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync("/api/payroll/my")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- yardımcılar ve yanıt modelleri ---

    private sealed record PayrollCalcDto(decimal NetPay, decimal EmployerCost, decimal SgkBase, List<string> Warnings);

    private sealed record PeriodSummaryDto(
        int PayrollPeriodId, int EmployeeCount, int ApprovedCount,
        decimal TotalGross, decimal TotalNet, decimal TotalDeductions, decimal TotalEmployerCost);

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"demo giriş başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task SeedCompleteTimesheetsAsync(
        HttpClient admin,
        IReadOnlyCollection<EmployeeListItemDto> employees,
        int year,
        int month,
        int overtimeEmployeeId)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        foreach (var employee in employees.Where(e =>
                     e.Status == "active" && e.HireDate <= periodEnd))
        {
            var firstRequiredDate = employee.HireDate > periodStart ? employee.HireDate : periodStart;
            for (var date = firstRequiredDate; date <= periodEnd; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }

                var checkOut = employee.Id == overtimeEmployeeId && date == periodStart
                    ? "20:00"
                    : "18:00";
                var response = await admin.PostAsJsonAsync("/api/attendance", new
                {
                    employeeId = employee.Id,
                    model = new
                    {
                        workDate = date.ToString("yyyy-MM-dd"),
                        checkIn = "09:00",
                        checkOut,
                        breakMinutes = 60,
                    },
                });
                response.StatusCode.Should().Be(HttpStatusCode.Created,
                    $"{employee.Name} {date:yyyy-MM-dd} puantajı hazırlanmalı");
            }
        }
    }

    private static async Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient admin,
        string firstName,
        string lastName,
        int departmentId,
        string hireDate)
    {
        var response = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName,
            lastName,
            title = "Test Uzmanı",
            departmentId,
            hireDate,
            status = "active",
            profile = new { iban = "TR120006400000112345678901" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }

    /// <summary>Mevcut ayarları temel alıp yalnızca istenen alanları değiştiren PUT gövdesi.</summary>
    private static object SettingsBody(
        PayrollSettingsDto baseline,
        string effectiveFrom,
        decimal sgkBaseMin,
        decimal? overtimeMultiplier = null,
        IReadOnlyList<TaxBracketDto>? taxBrackets = null,
        string sourceReference = "https://www.sgk.gov.tr/test-payroll-reference") => new
    {
        effectiveFrom,
        overtimeMultiplier = overtimeMultiplier ?? baseline.OvertimeMultiplier,
        monthlyWorkingHours = baseline.MonthlyWorkingHours,
        defaultWorkedDays = baseline.DefaultWorkedDays,
        sgkEmployeeRate = baseline.SgkEmployeeRate,
        unemploymentEmployeeRate = baseline.UnemploymentEmployeeRate,
        sgkEmployerRate = baseline.SgkEmployerRate,
        unemploymentEmployerRate = baseline.UnemploymentEmployerRate,
        stampTaxRate = baseline.StampTaxRate,
        sgkBaseMin,
        sgkBaseMax = baseline.SgkBaseMax,
        monthlyMinWageIncomeTaxExemption = baseline.MonthlyMinWageIncomeTaxExemption,
        monthlyMinWageStampTaxExemption = baseline.MonthlyMinWageStampTaxExemption,
        minWageGross = sgkBaseMin,
        mealSgkDailyExemption = baseline.MealSgkDailyExemption,
        mealIncomeTaxDailyExemption = baseline.MealIncomeTaxDailyExemption,
        otherSectorEmployerDiscountPoints = baseline.OtherSectorEmployerDiscountPoints,
        manufacturingEmployerDiscountPoints = baseline.ManufacturingEmployerDiscountPoints,
        firstDegreeDisabilityDeduction = baseline.FirstDegreeDisabilityDeduction,
        secondDegreeDisabilityDeduction = baseline.SecondDegreeDisabilityDeduction,
        thirdDegreeDisabilityDeduction = baseline.ThirdDegreeDisabilityDeduction,
        sourceReference,
        reviewNotes = "Entegrasyon testi taslağı",
        taxBrackets = taxBrackets ?? baseline.TaxBrackets,
    };

    private static async Task<PayrollSettingsDto> SaveAndApproveSettingsAsync(
        HttpClient admin,
        object body)
    {
        var save = await admin.PutAsJsonAsync("/api/payroll/settings", body);
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await save.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
        draft.Status.Should().Be("draft");

        var approve = await admin.PostAsJsonAsync(
            $"/api/payroll/settings/{draft.Id}/approve",
            new { id = draft.Id, expectedVersion = draft.Version });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await approve.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
    }
    /// <summary>Tabanın altında brütle önizleme: SGK matrahı, o tarihte yürürlükteki tabanı gösterir.</summary>
    private static async Task<decimal> PreviewSgkBaseAsync(HttpClient admin, string asOf)
    {
        var response = await admin.PostAsJsonAsync("/api/payroll/preview", new
        {
            grossSalary = 20000,
            statutoryProfile = "domesticPrivateSectorStandardFourA",
            disabilityReliefType = "none",
            workedDays = 30,
            overtimeHours = 0,
            premiumPay = 0,
            roadAllowance = 0,
            mealAllowance = 0,
            specialDeductions = 0,
            previousTaxBase = 0,
            asOf,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"önizleme {asOf}");
        return (await response.Content.ReadFromJsonAsync<PayrollCalcDto>())!.SgkBase;
    }

    private static async Task<int> FindEmployeeIdAsync(HttpClient adminClient, string search)
    {
        var response = await adminClient.GetAsync($"/api/employees?search={Uri.EscapeDataString(search)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<EmployeeListItemDto>>())!;
        return page.Items.First().Id;
    }

    private static async Task SeedEmployeeTimesheetsAsync(
        HttpClient admin,
        int employeeId,
        string employeeName,
        DateOnly start,
        DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var response = await admin.PostAsJsonAsync("/api/attendance", new
            {
                employeeId,
                model = new
                {
                    workDate = date.ToString("yyyy-MM-dd"),
                    checkIn = "09:00",
                    checkOut = "18:00",
                    breakMinutes = 60,
                },
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created,
                $"{employeeName} {date:yyyy-MM-dd} puantajı hazırlanmalı");
        }
    }
}
