namespace Otklik.Domain.Appeals;

public sealed class AppealStatusChange
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal? Appeal { get; set; }

    public AppealStatus Status { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public required string Source { get; set; }
}
