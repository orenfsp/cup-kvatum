using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Otklik.Application.Security;

namespace Otklik.Infrastructure.Security;

public sealed class TrackNumberService : ITrackNumberService
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Prefix = "ОТК";
    private readonly byte[] hashKey;

    public TrackNumberService(IConfiguration configuration)
    {
        var configuredKey = configuration["Security:TrackHashKey"]
            ?? throw new InvalidOperationException("Security:TrackHashKey is required.");

        try
        {
            hashKey = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Security:TrackHashKey must be Base64.", exception);
        }

        if (hashKey.Length < 32)
        {
            throw new InvalidOperationException("Security:TrackHashKey must contain at least 32 bytes.");
        }
    }

    public string Generate()
    {
        Span<char> secret = stackalloc char[8];
        for (var index = 0; index < secret.Length; index++)
        {
            secret[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{Prefix}-{secret[..4]}-{secret[4..]}";
    }

    public byte[] Hash(string normalizedTrackNumber) => HMACSHA256.HashData(
        hashKey,
        Encoding.UTF8.GetBytes(normalizedTrackNumber));

    public bool TryNormalize(string? value, out string normalizedTrackNumber)
    {
        normalizedTrackNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var canonical = new string(value
            .Trim()
            .ToUpperInvariant()
            .Select(character => character switch
            {
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
                _ => character
            })
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray());

        var compact = canonical.Replace("-", string.Empty, StringComparison.Ordinal);
        if (compact.Length != 11 || !compact.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var secret = compact[3..];
        if (secret.Any(character => !Alphabet.Contains(character, StringComparison.Ordinal)))
        {
            return false;
        }

        normalizedTrackNumber = $"{Prefix}-{secret[..4]}-{secret[4..]}";
        return true;
    }
}
