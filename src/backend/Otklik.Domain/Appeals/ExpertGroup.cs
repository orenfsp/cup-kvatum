namespace Otklik.Domain.Appeals;

public sealed class ExpertGroup
{
    public static readonly Guid SupportId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid MediationId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    public Guid Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public int ActiveAppealLimit { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UnixEpoch;
}
