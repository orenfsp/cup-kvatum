using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class StaffAccessTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();

    [Fact]
    public async Task Anonymous_staff_endpoint_returns_unauthorized()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("api/staff/auth/me", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("operator", "OTKLIK_OPERATOR_PASSWORD", "Operator", "operator", "expert")]
    [InlineData("expert", "OTKLIK_EXPERT_PASSWORD", "Expert", "expert", "administrator")]
    [InlineData("administrator", "OTKLIK_ADMINISTRATOR_PASSWORD", "Administrator", "administrator", "operator")]
    public async Task Role_can_use_own_route_but_not_foreign_route(
        string userName,
        string passwordVariable,
        string expectedRole,
        string ownRoute,
        string foreignRoute)
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var password = Environment.GetEnvironmentVariable(passwordVariable) ?? DevelopmentPassword(userName);
        var cancellationToken = TestContext.Current.CancellationToken;

        var login = await PostWithCsrfAsync(
            client,
            "api/staff/auth/login",
            new { userName, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(
            login.Headers.GetValues("Set-Cookie"),
            value => value.Contains("otklik.staff", StringComparison.Ordinal)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var session = await login.Content.ReadFromJsonAsync<StaffSessionPayload>(cancellationToken);
        Assert.NotNull(session);
        Assert.Contains(expectedRole, session.Roles);

        var own = await client.GetAsync($"api/staff/{ownRoute}/overview", cancellationToken);
        var foreign = await client.GetAsync($"api/staff/{foreignRoute}/overview", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        var logout = await PostWithCsrfAsync(client, "api/staff/auth/logout", body: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("api/staff/auth/me", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Login_without_csrf_is_rejected()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "api/staff/auth/login",
            new { userName = "operator", password = "Operator!2026" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client,
        string route,
        object? body)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var csrf = await client.GetFromJsonAsync<CsrfPayload>("api/staff/auth/csrf", cancellationToken);
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

    private static string DevelopmentPassword(string userName) => userName switch
    {
        "operator" => "Operator!2026",
        "expert" => "ExpertHelp!2026",
        "administrator" => "AdminPanel!2026",
        _ => throw new ArgumentOutOfRangeException(nameof(userName))
    };

    private sealed record CsrfPayload(string Token);

    private sealed record StaffSessionPayload(string[] Roles);
}
