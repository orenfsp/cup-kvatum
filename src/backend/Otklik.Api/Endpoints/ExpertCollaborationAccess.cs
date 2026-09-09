using Microsoft.EntityFrameworkCore;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

internal static class ExpertCollaborationAccess
{
    public static IQueryable<Appeal> VisibleTo(this IQueryable<Appeal> appeals, Guid expertId) =>
        appeals.Where(appeal => appeal.AssignedExpertId == expertId
            || appeal.ExpertParticipants.Any(participant =>
                participant.ExpertUserId == expertId && participant.RemovedAt == null));

    public static Task<bool> HasAccessAsync(
        OtklikDbContext database,
        Guid appealId,
        Guid expertId,
        CancellationToken cancellationToken) =>
        database.Appeals.VisibleTo(expertId).AnyAsync(appeal => appeal.Id == appealId, cancellationToken);

    public static async Task<bool> HasValidComposerLeaseAsync(
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        Guid appealId,
        Guid expertId,
        Guid? leaseId,
        CancellationToken cancellationToken)
    {
        var participantCount = await database.AppealExpertParticipants.AsNoTracking()
            .CountAsync(participant => participant.AppealId == appealId && participant.RemovedAt == null, cancellationToken);
        if (participantCount <= 1) return true;
        if (leaseId is null || leaseId == Guid.Empty) return false;

        var current = await redis.GetDatabase().StringGetAsync(ComposerKey(appealId));
        return current.HasValue && current == LeaseValue(expertId, leaseId.Value);
    }

    public static string PresenceKey(Guid appealId) => $"appeal-presence:{appealId:N}";
    public static string ComposerKey(Guid appealId) => $"appeal-composer:{appealId:N}";
    public static string LeaseValue(Guid expertId, Guid leaseId) => $"{expertId:N}:{leaseId:N}";
}
