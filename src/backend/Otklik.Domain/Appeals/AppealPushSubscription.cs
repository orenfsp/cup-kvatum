namespace Otklik.Domain.Appeals;

public sealed class AppealPushSubscription
{
    public Guid Id { get; set; }

    public Guid DeviceSessionId { get; set; }

    public AppealDeviceSession DeviceSession { get; set; } = null!;

    public Guid ThreadId { get; set; }

    public AppealThread Thread { get; set; } = null!;

    public required byte[] EndpointHash { get; set; }

    public required string EndpointCiphertext { get; set; }

    public required string P256dhCiphertext { get; set; }

    public required string AuthCiphertext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastDeliveryAt { get; set; }

    public int FailureCount { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
