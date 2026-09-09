using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Otklik.IntegrationTests;

public sealed partial class PublicAppealTests
{
    private static readonly Uri? BaseAddress = ReadBaseAddress();
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Student_can_submit_free_text_without_optional_answers_and_open_status()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();

        var createdResponse = await client.PostAsJsonAsync(
            "api/public/appeals",
            new
            {
                clientRequestId = requestId,
                applicantType = "Student",
                submissionPath = "FreeText",
                categoryId = (Guid?)null,
                narrative = "Мне мешают учиться и постоянно обзывают после уроков.",
                answers = new Dictionary<string, string>()
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<CreatedAppeal>(cancellationToken);
        Assert.NotNull(created);
        Assert.Matches(TrackPattern(), created.TrackNumber);
        Assert.Equal("New", created.Status);

        var normalizedInput = created.TrackNumber.ToLowerInvariant().Replace('-', '–');
        var statusResponse = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber = normalizedInput },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<AppealStatusPayload>(cancellationToken);
        Assert.NotNull(status);
        Assert.Equal("New", status.Status);
        Assert.Equal("Student", status.ApplicantType);
        Assert.Null(status.Category);
        Assert.Single(status.Timeline);
    }

    [Fact]
    public async Task Adult_can_submit_category_path_and_request_is_idempotent()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await client.GetFromJsonAsync<IntakeOptions>("api/public/intake/options", cancellationToken);
        Assert.NotNull(options);
        var category = Assert.Single(options.Categories, item => item.Code == "conflict");
        var requestId = Guid.NewGuid();
        var request = new
        {
            clientRequestId = requestId,
            applicantType = "Parent",
            submissionPath = "Category",
            categoryId = category.Id,
            narrative = (string?)null,
            answers = new Dictionary<string, string>()
        };

