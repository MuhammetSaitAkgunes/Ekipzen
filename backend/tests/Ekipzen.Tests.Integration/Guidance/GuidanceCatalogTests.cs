using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Constants;
using Ekipzen.Infrastructure.Identity;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Persistence.Migrations.Platform;
using Ekipzen.Tests.Integration.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ekipzen.Tests.Integration.Guidance;

[Collection(ApiCollection.Name)]
public sealed class GuidanceCatalogTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private const string Root = "/api/guidance/catalog";

    [Fact]
    public async Task Reading_IsExplicitIdempotentPersonalAndVersioned_WithoutCreatingWorkEvidence()
    {
        var team = await TeamAsync();
        await using var host = team.Host;
        var owner = team.Clients[0]; var other = team.Clients[1];
        var outsider = await AuthedClientAsync("globex-admin@globex.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var key = $"reading-{Guid.NewGuid():N}";
        try
        {
            var create = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body = CompleteBody("Okuma testi") });
            var item = (await create.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await owner.PostAsJsonAsync($"{Root}/versions/{item.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
            item = await ChangeAsync(owner, item, "submit");
            var publish = await PublishAsync(other, item);
            publish.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await publish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var readingPath = $"{Root}/versions/{item.Content.Id}/readings";
            (await owner.GetAsync($"{Root}/versions/{item.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().BeEmpty("ekran açılması okuma beyanı üretmez");
            (await owner.PostAsJsonAsync(readingPath, new { confirmed = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await manager.PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.NotFound, "hedef rol dışındaki okuyucu kayıt oluşturamaz");
            (await Factory.CreateClient().PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var responses = await Task.WhenAll(owner.PostAsJsonAsync(readingPath, new { confirmed = true }), owner.PostAsJsonAsync(readingPath, new { confirmed = true }));
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
            var first = (await responses[0].Content.ReadFromJsonAsync<GuidanceContentReadingDto>())!;
            var repeated = (await responses[1].Content.ReadFromJsonAsync<GuidanceContentReadingDto>())!;
            repeated.Should().BeEquivalentTo(first);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().ContainSingle();
            (await other.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().BeEmpty("aynı şirkette başka kişinin inceleme beyanı görünmez");
            (await outsider.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().NotContain(x => x.ContentKey == key);
            var export = System.Text.Json.JsonDocument.Parse(await (await owner.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(1);
            export.RootElement.GetProperty("guidanceContentReadings")[0].GetProperty("firstReadAtUtc").GetString().Should().EndWith("Z");
            export.RootElement.GetProperty("employeeCreationEvidences").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("candidateReviewEvidences").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("guidanceDecisionProgresses").GetArrayLength().Should().Be(0);
            var fork = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", basedOnId = item.Content.Id, body = CompleteBody("Yeni metin") });
            var next = (await fork.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next = await ChangeAsync(owner, next, "submit");
            next = (await (await PublishAsync(other, next)).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var history = (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!;
            history.Should().ContainSingle().Which.IsCurrent.Should().BeFalse("yeni sürüm eski okuma kaydını devralmaz");
            history[0].FirstReadAtUtc.Should().Be(first.FirstReadAtUtc);
            (await owner.PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.PostAsJsonAsync($"{Root}/versions/{next.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().HaveCount(2);
            using (var tenantScope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
            {
                var db = tenantScope.Services.GetRequiredService<AppDbContext>();
                var rollbackGuard = new Ekipzen.Infrastructure.Persistence.Migrations.AddGuidanceContentReadings()
                    .DownOperations.OfType<SqlOperation>().Single().Sql;
                var rollback = async () => await db.Database.ExecuteSqlRawAsync(rollbackGuard);
                (await rollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
                (await db.GuidanceContentReadings.CountAsync()).Should().Be(2);
            }
            next = await ChangeAsync(other, next, "retire", "Okuma güncellik testi");
            (await owner.PostAsJsonAsync($"{Root}/versions/{next.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().OnlyContain(x => !x.IsCurrent);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task CentralCatalog_RequiresIndependentTeamApproval_AndPreservesPublishedVersions()
    {
        var team = await TeamAsync();
        await using var editorHost = team.Host;
        var author = team.Clients[0]; var contributor = team.Clients[1]; var approver = team.Clients[2];
        var outsiderLogin = await AuthedClientAsync("globex-admin@globex.local");
        var outsider = editorHost.CreateClient();
        outsider.DefaultRequestHeaders.Authorization = outsiderLogin.DefaultRequestHeaders.Authorization;
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var key = $"catalog-{Guid.NewGuid():N}";
        var body = CompleteBody("İlk yayımlanmış gerekçe");
        try
        {
            (await outsider.GetFromJsonAsync<GuidanceEditorialAccessDto>($"{Root}/access"))!.CanManage.Should().BeFalse();
            (await team.Clients[3].GetFromJsonAsync<GuidanceEditorialAccessDto>($"{Root}/access"))!.CanManage.Should().BeFalse();
            (await team.Clients[3].GetAsync($"{Root}/editorial")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "ekip kiracısında hr-admin olmak tek başına yayın yetkisi vermez");
            (await outsider.GetAsync($"{Root}/editorial")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await outsider.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Factory.CreateClient().GetAsync(Root)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Revision.Should().Be(1); item.Content.ContentVersion.Should().Be(1);
            (await outsider.GetAsync($"{Root}/versions/{item.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var updateBody = new { expectedRevision = 1, body };
            var updates = await Task.WhenAll(author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", updateBody),
                author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", updateBody));
            updates.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
            updates.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
            item = (await updates.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item = await ChangeAsync(author, item, "submit");
            item.Content.State.Should().Be("in-review"); item.CanPublish.Should().BeFalse();
            (await PublishAsync(author, item)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await contributor.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await contributor.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/return", new { expectedRevision = item.Revision }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            item = await ChangeAsync(contributor, item, "return", "Örneğin açıklamasını yeniden kontrol ediyoruz.");
            var edit = await contributor.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body });
            edit.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await edit.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Contributors.Should().HaveCount(2);
            item = await ChangeAsync(contributor, item, "submit");
            (await PublishAsync(contributor, item)).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "taslağı düzenleyen farklı kullanıcı da bu sürümün hazırlayanıdır");
            var publish = await PublishAsync(approver, item);
            publish.StatusCode.Should().Be(HttpStatusCode.OK, await publish.Content.ReadAsStringAsync());
            item = (await publish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Content.IsCurrent.Should().BeTrue();
            var originalId = item.Content.Id;
            using (var platformScope = Factory.Services.CreateScope())
            {
                var platformDb = platformScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var rollbackGuard = new AddCentralGuidanceContentCatalog().DownOperations.OfType<SqlOperation>().Single().Sql;
                Func<Task> guardedRollback = () => platformDb.Database.ExecuteSqlRawAsync(rollbackGuard);
                (await guardedRollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
                (await platformDb.GuidanceContentRevisions.AnyAsync(x => x.Id == originalId)).Should().BeTrue();
            }
            var outsideRead = await outsider.GetAsync($"{Root}/versions/{originalId}");
            outsideRead.StatusCode.Should().Be(HttpStatusCode.OK, "katalog şirketlere ortak yayımlanır");
            var publicJson = await outsideRead.Content.ReadAsStringAsync();
            publicJson.Should().NotContain("PRIVATE-REVIEW").And.NotContain("contributors").And.NotContain("actorUserId");
            using (var json = System.Text.Json.JsonDocument.Parse(publicJson))
                json.RootElement.GetProperty("publishedAtUtc").GetString().Should().EndWith("Z");
            (await employee.GetAsync($"{Root}/versions/{originalId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await manager.GetAsync($"{Root}/versions/{originalId}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "okur hedef rolüne sunucuda göre süzülür");
            (await author.PutAsJsonAsync($"{Root}/editorial/{originalId}", new { expectedRevision = item.Revision, body = body with { Why = "Yerinde değişiklik" } }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            var forkRequest = new { contentKey = key, processKey = "personnel", basedOnId = originalId, body = body with { Why = "İkinci sürüm gerekçesi" } };
            var forks = await Task.WhenAll(author.PostAsJsonAsync($"{Root}/editorial", forkRequest), author.PostAsJsonAsync($"{Root}/editorial", forkRequest));
            forks.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(1);
            forks.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
            var next = (await forks.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next.Content.ContentVersion.Should().Be(2); next.BasedOnId.Should().Be(originalId);
            next = await ChangeAsync(author, next, "submit");
            var nextPublish = await PublishAsync(approver, next);
            nextPublish.StatusCode.Should().Be(HttpStatusCode.OK);
            next = (await nextPublish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var original = (await outsider.GetFromJsonAsync<GuidanceContentDto>($"{Root}/versions/{originalId}"))!;
            original.Body.Why.Should().Be("İlk yayımlanmış gerekçe"); original.IsCurrent.Should().BeFalse();
            var current = (await outsider.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Where(x => x.ContentKey == key).ToList();
            current.Should().ContainSingle().Which.Id.Should().Be(next.Content.Id);
            next = await ChangeAsync(approver, next, "retire", "Dayanak yeniden değerlendirilecek.");
            next.Content.State.Should().Be("retired");
            (await outsider.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key,
                "son sürüm kaldırılınca eski yayımlanmış sürüme sessizce geri dönülmez");
            (await outsider.GetAsync($"{Root}/versions/{next.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            var audit = (await approver.GetFromJsonAsync<List<GuidanceEditorialDto>>($"{Root}/editorial"))!.Single(x => x.Content.Id == originalId);
            audit.Events.Select(x => x.Action).Should().Contain(["created", "updated", "submit", "return", "publish"]);
            audit.Content.IsCurrent.Should().BeFalse();
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task Publication_RequiresCompleteOfficialSourceAndCurrentExpertReview()
    {
        var team = await TeamAsync();
        await using var editorHost = team.Host;
        var author = team.Clients[0]; var approver = team.Clients[2];
        var key = $"validation-{Guid.NewGuid():N}";
        var incomplete = CompleteBody("İnceleme") with { Example = "", SourceKind = "legislation", SourceReference = "https://unofficial.example.test/rule" };
        try
        {
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "leave", body = incomplete });
            created.StatusCode.Should().Be(HttpStatusCode.Created, "eksik içerik taslak olarak saklanabilir");
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await author.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/submit", new { expectedRevision = item.Revision }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = CompleteBody("İncelenen gerekçe") with { SourceKind = "legislation", SourceReference = "https://www.mevzuat.gov.tr/" };
            var updated = await author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body });
            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await updated.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item = await ChangeAsync(author, item, "submit");
            var review = Review();
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { Confirmed = false } }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { ReviewedOn = GuidanceContentCatalog.Today.AddDays(-1) } }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "inceleme son düzenlemeden önce olamaz");
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { ReviewDueOn = GuidanceContentCatalog.Today.AddDays(-1) } }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PublishAsync(approver, item)).StatusCode.Should().Be(HttpStatusCode.OK);
            // Gelecek sürüm bugünkü rehberi erken değiştiremez; süresi dolan son
            // sürüm eski sürüme düşmeden katalogdan çıkar.
            using var scope = Factory.Services.CreateScope();
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var stored = await platform.GuidanceContentRevisions.SingleAsync(x => x.Id == item.Content.Id);
            stored.EffectiveFrom = GuidanceContentCatalog.Today.AddDays(1);
            await platform.SaveChangesAsync(CancellationToken.None);
            (await author.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key);
            stored.EffectiveFrom = GuidanceContentCatalog.Today;
            stored.ReviewDueOn = GuidanceContentCatalog.Today.AddDays(-1);
            await platform.SaveChangesAsync(CancellationToken.None);
            (await author.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key);
            (await author.GetFromJsonAsync<GuidanceContentDto>($"{Root}/versions/{item.Content.Id}"))!.IsCurrent.Should().BeFalse();
        }
        finally { await CleanupAsync(key); }
    }

    private async Task<(WebApplicationFactory<Program> Host, HttpClient[] Clients, int TenantId)> TeamAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var emails = Enumerable.Range(0, 4).Select(x => $"editor-{x}-{suffix}@example.test").ToArray();
        var tenant = await ProvisionAndActivateAsync("Ekipzen yayın testi", emails[0]);
        var ids = new List<string>();
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenant.TenantId))
        {
            var users = scope.Services.GetRequiredService<UserManager<ApplicationUser>>();
            var directory = scope.Services.GetRequiredService<ITenantDirectory>();
            ids.Add((await users.FindByEmailAsync(emails[0]))!.Id);
            for (var i = 1; i < emails.Length; i++)
            {
                var user = new ApplicationUser { UserName = emails[i], Email = emails[i], EmailConfirmed = true,
                    TenantId = tenant.TenantId, DisplayName = $"Yayın inceleyen {i}" };
                (await users.CreateAsync(user, DefaultPassword)).Succeeded.Should().BeTrue();
                (await users.AddToRoleAsync(user, Roles.HrAdmin)).Succeeded.Should().BeTrue();
                await directory.ReserveAsync(emails[i], tenant.TenantId, CancellationToken.None);
                ids.Add(user.Id);
            }
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GuidanceEditorial:TenantId"] = tenant.TenantId.ToString(),
            ["GuidanceEditorial:UserIds:0"] = ids[0], ["GuidanceEditorial:UserIds:1"] = ids[1], ["GuidanceEditorial:UserIds:2"] = ids[2],
        }).Build();
        var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGuidanceEditorialAccess>();
            services.AddScoped<IGuidanceEditorialAccess>(sp => new GuidanceEditorialAccess(configuration,
                sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<ICurrentTenant>()));
        }));
        var clients = new List<HttpClient>();
        foreach (var email in emails)
        {
            var login = await host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = DefaultPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
            var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            clients.Add(client);
        }
        return (host, clients.ToArray(), tenant.TenantId);
    }
    private async Task CleanupAsync(string key)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformDbContext>().GuidanceContentRevisions
            .Where(x => x.ContentKey == key).ExecuteDeleteAsync();
    }
    private static GuidanceContentBody CompleteBody(string why) => new(
        "Test rehber başlığı", [Roles.HrAdmin, Roles.Employee], "Kaydı doğrulamayı öğren", "Doğru kaydı bul",
        "Kartı karşılaştır", why, "Anonim ayrıntılı örnek", "Yanlış kişiyi seçme", "Kart: personel kaydı",
        "Doğru kişiyi belirle", "İşlem sunucu sonucu", "İlgili kayda dön", "İşlem durumuna göre düzelt",
        "Çelişkide uzman incelemesi iste", "good-practice", "Ekipzen test kaynağı", "İşlem güvenliği",
        "Standart günlük işlem", GuidanceContentCatalog.Today, null);
    private static GuidanceExpertReview Review() => new("İnceleme uzmanı", "İK uzmanı", "PRIVATE-REVIEW-123",
        GuidanceContentCatalog.Today, GuidanceContentCatalog.Today.AddDays(30), true);
    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, GuidanceEditorialDto item) =>
        client.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = Review() });
    private static async Task<GuidanceEditorialDto> ChangeAsync(HttpClient client, GuidanceEditorialDto item, string action, string? reason = null)
    {
        var response = await client.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/{action}", new { expectedRevision = item.Revision, reason });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
    }
}
