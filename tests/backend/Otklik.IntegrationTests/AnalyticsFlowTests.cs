using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed class AnalyticsFlowTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly string[] ExportColumns =
    [
        "appeal_id", "applicant_type", "category", "status", "priority", "created_at_utc",
        "operator_accepted_at_utc", "first_expert_response_at_utc", "closed_at_utc", "return_count", "crisis_flag"
    ];

    [Fact]
    public async Task C8_projection_role_scope_and_one_time_exports_are_anonymous()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var anonymous = Client();
        using var applicant = Client();
        using var administrator = Client();
        using var operatorClient = Client();
        using var expert = Client();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            "api/staff/analytics/dashboard",
            cancellationToken)).StatusCode);
        await LoginAsync(administrator, "administrator", Password("OTKLIK_ADMINISTRATOR_PASSWORD", "AdminPanel!2026"));
        await LoginAsync(operatorClient, "operator", Password("OTKLIK_OPERATOR_PASSWORD", "Operator!2026"));
        await LoginAsync(expert, "expert", Password("OTKLIK_EXPERT_PASSWORD", "ExpertHelp!2026"));

        var period = PeriodQuery();
        var before = await GetJsonAsync(administrator, $"api/staff/analytics/dashboard?{period}");
        const string privateNarrative = "PRIVATE-C8-NARRATIVE must never enter analytics";
        var createdResponse = await applicant.PostAsJsonAsync("api/public/appeals", new
        {
            clientRequestId = Guid.NewGuid(),
            applicantType = "Student",
            submissionPath = "FreeText",
            categoryId = (Guid?)null,
            narrative = privateNarrative,
            answers = new Dictionary<string, string>()
        }, cancellationToken);
        createdResponse.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(createdResponse);
        var privateTrack = created.GetProperty("trackNumber").GetString()!;

        JsonElement after = default;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            after = await GetJsonAsync(administrator, $"api/staff/analytics/dashboard?{period}");
            if (after.GetProperty("total").GetInt32() >= before.GetProperty("total").GetInt32() + 1) break;
            await Task.Delay(500, cancellationToken);
        }
        Assert.True(after.GetProperty("total").GetInt32() >= before.GetProperty("total").GetInt32() + 1);
        Assert.Equal("Administrator", after.GetProperty("scope").GetProperty("role").GetString());
        Assert.Contains("Календарное время", after.GetProperty("definitions").GetProperty("operatorAccepted").GetString());
        Assert.DoesNotContain(privateNarrative, after.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(privateTrack, after.GetRawText(), StringComparison.Ordinal);

        var operatorDashboard = await GetJsonAsync(operatorClient, $"api/staff/analytics/dashboard?{period}");
        var expertDashboard = await GetJsonAsync(expert, $"api/staff/analytics/dashboard?{period}");
        Assert.Equal("Operator", operatorDashboard.GetProperty("scope").GetProperty("role").GetString());
        Assert.Equal("Expert", expertDashboard.GetProperty("scope").GetProperty("role").GetString());
        Assert.True(operatorDashboard.GetProperty("total").GetInt32() <= after.GetProperty("total").GetInt32());
        Assert.True(expertDashboard.GetProperty("total").GetInt32() <= after.GetProperty("total").GetInt32());

        var csvExport = await CreateExportAsync(administrator, "csv");
        var csvDownload = await PostCsrfAsync(
            administrator,
            csvExport.GetProperty("downloadUrl").GetString()!.TrimStart('/'),
            body: null);
        csvDownload.EnsureSuccessStatusCode();
        var csv = Encoding.UTF8.GetString(await csvDownload.Content.ReadAsByteArrayAsync(cancellationToken)).TrimStart('\uFEFF');
        Assert.Equal(string.Join(',', ExportColumns), csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('\r'));
        Assert.DoesNotContain(privateNarrative, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(privateTrack, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("narrative", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("track", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Gone, (await PostCsrfAsync(
            administrator,
            csvExport.GetProperty("downloadUrl").GetString()!.TrimStart('/'),
            body: null)).StatusCode);

        var xlsxExport = await CreateExportAsync(administrator, "xlsx");
        var xlsxDownload = await PostCsrfAsync(
            administrator,
            xlsxExport.GetProperty("downloadUrl").GetString()!.TrimStart('/'),
            body: null);
        xlsxDownload.EnsureSuccessStatusCode();
        var xlsxBytes = await xlsxDownload.Content.ReadAsByteArrayAsync(cancellationToken);
        using var workbook = new XLWorkbook(new MemoryStream(xlsxBytes));
        var sheet = workbook.Worksheet("Аналитика");
        var headers = sheet.Row(1).Cells(1, ExportColumns.Length).Select(cell => cell.GetString()).ToArray();
        Assert.Equal(ExportColumns, headers);
        var workbookText = string.Join('|', sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.DoesNotContain(privateNarrative, workbookText, StringComparison.Ordinal);
        Assert.DoesNotContain(privateTrack, workbookText, StringComparison.Ordinal);

        var exportId = xlsxExport.GetProperty("id").GetGuid();
        var audit = await GetJsonAsync(administrator, "api/staff/administrator/audit?action=AnalyticsExportDownloaded");
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), item => item.GetProperty("targetId").GetGuid() == exportId);
        Assert.DoesNotContain(privateNarrative, audit.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(privateTrack, audit.GetRawText(), StringComparison.Ordinal);
    }

    private static async Task<JsonElement> CreateExportAsync(HttpClient client, string format)
    {
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        return await ReadJsonAsync(await PostCsrfAsync(client, "api/staff/analytics/exports", new
        {
            format,
            from = to.AddDays(-365),
            to
        }));
    }

    private static string PeriodQuery()
    {
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        return $"from={Uri.EscapeDataString(to.AddDays(-365).ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
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

    private static async Task<HttpResponseMessage> PostCsrfAsync(HttpClient client, string route, object? body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfPayload>("api/staff/auth/csrf", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpClient Client() => new(new HttpClientHandler { UseCookies = true })
    {
        BaseAddress = BaseAddress,
        Timeout = TimeSpan.FromSeconds(45)
    };

    private static string Password(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) ?? fallback;

    private static Uri? ReadBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable("OTKLIK_INTEGRATION_BASE_URL");
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static void SkipWhenIntegrationTargetIsMissing()
    {
        if (BaseAddress is null) Assert.Skip("Set OTKLIK_INTEGRATION_BASE_URL to run live API integration tests.");
    }

    private sealed record CsrfPayload(string Token);
}
