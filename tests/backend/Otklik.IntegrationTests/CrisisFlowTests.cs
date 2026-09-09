using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class CrisisFlowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task Crisis_intake_with_or_without_contact_is_raised_without_automatic_urgent()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = Client();
        using var operatorClient = Client();
        using var expert = Client();
        using var administrator = Client();
        const string privateContact = "+7 900 555-42-10, можно написать в мессенджер";

        var options = await GetJsonAsync(applicant, "api/public/intake/options");
        Assert.Contains(options.GetProperty("crisisMarkers").EnumerateArray(), marker => marker.GetString() == "хочу умер*");
        var supportNumbers = options.GetProperty("crisisSupport").EnumerateArray()
            .Select(item => item.GetProperty("displayNumber").GetString()).ToArray();
        Assert.Contains("8-800-2000-122", supportNumbers);
        Assert.Contains("124", supportNumbers);

        var withContact = await CreateAppealAsync(
            applicant,
            "Я хлчу умереть и не понимаю, к кому обратиться.",
            privateContact);
        var withoutContact = await CreateAppealAsync(
            applicant,
            "Мне угрожают убить после школы.",
            null);
        var neutral = await CreateAppealAsync(
            applicant,
            "Мне нужна помощь с повторяющимся конфликтом в классе.",
            null);
        var structuredResponse = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Student",
            submissionPath = "FreeText",
            categoryId = (Guid?)null,
            narrative = "Мне трудно описать ситуацию подробнее.",
            answers = new Dictionary<string, string> { ["safety"] = "Мне угрожают убить прямо сейчас." },
            crisisContact = (string?)null
        }, TestContext.Current.CancellationToken);
        structuredResponse.EnsureSuccessStatusCode();
        var structured = (await structuredResponse.Content.ReadFromJsonAsync<CreatedAppeal>(
            TestContext.Current.CancellationToken))!;

        var publicStatus = await StatusAsync(applicant, withContact.TrackNumber);
        Assert.True(publicStatus.GetProperty("needsImmediateHelp").GetBoolean());
        Assert.Equal(2, publicStatus.GetProperty("crisisSupport").GetArrayLength());
        var neutralStatus = await StatusAsync(applicant, neutral.TrackNumber);
        Assert.False(neutralStatus.GetProperty("needsImmediateHelp").GetBoolean());
        Assert.True((await StatusAsync(applicant, structured.TrackNumber))
            .GetProperty("needsImmediateHelp").GetBoolean());

        await LoginAsync(operatorClient, "operator", "Operator!2026");
        var queue = await GetJsonAsync(operatorClient, "api/staff/operator/crisis");
        var items = queue.GetProperty("items").EnumerateArray().ToArray();
        var contactItem = Assert.Single(items, item => item.GetProperty("id").GetGuid() == withContact.AppealId);
        var noContactItem = Assert.Single(items, item => item.GetProperty("id").GetGuid() == withoutContact.AppealId);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == neutral.AppealId);
        Assert.Equal("Standard", contactItem.GetProperty("priority").GetString());
        Assert.True(contactItem.GetProperty("hasContact").GetBoolean());
        Assert.False(noContactItem.GetProperty("hasContact").GetBoolean());
        Assert.DoesNotContain(privateContact, queue.GetRawText(), StringComparison.Ordinal);
        var primaryQueue = await GetJsonAsync(operatorClient, "api/staff/operator/queue");
        var primaryIds = primaryQueue.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToList();
        Assert.True(primaryIds.IndexOf(withContact.AppealId) < primaryIds.IndexOf(neutral.AppealId));

        await LoginAsync(expert, "expert", "ExpertHelp!2026");
        await LoginAsync(administrator, "administrator", "AdminPanel!2026");
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsrfAsync(
            expert,
            $"api/staff/operator/crisis/{withContact.AppealId}/contact/read",
            new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsrfAsync(
            administrator,
            $"api/staff/operator/crisis/{withContact.AppealId}/contact/read",
            new { })).StatusCode);

        var firstRead = await ReadJsonAsync(await PostCsrfAsync(
            operatorClient,
            $"api/staff/operator/crisis/{withContact.AppealId}/contact/read",
            new { }));
        var secondRead = await ReadJsonAsync(await PostCsrfAsync(
            operatorClient,
            $"api/staff/operator/crisis/{withContact.AppealId}/contact/read",
            new { }));
        Assert.Equal(privateContact, firstRead.GetProperty("contact").GetString());
        Assert.Equal(2, secondRead.GetProperty("accessCount").GetInt32());

        var urgent = await PostCsrfAsync(
            operatorClient,
            $"api/staff/operator/crisis/{withContact.AppealId}/urgent",
            new { expectedVersion = contactItem.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, urgent.StatusCode);
        Assert.Equal("Urgent", (await ReadJsonAsync(urgent)).GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Crisis_marker_in_late_applicant_message_raises_an_assigned_appeal()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = Client();
        using var operatorClient = Client();
        using var expert = Client();
        var created = await CreateAppealAsync(
            applicant,
            "Нужен спокойный разговор о конфликте после уроков.",
            null,
            categoryPath: true);

        await LoginAsync(operatorClient, "operator", "Operator!2026");
        var queueDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(
            operatorClient,
            $"api/staff/operator/queue/{created.AppealId}/assign",
            new
            {
                expertId = MediatorId,
                expectedVersion = queueDetail.GetProperty("version").GetInt32(),
                allowOverCapacity = true,
                overrideReason = "Изоляция проверки позднего кризисного маркера"
            })).StatusCode);

        await LoginAsync(expert, "expert.mediator", "ExpertHelp!2026");
        var assigned = await GetJsonAsync(expert, $"api/staff/expert/appeals/{created.AppealId}");
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/accept",
            new { expectedVersion = assigned.GetProperty("version").GetInt32() })).StatusCode);
        var inProgress = await GetJsonAsync(expert, $"api/staff/expert/appeals/{created.AppealId}");
        Assert.Equal(HttpStatusCode.Created, (await PostCsrfAsync(
            expert,
            $"api/staff/expert/appeals/{created.AppealId}/questions",
            new
            {
                clientMessageId = Guid.NewGuid(),
                body = "Вы сейчас в безопасном месте?",
                expectedVersion = inProgress.GetProperty("version").GetInt32()
            })).StatusCode);

        var reply = await applicant.PostAsJsonAsync("api/public/appeals/messages", new
        {
            trackNumber = created.TrackNumber,
            clientMessageId = Guid.NewGuid(),
            body = "Нет, меня сейчас избивают и я не могу уйти.",
            crisisContact = (string?)null
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        var crisisQueue = await GetJsonAsync(operatorClient, "api/staff/operator/crisis");
        var item = Assert.Single(
            crisisQueue.GetProperty("items").EnumerateArray(),
            candidate => candidate.GetProperty("id").GetGuid() == created.AppealId);
        Assert.Equal("Standard", item.GetProperty("priority").GetString());
        Assert.False(item.GetProperty("hasContact").GetBoolean());
        Assert.False(item.GetProperty("canOpenInPrimaryQueue").GetBoolean());
    }

    private static async Task<CreatedAppeal> CreateAppealAsync(
        HttpClient client,
        string narrative,
        string? crisisContact,
        bool categoryPath = false)
    {
        var response = await client.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Student",
            submissionPath = categoryPath ? "Category" : "FreeText",
            categoryId = categoryPath ? ConflictCategoryId : (Guid?)null,
            narrative,
            answers = new Dictionary<string, string>(),
            crisisContact
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedAppeal>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<JsonElement> StatusAsync(HttpClient client, string trackNumber) =>
        await ReadJsonAsync(await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber },
            TestContext.Current.CancellationToken));

    private static async Task LoginAsync(HttpClient client, string userName, string password) =>
        Assert.Equal(HttpStatusCode.OK, (await PostCsrfAsync(
            client,
            "api/staff/auth/login",
            new { userName, password })).StatusCode);

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

    private static HttpClient Client() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        CookieContainer = new CookieContainer(),
        UseCookies = true
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
