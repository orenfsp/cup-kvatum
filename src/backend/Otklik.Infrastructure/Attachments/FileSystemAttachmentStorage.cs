using Microsoft.Extensions.Configuration;
using Otklik.Application.Attachments;

namespace Otklik.Infrastructure.Attachments;

public sealed class FileSystemAttachmentStorage : IPrivateAttachmentStorage
{
    private readonly string _rootPath;

    public FileSystemAttachmentStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:AttachmentsPath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "attachments")
            : configuredPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> StoreAsync(
        byte[] content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var normalizedExtension = extension.ToLowerInvariant();
        if (normalizedExtension is not ".png" and not ".jpg" and not ".pdf")
        {
            throw new InvalidOperationException("Unsupported attachment extension.");
        }

        var now = DateTimeOffset.UtcNow;
        var directory = Path.Combine(now.ToString("yyyy"), now.ToString("MM"));
        var storageKey = Path.Combine(directory, $"{Guid.NewGuid():N}{normalizedExtension}")
            .Replace(Path.DirectorySeparatorChar, '/');
        var destination = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var stream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(content, cancellationToken);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            Resolve(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || Path.IsPathRooted(storageKey)
            || storageKey.Contains('\\'))
        {
            throw new InvalidOperationException("Invalid attachment storage key.");
        }

        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid attachment storage key.");
        }

        return resolved;
    }
}
