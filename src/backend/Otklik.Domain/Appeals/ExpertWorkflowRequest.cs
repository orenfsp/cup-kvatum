namespace Otklik.Domain.Appeals;

public sealed class ExpertWorkflowRequest
{
    public Guid Id { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public ExpertWorkflowRequestType Type { get; set; }
    public required string Reason { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public ExpertWorkflowRequestStatus Status { get; set; } = ExpertWorkflowRequestStatus.Pending;
    public Guid? SelectedExpertUserId { get; set; }
    public AppealPriority? SelectedPriority { get; set; }
    public bool KeepPreviousAsCoExecutor { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int Version { get; set; } = 1;
}
