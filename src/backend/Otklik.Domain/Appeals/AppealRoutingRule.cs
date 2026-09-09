namespace Otklik.Domain.Appeals;

public sealed class AppealRoutingRule
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public AppealCategory Category { get; set; } = null!;

    public Guid ExpertGroupId { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;

    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UnixEpoch;
}
