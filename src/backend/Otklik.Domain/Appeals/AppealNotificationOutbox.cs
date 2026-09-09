namespace Otklik.Domain.Appeals;

public sealed class AppealNotificationOutbox
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
