using System.Text.Json;
using Otklik.Application.Analytics;
using Xunit;

namespace Otklik.UnitTests;

public sealed class AnalyticsContractTests
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "SchemaVersion", "EventId", "AppealId", "AppealVersion", "OccurredAt", "ApplicantType",
        "CategoryId", "ExpertGroupId", "Status", "Priority", "CreatedAt", "OperatorAcceptedAt",
        "FirstExpertResponseAt", "ClosedAt", "OperatorUserId", "AssignedExpertId", "CrisisFlag", "ReturnCount"
    };

    [Fact]
    public void Event_schema_contains_only_whitelisted_metadata()
    {
        var properties = typeof(AppealAnalyticsEvent).GetProperties().Select(property => property.Name).ToHashSet();
        Assert.Equal(AllowedFields.Order(), properties.Order());
        Assert.DoesNotContain(properties, name => new[]
        {
            "text", "body", "narrative", "note", "contact", "attachment", "filename", "track", "ip", "useragent"
        }.Any(forbidden => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Replayed_event_keeps_one_projection_and_preserves_earliest_timestamps()
    {
        var first = Event(version: 2, status: "Assigned") with
        {
            OperatorAcceptedAt = DateTimeOffset.Parse("2026-09-09T05:02:00Z"),
            OperatorUserId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            AssignedExpertId = Guid.Parse("10000000-0000-0000-0000-000000000002")
        };
        var projection = AppealAnalyticsProjector.Apply(null, first);
        var replayed = AppealAnalyticsProjector.Apply(projection, first);

        Assert.Same(projection, replayed);
        Assert.Equal(first.AppealId, replayed.AppealId);
        Assert.Equal(first.OperatorAcceptedAt, replayed.OperatorAcceptedAt);
        Assert.Equal(2, replayed.LastAppealVersion);

        var later = Event(version: 3, status: "RecommendationReady") with
        {
            FirstExpertResponseAt = DateTimeOffset.Parse("2026-09-09T05:11:00Z"),
            AssignedExpertId = first.AssignedExpertId
        };
        AppealAnalyticsProjector.Apply(projection, later);
        AppealAnalyticsProjector.Apply(projection, first);

        Assert.Equal("RecommendationReady", projection.Status);
        Assert.Equal(first.OperatorAcceptedAt, projection.OperatorAcceptedAt);
        Assert.Equal(later.FirstExpertResponseAt, projection.FirstExpertResponseAt);
        Assert.Equal(3, projection.LastAppealVersion);
    }

    [Fact]
    public void Serialized_event_cannot_carry_private_content()
    {
        const string privateText = "PRIVATE-NARRATIVE-DO-NOT-EXPORT";
        const string privateTrack = "ОТК-PRIV-1234";
        var json = JsonSerializer.Serialize(Event(1, "New"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain(privateText, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateTrack, json, StringComparison.Ordinal);
        Assert.DoesNotContain("track", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("narrative", json, StringComparison.OrdinalIgnoreCase);
    }

    private static AppealAnalyticsEvent Event(int version, string status) => new()
    {
        EventId = Guid.Parse("90000000-0000-0000-0000-000000000001"),
        AppealId = Guid.Parse("90000000-0000-0000-0000-000000000002"),
        AppealVersion = version,
        OccurredAt = DateTimeOffset.Parse("2026-09-09T05:00:00Z").AddMinutes(version),
        ApplicantType = "Student",
        CategoryId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Status = status,
        Priority = "Standard",
        CreatedAt = DateTimeOffset.Parse("2026-09-09T05:00:00Z")
    };
}