        var firstResponse = await client.PostAsJsonAsync("api/public/appeals", request, cancellationToken);
        var replayResponse = await client.PostAsJsonAsync("api/public/appeals", request, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<CreatedAppeal>(cancellationToken);
        var replay = await replayResponse.Content.ReadFromJsonAsync<CreatedAppeal>(cancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(first.TrackNumber, replay.TrackNumber);

        var statusResponse = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber = first.TrackNumber },
            cancellationToken);
        var status = await statusResponse.Content.ReadFromJsonAsync<AppealStatusPayload>(cancellationToken);
        Assert.NotNull(status);
        Assert.Equal("Родитель", options.ApplicantTypes.Single(item => item.Value == "Parent").Label);
        Assert.Equal("Конфликт", status.Category);
    }

    [Fact]
    public async Task Invalid_and_unknown_tracks_return_the_same_public_problem()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var malformedResponse = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber = "не номер" },
            cancellationToken);
        var unknownResponse = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber = "ОТК-2222-2222" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, malformedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        var malformed = await ReadPublicProblemAsync(malformedResponse, cancellationToken);
        var unknown = await ReadPublicProblemAsync(unknownResponse, cancellationToken);
        Assert.Equal(malformed, unknown);
    }

    [Fact]
    public async Task Closed_thread_continues_idempotently_under_the_same_track_with_full_history()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string trackNumber = "ОТК-FNSH-89AB";
        var beforeResponse = await client.PostAsJsonAsync(
            "api/public/appeals/open",
            new { trackNumber },
            cancellationToken);
        beforeResponse.EnsureSuccessStatusCode();
        var before = await beforeResponse.Content.ReadFromJsonAsync<AppealThreadPayload>(cancellationToken);
        Assert.NotNull(before);

        var command = new
        {
            trackNumber,
            clientContinuationId = Guid.Parse("91000000-0000-0000-0000-000000000001"),
            expectedThreadVersion = before.ThreadVersion,
            body = "Это продолжение той же ситуации для проверки сохранения полной истории.",
            crisisContact = (string?)null
        };
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("api/public/appeals/continue", command, cancellationToken),
            client.PostAsJsonAsync("api/public/appeals/continue", command, cancellationToken));
        var firstResponse = responses[0];
        var replayResponse = responses[1];
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<ContinuedAppeal>(cancellationToken);
        var replay = await replayResponse.Content.ReadFromJsonAsync<ContinuedAppeal>(cancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(first.CycleNumber, replay.CycleNumber);

        var afterResponse = await client.PostAsJsonAsync(
            "api/public/appeals/open",
            new { trackNumber },
            cancellationToken);
        var after = await afterResponse.Content.ReadFromJsonAsync<AppealThreadPayload>(cancellationToken);
        Assert.NotNull(after);
        Assert.True(after.CycleCount >= 2);
        Assert.Contains(after.Cycles, cycle => cycle.Number == 1);
        Assert.Contains(after.Cycles, cycle => cycle.Number == first.CycleNumber);
        Assert.Equal(first.CycleNumber, after.CurrentCycle.Number);
    }

    [Fact]
    public async Task Attachment_is_sanitized_idempotent_private_and_downloaded_as_attachment()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var trackNumber = await CreateStudentAppealAsync(client, cancellationToken);
        var uploadId = Guid.NewGuid();

        var firstResponse = await UploadAsync(
            client,
            trackNumber,
            uploadId,
            "evidence.png",
            "image/png",
            TinyPng,
            cancellationToken);
        var replayResponse = await UploadAsync(
            client,
            trackNumber,
            uploadId,
            "evidence.png",
            "image/png",
            TinyPng,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<AttachmentPayload>(cancellationToken);
        var replay = await replayResponse.Content.ReadFromJsonAsync<AttachmentPayload>(cancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("Изображение 1.png", first.DisplayName);

        using var otherDevice = CreateClient();
        var withoutSecret = await otherDevice.PostAsJsonAsync(
            $"api/public/appeals/attachments/{first.Id}/download",
            new { trackNumber = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, withoutSecret.StatusCode);

        var download = await client.PostAsJsonAsync(
            $"api/public/appeals/attachments/{first.Id}/download",
            new { trackNumber },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await download.Content.ReadAsByteArrayAsync(cancellationToken));

        var statusResponse = await client.PostAsJsonAsync(
            "api/public/appeals/status",
            new { trackNumber },
            cancellationToken);
        var status = await statusResponse.Content.ReadFromJsonAsync<AppealStatusWithAttachments>(cancellationToken);
        Assert.NotNull(status);
        Assert.Single(status.Attachments);
    }

    [Fact]
    public async Task Disguised_and_sixth_attachments_are_rejected()
    {
        SkipWhenIntegrationTargetIsMissing();
        using var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var trackNumber = await CreateStudentAppealAsync(client, cancellationToken);

        var disguised = await UploadAsync(
            client,
            trackNumber,
            Guid.NewGuid(),
            "disguised.jpg",
            "image/jpeg",
            TinyPng,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, disguised.StatusCode);

        for (var index = 0; index < 5; index++)
        {
            var accepted = await UploadAsync(
                client,
                trackNumber,
                Guid.NewGuid(),
                $"evidence-{index}.png",
                "image/png",
                TinyPng,
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        var sixth = await UploadAsync(
            client,
            trackNumber,
            Guid.NewGuid(),
            "sixth.png",
            "image/png",
            TinyPng,
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, sixth.StatusCode);
        var problem = await sixth.Content.ReadFromJsonAsync<ValidationProblemPayload>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Contains("пяти", problem.Errors["attachment"].Single(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> CreateStudentAppealAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "api/public/appeals",
            new
            {
                clientRequestId = Guid.NewGuid(),
                applicantType = "Student",
                submissionPath = "FreeText",
                categoryId = (Guid?)null,
                narrative = "Тестовое обращение для безопасной проверки файла.",
                answers = new Dictionary<string, string>()
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedAppeal>(cancellationToken);
        return Assert.IsType<string>(created?.TrackNumber);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string trackNumber,
        Guid clientUploadId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(trackNumber), "trackNumber");
        form.Add(new StringContent(clientUploadId.ToString()), "clientUploadId");
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return await client.PostAsync("api/public/appeals/attachments", form, cancellationToken);
    }

    private static async Task<PublicProblem> ReadPublicProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = document.RootElement;
        return new PublicProblem(
            root.GetProperty("status").GetInt32(),
            root.GetProperty("title").GetString(),
            root.GetProperty("detail").GetString());
    }

    private static HttpClient CreateClient() => new() { BaseAddress = BaseAddress };

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

    [GeneratedRegex("^ОТК-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{4}-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{4}$")]
    private static partial Regex TrackPattern();

    private sealed record CreatedAppeal(string TrackNumber, string Status);

    private sealed record IntakeOptions(ApplicantTypeOption[] ApplicantTypes, CategoryOption[] Categories);

    private sealed record ApplicantTypeOption(string Value, string Label);

    private sealed record CategoryOption(Guid Id, string Code);

    private sealed record AppealStatusPayload(
        string Status,
        string ApplicantType,
        string? Category,
        StatusTimelineItem[] Timeline);

    private sealed record StatusTimelineItem(string Status);

    private sealed record AttachmentPayload(Guid Id, string DisplayName);

    private sealed record AppealStatusWithAttachments(AttachmentPayload[] Attachments);

    private sealed record ContinuedAppeal(int ThreadVersion, int CycleNumber);

    private sealed record AppealThreadPayload(
        int ThreadVersion,
        int CycleCount,
        AppealCyclePayload CurrentCycle,
        AppealCyclePayload[] Cycles);

    private sealed record AppealCyclePayload(int Number, string Status);

    private sealed record ValidationProblemPayload(Dictionary<string, string[]> Errors);

    private sealed record PublicProblem(int Status, string? Title, string? Detail);
}
