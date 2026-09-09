using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class OperatorQueueTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly Guid BullyingCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ConflictCategoryId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Queue_is_operator_only()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var anonymous = CreateClient();
        using var expert = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("api/staff/operator/queue", cancellationToken)).StatusCode);
        await LoginAsync(expert, "expert", "ExpertHelp!2026");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await expert.GetAsync("api/staff/operator/queue", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Detail_returns_only_initial_content_and_a_separate_category_suggestion()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var created = await CreateAppealAsync(
            client,
            submissionPath: "FreeText",
            categoryId: null,
            narrative: "Меня постоянно обзывают и прячут мои вещи");
        await LoginAsync(client, "operator", "Operator!2026");

        var response = await client.GetAsync(
            $"api/staff/operator/queue/{created.AppealId}",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            BullyingCategoryId,
            document.RootElement.GetProperty("suggestion").GetProperty("categoryId").GetGuid());
        Assert.DoesNotContain("chat", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("note", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("messageHistory", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stale_operator_cannot_replace_an_atomic_assignment()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var firstOperator = CreateClient();
        using var secondOperator = CreateClient();
        var created = await CreateAppealAsync(
            firstOperator,
            submissionPath: "Category",
            categoryId: ConflictCategoryId,
            narrative: "Нужна помощь с конфликтом");
        await LoginAsync(firstOperator, "operator", "Operator!2026");
        await LoginAsync(secondOperator, "operator", "Operator!2026");

        var initial = await GetDetailsAsync(firstOperator, created.AppealId);
        var triage = await PostWithCsrfAsync(firstOperator, $"api/staff/operator/queue/{created.AppealId}/triage", new
        {
            categoryId = ConflictCategoryId,
            priority = "Urgent",
            expectedVersion = initial.Version,
            reason = "Оператор подтвердил высокий приоритет"
        });
        Assert.Equal(HttpStatusCode.OK, triage.StatusCode);

        var triaged = await GetDetailsAsync(firstOperator, created.AppealId);
        Assert.True(triaged.Routing.Experts.Length >= 2);
        var firstAssignment = await PostWithCsrfAsync(firstOperator, $"api/staff/operator/queue/{created.AppealId}/assign", new
        {
            expertId = triaged.Routing.Experts[0].Id,
            expectedVersion = triaged.Version,
            allowOverCapacity = true,
            overrideReason = "Изоляция проверки конкурентного назначения"
        });
        var staleAssignment = await PostWithCsrfAsync(secondOperator, $"api/staff/operator/queue/{created.AppealId}/assign", new
        {
            expertId = triaged.Routing.Experts[1].Id,
            expectedVersion = triaged.Version,
            allowOverCapacity = true,
            overrideReason = "Изоляция проверки конкурентного назначения"
        });

        Assert.Equal(HttpStatusCode.OK, firstAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleAssignment.StatusCode);
        var publicStatus = await clientPostAsync(firstOperator, "api/public/appeals/status", new
        {
            trackNumber = created.TrackNumber
        });
        var publicJson = await publicStatus.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Assigned", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Эксперт демо", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Эксперт по медиации", publicJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejection_keeps_internal_reason_out_of_public_status()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var created = await CreateAppealAsync(
            client,
            submissionPath: "Category",
            categoryId: ConflictCategoryId,
            narrative: "Содержимое для проверки отклонения");
        await LoginAsync(client, "operator", "Operator!2026");
        var details = await GetDetailsAsync(client, created.AppealId);
        const string internalReason = "Тестовая внутренняя причина спама";

        var rejected = await PostWithCsrfAsync(client, $"api/staff/operator/queue/{created.AppealId}/reject", new
        {
            reasonCode = "Spam",
            internalReason,
            expectedVersion = details.Version
        });
        var status = await clientPostAsync(client, "api/public/appeals/status", new
        {
            trackNumber = created.TrackNumber
        });
        var publicJson = await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Rejected", publicJson, StringComparison.Ordinal);
        Assert.Contains("не содержит запроса о помощи", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(internalReason, publicJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capacity_requires_an_explicit_reasoned_override()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        await LoginAsync(client, "operator", "Operator!2026");

        var probe = await CreateTriagedAppealAsync(client);
        var target = probe.Routing.Experts.OrderBy(expert => expert.ActiveCount).First();
        var current = probe;
        while (target.ActiveCount < target.Limit)
        {
            var assignment = await PostWithCsrfAsync(client, $"api/staff/operator/queue/{current.Id}/assign", new
            {
                expertId = target.Id,
                expectedVersion = current.Version,
                allowOverCapacity = false
            });
            Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
            target = target with { ActiveCount = target.ActiveCount + 1 };
            if (target.ActiveCount < target.Limit)
            {
                current = await CreateTriagedAppealAsync(client);
            }
        }

        var overCapacity = await CreateTriagedAppealAsync(client);
        var blocked = await PostWithCsrfAsync(client, $"api/staff/operator/queue/{overCapacity.Id}/assign", new
        {
            expertId = target.Id,
            expectedVersion = overCapacity.Version,
            allowOverCapacity = false
        });
        var overridden = await PostWithCsrfAsync(client, $"api/staff/operator/queue/{overCapacity.Id}/assign", new
        {
            expertId = target.Id,
            expectedVersion = overCapacity.Version,
            allowOverCapacity = true,
            overrideReason = "Нет другого свободного специалиста для этого обращения"
        });

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains(
            "capacityOverrideRequired",
            await blocked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
    }

    private static async Task<AppealDetails> CreateTriagedAppealAsync(HttpClient client)
    {
        var created = await CreateAppealAsync(
            client,
            submissionPath: "Category",
            categoryId: ConflictCategoryId,
            narrative: "Обращение для проверки нагрузки");
        var details = await GetDetailsAsync(client, created.AppealId);
        var response = await PostWithCsrfAsync(client, $"api/staff/operator/queue/{created.AppealId}/triage", new
        {
            categoryId = ConflictCategoryId,
            priority = "Standard",
            expectedVersion = details.Version
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await GetDetailsAsync(client, created.AppealId);
    }

    private static async Task<CreatedAppeal> CreateAppealAsync(
        HttpClient client,
        string submissionPath,
        Guid? categoryId,
        string narrative)
    {
        var response = await clientPostAsync(client, "api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Parent",
            submissionPath,
            categoryId,
            narrative,
            answers = new Dictionary<string, string>()
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedAppeal>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<AppealDetails> GetDetailsAsync(HttpClient client, Guid appealId) =>
        (await client.GetFromJsonAsync<AppealDetails>(
            $"api/staff/operator/queue/{appealId}",
            TestContext.Current.CancellationToken))!;

    private static async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await PostWithCsrfAsync(client, "api/staff/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client,
        string route,
        object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>(
            "api/staff/auth/csrf",
            TestContext.Current.CancellationToken);
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> clientPostAsync(HttpClient client, string route, object body) =>
        client.PostAsJsonAsync(route, body, TestContext.Current.CancellationToken);

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

    private sealed record CreatedAppeal(Guid AppealId, string TrackNumber, string Status);

    private sealed record AppealDetails(Guid Id, int Version, RoutingDetails Routing);

    private sealed record RoutingDetails(RoutingExpert[] Experts);

    private sealed record RoutingExpert(Guid Id, string DisplayName, int ActiveCount, int Limit, bool AtCapacity);
}
