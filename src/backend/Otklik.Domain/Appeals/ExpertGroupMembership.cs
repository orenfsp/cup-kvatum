namespace Otklik.Domain.Appeals;

public sealed class ExpertGroupMembership
{
    public Guid ExpertGroupId { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;

    public Guid ExpertUserId { get; set; }
}
