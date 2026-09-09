namespace Otklik.Domain.Appeals;

public sealed class AppealAnswer
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal? Appeal { get; set; }

    public required string QuestionCode { get; set; }

    public required string Value { get; set; }
}
