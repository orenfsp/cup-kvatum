using Microsoft.EntityFrameworkCore;
using Otklik.Application.Security;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;

namespace Otklik.Api.Endpoints;

internal static class ApplicantAppealAccess
{
    public static async Task<Guid?> ResolveThreadIdAsync(
        string? trackNumber,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(trackNumber))
        {
            if (!trackNumbers.TryNormalize(trackNumber, out var normalized)) return null;
            var hash = trackNumbers.Hash(normalized);
            return await database.AppealThreads
                .Where(thread => thread.TrackHash == hash)
                .Select(thread => (Guid?)thread.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return (await deviceCapabilities.ResolveAsync(context, cancellationToken))?.ThreadId;
    }

    public static async Task<Guid?> ResolveAppealIdAsync(
        string? trackNumber,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        var threadId = await ResolveThreadIdAsync(
            trackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (threadId is null) return null;
        return await database.AppealThreads
            .Where(thread => thread.Id == threadId)
            .Select(thread => thread.CurrentCycleId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
