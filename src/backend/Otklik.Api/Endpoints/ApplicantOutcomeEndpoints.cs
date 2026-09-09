using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Api.Hubs;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;

namespace Otklik.Api.Endpoints;

public static class ApplicantOutcomeEndpoints
{
    public static IEndpointRouteBuilder MapApplicantOutcomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var appeals = endpoints.MapGroup("/api/public/appeals").RequireRateLimiting("public-global");
        appeals.MapPost("/outcomes/helped", HelpedAsync);
        appeals.MapPost("/outcomes/returned", ReturnAsync);
        appeals.MapPost("/feedback", AddFeedbackAsync);
        appeals.MapPost("/complaints", AddComplaintAsync);
        return endpoints;
    }

    private static async Task<IResult> HelpedAsync(
        OutcomeRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        if (request.ClientActionId == Guid.Empty) return Validation("clientActionId", "Обновите страницу и повторите действие.");
        var appeal = await FindAppealAsync(request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (appeal is null) return TrackNotFound();
        var replay = await database.ApplicantOutcomeActions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientActionId == request.ClientActionId, cancellationToken);
        if (replay is not null)
            return replay.AppealId == appeal.Id && replay.Type == ApplicantOutcomeType.Helped
                ? Results.Ok(OutcomePayload(appeal)) : IdempotencyConflict();
        if (appeal.Status != AppealStatus.RecommendationReady) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        var now = DateTimeOffset.UtcNow;
        appeal.Status = AppealStatus.Closed;
        appeal.CompletedAt = now;
        appeal.PublicResolution = "Заявитель отметил, что рекомендация помогла.";
        appeal.Version++;
        database.ApplicantOutcomeActions.Add(new ApplicantOutcomeAction
        {
            Id = Guid.NewGuid(), ClientActionId = request.ClientActionId, AppealId = appeal.Id,
            Type = ApplicantOutcomeType.Helped, CreatedAt = now
        });
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Closed, now));
        await EndParticipantsAsync(database, appeal.Id, now, cancellationToken);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        await NotifyAsync(updates, appeal, "Helped", cancellationToken);
        return Results.Ok(OutcomePayload(appeal));
    }

    private static async Task<IResult> ReturnAsync(
        ReturnRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        if (request.ClientActionId == Guid.Empty
            || !Enum.TryParse<AppealReturnReason>(request.Reason, true, out var reason)
            || !Enum.IsDefined(reason))
            return Validation("reason", "Выберите, чего не хватило в ответе.");
        var details = Normalize(request.Details);
        if (details is not null && details.Length > 2_000) return Validation("details", "Комментарий должен быть короче 2 000 знаков.");
        if (reason == AppealReturnReason.Other && (details?.Length ?? 0) < 5)
            return Validation("details", "Кратко опишите, чего не хватило.");
        var appeal = await FindAppealAsync(request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (appeal is null) return TrackNotFound();
        var replay = await database.ApplicantOutcomeActions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientActionId == request.ClientActionId, cancellationToken);
        if (replay is not null)
            return replay.AppealId == appeal.Id && replay.Type == ApplicantOutcomeType.Returned
                ? Results.Ok(OutcomePayload(appeal)) : IdempotencyConflict();
        if (appeal.Status != AppealStatus.RecommendationReady) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        if (appeal.ReturnCount >= 2)
            return Results.Problem(statusCode: 409, title: "Нужна ручная помощь", detail: "Обращение уже возвращалось дважды. Оператор примет окончательное решение.");

        var now = DateTimeOffset.UtcNow;
        appeal.ReturnCount++;
        appeal.ReturnedAt = now;
        appeal.Status = AppealStatus.Returned;
        appeal.AssignedExpertId = null;
        appeal.AssignedAt = null;
        appeal.Version++;
        database.ApplicantOutcomeActions.Add(new ApplicantOutcomeAction
        {
            Id = Guid.NewGuid(), ClientActionId = request.ClientActionId, AppealId = appeal.Id,
            Type = ApplicantOutcomeType.Returned, ReturnReason = reason, Details = details,
            ReturnSequence = appeal.ReturnCount, CreatedAt = now
        });
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Returned, now));
        await EndParticipantsAsync(database, appeal.Id, now, cancellationToken);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        await NotifyAsync(updates, appeal, "Returned", cancellationToken);
        return Results.Ok(OutcomePayload(appeal));
    }

    private static async Task<IResult> AddFeedbackAsync(
        FeedbackRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        if (request.ClientFeedbackId == Guid.Empty || request.Score is < 1 or > 5)
            return Validation("score", "Выберите оценку от 1 до 5.");
        var comment = Normalize(request.Comment);
        if (comment is not null && comment.Length > 2_000) return Validation("comment", "Комментарий должен быть короче 2 000 знаков.");
        var appeal = await FindAppealAsync(request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (appeal is null) return TrackNotFound();
        var replay = await database.AppealResultFeedbacks.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientFeedbackId == request.ClientFeedbackId || item.AppealId == appeal.Id, cancellationToken);
        if (replay is not null)
            return replay.AppealId == appeal.Id && replay.Score == request.Score && replay.Comment == comment
                ? Results.Ok(FeedbackPayload(replay)) : IdempotencyConflict();
        var helped = await database.ApplicantOutcomeActions.AsNoTracking().AnyAsync(
            item => item.AppealId == appeal.Id && item.Type == ApplicantOutcomeType.Helped, cancellationToken);
        if (appeal.Status != AppealStatus.Closed || !helped) return InvalidState();
        var feedback = new AppealResultFeedback
        {
            Id = Guid.NewGuid(), ClientFeedbackId = request.ClientFeedbackId, AppealId = appeal.Id,
            Score = request.Score, Comment = comment, CreatedAt = DateTimeOffset.UtcNow
        };
        database.AppealResultFeedbacks.Add(feedback);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return IdempotencyConflict(); }
        return Results.Created($"/api/public/appeals/feedback/{feedback.Id}", FeedbackPayload(feedback));
    }

    private static async Task<IResult> AddComplaintAsync(
        ComplaintRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        var body = Normalize(request.Body);
        if (request.ClientComplaintId == Guid.Empty || body is null || body.Length < 10 || body.Length > 2_000)
            return Validation("body", "Опишите проблему: от 10 до 2 000 знаков.");
        var appeal = await FindAppealAsync(request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (appeal is null) return TrackNotFound();
        var replay = await database.AppealComplaints.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientComplaintId == request.ClientComplaintId, cancellationToken);
        if (replay is not null)
            return replay.AppealId == appeal.Id && replay.Body == body ? Results.Ok(ComplaintPayload(replay)) : IdempotencyConflict();
        var hasExpertWork = await database.AppealMessages.AsNoTracking().AnyAsync(
                item => item.AppealId == appeal.Id && item.Author == AppealMessageAuthor.Expert, cancellationToken)
            || await database.AppealRecommendations.AsNoTracking().AnyAsync(item => item.AppealId == appeal.Id, cancellationToken);
        if (!hasExpertWork) return InvalidState();
        if (await database.AppealComplaints.AnyAsync(item => item.AppealId == appeal.Id && item.ResolvedAt == null, cancellationToken))
            return Results.Conflict(new { detail = "Жалоба уже передана оператору." });
        var complaint = new AppealComplaint
        {
            Id = Guid.NewGuid(), ClientComplaintId = request.ClientComplaintId, AppealId = appeal.Id,
            Body = body, CreatedAt = DateTimeOffset.UtcNow
        };
        database.AppealComplaints.Add(complaint);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return IdempotencyConflict(); }
        return Results.Created($"/api/public/appeals/complaints/{complaint.Id}", ComplaintPayload(complaint));
    }

    private static async Task<Appeal?> FindAppealAsync(
        string? trackNumber,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        var appealId = await ApplicantAppealAccess.ResolveAppealIdAsync(
            trackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        return appealId is null
            ? null
            : await database.Appeals.SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
    }

    private static async Task EndParticipantsAsync(OtklikDbContext database, Guid appealId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var participants = await database.AppealExpertParticipants
            .Where(item => item.AppealId == appealId && item.RemovedAt == null).ToListAsync(cancellationToken);
        foreach (var participant in participants) participant.RemovedAt = now;
    }

    private static async Task<bool> SaveAsync(OtklikDbContext database, CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException) { return false; }
    }

    private static Task NotifyAsync(IHubContext<AppealUpdatesHub> updates, Appeal appeal, string change,
        CancellationToken cancellationToken) => updates.Clients.Group(AppealUpdatesHub.GroupName(appeal.Id))
            .SendAsync("AppealChanged", new { appealId = appeal.Id, appeal.Version, eventType = change }, cancellationToken);
    private static AppealStatusChange StatusChange(Guid appealId, AppealStatus status, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), AppealId = appealId, Status = status, Source = "Applicant", ChangedAt = now };
    private static object OutcomePayload(Appeal appeal) => new { appeal.Id, appeal.Version, status = appeal.Status.ToString(), statusText = StatusText(appeal.Status), appeal.ReturnCount };
    private static object FeedbackPayload(AppealResultFeedback item) => new { item.Id, item.Score, item.Comment, item.CreatedAt };
    private static object ComplaintPayload(AppealComplaint item) => new { item.Id, item.CreatedAt };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Validation(string field, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    private static IResult TrackNotFound() => Results.Problem(statusCode: 404, title: "Обращение не найдено", detail: "Проверьте трек-номер. Если номер потерян, его нельзя восстановить — можно оставить новое обращение.");
    private static IResult InvalidState() => Results.Problem(statusCode: 409, title: "Действие сейчас недоступно", detail: "Обновите статус обращения.");
    private static IResult VersionConflict() => Results.Problem(statusCode: 409, title: "Обращение уже изменилось", detail: "Обновите статус перед повторным действием.");
    private static IResult IdempotencyConflict() => Results.Problem(statusCode: 409, title: "Команда уже использована", detail: "Обновите страницу и повторите действие.");
    private static string StatusText(AppealStatus status) => status switch { AppealStatus.Returned => "Обращение вернулось оператору", AppealStatus.Closed => "Обращение закрыто", _ => "Статус обновлен" };

    private sealed record OutcomeRequest(string? TrackNumber, Guid ClientActionId, int ExpectedVersion);
    private sealed record ReturnRequest(string? TrackNumber, Guid ClientActionId, int ExpectedVersion, string? Reason, string? Details);
    private sealed record FeedbackRequest(string? TrackNumber, Guid ClientFeedbackId, int Score, string? Comment);
    private sealed record ComplaintRequest(string? TrackNumber, Guid ClientComplaintId, string? Body);
}
