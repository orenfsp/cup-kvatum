namespace Otklik.Domain.Appeals;

public sealed class AppealDeviceGrant
{
    public Guid DeviceSessionId { get; set; }

    public AppealDeviceSession DeviceSession { get; set; } = null!;

    public Guid ThreadId { get; set; }

    public AppealThread Thread { get; set; } = null!;

    public DateTimeOffset GrantedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
