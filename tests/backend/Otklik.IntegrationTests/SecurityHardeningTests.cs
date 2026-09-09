using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class SecurityHardeningTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid BullyingCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ExpertId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Role_and_object_boundaries_reject_direct_content_access()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var firstApplicant = CreateClient();
        using var secondApplicant = CreateClient();
        using var operatorClient = CreateClient();
        using var mediator = CreateClient();
        using var otherExpert = CreateClient();
        using var administrator = CreateClient();
        using var anonymous = CreateClient();

        await LoginAsync(operatorClient, "operator", "Operator!2026");
        await LoginAsync(mediator, "expert.mediator", "ExpertHelp!2026");
        await LoginAsync(otherExpert, "expert", "ExpertHelp!2026");
        await LoginAsync(administrator, "administrator", "AdminPanel!2026");

        var first = await CreateAppealAsync(firstApplicant, ConflictCategoryId, "Закрытый текст первого обращения");
        var second = await CreateAppealAsync(secondApplicant, BullyingCategoryId, "Закрытый текст второго обращения");
        var firstAttachment = await UploadAsync(firstApplicant, first.TrackNumber, "first.png");
        var secondAttachment = await UploadAsync(secondApplicant, second.TrackNumber, "second.png");

        var operatorView = await operatorClient.GetAsync(
            $"api/staff/operator/queue/{first.AppealId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, operatorView.StatusCode);
        var operatorBody = await operatorView.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("\"notes\"", operatorBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"messages\"", operatorBody, StringComparison.OrdinalIgnoreCase);

        await AssignAsync(operatorClient, first.AppealId, MediatorId);
        await AssignAsync(operatorClient, second.AppealId, ExpertId);

        var firstDetails = await ReadJsonAsync(await mediator.GetAsync(
            $"api/staff/expert/appeals/{first.AppealId}", TestContext.Current.CancellationToken));
        var accepted = await PostWithCsrfAsync(mediator, $"api/staff/expert/appeals/{first.AppealId}/accept",
            new { expectedVersion = firstDetails.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        const string privateNote = "Внутренняя заметка не для оператора или администратора";
        Assert.Equal(HttpStatusCode.Created, (await PostWithCsrfAsync(
            mediator,
            $"api/staff/expert/appeals/{first.AppealId}/notes",
            new { clientNoteId = Guid.NewGuid(), body = privateNote })).StatusCode);

        Assert.DoesNotContain(privateNote, operatorBody, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"api/staff/operator/queue/{first.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync(
            $"api/staff/expert/appeals/{first.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostWithCsrfAsync(operatorClient,
            $"api/staff/expert/appeals/{first.AppealId}/notes",
            new { clientNoteId = Guid.NewGuid(), body = "Оператор не может записать заметку" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await administrator.GetAsync(
            $"api/staff/operator/queue/{first.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await administrator.GetAsync(
            $"api/staff/expert/appeals/{first.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await administrator.GetAsync(
            $"api/staff/operator/queue/{first.AppealId}/attachments/{firstAttachment}",
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostWithCsrfAsync(administrator,
            $"api/staff/operator/crisis/{first.AppealId}/contact/read", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await mediator.GetAsync(
            $"api/staff/expert/appeals/{second.AppealId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mediator.GetAsync(
            $"api/staff/expert/appeals/{second.AppealId}/attachments/{secondAttachment}",
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mediator.GetAsync(
            $"api/staff/expert/appeals/{first.AppealId}/attachments/{secondAttachment}",
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await operatorClient.GetAsync(
            $"api/staff/operator/queue/{first.AppealId}/attachments/{secondAttachment}",
            TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await firstApplicant.PostAsJsonAsync(
            $"api/public/appeals/attachments/{secondAttachment}/download",
            new { trackNumber = first.TrackNumber }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secondApplicant.PostAsJsonAsync(
            $"api/public/appeals/attachments/{firstAttachment}/download",
            new { trackNumber = second.TrackNumber }, TestContext.Current.CancellationToken)).StatusCode);

        var adminConfiguration = await administrator.GetStringAsync(
            "api/staff/administrator/configuration", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Закрытый текст", adminConfiguration, StringComparison.Ordinal);
        Assert.DoesNotContain(privateNote, adminConfiguration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Track_lookup_is_uniform_and_limited_after_five_failures()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = CreateClient();
        var created = await CreateAppealAsync(applicant, ConflictCategoryId, "Проверка ограничения подбора номера");
        using var direct = CreateDirectApiClient(RandomTestAddress());
        using var otherAddress = CreateDirectApiClient(RandomTestAddress());

        var malformed = await direct.PostAsJsonAsync(
            "api/public/appeals/status", new { trackNumber = "не номер" }, TestContext.Current.CancellationToken);
        var unknown = await direct.PostAsJsonAsync(
            "api/public/appeals/status", new { trackNumber = "ОТК-2222-2222" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var malformedBody = await malformed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(malformedBody.Length, unknownBody.Length);
        using var malformedJson = JsonDocument.Parse(malformedBody);
        using var unknownJson = JsonDocument.Parse(unknownBody);
        Assert.Equal(
            malformedJson.RootElement.EnumerateObject().Select(item => item.Name),
            unknownJson.RootElement.EnumerateObject().Select(item => item.Name));
        Assert.Equal(
            malformedJson.RootElement.GetProperty("detail").GetString(),
            unknownJson.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("не номер", malformedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ОТК-2222-2222", unknownBody, StringComparison.OrdinalIgnoreCase);

        using var limited = CreateDirectApiClient(RandomTestAddress());
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await limited.PostAsJsonAsync(
                "api/public/appeals/status",
                new { trackNumber = $"ОТК-2222-22{attempt + 2}2" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, failure.StatusCode);
        }

        var blocked = await limited.PostAsJsonAsync(
            "api/public/appeals/status", new { trackNumber = "ОТК-2222-2282" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
        var blockedBody = await blocked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("ОТК-", blockedBody, StringComparison.OrdinalIgnoreCase);

        var validButBlocked = await limited.PostAsJsonAsync(
            "api/public/appeals/status", new { created.TrackNumber }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, validButBlocked.StatusCode);
        var validElsewhere = await otherAddress.PostAsJsonAsync(
            "api/public/appeals/status", new { created.TrackNumber }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, validElsewhere.StatusCode);
    }

    [Fact]
    public async Task Api_uses_security_headers_same_origin_and_csrf_protection()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/health/live");
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.invalid");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nosniff", Header(response, "X-Content-Type-Options"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DENY", Header(response, "X-Frame-Options"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default-src 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Contains("no-referrer", Header(response, "Referrer-Policy"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, Header(response, "Access-Control-Allow-Origin"));

        var noCsrf = await client.PostAsJsonAsync(
            "api/staff/auth/login",
            new { userName = "operator", password = "Operator!2026" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);

        var csrf = await client.GetAsync("api/staff/auth/csrf", TestContext.Current.CancellationToken);
        var cookies = csrf.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join("\n", values) : string.Empty;
        Assert.Contains("HttpOnly", cookies, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", cookies, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CreatedAppeal> CreateAppealAsync(HttpClient applicant, Guid categoryId, string narrative)
    {
        var response = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(), applicantType = "Student", submissionPath = "Category",
            categoryId, narrative, answers = new Dictionary<string, string>()
        }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedAppeal>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<Guid> UploadAsync(HttpClient applicant, string trackNumber, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(trackNumber), "trackNumber");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "clientUploadId");
        var content = new ByteArrayContent(TinyPng);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", fileName);
        var response = await applicant.PostAsync(
            "api/public/appeals/attachments", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        return payload.GetProperty("id").GetGuid();
    }

    private static async Task AssignAsync(HttpClient operatorClient, Guid appealId, Guid expertId)
    {
        var details = await ReadJsonAsync(await operatorClient.GetAsync(
            $"api/staff/operator/queue/{appealId}", TestContext.Current.CancellationToken));
        var response = await PostWithCsrfAsync(operatorClient, $"api/staff/operator/queue/{appealId}/assign", new
        {
            expertId,
            expectedVersion = details.GetProperty("version").GetInt32(),
            allowOverCapacity = true,
            overrideReason = "Изоляция негативной проверки объектных границ"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName, string password) =>
        Assert.Equal(HttpStatusCode.OK, (await PostWithCsrfAsync(
            client, "api/staff/auth/login", new { userName, password })).StatusCode);

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string route, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>(
            "api/staff/auth/csrf", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : string.Empty;

    private static HttpClient CreateDirectApiClient(string forwardedAddress)
    {
        var address = BaseAddress!.Host is "localhost" or "127.0.0.1"
            ? new Uri("http://localhost:8080/")
            : BaseAddress;
        var client = new HttpClient { BaseAddress = address };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", forwardedAddress);
        return client;
    }

    private static string RandomTestAddress()
    {
        var value = Guid.NewGuid().ToString("N");
        return $"fd00:{value[..4]}:{value[4..8]}:{value[8..12]}:{value[12..16]}::1";
    }

    private static HttpClient CreateClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        CookieContainer = new CookieContainer(),
        UseCookies = true
    }) { BaseAddress = BaseAddress };

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
}
