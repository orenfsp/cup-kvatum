namespace Otklik.Domain.Appeals;

public sealed class AppealAttachment
{
    public Guid Id { get; set; }

    public Guid AppealId { get; set; }

    public Appeal Appeal { get; set; } = null!;

    public Guid ClientUploadId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
