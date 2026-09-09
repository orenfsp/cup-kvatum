namespace Otklik.Domain.Analytics;

public sealed class ProcessedAnalyticsEvent
{
    public Guid EventId { get; set; }

    public Guid AppealId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
