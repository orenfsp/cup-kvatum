using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Hubs;

public sealed class AppealUpdatesHub(
    OtklikDbContext database,
    ITrackNumberService trackNumbers) : Hub
{
    private static readonly AppealStatus[] ActiveStatuses =
        [AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.NeedsClarification, AppealStatus.RecommendationReady];

    public async Task JoinStaffAppeal(Guid appealId)
    {
        if (Context.User?.IsInRole(StaffRoles.Expert) != true
            || !Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var expertId))
        {
            return;
        }

        var allowed = await database.Appeals.AsNoTracking().AnyAsync(
            appeal => appeal.Id == appealId
                && (appeal.AssignedExpertId == expertId || appeal.ExpertParticipants.Any(participant =>
                    participant.ExpertUserId == expertId && participant.RemovedAt == null))
                && ActiveStatuses.Contains(appeal.Status),
            Context.ConnectionAborted);
        if (!allowed) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(appealId), Context.ConnectionAborted);
    }

    public Task JoinExpertWork()
    {
        if (Context.User?.IsInRole(StaffRoles.Expert) != true
            || !Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var expertId))
        {
            return Task.CompletedTask;
        }

        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            ExpertWorkGroupName(expertId),
            Context.ConnectionAborted);
    }

    public async Task JoinApplicantAppeal(string? trackNumber)
    {
        if (!trackNumbers.TryNormalize(trackNumber, out var normalized))
        {
            throw new HubException("Не удалось открыть обновления обращения.");
        }

        var hash = trackNumbers.Hash(normalized);
        var appealId = await database.AppealThreads.AsNoTracking()
            .Where(thread => thread.TrackHash == hash)
            .Select(thread => thread.CurrentCycleId)
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        if (appealId is null) throw new HubException("Не удалось открыть обновления обращения.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(appealId.Value), Context.ConnectionAborted);
    }

    public static string GroupName(Guid appealId) => $"appeal:{appealId:N}";

    public static string ExpertWorkGroupName(Guid expertId) => $"expert-work:{expertId:N}";
}
