using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

public static class OperatorWorkEndpoints
{
    private static readonly AppealStatus[] QueueStatuses = [AppealStatus.New, AppealStatus.Triaged];
    private static readonly AppealStatus[] CrisisStatuses =
    [
        AppealStatus.New,
        AppealStatus.Triaged,
        AppealStatus.Assigned,
        AppealStatus.InProgress,
        AppealStatus.NeedsClarification,
        AppealStatus.RecommendationReady,
        AppealStatus.Returned
    ];

    public static IEndpointRouteBuilder MapOperatorWorkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var work = endpoints.MapGroup("/api/staff/operator/work")
            .RequireAuthorization(StaffPolicies.OperatorOnly);
        work.MapPost("/{appealId:guid}/acquire", AcquireAsync);
        work.MapPost("/{appealId:guid}/heartbeat", HeartbeatAsync);
        work.MapPost("/{appealId:guid}/release", ReleaseAsync);
        work.MapPost("/acquire-next", AcquireNextAsync);
        return endpoints;
    }

    private static async Task<IResult> AcquireAsync(
        Guid appealId,
        LeaseRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        if (request.LeaseId == Guid.Empty) return Validation();
        if (!await IsAvailableToOperatorAsync(database, appealId, cancellationToken)) return Results.NotFound();

        var acquired = await OperatorWorkLease.TryAcquireAsync(redis, appealId, operatorId, request.LeaseId);
        return acquired ? LeasePayload(appealId) : LeaseConflict();
    }

    private static async Task<IResult> HeartbeatAsync(
        Guid appealId,
        LeaseRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        IConnectionMultiplexer redis)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        if (request.LeaseId == Guid.Empty) return Validation();
        return await OperatorWorkLease.RenewAsync(redis, appealId, operatorId, request.LeaseId)
            ? LeasePayload(appealId)
            : LeaseConflict();
    }

    private static async Task<IResult> ReleaseAsync(
        Guid appealId,
        LeaseRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        IConnectionMultiplexer redis)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        if (request.LeaseId == Guid.Empty) return Validation();
        await OperatorWorkLease.ReleaseAsync(redis, appealId, operatorId, request.LeaseId);
        return Results.NoContent();
    }

    private static async Task<IResult> AcquireNextAsync(
        AcquireNextRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        if (request.LeaseId == Guid.Empty) return Validation();

        var scope = request.Scope?.Trim().ToLowerInvariant() ?? "queue";
        if (scope is not ("queue" or "crisis"))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["scope"] = ["Неизвестный режим работы оператора."] });
        }

        AppealPriority? requestedPriority = null;
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            if (scope != "queue"
                || !Enum.TryParse<AppealPriority>(request.Priority, true, out var parsedPriority)
                || !Enum.IsDefined(parsedPriority))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["priority"] = ["Выберите срочную, обычную или низкую линию."] });
            }
            requestedPriority = parsedPriority;
        }

        IQueryable<Appeal> query = database.Appeals.AsNoTracking();
        query = scope == "crisis"
            ? query.Where(appeal => appeal.CrisisFlag && CrisisStatuses.Contains(appeal.Status))
                .OrderBy(appeal => appeal.Priority == AppealPriority.Urgent ? 1 : 0)
                .ThenBy(appeal => appeal.CrisisDetectedAt ?? appeal.CreatedAt)
            : query.Where(appeal => QueueStatuses.Contains(appeal.Status)
                    && (requestedPriority == null || appeal.Priority == requestedPriority))
                .OrderByDescending(appeal => appeal.CrisisFlag)
                .ThenByDescending(appeal => appeal.Priority == AppealPriority.Urgent ? 3
                    : appeal.Priority == AppealPriority.Standard ? 2 : 1)
                .ThenBy(appeal => appeal.CreatedAt);
        var appealIds = await query.Select(appeal => appeal.Id).ToListAsync(cancellationToken);
        Guid? appealId = null;
        foreach (var candidates in appealIds.Chunk(500))
        {
            appealId = await OperatorWorkLease.TryAcquireFirstAsync(
                redis,
                candidates,
                operatorId,
                request.LeaseId);
            if (appealId is not null) break;
        }
        return appealId is null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Свободных обращений сейчас нет",
                detail: "Очередь обновилась: все подходящие обращения уже взяты другими операторами.",
                extensions: new Dictionary<string, object?> { ["operatorQueueExhausted"] = true })
            : LeasePayload(appealId.Value);
    }

    private static Task<bool> IsAvailableToOperatorAsync(
        OtklikDbContext database,
        Guid appealId,
        CancellationToken cancellationToken) =>
        database.Appeals.AsNoTracking().AnyAsync(
            appeal => appeal.Id == appealId
                && (QueueStatuses.Contains(appeal.Status)
                    || appeal.CrisisFlag && CrisisStatuses.Contains(appeal.Status)),
            cancellationToken);

    private static IResult LeasePayload(Guid appealId) => Results.Ok(new
    {
        appealId,
        state = OperatorWorkState.Mine.ToString(),
        leaseSeconds = OperatorWorkLease.LeaseSeconds
    });

    private static IResult LeaseConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже в работе",
        detail: "Другой оператор уже разбирает это обращение. Выберите свободное или возьмите следующее.",
        extensions: new Dictionary<string, object?> { ["operatorLeaseConflict"] = true });

    private static IResult Validation() => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["leaseId"] = ["Не удалось закрепить рабочее окно оператора."] });

    private static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Запрос устарел",
                detail: "Обновите страницу и повторите действие.");
        }
    }

    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private sealed record LeaseRequest(Guid LeaseId);
    private sealed record AcquireNextRequest(Guid LeaseId, string? Scope, string? Priority);
}
