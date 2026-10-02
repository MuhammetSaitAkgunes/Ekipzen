using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Constants;
using FluentAssertions;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    [Fact]
    public async Task T06_SeparateClientReadsSamePersonalVersionEvidence_AnotherUserDoesNotInheritIt()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "daily-task-action";
        try
        {
            var author = team.Clients[0]; var reviewer = team.Clients[1];
            var body = CompleteBody("Oturum devamı test verisi") with { Audience = [Roles.HrAdmin, Roles.Manager], StepKey = "task-action",
                Practice = new("Kayıt nasıl doğrulanır?", ["Tahmin", "Gerçek kayıt"], 1, "Gerçek kaydı inceleyin.") };
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "daily", body });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var source = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            source = await ChangeAsync(author, source, "submit");
            (await PublishAsync(reviewer, source)).StatusCode.Should().Be(HttpStatusCode.OK);
            var id = source.Content.Id;
            (await author.PostAsJsonAsync($"{Root}/versions/{id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.OK);
            var attempt = Guid.NewGuid();
            var answer = new { selectedChoice = 1, expectedAttemptCount = 0, attemptId = attempt };
            (await author.PostAsJsonAsync($"{Root}/versions/{id}/practice/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.OK);
            // Aynı makbuz tekrarında ikinci alıştırma denemesi üretilmez.
            (await author.PostAsJsonAsync($"{Root}/versions/{id}/practice/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PostAsJsonAsync("/api/actions", new { title = "Gerçek takip test kaydı", source = "İK", owner = "İK", guidanceContentRevisionId = id }))
                .StatusCode.Should().Be(HttpStatusCode.Created);
            using var separateClient = host.CreateClient();
            separateClient.DefaultRequestHeaders.Authorization = author.DefaultRequestHeaders.Authorization;
            (await OwnOperationsAsync(separateClient)).Should().ContainSingle().Subject.Evidence.Content.ContentRevisionId.Should().Be(id);
            var readings = (await separateClient.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!;
            readings.Should().ContainSingle().Subject.ContentRevisionId.Should().Be(id);
            var progress = (await separateClient.GetFromJsonAsync<JsonElement>($"{Root}/practice-progress"));
            progress.GetArrayLength().Should().Be(1);
            progress[0].GetProperty("attemptCount").GetInt32().Should().Be(1);
            progress[0].GetProperty("lastCorrect").GetBoolean().Should().BeTrue();
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await reviewer.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().BeEmpty();
            (await reviewer.GetFromJsonAsync<JsonElement>($"{Root}/practice-progress")).GetArrayLength().Should().Be(0);
        }
        finally { await CleanupAsync(key); }
    }
}
