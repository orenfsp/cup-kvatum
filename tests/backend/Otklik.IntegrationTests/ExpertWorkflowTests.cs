using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class ExpertWorkflowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorExpertId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task Expert_scope_and_private_content_follow_the_permission_matrix()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = CreateClient();
        using var operatorClient = CreateClient();
        using var assignedExpert = CreateClient();
        using var otherExpert = CreateClient();
        using var administrator = CreateClient();
        var created = await CreateAssignedAppealAsync(applicant, operatorClient);

        await LoginAsync(assignedExpert, "expert.mediator", "ExpertHelp!2026");
        await LoginAsync(otherExpert, "expert", "ExpertHelp!2026");
        await LoginAsync(administrator, "administrator", "AdminPanel!2026");

        var assignedList = await assignedExpert.GetStringAsync(
            "api/staff/expert/appeals?priority=Standard",
            TestContext.Current.CancellationToken);
        Assert.Contains(created.AppealId.ToString(), assignedList, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherExpert.GetAsync(
                $"api/staff/expert/appeals/{created.AppealId}",
                TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await operatorClient.GetAsync(
                $"api/staff/expert/appeals/{created.AppealId}",
                TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await administrator.GetAsync(
                $"api/staff/expert/appeals/{created.AppealId}",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Assigned_expert_and_applicant_complete_an_idempotent_conversation()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = CreateClient();
        using var operatorClient = CreateClient();
        using var expert = CreateClient();
        var created = await CreateAssignedAppealAsync(applicant, operatorClient);
        await LoginAsync(expert, "expert.mediator", "ExpertHelp!2026");

        var assigned = await GetExpertDetailsAsync(expert, created.AppealId);
        var accepted = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/accept",
            new { expectedVersion = assigned.Version });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        const string privateNote = "Заметка только для участников обращения";
        var noteId = Guid.NewGuid();
        var firstNote = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/notes",
            new { clientNoteId = noteId, body = privateNote });
        var replayedNote = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/notes",
            new { clientNoteId = noteId, body = privateNote });
        Assert.Equal(HttpStatusCode.Created, firstNote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayedNote.StatusCode);

        var inProgress = await GetExpertDetailsAsync(expert, created.AppealId);
        const string questionText = "Подскажите, удалось ли сейчас перейти в безопасное место?";
        var questionId = Guid.NewGuid();
        var firstQuestion = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/questions",
            new { clientMessageId = questionId, body = questionText, expectedVersion = inProgress.Version });
        var replayedQuestion = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/questions",
            new { clientMessageId = questionId, body = questionText, expectedVersion = inProgress.Version });
        Assert.Equal(HttpStatusCode.Created, firstQuestion.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayedQuestion.StatusCode);

        var waitingStatus = await PublicStatusAsync(applicant, created.TrackNumber);
        Assert.Equal("NeedsClarification", waitingStatus.GetProperty("status").GetString());
        var waitingJson = waitingStatus.GetRawText();
        Assert.Contains(questionText, waitingJson, StringComparison.Ordinal);
        Assert.DoesNotContain(privateNote, waitingJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Эксперт по медиации", waitingJson, StringComparison.Ordinal);

        const string applicantReply = "Да, сейчас рядом есть взрослый, которому я доверяю.";
        var replyId = Guid.NewGuid();
        var firstReply = await applicant.PostAsJsonAsync(
            "api/public/appeals/messages",
            new { trackNumber = created.TrackNumber, clientMessageId = replyId, body = applicantReply },
            TestContext.Current.CancellationToken);
        var replayedReply = await applicant.PostAsJsonAsync(
            "api/public/appeals/messages",
            new { trackNumber = created.TrackNumber, clientMessageId = replyId, body = applicantReply },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstReply.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayedReply.StatusCode);

        var resumed = await GetExpertDetailsAsync(expert, created.AppealId);
        Assert.Equal("InProgress", resumed.Status);
        Assert.Equal(2, resumed.Messages.Length);
        Assert.Single(resumed.Notes);

        const string recommendationText =
            "Оставайтесь рядом с доверенным взрослым и вместе зафиксируйте факты ситуации для спокойного разговора со школой.";
        var recommendationId = Guid.NewGuid();
        var firstRecommendation = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/recommendations",
            new
            {
                clientRecommendationId = recommendationId,
                body = recommendationText,
                expectedVersion = resumed.Version
            });
        var replayedRecommendation = await PostWithCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/recommendations",
            new
            {
                clientRecommendationId = recommendationId,
                body = recommendationText,
                expectedVersion = resumed.Version
            });
        Assert.Equal(HttpStatusCode.Created, firstRecommendation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayedRecommendation.StatusCode);

        var readyStatus = await PublicStatusAsync(applicant, created.TrackNumber);
        var readyJson = readyStatus.GetRawText();
        Assert.Equal("RecommendationReady", readyStatus.GetProperty("status").GetString());
        Assert.Contains(recommendationText, readyJson, StringComparison.Ordinal);
        Assert.DoesNotContain(privateNote, readyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Эксперт по медиации", readyJson, StringComparison.Ordinal);
    }

    private static async Task<CreatedAppeal> CreateAssignedAppealAsync(
        HttpClient applicant,
        HttpClient operatorClient)
    {
        var createdResponse = await applicant.PostAsJsonAsync(
            "api/public/appeals",
            new
            {
                clientRequestId = Guid.NewGuid(),
                applicantType = "Student",
                submissionPath = "Category",
                categoryId = ConflictCategoryId,
                narrative = "Нужна помощь специалиста в тестовом диалоге.",
                answers = new Dictionary<string, string>()
            },
            TestContext.Current.CancellationToken);
        createdResponse.EnsureSuccessStatusCode();
        var created = (await createdResponse.Content.ReadFromJsonAsync<CreatedAppeal>(
            TestContext.Current.CancellationToken))!;
        await LoginAsync(operatorClient, "operator", "Operator!2026");
        var details = await operatorClient.GetFromJsonAsync<OperatorDetails>(
            $"api/staff/operator/queue/{created.AppealId}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        var assigned = await PostWithCsrfAsync(
            operatorClient,
            $"api/staff/operator/queue/{created.AppealId}/assign",
            new
            {
                expertId = MediatorExpertId,
                expectedVersion = details.Version,
                allowOverCapacity = true,
                overrideReason = "Изоляция сквозного теста рабочего места эксперта"
            });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        return created;
    }

    private static async Task<ExpertDetails> GetExpertDetailsAsync(HttpClient client, Guid appealId) =>
        (await client.GetFromJsonAsync<ExpertDetails>(
            $"api/staff/expert/appeals/{appealId}",
            TestContext.Current.CancellationToken))!;

    private static async Task<JsonElement> PublicStatusAsync(HttpClient client, string trackNumber)
    {
        var response = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await PostWithCsrfAsync(client, "api/staff/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string route, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>(
            "api/staff/auth/csrf",
            TestContext.Current.CancellationToken);
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
        return new HttpClient(handler) { BaseAddress = BaseAddress };
    }

    private static void SkipWhenIntegrationTargetIsMissing()
    {
        if (BaseAddress is null)
        {
            Assert.Skip("Set OTKLIK_INTEGRATION_BASE_URL to run live API integration tests.");
        }
    }

    private static Uri? ReadBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable("OTKLIK_INTEGRATION_BASE_URL");
        return Uri.TryCreate(value, UriKind.Absolute, out var address) ? address : null;
    }

    private sealed record CsrfPayload(string Token);

    private sealed record CreatedAppeal(Guid AppealId, string TrackNumber);

    private sealed record OperatorDetails(Guid Id, int Version);

    private sealed record ExpertDetails(Guid Id, int Version, string Status, MessageItem[] Messages, NoteItem[] Notes);

    private sealed record MessageItem(Guid Id);

    private sealed record NoteItem(Guid Id);
}
