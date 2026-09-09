using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class DeviceSessionFlowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();

    [Fact]
    public async Task HttpOnly_device_capability_returns_without_url_secret_and_can_be_revoked()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var device = CreateClient();
        using var otherDevice = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string privateText = "PRIVATE-PWA-CAPABILITY-TEXT";

        var createdResponse = await device.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Student",
            submissionPath = "FreeText",
            categoryId = (Guid?)null,
            narrative = $"Мне нужна помощь. {privateText}",
            answers = new Dictionary<string, string>()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<CreatedAppeal>(cancellationToken);
        Assert.NotNull(created);
        Assert.Equal("/api/public/appeals/status", createdResponse.Headers.Location?.OriginalString);
        var deviceCookie = Assert.Single(createdResponse.Headers.GetValues("Set-Cookie"),
            value => value.Contains("otklik.device", StringComparison.Ordinal));
        Assert.Contains("httponly", deviceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", deviceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.TrackNumber, deviceCookie, StringComparison.Ordinal);

        var returned = await device.GetAsync("api/public/device-session", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal("no-store", returned.Headers.CacheControl?.NoStore is true ? "no-store" : null);
        var returnedJson = await returned.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain(created.TrackNumber, returnedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("trackNumber", returnedJson, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await otherDevice.GetAsync("api/public/device-session", cancellationToken)).StatusCode);

        var pushConfig = await device.GetFromJsonAsync<JsonElement>(
            "api/public/device-session/push-config",
            cancellationToken);
        Assert.False(pushConfig.GetProperty("subscribed").GetBoolean());
        Assert.Equal("В обращении есть обновление", pushConfig.GetProperty("notificationBody").GetString());
        Assert.DoesNotContain("endpoint", pushConfig.ToString(), StringComparison.OrdinalIgnoreCase);

        var subscriptionBody = new
        {
            endpoint = $"https://push.invalid/{Guid.NewGuid():N}",
            keys = new { p256dh = "0123456789abcdef0123456789abcdef", auth = "abcdef0123456789" }
        };
        var withoutCsrf = await device.PostAsJsonAsync(
            "api/public/device-session/push-subscriptions",
            subscriptionBody,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, withoutCsrf.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostWithCsrfAsync(device, "api/public/device-session/push-subscriptions", subscriptionBody)).StatusCode);
        var subscribedConfig = await device.GetFromJsonAsync<JsonElement>(
            "api/public/device-session/push-config",
            cancellationToken);
        Assert.True(subscribedConfig.GetProperty("subscribed").GetBoolean());
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostWithCsrfAsync(device, "api/public/device-session/push-subscriptions/revoke", null)).StatusCode);

        var removeWithoutCsrf = await device.PostAsync(
            "api/public/device-session/remove",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, removeWithoutCsrf.StatusCode);
        var removed = await PostWithCsrfAsync(device, "api/public/device-session/remove", null);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Contains(removed.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("otklik.device", StringComparison.Ordinal)
            && (value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)
                || value.Contains("expires=", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await device.GetAsync("api/public/device-session", cancellationToken)).StatusCode);

        var manualReturn = await device.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber = created.TrackNumber },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, manualReturn.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string route, object? body)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var csrf = await client.GetFromJsonAsync<CsrfPayload>("api/public/device-session/csrf", cancellationToken);
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.SendAsync(request, cancellationToken);
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
        if (BaseAddress is null) Assert.Skip("Set OTKLIK_INTEGRATION_BASE_URL to run live API integration tests.");
    }

    private static Uri? ReadBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable("OTKLIK_INTEGRATION_BASE_URL");
        return Uri.TryCreate(value, UriKind.Absolute, out var address) ? address : null;
    }

    private sealed record CreatedAppeal(string TrackNumber);
    private sealed record CsrfPayload(string Token);
}
