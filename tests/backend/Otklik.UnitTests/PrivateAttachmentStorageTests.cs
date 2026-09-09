using Microsoft.Extensions.Configuration;
using Otklik.Infrastructure.Attachments;
using Xunit;

namespace Otklik.UnitTests;

public sealed class PrivateAttachmentStorageTests
{
    [Theory]
    [InlineData("../outside.png")]
    [InlineData("safe/../../outside.png")]
    [InlineData("C:\\outside.png")]
    public async Task Traversal_and_absolute_storage_keys_are_rejected(string storageKey)
    {
        var root = Path.Combine(Path.GetTempPath(), $"otklik-storage-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:AttachmentsPath"] = root
                })
                .Build();
            var storage = new FileSystemAttachmentStorage(configuration);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.OpenReadAsync(storageKey, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
