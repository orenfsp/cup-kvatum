namespace Otklik.Domain.Appeals;

public sealed class AppealResultFeedback
{
    public Guid Id { get; set; }
    public Guid ClientFeedbackId { get; set; }
    public Guid AppealId { get; set; }
    public Appeal? Appeal { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
