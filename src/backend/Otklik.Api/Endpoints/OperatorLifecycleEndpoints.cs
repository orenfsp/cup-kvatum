using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Api.Hubs;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Endpoints;

public static class OperatorLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapOperatorLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/staff/operator/lifecycle")
            .RequireAuthorization(StaffPolicies.OperatorOnly);
        group.MapGet("", GetWorkAsync);
        group.MapGet("/returns/{appealId:guid}", GetReturnAsync);
        group.MapPost("/returns/{appealId:guid}/reassign", ReassignAsync);
        group.MapPost("/returns/{appealId:guid}/close", CloseAsync);
        group.MapPost("/complaints/{complaintId:guid}/resolve", ResolveComplaintAsync);
        return endpoints;
    }

    private static async Task<IResult> GetWorkAsync(OtklikDbContext database, CancellationToken cancellationToken)
    {
        var returns = await database.Appeals.AsNoTracking()
            .Where(appeal => appeal.Status == AppealStatus.Returned)
            .Include(appeal => appeal.Category)
            .OrderByDescending(appeal => appeal.ReturnCount >= 2)
            .ThenBy(appeal => appeal.ReturnedAt)
            .Select(appeal => new
            {
                appeal.Id, appeal.Version, appeal.ReturnCount, appeal.ReturnedAt,
                applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
                category = appeal.Category != null ? appeal.Category.DisplayName : "Без категории",
                priority = appeal.Priority.ToString(), priorityText = PriorityText(appeal.Priority),
                requiresFinalDecision = appeal.ReturnCount >= 2
            }).ToListAsync(cancellationToken);
        var complaints = await (
            from complaint in database.AppealComplaints.AsNoTracking()
            join appeal in database.Appeals.AsNoTracking() on complaint.AppealId equals appeal.Id
            join category in database.AppealCategories.AsNoTracking() on appeal.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            where complaint.ResolvedAt == null
            orderby complaint.CreatedAt
            select new
            {
                complaint.Id, complaint.AppealId, complaint.Body, complaint.CreatedAt,
                applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
                category = category != null ? category.DisplayName : "Без категории"
            }).ToListAsync(cancellationToken);
        var crisisReviews = await database.AdminAlerts.AsNoTracking()
            .Where(alert => alert.Type == "CrisisNoResponseReview" && alert.ResolvedAt == null)
            .OrderBy(alert => alert.CreatedAt)
            .Select(alert => new { alert.Id, alert.AppealId, alert.CreatedAt })
            .ToListAsync(cancellationToken);
        return Results.Ok(new { returns, complaints, crisisReviews });
    }

    private static async Task<IResult> GetReturnAsync(Guid appealId, OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var appeal = await database.Appeals.AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.OutcomeActions)
            .Include(item => item.Complaints)
            .SingleOrDefaultAsync(item => item.Id == appealId && item.Status == AppealStatus.Returned, cancellationToken);
        if (appeal is null) return Results.NotFound();
        var candidates = await EligibleExpertsAsync(database, appeal.CategoryId, cancellationToken);
        return Results.Ok(new
        {
            appeal.Id, appeal.Version, appeal.ReturnCount, appeal.ReturnedAt,
            applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
            category = appeal.Category?.DisplayName ?? "Без категории",
            priority = appeal.Priority.ToString(), priorityText = PriorityText(appeal.Priority),
            requiresFinalDecision = appeal.ReturnCount >= 2,
            returns = appeal.OutcomeActions.Where(action => action.Type == ApplicantOutcomeType.Returned)
                .OrderBy(action => action.ReturnSequence).Select(action => new
                {
                    action.Id, action.ReturnSequence, reason = action.ReturnReason.ToString(),
                    reasonText = ReturnReasonText(action.ReturnReason), action.Details, action.CreatedAt
                }),
            complaints = appeal.Complaints.Where(complaint => complaint.ResolvedAt == null)
                .Select(complaint => new { complaint.Id, complaint.Body, complaint.CreatedAt }),
            candidates
        });
    }

    private static async Task<IResult> ReassignAsync(Guid appealId, ReassignRequest request,
        ClaimsPrincipal principal, HttpContext context, IAntiforgery antiforgery, OtklikDbContext database,
        IHubContext<AppealUpdatesHub> updates, CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var appeal = await database.Appeals.SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (appeal.Status != AppealStatus.Returned) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        if (appeal.ReturnCount >= 2)
            return Results.Problem(statusCode: 409, title: "Нужно окончательное решение", detail: "После второго возврата обращение нельзя снова назначить. Завершите его с понятным объяснением.");
        var candidates = await EligibleExpertsAsync(database, appeal.CategoryId, cancellationToken);
        if (!candidates.Any(item => item.Id == request.ExpertId)) return Validation("expertId", "Выберите специалиста подходящего профиля.");
        var now = DateTimeOffset.UtcNow;
        appeal.AssignedExpertId = request.ExpertId;
        appeal.AssignedAt = now;
        appeal.Status = AppealStatus.Assigned;
        appeal.Version++;
        database.AppealExpertParticipants.Add(new AppealExpertParticipant
        {
            Id = Guid.NewGuid(), AppealId = appeal.Id, ExpertUserId = request.ExpertId,
            Role = AppealExpertRole.Responsible, AddedByUserId = operatorId, AddedAt = now
        });
        database.AppealAssignmentEvents.Add(new AppealAssignmentEvent
        {
            Id = Guid.NewGuid(), AppealId = appeal.Id, EventType = "Reassigned", ExpertUserId = request.ExpertId,
            Role = AppealExpertRole.Responsible, ActorUserId = operatorId, OccurredAt = now
        });
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Assigned, "Operator", now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        await NotifyAsync(updates, appeal, "Reassigned", cancellationToken);
        return Results.Ok(new { appeal.Id, appeal.Version, status = appeal.Status.ToString() });
    }

    private static async Task<IResult> CloseAsync(Guid appealId, CloseRequest request,
        ClaimsPrincipal principal, HttpContext context, IAntiforgery antiforgery, OtklikDbContext database,
        IHubContext<AppealUpdatesHub> updates, CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var explanation = Normalize(request.Explanation);
        if (explanation is null || explanation.Length < 10 || explanation.Length > 4_000)
            return Validation("explanation", "Напишите бережное объяснение от 10 до 4 000 знаков.");
        var appeal = await database.Appeals.SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (appeal.Status != AppealStatus.Returned) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        var now = DateTimeOffset.UtcNow;
        appeal.Status = AppealStatus.Closed;
        appeal.CompletedAt = now;
        appeal.PublicResolution = explanation;
        appeal.Version++;
        database.OperatorActionLogs.Add(new OperatorActionLog
        {
            Id = Guid.NewGuid(), AppealId = appeal.Id, ActorUserId = operatorId, Action = "ClosedAfterReturn",
            ToValue = "PublicResolution", OccurredAt = now
        });
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Closed, "Operator", now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        await NotifyAsync(updates, appeal, "Closed", cancellationToken);
        return Results.Ok(new { appeal.Id, appeal.Version, status = appeal.Status.ToString() });
    }

    private static async Task<IResult> ResolveComplaintAsync(Guid complaintId, VersionRequest request,
        ClaimsPrincipal principal, HttpContext context, IAntiforgery antiforgery, OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var complaint = await database.AppealComplaints.SingleOrDefaultAsync(item => item.Id == complaintId, cancellationToken);
        if (complaint is null) return Results.NotFound();
        if (complaint.ResolvedAt is not null) return Results.NoContent();
        complaint.ResolvedByUserId = operatorId;
        complaint.ResolvedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<List<ExpertCandidate>> EligibleExpertsAsync(OtklikDbContext database, Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var groupId = categoryId is null ? null : await database.AppealRoutingRules.AsNoTracking()
            .Where(rule => rule.CategoryId == categoryId).Select(rule => (Guid?)rule.ExpertGroupId)
            .SingleOrDefaultAsync(cancellationToken);
        var roleId = await database.Roles.AsNoTracking().Where(role => role.Name == StaffRoles.Expert)
            .Select(role => role.Id).SingleAsync(cancellationToken);
        return await (from user in database.Users.AsNoTracking()
            join userRole in database.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            where userRole.RoleId == roleId && user.IsActive
                && (groupId == null || database.ExpertGroupMemberships.Any(member =>
                    member.ExpertGroupId == groupId && member.ExpertUserId == user.Id))
            orderby user.DisplayName
            select new ExpertCandidate(user.Id, user.DisplayName)).ToListAsync(cancellationToken);
    }

    private static async Task<bool> SaveAsync(OtklikDbContext database, CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }
    private static Task NotifyAsync(IHubContext<AppealUpdatesHub> updates, Appeal appeal, string change, CancellationToken token) =>
        updates.Clients.Group(AppealUpdatesHub.GroupName(appeal.Id)).SendAsync("AppealChanged", new { appealId = appeal.Id, appeal.Version, eventType = change }, token);
    private static AppealStatusChange StatusChange(Guid appealId, AppealStatus status, string source, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), AppealId = appealId, Status = status, Source = source, ChangedAt = now };
    private static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return null; }
        catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "Запрос устарел", detail: "Обновите страницу и повторите действие."); }
    }
    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Validation(string field, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    private static IResult VersionConflict() => Results.Problem(statusCode: 409, title: "Обращение уже изменилось", detail: "Обновите карточку перед повторным действием.");
    private static IResult InvalidState() => Results.Problem(statusCode: 409, title: "Действие больше недоступно", detail: "Обновите список возвратов.");
    private static string ApplicantTypeText(ApplicantType type) => type switch { ApplicantType.Student => "Школьник", ApplicantType.Parent => "Родитель", ApplicantType.Teacher => "Педагог", _ => "Заявитель" };
    private static string PriorityText(AppealPriority priority) => priority switch { AppealPriority.Urgent => "Срочный", AppealPriority.Low => "Низкий", _ => "Обычный" };
    private static string ReturnReasonText(AppealReturnReason? reason) => reason switch { AppealReturnReason.NotClear => "Ответ непонятен", AppealReturnReason.NotSuitable => "Совет не подходит", AppealReturnReason.NeedMoreHelp => "Нужна дополнительная помощь", AppealReturnReason.SituationChanged => "Ситуация изменилась", _ => "Другая причина" };

    private sealed record ExpertCandidate(Guid Id, string DisplayName);
    private sealed record ReassignRequest(Guid ExpertId, int ExpectedVersion);
    private sealed record CloseRequest(string? Explanation, int ExpectedVersion);
    private sealed record VersionRequest(int ExpectedVersion);
}
