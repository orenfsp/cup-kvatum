namespace Otklik.Domain.Appeals;

public sealed class AppealExpertParticipant
{
    public Guid Id { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public Guid ExpertUserId { get; set; }
    public AppealExpertRole Role { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}
