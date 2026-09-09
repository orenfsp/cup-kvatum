namespace Otklik.Domain.Appeals;

public sealed class AdministrativeAuditEvent
{
    public Guid Id { get; set; }

    public Guid ActorUserId { get; set; }

    public required string ActorDisplayName { get; set; }

    public required string Action { get; set; }

    public required string TargetType { get; set; }

    public Guid TargetId { get; set; }

    public string? BeforeMetadataJson { get; set; }

    public string? AfterMetadataJson { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
