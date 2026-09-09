namespace Otklik.Domain.Appeals;

public sealed class AppealThread
{
    public Guid Id { get; set; }

    public required byte[] TrackHash { get; set; }

    public required string TrackRecoveryCiphertext { get; set; }

    public Guid? CurrentCycleId { get; set; }

    public Appeal? CurrentCycle { get; set; }

    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }

    public List<Appeal> Cycles { get; set; } = [];
}
