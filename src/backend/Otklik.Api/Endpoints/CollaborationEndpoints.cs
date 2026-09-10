using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Api.Hubs;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

public static class CollaborationEndpoints
{
    private const int PresenceSeconds = 45;
    private const int LeaseSeconds = 25;

    public static IEndpointRouteBuilder MapCollaborationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var expert = endpoints.MapGroup("/api/staff/expert/appeals")
            .RequireAuthorization(StaffPolicies.ExpertOnly);
        expert.MapPost("/{appealId:guid}/workflow-requests", CreateWorkflowRequestAsync);
        expert.MapGet("/{appealId:guid}/presence", GetPresenceAsync);
        expert.MapPost("/{appealId:guid}/presence", HeartbeatPresenceAsync);
        expert.MapPost("/{appealId:guid}/composer/acquire", AcquireComposerAsync);
        expert.MapPost("/{appealId:guid}/composer/release", ReleaseComposerAsync);

        var operatorGroup = endpoints.MapGroup("/api/staff/operator/collaboration")
            .RequireAuthorization(StaffPolicies.OperatorOnly);
        operatorGroup.MapGet("/requests", GetWorkflowRequestsAsync);
        operatorGroup.MapGet("/requests/{requestId:guid}", GetWorkflowRequestAsync);
        operatorGroup.MapPost("/requests/{requestId:guid}/approve", ApproveWorkflowRequestAsync);
        operatorGroup.MapPost("/requests/{requestId:guid}/reject", RejectWorkflowRequestAsync);
        operatorGroup.MapPost("/appeals/{appealId:guid}/participants/{participantId:guid}/remove", RemoveCoExecutorAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateWorkflowRequestAsync(
        Guid appealId,
        CreateRequest body,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        if (body.ClientRequestId == Guid.Empty
            || !Enum.TryParse<ExpertWorkflowRequestType>(body.Type, true, out var type)
            || !Enum.IsDefined(type))
        {
            return Validation("type", "Выберите тип запроса.");
        }
        var reason = Normalize(body.Reason);
        if (reason is null || reason.Length < 10 || reason.Length > 1_000)
        {
            return Validation("reason", "Опишите причину запроса: от 10 до 1 000 знаков.");
        }
        if (!await ExpertCollaborationAccess.HasAccessAsync(database, appealId, expertId, cancellationToken))
        {
            return Results.NotFound();
        }

        var replay = await database.ExpertWorkflowRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientRequestId == body.ClientRequestId, cancellationToken);
        if (replay is not null)
        {
            return replay.AppealId == appealId && replay.Type == type && replay.Reason == reason
                ? Results.Ok(RequestPayload(replay))
                : IdempotencyConflict();
        }
        var hasPending = await database.ExpertWorkflowRequests.AnyAsync(
            item => item.AppealId == appealId && item.Type == type && item.Status == ExpertWorkflowRequestStatus.Pending,
            cancellationToken);
        if (hasPending) return Results.Conflict(new { detail = "Запрос этого типа уже ожидает решения оператора." });

        var request = new ExpertWorkflowRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = body.ClientRequestId,
            AppealId = appealId,
            Type = type,
            Reason = reason,
            RequestedByUserId = expertId,
            RequestedAt = DateTimeOffset.UtcNow
        };
        database.ExpertWorkflowRequests.Add(request);
        await database.SaveChangesAsync(cancellationToken);
        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates, database, appealId, "WorkflowRequested", cancellationToken);
        return Results.Created($"/api/staff/expert/appeals/{appealId}/workflow-requests/{request.Id}", RequestPayload(request));
    }

    private static async Task<IResult> GetWorkflowRequestsAsync(
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from request in database.ExpertWorkflowRequests.AsNoTracking()
            join appeal in database.Appeals.AsNoTracking() on request.AppealId equals appeal.Id
            join category in database.AppealCategories.AsNoTracking() on appeal.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            join requester in database.Users.AsNoTracking() on request.RequestedByUserId equals requester.Id
            orderby request.Status == ExpertWorkflowRequestStatus.Pending ? 0 : 1, request.RequestedAt descending
            select new
            {
                request.Id,
                request.Version,
                type = request.Type.ToString(),
                typeText = RequestTypeText(request.Type),
                status = request.Status.ToString(),
                statusText = RequestStatusText(request.Status),
                request.Reason,
                request.RequestedAt,
                requestedBy = requester.DisplayName,
                appealId = appeal.Id,
                applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
                category = category != null ? category.DisplayName : "Без категории",
                priority = appeal.Priority.ToString(),
                priorityText = PriorityText(appeal.Priority)
            }).Take(100).ToListAsync(cancellationToken);
        return Results.Ok(new { total = rows.Count, items = rows });
    }

    private static async Task<IResult> GetWorkflowRequestAsync(
        Guid requestId,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var request = await database.ExpertWorkflowRequests.AsNoTracking()
            .Include(item => item.Appeal)!.ThenInclude(appeal => appeal!.Category)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request?.Appeal is null) return Results.NotFound();

        var requester = await database.Users.AsNoTracking().Where(user => user.Id == request.RequestedByUserId)
            .Select(user => user.DisplayName).SingleAsync(cancellationToken);
        var candidates = await EligibleExpertsAsync(database, request.Appeal, cancellationToken);
        var participants = await ActiveParticipantsAsync(database, request.AppealId, cancellationToken);
        return Results.Ok(new
        {
            request.Id,
            request.Version,
            type = request.Type.ToString(),
            typeText = RequestTypeText(request.Type),
            status = request.Status.ToString(),
            statusText = RequestStatusText(request.Status),
            request.Reason,
            request.RequestedAt,
            requestedBy = requester,
            appeal = new
            {
                request.Appeal.Id,
                request.Appeal.Version,
                applicantTypeText = ApplicantTypeText(request.Appeal.ApplicantType),
                category = request.Appeal.Category?.DisplayName ?? "Без категории",
                priority = request.Appeal.Priority.ToString(),
                priorityText = PriorityText(request.Appeal.Priority),
                status = request.Appeal.Status.ToString()
            },
            candidates,
            participants
        });
    }

    private static async Task<IResult> ApproveWorkflowRequestAsync(
        Guid requestId,
        DecisionRequest body,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await database.ExpertWorkflowRequests.Include(item => item.Appeal)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request?.Appeal is null) return Results.NotFound();
        if (request.Status != ExpertWorkflowRequestStatus.Pending) return InvalidState();
        if (request.Version != body.ExpectedVersion) return VersionConflict();
        var appeal = request.Appeal;
        var now = DateTimeOffset.UtcNow;

        if (request.Type == ExpertWorkflowRequestType.PriorityReview)
        {
            if (!Enum.TryParse<AppealPriority>(body.Priority, true, out var priority) || !Enum.IsDefined(priority))
                return Validation("priority", "Выберите новый приоритет.");
            appeal.Priority = priority;
            request.SelectedPriority = priority;
        }
        else
        {
            if (body.ExpertId is null || body.ExpertId == Guid.Empty) return Validation("expertId", "Выберите специалиста.");
            var eligible = (await EligibleExpertsAsync(database, appeal, cancellationToken)).Any(item => item.Id == body.ExpertId);
            if (!eligible) return Validation("expertId", "Этот специалист не входит в подходящую профильную группу.");
            if (request.Type == ExpertWorkflowRequestType.CoExecutor)
            {
                if (appeal.AssignedExpertId == body.ExpertId) return Validation("expertId", "Ответственный уже работает с обращением.");
                if (await database.AppealExpertParticipants.AnyAsync(item => item.AppealId == appeal.Id
                    && item.ExpertUserId == body.ExpertId && item.RemovedAt == null, cancellationToken))
                    return Validation("expertId", "Специалист уже участвует в обращении.");
                AddParticipant(database, appeal.Id, body.ExpertId.Value, AppealExpertRole.CoExecutor, operatorId, request.Id, now);
            }
            else
            {
                if (appeal.AssignedExpertId == body.ExpertId) return Validation("expertId", "Выберите другого ответственного.");
                var previousId = appeal.AssignedExpertId;
                var active = await database.AppealExpertParticipants
                    .Where(item => item.AppealId == appeal.Id && item.RemovedAt == null).ToListAsync(cancellationToken);
                foreach (var participant in active.Where(item => item.Role == AppealExpertRole.Responsible || item.ExpertUserId == body.ExpertId))
                {
                    EndParticipant(database, participant, operatorId, request.Id, now);
                }
                if (previousId is not null && body.KeepPreviousAsCoExecutor)
                    AddParticipant(database, appeal.Id, previousId.Value, AppealExpertRole.CoExecutor, operatorId, request.Id, now);
                AddParticipant(database, appeal.Id, body.ExpertId.Value, AppealExpertRole.Responsible, operatorId, request.Id, now);
                appeal.AssignedExpertId = body.ExpertId;
                appeal.AssignedAt = now;
                request.KeepPreviousAsCoExecutor = body.KeepPreviousAsCoExecutor;
            }
            request.SelectedExpertUserId = body.ExpertId;
        }

        request.Status = ExpertWorkflowRequestStatus.Approved;
        request.DecidedByUserId = operatorId;
        request.DecisionReason = Normalize(body.DecisionReason);
        request.DecidedAt = now;
        request.Version++;
        appeal.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RemoveStaleLeaseAsync(redis, appeal.Id);
        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates, database, appeal.Id, "CollaborationChanged", cancellationToken);
        return Results.Ok(RequestPayload(request));
    }

    private static async Task<IResult> RejectWorkflowRequestAsync(
        Guid requestId,
        RejectRequest body,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var reason = Normalize(body.DecisionReason);
        if (reason is null || reason.Length < 5) return Validation("decisionReason", "Кратко укажите причину решения.");
        var request = await database.ExpertWorkflowRequests.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null) return Results.NotFound();
        if (request.Status != ExpertWorkflowRequestStatus.Pending) return InvalidState();
        if (request.Version != body.ExpectedVersion) return VersionConflict();
        request.Status = ExpertWorkflowRequestStatus.Rejected;
        request.DecidedByUserId = operatorId;
        request.DecisionReason = reason;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates, database, request.AppealId, "WorkflowRejected", cancellationToken);
        return Results.Ok(RequestPayload(request));
    }

    private static async Task<IResult> RemoveCoExecutorAsync(
        Guid appealId,
        Guid participantId,
        RemoveParticipantRequest body,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var reason = Normalize(body.Reason);
        if (reason is null || reason.Length < 5) return Validation("reason", "Кратко укажите причину удаления.");
        var appeal = await database.Appeals.SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (appeal.Version != body.ExpectedAppealVersion) return VersionConflict();
        var participant = await database.AppealExpertParticipants.SingleOrDefaultAsync(item => item.Id == participantId
            && item.AppealId == appealId && item.Role == AppealExpertRole.CoExecutor && item.RemovedAt == null, cancellationToken);
        if (participant is null) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        participant.RemovedAt = now;
        participant.RemovedByUserId = operatorId;
        database.AppealAssignmentEvents.Add(Event(appealId, "Removed", participant.ExpertUserId,
            AppealExpertRole.CoExecutor, operatorId, null, now));
        appeal.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await RemoveStaleLeaseAsync(redis, appealId);
        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates, database, appealId, "CollaborationChanged", cancellationToken);
        return Results.Ok(new { appeal.Id, appeal.Version });
    }

    private static async Task<IResult> HeartbeatPresenceAsync(Guid appealId, ClaimsPrincipal principal,
        HttpContext context, IAntiforgery antiforgery, OtklikDbContext database, IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        if (!await ExpertCollaborationAccess.HasAccessAsync(database, appealId, expertId, cancellationToken)) return Results.NotFound();
        var expiry = DateTimeOffset.UtcNow.AddSeconds(PresenceSeconds).ToUnixTimeMilliseconds();
        await redis.GetDatabase().SortedSetAddAsync(ExpertCollaborationAccess.PresenceKey(appealId), expertId.ToString("N"), expiry);
        return await PresencePayloadAsync(appealId, expertId, database, redis, cancellationToken);
    }

    private static async Task<IResult> GetPresenceAsync(Guid appealId, ClaimsPrincipal principal,
        OtklikDbContext database, IConnectionMultiplexer redis, CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        if (!await ExpertCollaborationAccess.HasAccessAsync(database, appealId, expertId, cancellationToken)) return Results.NotFound();
        return await PresencePayloadAsync(appealId, expertId, database, redis, cancellationToken);
    }

    private static async Task<IResult> AcquireComposerAsync(Guid appealId, LeaseRequest body, ClaimsPrincipal principal,
        HttpContext context, IAntiforgery antiforgery, OtklikDbContext database, IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        if (body.LeaseId == Guid.Empty) return Validation("leaseId", "Не удалось открыть редактор.");
        if (!await ExpertCollaborationAccess.HasAccessAsync(database, appealId, expertId, cancellationToken)) return Results.NotFound();
        var value = ExpertCollaborationAccess.LeaseValue(expertId, body.LeaseId);
        const string script = "local c=redis.call('GET',KEYS[1]); if (not c) or c==ARGV[1] then redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2]); return 1 else return 0 end";
        var acquired = (long)await redis.GetDatabase().ScriptEvaluateAsync(script,
            [ExpertCollaborationAccess.ComposerKey(appealId)], [value, (RedisValue)(LeaseSeconds * 1000)]) == 1;
        var payload = await PresencePayloadValueAsync(appealId, expertId, database, redis, cancellationToken);
        return acquired ? Results.Ok(payload) : Results.Conflict(payload);
    }

    private static async Task<IResult> ReleaseComposerAsync(Guid appealId, LeaseRequest body, ClaimsPrincipal principal,
        HttpContext context, IAntiforgery antiforgery, OtklikDbContext database, IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        if (!await ExpertCollaborationAccess.HasAccessAsync(database, appealId, expertId, cancellationToken)) return Results.NotFound();
        const string script = "if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) else return 0 end";
        await redis.GetDatabase().ScriptEvaluateAsync(script, [ExpertCollaborationAccess.ComposerKey(appealId)],
            [ExpertCollaborationAccess.LeaseValue(expertId, body.LeaseId)]);
        return Results.NoContent();
    }

    private static async Task<IResult> PresencePayloadAsync(Guid appealId, Guid currentUserId, OtklikDbContext database,
        IConnectionMultiplexer redis, CancellationToken cancellationToken) =>
        Results.Ok(await PresencePayloadValueAsync(appealId, currentUserId, database, redis, cancellationToken));

    private static async Task<object> PresencePayloadValueAsync(Guid appealId, Guid currentUserId, OtklikDbContext database,
        IConnectionMultiplexer redis, CancellationToken cancellationToken)
    {
        var cache = redis.GetDatabase();
        var key = ExpertCollaborationAccess.PresenceKey(appealId);
        await cache.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var members = await cache.SortedSetRangeByRankAsync(key);
        var ids = members.Select(value => Guid.TryParse(value.ToString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray();
        var activeIds = await database.AppealExpertParticipants.AsNoTracking()
            .Where(item => item.AppealId == appealId && item.RemovedAt == null && ids.Contains(item.ExpertUserId))
            .Select(item => item.ExpertUserId).Distinct().ToListAsync(cancellationToken);
        var people = await database.Users.AsNoTracking().Where(user => activeIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName).Select(user => new { user.Id, user.DisplayName }).ToListAsync(cancellationToken);
        var lease = await cache.StringGetAsync(ExpertCollaborationAccess.ComposerKey(appealId));
        Guid? leaseOwnerId = null;
        if (lease.HasValue && Guid.TryParse(lease.ToString().Split(':')[0], out var parsed)) leaseOwnerId = parsed;
        var leaseOwner = people.FirstOrDefault(person => person.Id == leaseOwnerId);
        return new
        {
            active = people.Select(person => new { person.Id, person.DisplayName, isCurrent = person.Id == currentUserId }),
            composer = leaseOwner is null ? null : new { expertId = leaseOwner.Id, expertName = leaseOwner.DisplayName, isCurrent = leaseOwner.Id == currentUserId },
            leaseSeconds = LeaseSeconds
        };
    }

    private static async Task<List<ExpertCandidate>> EligibleExpertsAsync(OtklikDbContext database, Appeal appeal, CancellationToken cancellationToken)
    {
        var groupId = appeal.CategoryId is null ? null : await database.AppealRoutingRules.AsNoTracking()
            .Where(rule => rule.CategoryId == appeal.CategoryId).Select(rule => (Guid?)rule.ExpertGroupId)
            .SingleOrDefaultAsync(cancellationToken);
        var roleId = await database.Roles.AsNoTracking().Where(role => role.Name == StaffRoles.Expert)
            .Select(role => role.Id).SingleAsync(cancellationToken);
        return await (from user in database.Users.AsNoTracking()
            join userRole in database.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            where userRole.RoleId == roleId && user.IsActive
                && (groupId == null || database.ExpertGroupMemberships.Any(membership =>
                    membership.ExpertGroupId == groupId && membership.ExpertUserId == user.Id))
            orderby user.DisplayName
            select new ExpertCandidate(user.Id, user.DisplayName)).ToListAsync(cancellationToken);
    }

    private static async Task<List<object>> ActiveParticipantsAsync(OtklikDbContext database, Guid appealId, CancellationToken cancellationToken) =>
        await (from participant in database.AppealExpertParticipants.AsNoTracking()
            join user in database.Users.AsNoTracking() on participant.ExpertUserId equals user.Id
            where participant.AppealId == appealId && participant.RemovedAt == null
            orderby participant.Role, participant.AddedAt
            select (object)new { participant.Id, expertId = user.Id, user.DisplayName, role = participant.Role.ToString(), roleText = RoleText(participant.Role) })
            .ToListAsync(cancellationToken);

    private static void AddParticipant(OtklikDbContext database, Guid appealId, Guid expertId, AppealExpertRole role,
        Guid actorId, Guid? requestId, DateTimeOffset now)
    {
        database.AppealExpertParticipants.Add(new AppealExpertParticipant
        {
            Id = Guid.NewGuid(), AppealId = appealId, ExpertUserId = expertId, Role = role,
            AddedByUserId = actorId, AddedAt = now
        });
        database.AppealAssignmentEvents.Add(Event(appealId, "Added", expertId, role, actorId, requestId, now));
    }

    private static void EndParticipant(OtklikDbContext database, AppealExpertParticipant participant,
        Guid actorId, Guid? requestId, DateTimeOffset now)
    {
        participant.RemovedAt = now;
        participant.RemovedByUserId = actorId;
        database.AppealAssignmentEvents.Add(Event(participant.AppealId, "Removed", participant.ExpertUserId,
            participant.Role, actorId, requestId, now));
    }

    private static AppealAssignmentEvent Event(Guid appealId, string eventType, Guid expertId, AppealExpertRole role,
        Guid actorId, Guid? requestId, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(), AppealId = appealId, EventType = eventType, ExpertUserId = expertId,
            Role = role, ActorUserId = actorId, WorkflowRequestId = requestId, OccurredAt = now
        };

    private static Task RemoveStaleLeaseAsync(IConnectionMultiplexer redis, Guid appealId) =>
        redis.GetDatabase().KeyDeleteAsync(ExpertCollaborationAccess.ComposerKey(appealId));

    private static object RequestPayload(ExpertWorkflowRequest request) => new
    {
        request.Id, request.Version, type = request.Type.ToString(), typeText = RequestTypeText(request.Type),
        status = request.Status.ToString(), statusText = RequestStatusText(request.Status), request.Reason,
        request.RequestedAt, request.DecisionReason, request.DecidedAt
    };

    private static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return null; }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "Запрос устарел", detail: "Обновите страницу и повторите действие.");
        }
    }

    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    private static IResult VersionConflict() => Results.Problem(statusCode: 409, title: "Данные уже изменились", detail: "Обновите карточку перед повторным действием.");
    private static IResult InvalidState() => Results.Problem(statusCode: 409, title: "Запрос уже обработан", detail: "Обновите список запросов.");
    private static IResult IdempotencyConflict() => Results.Problem(statusCode: 409, title: "Команда уже использована", detail: "Повторите действие с новым идентификатором.");
    private static string RequestTypeText(ExpertWorkflowRequestType type) => type switch { ExpertWorkflowRequestType.Transfer => "Передать обращение", ExpertWorkflowRequestType.CoExecutor => "Добавить соисполнителя", _ => "Пересмотреть приоритет" };
    private static string RequestStatusText(ExpertWorkflowRequestStatus status) => status switch { ExpertWorkflowRequestStatus.Pending => "Ожидает решения", ExpertWorkflowRequestStatus.Approved => "Подтвержден", _ => "Отклонен" };
    private static string RoleText(AppealExpertRole role) => role == AppealExpertRole.Responsible ? "Ответственный" : "Соисполнитель";
    private static string ApplicantTypeText(ApplicantType type) => type switch { ApplicantType.Student => "Школьник", ApplicantType.Parent => "Родитель", ApplicantType.Teacher => "Педагог", _ => "Заявитель" };
    private static string PriorityText(AppealPriority priority) => priority switch { AppealPriority.Urgent => "Срочный", AppealPriority.Low => "Низкий", _ => "Обычный" };

    private sealed record CreateRequest(Guid ClientRequestId, string? Type, string? Reason);
    private sealed record DecisionRequest(Guid? ExpertId, string? Priority, bool KeepPreviousAsCoExecutor, string? DecisionReason, int ExpectedVersion);
    private sealed record RejectRequest(string? DecisionReason, int ExpectedVersion);
    private sealed record RemoveParticipantRequest(string? Reason, int ExpectedAppealVersion);
    private sealed record LeaseRequest(Guid LeaseId);
    private sealed record ExpertCandidate(Guid Id, string DisplayName);
}
