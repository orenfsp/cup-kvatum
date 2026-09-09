namespace Otklik.Domain.Appeals;

public sealed class AppealDeviceSession
{
    public Guid Id { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public List<AppealDeviceGrant> Grants { get; set; } = [];
}
