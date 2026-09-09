using System.Text.Json;
using Otklik.Application.Appeals;
using Otklik.Infrastructure.Security;
using Xunit;

namespace Otklik.UnitTests;

public sealed class PushNotificationContractTests
{
    [Fact]
    public void Push_payload_is_neutral_and_has_only_safe_fields()
    {
        using var document = JsonDocument.Parse(PushNotificationContract.PayloadJson);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.Equal(["body", "title", "url"], properties.Select(item => item.Name).Order());
        Assert.Equal("Отклик", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("В обращении есть обновление", document.RootElement.GetProperty("body").GetString());
        Assert.Equal("/appeal/status", document.RootElement.GetProperty("url").GetString());

        var payload = PushNotificationContract.PayloadJson;
        Assert.DoesNotContain("track", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("category", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("priority", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expert", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Device_capability_hash_does_not_retain_the_raw_token()
    {
        const string token = "private-capability-token-that-is-never-stored";
        var hash = DeviceCapabilityService.Hash(token);

        Assert.Equal(32, hash.Length);
        Assert.DoesNotContain(System.Text.Encoding.UTF8.GetBytes(token), hash);
    }
}
