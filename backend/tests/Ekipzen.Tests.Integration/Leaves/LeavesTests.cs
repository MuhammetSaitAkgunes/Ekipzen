using FluentAssertions;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Leaves.Queries;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Leaves;

/// <summary>
/// Faz 4 uçtan uca: iş-günü hesabı (SQL function), talep yaşam döngüsü
/// (oluştur → onay kuyruğu → onay → bakiye view), çakışma/bakiye korumaları,
/// otomatik onaylı tipler, iptal ve takım yokluk widget'ı.
/// Testler Ahmet için ayrık tarih aralıkları kullanır (aynı koşuda çakışmasın).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LeavesTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task LeaveTypes_ReturnsSeededCatalog()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var types = await GetAsync<List<LeaveTypeDto>>(client, "/api/leaves/types");

        types.Select(t => t.Code).Should().Contain(["Yıllık", "Mazeret", "Raporlu", "Uzaktan"]);
        types.Single(t => t.Code == "Yıllık").DeductsFromAnnualBalance.Should().BeTrue();
        types.Single(t => t.Code == "Raporlu").RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public async Task Balance_WithoutEmployeeLink_Returns403()
    {
        // hr-admin demo hesabının Employee bağı yok.
        var client = await AuthedClientAsync("ik@hrmaster.local");
        (await client.GetAsync("/api/leaves/balance")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LeaveLifecycle_CreateApproveBalanceTeamWidget()
    {
        var ahmet = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var ece = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var types = await GetAsync<List<LeaveTypeDto>>(ahmet, "/api/leaves/types");
        var yillik = types.Single(t => t.Code == "Yıllık");

        // 13–17 Tem 2026: 5 hafta içi − 15 Tem tatili = 4 iş günü.
        var createResponse = await ahmet.PostAsJsonAsync("/api/leaves", new
        {
            leaveTypeId = yillik.Id,
            startDate = "2026-07-13",
            endDate = "2026-07-17",
            description = "Yaz tatili",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        created.Days.Should().Be(4, "hafta sonu + 15 Temmuz tatili düşülür");
        created.Status.Should().Be("pending");

        // Çalışan onay kuyruğuna erişemez.
        (await ahmet.GetAsync("/api/leaves/pending")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Manager kuyruğunda ekip talebi görünür.
        var pending = await GetAsync<List<LeaveRequestDto>>(ece, "/api/leaves/pending");
        pending.Should().Contain(r => r.Id == created.Id && r.EmployeeName == "Ahmet Yılmaz");

        // Onay → durum + bakiye (SQL view) güncellenir.
        var approveResponse = await ece.PostAsJsonAsync(
            $"/api/leaves/{created.Id}/approve", new { note = "İyi tatiller" });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approveResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!.Status.Should().Be("approved");

        var balance = await GetAsync<LeaveBalanceDto>(ahmet, "/api/leaves/balance?year=2026");
        balance.EntitledDays.Should().Be(24);
        balance.UsedDays.Should().Be(4);
        balance.RemainingDays.Should().Be(20);

        // Onaylanan talep tekrar karara bağlanamaz.
        (await ece.PostAsJsonAsync($"/api/leaves/{created.Id}/reject", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Takım widget'ı ileri-bakışlı 14 günlük pencere kullanır (EndDate >= bugün).
        // Bugüne GÖRELİ, onaylı bir izinle test et — sabit tarihli senaryo takvim
        // ilerledikçe kırılmasın (yukarıdaki Temmuz izni geçmişte kalabilir).
        var soonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var soonEnd = soonStart.AddDays(4);
        var soonResponse = await ahmet.PostAsJsonAsync("/api/leaves", new
        {
            leaveTypeId = yillik.Id,
            startDate = soonStart.ToString("yyyy-MM-dd"),
            endDate = soonEnd.ToString("yyyy-MM-dd"),
            description = "Yaklaşan izin",
        });
        soonResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var soon = (await soonResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        (await ece.PostAsJsonAsync($"/api/leaves/{soon.Id}/approve", new { note = "onay" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var team = await GetAsync<List<TeamLeaveDto>>(ece, "/api/leaves/team");
        team.Should().Contain(t => t.EmployeeName == "Ahmet Yılmaz" && t.LeaveTypeName == "Yıllık İzin");

        var ownTeamView = await GetAsync<List<TeamLeaveDto>>(ahmet, "/api/leaves/team");
        ownTeamView.Should().Contain(t => t.EmployeeName == "Ahmet Yılmaz");
    }

    [Fact]
    public async Task CreateRequest_OverlappingRange_Returns409()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var mazeret = await GetTypeAsync(client, "Mazeret");

        // Tarihler GÖRELİ ve uzak: sabit tarih kullanmak, aynı çalışana bugüne göre
        // izin oluşturan diğer testle (yukarıdaki "soon" penceresi) takvim ilerledikçe
        // çakışıyordu ve ilk oluşturma 409 dönüyordu. Uzak pencere hem o testten hem
        // ekim/aralık aralıklarından ayrık kalır.
        var baslangic = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(300);
        var orta = baslangic.AddDays(1);
        var bitis = baslangic.AddDays(2);
        static string G(DateOnly d) => d.ToString("yyyy-MM-dd");

        (await CreateAsync(client, mazeret.Id, G(baslangic), G(orta)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await CreateAsync(client, mazeret.Id, G(orta), G(bitis)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRequest_WeekendOnly_Returns409()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var mazeret = await GetTypeAsync(client, "Mazeret");

        // 2026-08-01/02 cumartesi-pazar.
        (await CreateAsync(client, mazeret.Id, "2026-08-01", "2026-08-02"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRequest_ExceedingAnnualBalance_Returns409()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var yillik = await GetTypeAsync(client, "Yıllık");

        // Eki–Kas 2026 ≈ 40+ iş günü > 24 gün hak ediş.
        (await CreateAsync(client, yillik.Id, "2026-10-01", "2026-11-30"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRequest_PendingRequestsCannotCollectivelyExceedBalance()
    {
        // vw_LeaveBalanceSummary yalnız onaylı talepleri sayar; bekleyen düşümlü talepler
        // toplamda bakiyeyi aşamamalı. Bakiye/onay durumu koşuya göre değişebildiğinden
        // sayı sabitlenmez: art arda ayrık tekil-gün yıllık talepler oluşturulur; bir
        // noktada bakiye 409'u gelmeli ve oluşan bekleyen gün toplamı kalanı aşmamalı.
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var yillik = await GetTypeAsync(client, "Yıllık");
        // Seed bakiyesi içinde bulunulan yıla (2026) yazılır; ilk yarı ayları başka
        // testlerin aralıklarıyla çakışmaz.
        var balance = await GetAsync<LeaveBalanceDto>(client, "/api/leaves/balance?year=2026");

        var day = new DateOnly(2026, 1, 6); // Salı
        var createdIds = new List<int>();
        var hitBalance409 = false;
        try
        {
            for (var i = 0; i < balance.RemainingDays + 5; i++, day = day.AddDays(7))
            {
                var iso = day.ToString("yyyy-MM-dd");
                var response = await CreateAsync(client, yillik.Id, iso, iso);
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    createdIds.Add((await response.Content.ReadFromJsonAsync<LeaveRequestDto>())!.Id);
                    continue;
                }
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                var body = await response.Content.ReadAsStringAsync();
                if (body.Contains("bakiye", StringComparison.OrdinalIgnoreCase))
                {
                    hitBalance409 = true;
                    break;
                }
                // Tatil/iş-günü yok (ör. resmi tatile denk gelen Salı) → say ve devam et.
            }

            hitBalance409.Should().BeTrue("bekleyen talepler kalan bakiyeye ulaşınca bakiye 409'u gelmeli");
            createdIds.Count.Should().BeLessThanOrEqualTo(balance.RemainingDays,
                "oluşan bekleyen gün toplamı kalan bakiyeyi aşmamalı");
        }
        finally
        {
            // Paylaşımlı DB'yi kirletmemek için oluşturulan bekleyen talepleri geri al.
            foreach (var id in createdIds)
            {
                await client.PostAsync($"/api/leaves/{id}/cancel", null);
            }
        }
    }

    [Fact]
    public async Task CreateRequest_NonApprovalType_IsAutoApproved()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var raporlu = await GetTypeAsync(client, "Raporlu");

        var response = await CreateAsync(client, raporlu.Id, "2026-12-07", "2026-12-08");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<LeaveRequestDto>())!
            .Status.Should().Be("approved", "Raporlu onay gerektirmez");
    }

    [Fact]
    public async Task CancelRequest_PendingOnly_AndOwnershipEnforced()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var mazeret = await GetTypeAsync(client, "Mazeret");

        // Bugüne bağlı takım-izin testiyle takvim ilerledikçe çakışmaması için
        // uzak bir pencere seç; başlangıcı çalışma gününe taşı.
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(600);
        while (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            start = start.AddDays(1);
        }

        var end = start.AddDays(1);
        var createResponse = await CreateAsync(
            client, mazeret.Id, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!;

        (await client.PostAsync($"/api/leaves/{request.Id}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsync($"/api/leaves/{request.Id}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "iptal edilen talep tekrar iptal edilemez");

        var mine = await GetAsync<List<LeaveRequestDto>>(client, $"/api/leaves/my?year={start.Year}");
        mine.Should().Contain(r => r.Id == request.Id && r.Status == "cancelled");
    }

    [Fact]
    public async Task AnnualLeave_SplitsUsageAcrossYears_AndApprovedCancellationReversesLedger()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var annual = await GetTypeAsync(employee, "Yıllık");

        var createResponse = await CreateAsync(
            employee, annual.Id, "2026-12-28", "2027-01-05");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!;

        (await manager.PostAsJsonAsync(
            $"/api/leaves/{request.Id}/approve", new { note = "Yıl geçişi" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var usage = (await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2026"))
            .Concat(await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2027"))
            .Where(item => item.LeaveRequestId == request.Id && item.Type == "usage")
            .ToList();
        usage.Should().HaveCount(2, "yıl aşan kullanım her takvim yılına ayrı hareket yazmalı");
        usage.Sum(item => item.Days).Should().Be(-request.Days);

        (await employee.PostAsync($"/api/leaves/{request.Id}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reversed = (await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2026"))
            .Concat(await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2027"))
            .Where(item => item.LeaveRequestId == request.Id && item.Type == "usage-reversal")
            .ToList();
        reversed.Should().HaveCount(2);
        reversed.Sum(item => item.Days).Should().Be(request.Days);
    }

    [Fact]
    public async Task FutureRequest_MaterializesAnniversaryOnce()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var annual = await GetTypeAsync(employee, "Yıllık");

        var response = await CreateAsync(employee, annual.Id, "2027-04-05", "2027-04-05");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await response.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        try
        {
            var first = await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2027");
            var second = await GetAsync<List<LeaveLedgerEntryDto>>(
                employee, "/api/leaves/ledger?year=2027");
            first.Should().ContainSingle(item =>
                item.Type == "service-anniversary" && item.ServiceYear == 5 && item.Days == 24);
            second.Count(item => item.Type == "service-anniversary" && item.ServiceYear == 5)
                .Should().Be(1, "aynı yıldönümü idempotent olmalı");
        }
        finally
        {
            await employee.PostAsync($"/api/leaves/{request.Id}/cancel", null);
        }
    }

    [Fact]
    public async Task PreviewRequest_UsesServerDaysWithoutCreatingRequestOrFutureAccrual()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var annual = await GetTypeAsync(employee, "Yıllık");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1400);
        while (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            start = start.AddDays(1);
        }
        var end = start.AddDays(4);
        var isoStart = start.ToString("yyyy-MM-dd");
        var isoEnd = end.ToString("yyyy-MM-dd");
        var year = start.Year;
        var beforeRequests = await GetAsync<List<LeaveRequestDto>>(employee, $"/api/leaves/my?year={year}");
        var beforeLedger = await GetAsync<List<LeaveLedgerEntryDto>>(employee, $"/api/leaves/ledger?year={year}");

        var previewResponse = await employee.PostAsJsonAsync("/api/leaves/preview", new
        {
            leaveTypeId = annual.Id,
            startDate = isoStart,
            endDate = isoEnd,
        });
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<LeaveRequestPreviewDto>())!;
        preview.CanSubmit.Should().BeTrue(preview.BlockReason);
        preview.WorkingDays.Should().BeGreaterThan(0);
        preview.DeductsFromAnnualBalance.Should().BeTrue();
        preview.RequiresApproval.Should().BeTrue();
        (await GetAsync<List<LeaveRequestDto>>(employee, $"/api/leaves/my?year={year}"))
            .Should().BeEquivalentTo(beforeRequests);
        (await GetAsync<List<LeaveLedgerEntryDto>>(employee, $"/api/leaves/ledger?year={year}"))
            .Should().BeEquivalentTo(beforeLedger, "önizleme gelecek hak edişi bile deftere yazmamalı");

        var createResponse = await CreateAsync(employee, annual.Id, isoStart, isoEnd);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await createResponse.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        try
        {
            request.Days.Should().Be(preview.WorkingDays);
            var overlapResponse = await employee.PostAsJsonAsync("/api/leaves/preview", new
            {
                leaveTypeId = annual.Id,
                startDate = isoStart,
                endDate = isoEnd,
            });
            overlapResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var overlap = (await overlapResponse.Content.ReadFromJsonAsync<LeaveRequestPreviewDto>())!;
            overlap.CanSubmit.Should().BeFalse();
            overlap.BlockReason.Should().Contain("çakışan");
        }
        finally
        {
            await employee.PostAsync($"/api/leaves/{request.Id}/cancel", null);
        }
    }

    // --- yardımcılar ---

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
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<LeaveTypeDto> GetTypeAsync(HttpClient client, string code)
        => (await GetAsync<List<LeaveTypeDto>>(client, "/api/leaves/types")).Single(t => t.Code == code);

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, int typeId, string start, string end)
        => client.PostAsJsonAsync("/api/leaves", new
        {
            leaveTypeId = typeId,
            startDate = start,
            endDate = end,
        });
}
