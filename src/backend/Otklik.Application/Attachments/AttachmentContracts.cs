namespace Otklik.Application.Attachments;

public interface IAttachmentSanitizer
{
    Task<SanitizedAttachment> SanitizeAsync(
        Stream source,
        long declaredLength,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public interface IPrivateAttachmentStorage
{
    Task<string> StoreAsync(
        byte[] content,
        string extension,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}

public sealed record SanitizedAttachment(byte[] Content, string ContentType, string Extension);

public sealed class AttachmentValidationException(string message) : Exception(message);
