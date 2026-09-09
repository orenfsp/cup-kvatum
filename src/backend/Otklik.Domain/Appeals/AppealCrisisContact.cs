namespace Otklik.Domain.Appeals;

public sealed class AppealCrisisContact
{
    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public required string Ciphertext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
