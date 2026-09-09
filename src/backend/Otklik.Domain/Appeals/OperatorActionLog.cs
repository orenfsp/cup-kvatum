namespace Otklik.Domain.Appeals;

public sealed class OperatorActionLog
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public Guid ActorUserId { get; set; }

    public required string Action { get; set; }

    public string? FromValue { get; set; }

    public string? ToValue { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
