namespace Otklik.Application.Analytics;

public sealed record AppealAnalyticsEvent
{
    public const int CurrentSchemaVersion = 1;
    public const string Type = "appeal.analytics.snapshot.v1";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Guid EventId { get; init; }

    public Guid AppealId { get; init; }

    public int AppealVersion { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public required string ApplicantType { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? ExpertGroupId { get; init; }

    public required string Status { get; init; }

    public required string Priority { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? OperatorAcceptedAt { get; init; }

    public DateTimeOffset? FirstExpertResponseAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public Guid? OperatorUserId { get; init; }

    public Guid? AssignedExpertId { get; init; }

    public bool CrisisFlag { get; init; }

    public int ReturnCount { get; init; }
}
