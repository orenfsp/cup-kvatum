namespace Otklik.Domain.Appeals;

public sealed class AppealMessage
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public Guid ClientMessageId { get; set; }

    public AppealMessageAuthor Author { get; set; }

    public Guid? AuthorUserId { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
