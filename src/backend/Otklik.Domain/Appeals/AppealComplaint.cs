namespace Otklik.Domain.Appeals;

public sealed class AppealComplaint
{
    public Guid Id { get; set; }
    public Guid ClientComplaintId { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
