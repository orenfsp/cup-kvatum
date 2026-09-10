using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Hubs;

internal static class ExpertWorkUpdateNotifier
{
    public static async Task NotifyAsync(
        IHubContext<AppealUpdatesHub> updates,
        OtklikDbContext database,
        Guid appealId,
        string change,
        CancellationToken cancellationToken)
    {
        var expertIds = await database.AppealExpertParticipants
            .AsNoTracking()
            .Where(participant => participant.AppealId == appealId)
            .Select(participant => participant.ExpertUserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var assignedExpertId = await database.Appeals
            .AsNoTracking()
            .Where(appeal => appeal.Id == appealId)
            .Select(appeal => appeal.AssignedExpertId)
            .SingleOrDefaultAsync(cancellationToken);
        if (assignedExpertId is Guid assignedId && !expertIds.Contains(assignedId)) expertIds.Add(assignedId);

        var payload = new { appealId, change };
        var notifications = new List<Task>
        {
            updates.Clients.Group(AppealUpdatesHub.GroupName(appealId))
                .SendAsync("appealUpdated", payload, cancellationToken)
        };
        notifications.AddRange(expertIds.Select(expertId =>
            updates.Clients.Group(AppealUpdatesHub.ExpertWorkGroupName(expertId))
                .SendAsync("expertWorkUpdated", payload, cancellationToken)));
        await Task.WhenAll(notifications);
    }
}
