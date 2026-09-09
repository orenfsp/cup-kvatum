namespace Otklik.Application.Security;

public interface ITrackNumberService
{
    string Generate();

    byte[] Hash(string normalizedTrackNumber);

    bool TryNormalize(string? value, out string normalizedTrackNumber);
}
