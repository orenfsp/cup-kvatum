namespace Otklik.Domain.Appeals;

public sealed class CrisisContactAccessLog
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public Guid OperatorUserId { get; set; }

    public DateTimeOffset AccessedAt { get; set; }
}
