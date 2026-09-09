namespace Otklik.Domain.Appeals;

public sealed class ApplicantOutcomeAction
{
    public Guid Id { get; set; }
    public Guid ClientActionId { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public ApplicantOutcomeType Type { get; set; }
    public AppealReturnReason? ReturnReason { get; set; }
    public string? Details { get; set; }
    public int? ReturnSequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
