using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class AdminFlowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();

    [Fact]
    public async Task C7_configuration_accounts_stuck_intervention_and_audit_are_safe()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var applicant = Client();
        using var administrator = Client();
        using var operatorClient = Client();
        using var createdExpertClient = Client();
        using var blockedLoginClient = Client();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var categoryCode = $"social-{suffix}";
        var groupCode = $"support-{suffix}";
        var expertUserName = $"expert.{suffix}";
        const string expertPassword = "SupportHelp!2026";
        const string privateNarrative = "Мне угрожают убить, но я хочу сохранить анонимность в тестовом обращении.";

        await LoginAsync(administrator, "administrator", "AdminPanel!2026");
        await LoginAsync(operatorClient, "operator", "Operator!2026");

        var category = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            "api/staff/administrator/categories",
            new { code = categoryCode, displayName = "Поддержка при изоляции", sortOrder = 35 }));
        var categoryId = category.GetProperty("id").GetGuid();
        var updatedCategory = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/categories/{categoryId}/update",
            new
            {
                code = categoryCode,
                displayName = "Социальная изоляция",
                sortOrder = 36,
                expectedVersion = category.GetProperty("version").GetInt32()
            }));
        Assert.Equal(2, updatedCategory.GetProperty("version").GetInt32());

        var expert = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            "api/staff/administrator/users",
            new
            {
                userName = expertUserName,
                displayName = "Эксперт тестовой группы",
                password = expertPassword,
                role = "Expert",
                isAvailable = true
            }));
        var expertId = expert.GetProperty("id").GetGuid();
        Assert.Equal("Expert", expert.GetProperty("role").GetString());

        var group = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            "api/staff/administrator/groups",
            new
            {
                code = groupCode,
                displayName = "Профильная поддержка",
                activeAppealLimit = 3,
                expertIds = new[] { expertId }
            }));
        var groupId = group.GetProperty("id").GetGuid();
        var updatedGroup = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/groups/{groupId}/update",
            new
            {
                code = groupCode,
                displayName = "Профильная поддержка",
                activeAppealLimit = 4,
                expertIds = new[] { expertId },
                expectedVersion = group.GetProperty("version").GetInt32()
            }));
        Assert.Equal(4, updatedGroup.GetProperty("activeAppealLimit").GetInt32());

        var rule = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            "api/staff/administrator/rules",
            new { categoryId, expertGroupId = groupId, expectedVersion = (int?)null }));
        var ruleId = rule.GetProperty("id").GetGuid();
        Assert.Equal(1, rule.GetProperty("version").GetInt32());

        var publicOptions = await GetJsonAsync(applicant, "api/public/intake/options");
        Assert.Contains(
            publicOptions.GetProperty("categories").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == categoryId);

        var createdResponse = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Student",
            submissionPath = "Category",
            categoryId,
            narrative = privateNarrative,
            answers = new Dictionary<string, string>(),
            crisisContact = (string?)null
        }, TestContext.Current.CancellationToken);
        createdResponse.EnsureSuccessStatusCode();
        var created = (await createdResponse.Content.ReadFromJsonAsync<CreatedAppeal>(
            TestContext.Current.CancellationToken))!;

        var operatorDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Equal("Профильная поддержка", operatorDetail.GetProperty("routing").GetProperty("groupName").GetString());
        Assert.Contains(
            operatorDetail.GetProperty("routing").GetProperty("experts").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == expertId);
        Assert.Equal(1, operatorDetail.GetProperty("appliedRouting").GetProperty("ruleVersion").GetInt32());
        Assert.Equal(groupId, operatorDetail.GetProperty("appliedRouting").GetProperty("groupId").GetGuid());

        var unavailable = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/users/{expertId}/update",
            new { displayName = "Эксперт тестовой группы", role = "Expert", isAvailable = false }));
        Assert.False(unavailable.GetProperty("isAvailable").GetBoolean());
        operatorDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Empty(operatorDetail.GetProperty("routing").GetProperty("experts").EnumerateArray());
        await EnsureSuccessAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/users/{expertId}/update",
            new { displayName = "Эксперт тестовой группы", role = "Expert", isAvailable = true }));

        var defaultGroupId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var changedRule = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            "api/staff/administrator/rules",
            new { categoryId, expertGroupId = defaultGroupId, expectedVersion = 1 }));
        Assert.Equal(2, changedRule.GetProperty("version").GetInt32());
        operatorDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Equal("Психологическая поддержка", operatorDetail.GetProperty("routing").GetProperty("groupName").GetString());
        Assert.Equal(1, operatorDetail.GetProperty("appliedRouting").GetProperty("ruleVersion").GetInt32());
        Assert.Equal(groupId, operatorDetail.GetProperty("appliedRouting").GetProperty("groupId").GetGuid());

        await LoginAsync(createdExpertClient, expertUserName, expertPassword);
        Assert.Equal(HttpStatusCode.OK, (await createdExpertClient.GetAsync(
            "api/staff/expert/overview",
            TestContext.Current.CancellationToken)).StatusCode);
        await EnsureSuccessAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/users/{expertId}/block",
            new { reason = "Проверка немедленного отзыва служебной сессии" }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await createdExpertClient.GetAsync(
            "api/staff/expert/overview",
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostCsrfAsync(
            blockedLoginClient,
            "api/staff/auth/login",
            new { userName = expertUserName, password = expertPassword })).StatusCode);
        await EnsureSuccessAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/users/{expertId}/restore",
            new { reason = "Тест отзыва сессии завершен" }));

        var stuck = await GetJsonAsync(administrator, "api/staff/administrator/stuck");
        var stuckItem = Assert.Single(
            stuck.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == created.AppealId);
        Assert.DoesNotContain(privateNarrative, stuck.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("narrative", stuck.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var missingReason = await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/stuck/{created.AppealId}/intervene",
            new
            {
                status = "New",
                priority = "Low",
                assignedExpertId = (Guid?)null,
                expectedVersion = stuckItem.GetProperty("version").GetInt32(),
                reason = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        var intervention = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/stuck/{created.AppealId}/intervene",
            new
            {
                status = "New",
                priority = "Low",
                assignedExpertId = (Guid?)null,
                expectedVersion = stuckItem.GetProperty("version").GetInt32(),
                reason = "Возврат в операторскую очередь после проверки зависания"
            }));
        var interventionAuditEventId = intervention.GetProperty("auditEventId").GetGuid();

        var deactivatedCategory = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/categories/{categoryId}/deactivate",
            new { expectedVersion = updatedCategory.GetProperty("version").GetInt32() }));
        Assert.False(deactivatedCategory.GetProperty("isActive").GetBoolean());
        publicOptions = await GetJsonAsync(applicant, "api/public/intake/options");
        Assert.DoesNotContain(
            publicOptions.GetProperty("categories").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == categoryId);
        operatorDetail = await GetJsonAsync(operatorClient, $"api/staff/operator/queue/{created.AppealId}");
        Assert.Equal("Социальная изоляция", operatorDetail.GetProperty("category").GetString());

        var deactivatedRule = await ReadJsonAsync(await PostCsrfAsync(
            administrator,
            $"api/staff/administrator/rules/{ruleId}/deactivate",
            new { expectedVersion = changedRule.GetProperty("version").GetInt32() }));
        Assert.False(deactivatedRule.GetProperty("isActive").GetBoolean());

        var audit = await GetJsonAsync(administrator, "api/staff/administrator/audit?action=StuckAppealIntervened");
        var auditItem = Assert.Single(
            audit.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("targetId").GetGuid() == created.AppealId);
        Assert.Equal(interventionAuditEventId, auditItem.GetProperty("id").GetGuid());
        var auditDetail = await GetJsonAsync(
            administrator,
            $"api/staff/administrator/audit/{auditItem.GetProperty("id").GetGuid()}");
        Assert.Equal("Low", auditDetail.GetProperty("after").GetProperty("priority").GetString());
        Assert.Contains("проверки зависания", auditDetail.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(privateNarrative, auditDetail.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("body", auditDetail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact", auditDetail.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var adminConfigurationRaw = (await administrator.GetStringAsync(
            "api/staff/administrator/configuration",
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(privateNarrative, adminConfigurationRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("narrative", adminConfigurationRaw, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync(
            "api/staff/administrator/configuration",
            TestContext.Current.CancellationToken)).StatusCode);
    }

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

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        await Task.CompletedTask;
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
