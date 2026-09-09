namespace Otklik.Domain.Appeals;

public sealed class AppealAssignmentEvent
{
    public Guid Id { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public required string EventType { get; set; }
    public Guid ExpertUserId { get; set; }
    public AppealExpertRole Role { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? WorkflowRequestId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
