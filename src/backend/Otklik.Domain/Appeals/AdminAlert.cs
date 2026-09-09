namespace Otklik.Domain.Appeals;

public sealed class AdminAlert
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public required string Type { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
