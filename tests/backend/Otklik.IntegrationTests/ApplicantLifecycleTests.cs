using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class ApplicantLifecycleTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ExpertId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task Helped_is_atomic_then_feedback_and_private_complaint_are_accepted()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = Client(); using var operatorClient = Client(); using var expert = Client();
        var appeal = await ReadyAppealAsync(applicant, operatorClient, expert, MediatorId, "expert.mediator");
        const string complaintText = "Специалист использовал формулировку, которая показалась мне небережной.";
        Assert.Equal(HttpStatusCode.Created, (await applicant.PostAsJsonAsync("api/public/appeals/complaints", new
        {
            trackNumber = appeal.TrackNumber, clientComplaintId = Guid.NewGuid(), body = complaintText
        }, TestContext.Current.CancellationToken)).StatusCode);
        var expertJson = await expert.GetStringAsync($"api/staff/expert/appeals/{appeal.AppealId}", TestContext.Current.CancellationToken);
        Assert.DoesNotContain(complaintText, expertJson, StringComparison.Ordinal);
        var operatorWork = await operatorClient.GetStringAsync("api/staff/operator/lifecycle", TestContext.Current.CancellationToken);
        Assert.Contains(complaintText, operatorWork, StringComparison.Ordinal);
        Assert.DoesNotContain("Первый итоговый ответ интеграционного теста", operatorWork, StringComparison.Ordinal);

        var status = await StatusAsync(applicant, appeal.TrackNumber);
        var version = status.GetProperty("version").GetInt32();
        var attempts = await Task.WhenAll(
            applicant.PostAsJsonAsync("api/public/appeals/outcomes/helped", new
            {
                trackNumber = appeal.TrackNumber, clientActionId = Guid.NewGuid(), expectedVersion = version
            }, TestContext.Current.CancellationToken),
            applicant.PostAsJsonAsync("api/public/appeals/outcomes/helped", new
            {
                trackNumber = appeal.TrackNumber, clientActionId = Guid.NewGuid(), expectedVersion = version
            }, TestContext.Current.CancellationToken));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);

        var feedbackId = Guid.NewGuid();
        var feedback = await applicant.PostAsJsonAsync("api/public/appeals/feedback", new
        {
            trackNumber = appeal.TrackNumber, clientFeedbackId = feedbackId, score = 4,
            comment = "Стало понятнее, что делать дальше."
        }, TestContext.Current.CancellationToken);
        var feedbackReplay = await applicant.PostAsJsonAsync("api/public/appeals/feedback", new
        {
            trackNumber = appeal.TrackNumber, clientFeedbackId = feedbackId, score = 4,
            comment = "Стало понятнее, что делать дальше."
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, feedback.StatusCode);
        Assert.Equal(HttpStatusCode.OK, feedbackReplay.StatusCode);
        var closed = await StatusAsync(applicant, appeal.TrackNumber);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.True(closed.GetProperty("feedbackSubmitted").GetBoolean());
    }

    [Fact]
    public async Task Two_returns_revoke_experts_and_require_final_operator_decision()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = Client(); using var operatorClient = Client();
        using var firstExpert = Client(); using var secondExpert = Client();
        var appeal = await ReadyAppealAsync(applicant, operatorClient, firstExpert, MediatorId, "expert.mediator");
        var firstStatus = await StatusAsync(applicant, appeal.TrackNumber);
        Assert.Equal(HttpStatusCode.OK, (await applicant.PostAsJsonAsync("api/public/appeals/outcomes/returned", new
        {
            trackNumber = appeal.TrackNumber, clientActionId = Guid.NewGuid(),
            expectedVersion = firstStatus.GetProperty("version").GetInt32(), reason = "NeedMoreHelp",
            details = "Нужен более конкретный порядок действий."
        }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await firstExpert.GetAsync(
            $"api/staff/expert/appeals/{appeal.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsrfAsync(firstExpert,
            $"api/staff/expert/appeals/{appeal.AppealId}/questions",
            new { clientMessageId = Guid.NewGuid(), body = "Недоступное сообщение", expectedVersion = 99 })).StatusCode);

        var returnDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/lifecycle/returns/{appeal.AppealId}");
        Assert.False(returnDetail.GetProperty("requiresFinalDecision").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(operatorClient,
            $"api/staff/operator/lifecycle/returns/{appeal.AppealId}/reassign",
            new { expertId = ExpertId, expectedVersion = returnDetail.GetProperty("version").GetInt32() })).StatusCode);
        await LoginAsync(secondExpert, "expert", "ExpertHelp!2026");
        var assigned = await GetJsonAsync(secondExpert, $"api/staff/expert/appeals/{appeal.AppealId}");
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(secondExpert,
            $"api/staff/expert/appeals/{appeal.AppealId}/accept",
            new { expectedVersion = assigned.GetProperty("version").GetInt32() })).StatusCode);
        var inProgress = await GetJsonAsync(secondExpert, $"api/staff/expert/appeals/{appeal.AppealId}");
        Assert.Equal(HttpStatusCode.Created, (await PostCsrfAsync(secondExpert,
            $"api/staff/expert/appeals/{appeal.AppealId}/recommendations", new
            {
                clientRecommendationId = Guid.NewGuid(), body = "Второй итоговый ответ с более конкретным и понятным порядком действий.",
                expectedVersion = inProgress.GetProperty("version").GetInt32()
            })).StatusCode);
        var secondStatus = await StatusAsync(applicant, appeal.TrackNumber);
        Assert.Equal(HttpStatusCode.OK, (await applicant.PostAsJsonAsync("api/public/appeals/outcomes/returned", new
        {
            trackNumber = appeal.TrackNumber, clientActionId = Guid.NewGuid(),
            expectedVersion = secondStatus.GetProperty("version").GetInt32(), reason = "NotSuitable", details = "Совет все еще не подходит."
        }, TestContext.Current.CancellationToken)).StatusCode);

        var finalDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/lifecycle/returns/{appeal.AppealId}");
        Assert.True(finalDetail.GetProperty("requiresFinalDecision").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await PostCsrfAsync(operatorClient,
            $"api/staff/operator/lifecycle/returns/{appeal.AppealId}/reassign",
            new { expertId = MediatorId, expectedVersion = finalDetail.GetProperty("version").GetInt32() })).StatusCode);
        const string resolution = "Мы завершили это обращение после повторной проверки. Если ситуация продолжается, оставьте новое обращение.";
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(operatorClient,
            $"api/staff/operator/lifecycle/returns/{appeal.AppealId}/close",
            new { explanation = resolution, expectedVersion = finalDetail.GetProperty("version").GetInt32() })).StatusCode);
        var closed = await StatusAsync(applicant, appeal.TrackNumber);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(resolution, closed.GetProperty("resolution").GetString());
    }

    private static async Task<CreatedAppeal> ReadyAppealAsync(HttpClient applicant, HttpClient operatorClient,
        HttpClient expert, Guid expertId, string expertName)
    {
        var response = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(), applicantType = "Student", submissionPath = "Category",
            categoryId = ConflictCategoryId, narrative = "Нужен тестовый ответ специалиста.", answers = new Dictionary<string, string>()
        }, TestContext.Current.CancellationToken);
        var created = (await response.Content.ReadFromJsonAsync<CreatedAppeal>(TestContext.Current.CancellationToken))!;
        await LoginAsync(operatorClient, "operator", "Operator!2026");
        var queue = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}/assign", new
        {
            expertId, expectedVersion = queue.GetProperty("version").GetInt32(), allowOverCapacity = true,
            overrideReason = "Изоляция теста жизненного цикла обращения"
        })).StatusCode);
        await LoginAsync(expert, expertName, "ExpertHelp!2026");
        var assigned = await GetJsonAsync(expert, $"api/staff/expert/appeals/{created.AppealId}");
        await PostCsrfAsync(expert, $"api/staff/expert/appeals/{created.AppealId}/accept",
            new { expectedVersion = assigned.GetProperty("version").GetInt32() });
        var inProgress = await GetJsonAsync(expert, $"api/staff/expert/appeals/{created.AppealId}");
        var recommendation = await PostCsrfAsync(expert, $"api/staff/expert/appeals/{created.AppealId}/recommendations", new
        {
            clientRecommendationId = Guid.NewGuid(), body = "Первый итоговый ответ интеграционного теста с понятным следующим шагом.",
            expectedVersion = inProgress.GetProperty("version").GetInt32()
        });
        Assert.Equal(HttpStatusCode.Created, recommendation.StatusCode);
        return created;
    }

    private static async Task<JsonElement> StatusAsync(HttpClient client, string track)
    {
        var response = await client.PostAsJsonAsync("api/public/appeals/status", new { trackNumber = track }, TestContext.Current.CancellationToken);
        return await ReadJsonAsync(response);
    }
    private static async Task LoginAsync(HttpClient client, string userName, string password) =>
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(client, "api/staff/auth/login", new { userName, password })).StatusCode);
    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string route) =>
        await ReadJsonAsync(await client.GetAsync(route, TestContext.Current.CancellationToken));
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }
    private static async Task<HttpResponseMessage> PostCsrfAsync(HttpClient client, string route, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>("api/staff/auth/csrf", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private static HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer(), UseCookies = true }) { BaseAddress = BaseAddress };
    private static void SkipWhenIntegrationTargetIsMissing() { if (BaseAddress is null) Assert.Skip("Set OTKLIK_INTEGRATION_BASE_URL to run live API integration tests."); }
    private static Uri? ReadBaseAddress() { var value = Environment.GetEnvironmentVariable("OTKLIK_INTEGRATION_BASE_URL"); return Uri.TryCreate(value, UriKind.Absolute, out var address) ? address : null; }
    private sealed record CsrfPayload(string Token);
    private sealed record CreatedAppeal(Guid AppealId, string TrackNumber);
}
