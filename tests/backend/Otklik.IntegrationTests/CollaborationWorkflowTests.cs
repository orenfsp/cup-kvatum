using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class CollaborationWorkflowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ExpertId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task Coexecutor_lease_removal_and_transfer_preserve_access_boundaries()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = CreateClient();
        using var operatorClient = CreateClient();
        using var responsible = CreateClient();
        using var colleague = CreateClient();
        var created = await CreateAssignedAppealAsync(applicant, operatorClient);
        await LoginAsync(responsible, "expert.mediator", "ExpertHelp!2026");
        await LoginAsync(colleague, "expert", "ExpertHelp!2026");

        var details = await GetJsonAsync(responsible, $"api/staff/expert/appeals/{created.AppealId}");
        var accepted = await PostWithCsrfAsync(responsible, $"api/staff/expert/appeals/{created.AppealId}/accept",
            new { expectedVersion = details.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        const string privateNote = "Закрытая деталь для совместной работы экспертов";
        var note = await PostWithCsrfAsync(responsible, $"api/staff/expert/appeals/{created.AppealId}/notes",
            new { clientNoteId = Guid.NewGuid(), body = privateNote });
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);

        var coexecutorRequest = await PostWithCsrfAsync(responsible,
            $"api/staff/expert/appeals/{created.AppealId}/workflow-requests",
            new { clientRequestId = Guid.NewGuid(), type = "CoExecutor", reason = "Нужен второй взгляд на план безопасного разговора" });
        Assert.Equal(HttpStatusCode.Created, coexecutorRequest.StatusCode);
        var requestJson = await ReadJsonAsync(coexecutorRequest);
        var requestId = requestJson.GetProperty("id").GetGuid();

        var operatorList = await operatorClient.GetStringAsync("api/staff/operator/collaboration/requests",
            TestContext.Current.CancellationToken);
        Assert.Contains(requestId.ToString(), operatorList, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateNote, operatorList, StringComparison.Ordinal);
        Assert.DoesNotContain("messages", operatorList, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", operatorList, StringComparison.OrdinalIgnoreCase);

        var approved = await PostWithCsrfAsync(operatorClient,
            $"api/staff/operator/collaboration/requests/{requestId}/approve",
            new { expertId = ExpertId, keepPreviousAsCoExecutor = false, expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var colleagueDetails = await GetJsonAsync(colleague, $"api/staff/expert/appeals/{created.AppealId}");
        Assert.Equal("CoExecutor", colleagueDetails.GetProperty("role").GetString());
        Assert.Contains(privateNote, colleagueDetails.GetRawText(), StringComparison.Ordinal);

        var responsibleLease = Guid.NewGuid();
        var colleagueLease = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await PostWithCsrfAsync(responsible,
            $"api/staff/expert/appeals/{created.AppealId}/composer/acquire", new { leaseId = responsibleLease })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostWithCsrfAsync(colleague,
            $"api/staff/expert/appeals/{created.AppealId}/composer/acquire", new { leaseId = colleagueLease })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await PostWithCsrfAsync(colleague,
            $"api/staff/expert/appeals/{created.AppealId}/notes",
            new { clientNoteId = Guid.NewGuid(), body = "Конфликтующая запись", leaseId = colleagueLease })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostWithCsrfAsync(responsible,
            $"api/staff/expert/appeals/{created.AppealId}/notes",
            new { clientNoteId = Guid.NewGuid(), body = "Запись владельца редактора", leaseId = responsibleLease })).StatusCode);

        var operatorDetails = await GetJsonAsync(operatorClient,
            $"api/staff/operator/collaboration/requests/{requestId}");
        var coexecutor = operatorDetails.GetProperty("participants").EnumerateArray()
            .Single(item => item.GetProperty("role").GetString() == "CoExecutor");
        var removed = await PostWithCsrfAsync(operatorClient,
            $"api/staff/operator/collaboration/appeals/{created.AppealId}/participants/{coexecutor.GetProperty("id").GetGuid()}/remove",
            new { reason = "Совместная консультация завершена", expectedAppealVersion = operatorDetails.GetProperty("appeal").GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await colleague.GetAsync(
            $"api/staff/expert/appeals/{created.AppealId}", TestContext.Current.CancellationToken)).StatusCode);

        var transferRequest = await PostWithCsrfAsync(responsible,
            $"api/staff/expert/appeals/{created.AppealId}/workflow-requests",
            new { clientRequestId = Guid.NewGuid(), type = "Transfer", reason = "Нужна передача специалисту другого рабочего профиля" });
        Assert.Equal(HttpStatusCode.Created, transferRequest.StatusCode);
        var transfer = await ReadJsonAsync(transferRequest);
        var transferred = await PostWithCsrfAsync(operatorClient,
            $"api/staff/operator/collaboration/requests/{transfer.GetProperty("id").GetGuid()}/approve",
            new { expertId = ExpertId, keepPreviousAsCoExecutor = false, expectedVersion = transfer.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await responsible.GetAsync(
            $"api/staff/expert/appeals/{created.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostWithCsrfAsync(responsible,
            $"api/staff/expert/appeals/{created.AppealId}/questions",
            new { clientMessageId = Guid.NewGuid(), body = "Старый специалист не должен отправить сообщение", expectedVersion = 999 })).StatusCode);

        var newResponsible = await GetJsonAsync(colleague, $"api/staff/expert/appeals/{created.AppealId}");
        Assert.Equal("Responsible", newResponsible.GetProperty("role").GetString());
        Assert.True(newResponsible.GetProperty("assignmentHistory").GetArrayLength() >= 5);
        var publicStatus = await applicant.PostAsJsonAsync("api/public/appeals/status",
            new { trackNumber = created.TrackNumber }, TestContext.Current.CancellationToken);
        var publicJson = await publicStatus.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Эксперт демо", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Эксперт по медиации", publicJson, StringComparison.Ordinal);
    }

    private static async Task<CreatedAppeal> CreateAssignedAppealAsync(HttpClient applicant, HttpClient operatorClient)
    {
        var response = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(), applicantType = "Student", submissionPath = "Category",
            categoryId = ConflictCategoryId, narrative = "Нужна совместная помощь двух специалистов.",
            answers = new Dictionary<string, string>()
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreatedAppeal>(TestContext.Current.CancellationToken))!;
        await LoginAsync(operatorClient, "operator", "Operator!2026");
        var detail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        var assign = await PostWithCsrfAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}/assign", new
        {
            expertId = MediatorId, expectedVersion = detail.GetProperty("version").GetInt32(), allowOverCapacity = true,
            overrideReason = "Изоляция интеграционного теста совместной работы"
        });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        return created;
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password) =>
        Assert.Equal(HttpStatusCode.OK, (await PostWithCsrfAsync(client, "api/staff/auth/login", new { userName, password })).StatusCode);

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string route, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>("api/staff/auth/csrf", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = new CookieContainer(), UseCookies = true
    }) { BaseAddress = BaseAddress };

    private static void SkipWhenIntegrationTargetIsMissing()
    {
        if (BaseAddress is null) Assert.Skip("Set OTKLIK_INTEGRATION_BASE_URL to run live API integration tests.");
    }

    private static Uri? ReadBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable("OTKLIK_INTEGRATION_BASE_URL");
        return Uri.TryCreate(value, UriKind.Absolute, out var address) ? address : null;
    }

    private sealed record CsrfPayload(string Token);
    private sealed record CreatedAppeal(Guid AppealId, string TrackNumber);
}
