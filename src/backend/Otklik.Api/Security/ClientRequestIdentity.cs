using System.Net;

namespace Otklik.Api.Security;

internal static class ClientRequestIdentity
{
    public static string Address(HttpContext context)
    {
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var remoteAddress = context.Connection.RemoteIpAddress;
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var trustsPrivateProxy = configuration.GetValue<bool>("Security:TrustForwardedForFromPrivateNetworks");
        if ((environment.IsDevelopment() || (trustsPrivateProxy && IsPrivateOrLoopback(remoteAddress)))
            && IPAddress.TryParse(forwarded, out var forwardedAddress))
        {
            return forwardedAddress.ToString();
        }

        return remoteAddress?.ToString() ?? "unknown";
    }

    private static bool IsPrivateOrLoopback(IPAddress? address)
    {
        if (address is null || IPAddress.IsLoopback(address)) return true;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
    }
}
