namespace Otklik.Domain.Analytics;

public sealed class AnalyticsOutboxMessage
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public int AppealVersion { get; set; }

    public required string EventType { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int PublishAttempts { get; set; }

    public string? LastPublishError { get; set; }
}
