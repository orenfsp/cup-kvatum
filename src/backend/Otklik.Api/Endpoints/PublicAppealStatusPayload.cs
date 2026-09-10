using Microsoft.EntityFrameworkCore;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Endpoints;

internal static class PublicAppealStatusPayload
{
    public static IQueryable<AppealThread> WithPublicDetails(this IQueryable<AppealThread> threads) => threads
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.Category)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.StatusHistory)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.Attachments)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.Messages)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.Recommendations)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.OutcomeActions)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.ResultFeedback)
        .Include(thread => thread.Cycles).ThenInclude(cycle => cycle.Complaints);

    public static async Task<object> CreateAsync(
        OtklikDbContext database,
        AppealThread thread,
        CancellationToken cancellationToken)
    {
        var cycles = thread.Cycles.OrderBy(cycle => cycle.Sequence).ToArray();
        var current = cycles.SingleOrDefault(cycle => cycle.Id == thread.CurrentCycleId)
            ?? cycles.LastOrDefault()
            ?? throw new InvalidOperationException("Appeal thread has no cycles.");
        var crisisSupport = current.CrisisFlag
            ? await database.CrisisSupportContacts
                .AsNoTracking()
                .Where(contact => contact.IsActive)
                .OrderBy(contact => contact.SortOrder)
                .Select(contact => new
                {
                    contact.DisplayName,
                    contact.DisplayNumber,
                    contact.DialNumber,
                    contact.Description
                })
                .ToArrayAsync(cancellationToken)
            : [];

        return new
        {
            threadVersion = thread.Version,
            cycleCount = cycles.Length,
            summary = new
            {
                createdAt = thread.CreatedAt,
                lastActivityAt = LastPublicActivity(thread),
                currentCycle = current.Sequence,
                status = current.Status.ToString(),
                statusText = StatusText(current.Status)
            },
            currentCycle = CyclePayload(current),
            publicActivity = current.StatusHistory
                .Select(change => new
                {
                    type = "status",
                    text = StatusText(change.Status),
                    at = change.ChangedAt
                })
                .Concat(current.Messages.Select(message => new
                {
                    type = "message",
                    text = message.Body,
                    at = message.CreatedAt
                }))
                .Concat(current.Recommendations.Select(recommendation => new
                {
                    type = "recommendation",
                    text = recommendation.Body,
                    at = recommendation.CreatedAt
                }))
                .OrderBy(item => item.at),
            recommendations = RecommendationPayload(current),
            cycles = cycles.Select(CyclePayload),
            permissions = new
            {
                canContinue = current.Status == AppealStatus.Closed,
                canReply = current.Status == AppealStatus.NeedsClarification,
                canMarkOutcome = current.Status == AppealStatus.RecommendationReady && current.ReturnCount < 2,
                requiresFinalOperatorDecision = current.Status == AppealStatus.Returned && current.ReturnCount >= 2,
                canLeaveFeedback = current.Status == AppealStatus.Closed
                    && current.OutcomeActions.Any(action => action.Type == ApplicantOutcomeType.Helped)
                    && current.ResultFeedback == null,
                feedbackSubmitted = current.ResultFeedback != null,
                complaintSubmitted = current.Complaints.Any(complaint => complaint.ResolvedAt == null)
            },
            needsImmediateHelp = current.CrisisFlag,
            crisisSupport,

            // Compatibility fields while clients move from a single cycle to a thread.
            status = current.Status.ToString(),
            statusText = StatusText(current.Status),
            current.Version,
            current.ReturnCount,
            createdAt = current.CreatedAt,
            applicantType = current.ApplicantType.ToString(),
            category = current.Category?.DisplayName,
            resolution = current.PublicResolution,
            canReply = current.Status == AppealStatus.NeedsClarification,
            canMarkOutcome = current.Status == AppealStatus.RecommendationReady && current.ReturnCount < 2,
            requiresFinalOperatorDecision = current.Status == AppealStatus.Returned && current.ReturnCount >= 2,
            canLeaveFeedback = current.Status == AppealStatus.Closed
                && current.OutcomeActions.Any(action => action.Type == ApplicantOutcomeType.Helped)
                && current.ResultFeedback == null,
            feedbackSubmitted = current.ResultFeedback != null,
            complaintSubmitted = current.Complaints.Any(complaint => complaint.ResolvedAt == null),
            messages = MessagePayload(current),
            attachments = AttachmentPayload(current),
            timeline = TimelinePayload(current)
        };
    }

    private static object CyclePayload(Appeal cycle) => new
    {
        id = cycle.Id,
        number = cycle.Sequence,
        status = cycle.Status.ToString(),
        statusText = StatusText(cycle.Status),
        cycle.Version,
        cycle.ReturnCount,
        createdAt = cycle.CreatedAt,
        completedAt = cycle.CompletedAt,
        applicantType = cycle.ApplicantType.ToString(),
        category = cycle.Category?.DisplayName,
        narrative = cycle.Narrative,
        resolution = cycle.PublicResolution,
        messages = MessagePayload(cycle),
        recommendations = RecommendationPayload(cycle),
        attachments = AttachmentPayload(cycle),
        timeline = TimelinePayload(cycle)
    };

    private static object MessagePayload(Appeal appeal) => appeal.Messages
        .OrderBy(message => message.CreatedAt)
        .Select(message => new
        {
            message.Id,
            author = message.Author.ToString(),
            authorLabel = message.Author == AppealMessageAuthor.Expert
                ? "Специалист"
                : appeal.ApplicantType == ApplicantType.Student ? "Ты" : "Вы",
            message.Body,
            message.CreatedAt
        });

    private static object RecommendationPayload(Appeal appeal) => appeal.Recommendations
        .OrderBy(recommendation => recommendation.Version)
        .Select(recommendation => new
        {
            recommendation.Id,
            recommendation.Version,
            recommendation.Body,
            recommendation.CreatedAt
        });

    private static object AttachmentPayload(Appeal appeal) => appeal.Attachments
        .OrderBy(attachment => attachment.CreatedAt)
        .Select(attachment => new
        {
            attachment.Id,
            attachment.DisplayName,
            attachment.ContentType,
            attachment.Size,
            attachment.CreatedAt
        });

    private static object TimelinePayload(Appeal appeal) => appeal.StatusHistory
        .OrderBy(change => change.ChangedAt)
        .Select(change => new
        {
            status = change.Status.ToString(),
            text = StatusText(change.Status),
            at = change.ChangedAt
        });

    private static DateTimeOffset LastPublicActivity(AppealThread thread) => thread.Cycles
        .SelectMany(cycle => new[] { cycle.CreatedAt }
            .Concat(cycle.CompletedAt is null ? [] : [cycle.CompletedAt.Value])
            .Concat(cycle.StatusHistory.Select(change => change.ChangedAt))
            .Concat(cycle.Messages.Select(message => message.CreatedAt))
            .Concat(cycle.Recommendations.Select(recommendation => recommendation.CreatedAt))
            .Concat(cycle.Attachments.Select(attachment => attachment.CreatedAt)))
        .DefaultIfEmpty(thread.LastActivityAt)
        .Max();

    internal static string StatusText(AppealStatus status) => status switch
    {
        AppealStatus.New => "Обращение получено",
        AppealStatus.Triaged => "Обращение проверено оператором",
        AppealStatus.Assigned => "Подключён специалист",
        AppealStatus.InProgress => "Специалист работает с обращением",
        AppealStatus.NeedsClarification => "Нужно уточнение",
        AppealStatus.RecommendationReady => "Подготовлена рекомендация",
        AppealStatus.Returned => "Обращение вернулось оператору",
        AppealStatus.Closed => "Обращение закрыто",
        AppealStatus.Rejected => "Работа с обращением завершена",
        _ => "Статус обновлён"
    };
}
