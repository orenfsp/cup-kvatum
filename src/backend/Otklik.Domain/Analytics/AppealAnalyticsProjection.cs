namespace Otklik.Domain.Analytics;

public sealed class AppealAnalyticsProjection
{
    public Guid AppealId { get; set; }

    public required string ApplicantType { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? ExpertGroupId { get; set; }

    public required string Status { get; set; }

    public required string Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OperatorAcceptedAt { get; set; }

    public DateTimeOffset? FirstExpertResponseAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? OperatorUserId { get; set; }

    public Guid? AssignedExpertId { get; set; }

    public Guid? LastAssignedExpertId { get; set; }

    public bool CrisisFlag { get; set; }

    public int ReturnCount { get; set; }

    public int LastAppealVersion { get; set; }

    public DateTimeOffset LastEventAt { get; set; }
}
