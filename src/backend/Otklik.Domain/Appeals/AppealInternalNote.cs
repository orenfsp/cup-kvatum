namespace Otklik.Domain.Appeals;

public sealed class AppealInternalNote
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public Guid ClientNoteId { get; set; }

    public Guid AuthorUserId { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
