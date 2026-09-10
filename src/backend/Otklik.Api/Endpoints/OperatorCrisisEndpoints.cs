using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Appeals;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

public static class OperatorCrisisEndpoints
{
    private const string CrisisContactPurpose = "AppealCrisisContact.v1";
    private static readonly IReadOnlyDictionary<string, string> QuestionLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["duration"] = "Как давно это происходит?",
            ["place"] = "Где это происходит?",
            ["frequency"] = "Как часто это повторяется?",
            ["safety"] = "Сейчас в безопасности?"
        };
    private static readonly AppealStatus[] ActiveStatuses =
    [
        AppealStatus.New,
        AppealStatus.Triaged,
        AppealStatus.Assigned,
        AppealStatus.InProgress,
        AppealStatus.NeedsClarification,
        AppealStatus.RecommendationReady,
        AppealStatus.Returned
    ];

    public static IEndpointRouteBuilder MapOperatorCrisisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var crisis = endpoints.MapGroup("/api/staff/operator/crisis")
            .RequireAuthorization(StaffPolicies.OperatorOnly);

        crisis.MapGet("", GetQueueAsync);
        crisis.MapPost("/{appealId:guid}/urgent", ConfirmUrgentAsync);
        crisis.MapPost("/{appealId:guid}/dismiss", DismissSignalAsync);
        crisis.MapPost("/{appealId:guid}/contact/read", ReadContactAsync);
        return endpoints;
    }

    private static async Task<IResult> GetQueueAsync(
        ClaimsPrincipal principal,
        HttpRequest request,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var appeals = await database.Appeals
            .AsNoTracking()
            .Where(appeal => appeal.CrisisFlag && ActiveStatuses.Contains(appeal.Status))
            .Include(appeal => appeal.Category)
            .Include(appeal => appeal.CrisisContact)
            .Include(appeal => appeal.Answers)
            .OrderBy(appeal => appeal.Priority == AppealPriority.Urgent ? 1 : 0)
            .ThenBy(appeal => appeal.CrisisDetectedAt ?? appeal.CreatedAt)
            .ToListAsync(cancellationToken);

        var appealIds = appeals.Select(appeal => appeal.Id).ToArray();
        var alerts = await database.AdminAlerts.AsNoTracking()
            .Where(alert => appealIds.Contains(alert.AppealId)
                && alert.ResolvedAt == null
                && alert.Type.StartsWith("CrisisDetected"))
            .OrderByDescending(alert => alert.CreatedAt)
            .Select(alert => new { alert.AppealId, alert.Type })
            .ToListAsync(cancellationToken);
        var markers = await database.CrisisMarkers.AsNoTracking()
            .Where(marker => marker.IsActive)
            .OrderBy(marker => marker.SortOrder)
            .Select(marker => new CrisisMarkerDefinition(marker.Pattern, marker.RiskType))
            .ToListAsync(cancellationToken);
        var workStates = await OperatorWorkLease.ReadStatesAsync(
            redis,
            appealIds,
            operatorId,
            OperatorWorkLease.ReadLeaseId(request));

        var items = appeals.Select(appeal =>
        {
            var detectedAt = appeal.CrisisDetectedAt ?? appeal.CreatedAt;
            var alertType = alerts.FirstOrDefault(alert => alert.AppealId == appeal.Id)?.Type;
            var detected = CrisisTextMatcher.Detect(
                new[] { appeal.Narrative }.Concat(appeal.Answers.Select(answer => answer.Value)),
                markers);
            return new
            {
                appeal.Id,
                caseNumber = CaseNumber(appeal.Id),
                appeal.Version,
                status = appeal.Status.ToString(),
                statusText = StatusText(appeal.Status),
                priority = appeal.Priority.ToString(),
                priorityText = appeal.Priority == AppealPriority.Urgent ? "Срочный" : "Не подтвержден оператором",
                detectedAt,
                waitingMinutes = Math.Max(0, (int)(now - detectedAt).TotalMinutes),
                hasContact = appeal.CrisisContact != null,
                applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
                category = appeal.Category?.DisplayName ?? "Категория не подтверждена",
                workState = workStates.GetValueOrDefault(appeal.Id, OperatorWorkState.Available).ToString(),
                sourceText = alertType == "CrisisDetectedInMessage"
                    ? "Новое сообщение заявителя в диалоге"
                    : appeal.Sequence > 1 ? "Продолжение обращения" : "Первоначальное обращение",
                riskTypeText = RiskTypeText(detected.RiskType),
                appeal.Narrative,
                answers = appeal.Answers.OrderBy(answer => answer.QuestionCode).Select(answer => new
                {
                    answer.QuestionCode,
                    question = QuestionLabels.GetValueOrDefault(answer.QuestionCode, answer.QuestionCode),
                    answer.Value
                }),
                canOpenInPrimaryQueue = appeal.Status is AppealStatus.New or AppealStatus.Triaged
            };
        }).ToArray();

        return Results.Ok(new
        {
            total = items.Length,
            unconfirmedCount = items.Count(item => item.priority != AppealPriority.Urgent.ToString()),
            items
        });
    }

    private static async Task<IResult> ConfirmUrgentAsync(
        Guid appealId,
        VersionRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        if (!TryActorId(principal, out var actorId)) return Results.Unauthorized();

        var appeal = await database.Appeals
            .Include(item => item.OperatorActions)
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null || !appeal.CrisisFlag || !ActiveStatuses.Contains(appeal.Status))
        {
            return Results.NotFound();
        }
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        if (appeal.Priority != AppealPriority.Urgent)
        {
            var now = DateTimeOffset.UtcNow;
            database.OperatorActionLogs.Add(new OperatorActionLog
            {
                Id = Guid.NewGuid(),
                AppealId = appeal.Id,
                ActorUserId = actorId,
                Action = "CrisisPriorityConfirmed",
                FromValue = appeal.Priority.ToString(),
                ToValue = AppealPriority.Urgent.ToString(),
                OccurredAt = now
            });
            appeal.Priority = AppealPriority.Urgent;
            appeal.Version++;
            var alerts = await database.AdminAlerts
                .Where(alert => alert.AppealId == appeal.Id
                    && alert.ResolvedAt == null
                    && alert.Type.StartsWith("CrisisDetected"))
                .ToListAsync(cancellationToken);
            alerts.ForEach(alert => alert.ResolvedAt = now);
            if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        }

        if (appeal.Status is not (AppealStatus.New or AppealStatus.Triaged)
            && request.LeaseId is { } leaseId)
        {
            await OperatorWorkLease.ReleaseAsync(redis, appeal.Id, actorId, leaseId);
        }

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            priority = appeal.Priority.ToString(),
            priorityText = "Срочный"
        });
    }

    private static async Task<IResult> DismissSignalAsync(
        Guid appealId,
        DismissRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        if (!TryActorId(principal, out var actorId)) return Results.Unauthorized();
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if ((reason?.Length ?? 0) < 10 || reason!.Length > 1_000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = ["Объясните решение — от 10 до 1 000 символов."]
            });
        }

        var appeal = await database.Appeals.SingleOrDefaultAsync(
            item => item.Id == appealId && item.CrisisFlag && ActiveStatuses.Contains(item.Status),
            cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        if (appeal.Priority == AppealPriority.Urgent)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Срочность уже подтверждена",
                detail: "Вернитесь в основную очередь и продолжите назначение помощи.");
        }

        var now = DateTimeOffset.UtcNow;
        database.OperatorActionLogs.Add(new OperatorActionLog
        {
            Id = Guid.NewGuid(), AppealId = appeal.Id, ActorUserId = actorId,
            Action = "CrisisSignalDismissed", FromValue = "Detected", ToValue = "NotConfirmed",
            Reason = reason, OccurredAt = now
        });
        appeal.CrisisFlag = false;
        appeal.CrisisDetectedAt = null;
        appeal.Version++;
        var alerts = await database.AdminAlerts
            .Where(alert => alert.AppealId == appeal.Id
                && alert.ResolvedAt == null
                && alert.Type.StartsWith("CrisisDetected"))
            .ToListAsync(cancellationToken);
        alerts.ForEach(alert => alert.ResolvedAt = now);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        if (request.LeaseId is { } leaseId)
        {
            await OperatorWorkLease.ReleaseAsync(redis, appeal.Id, actorId, leaseId);
        }
        return Results.Ok(new { appeal.Id, appeal.Version, crisisFlag = false });
    }

    private static async Task<IResult> ReadContactAsync(
        Guid appealId,
        ContactRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtection,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        if (!TryActorId(principal, out var actorId)) return Results.Unauthorized();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appealId, actorId, request.LeaseId)) return WorkLeaseConflict();

        var contact = await database.AppealCrisisContacts
            .AsNoTracking()
            .Include(item => item.Appeal)
            .SingleOrDefaultAsync(item => item.AppealId == appealId && item.Appeal.CrisisFlag, cancellationToken);
        if (contact is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;
        database.CrisisContactAccessLogs.Add(new CrisisContactAccessLog
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            OperatorUserId = actorId,
            AccessedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        var accessCount = await database.CrisisContactAccessLogs.CountAsync(
            access => access.AppealId == appealId,
            cancellationToken);

        return Results.Ok(new
        {
            contact = dataProtection.CreateProtector(CrisisContactPurpose).Unprotect(contact.Ciphertext),
            accessedAt = now,
            accessCount
        });
    }

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

    private static async Task<bool> SaveAsync(OtklikDbContext database, CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static string CaseNumber(Guid appealId) =>
        $"ОБР-{appealId.ToString("N")[..8].ToUpperInvariant()}";

    private static IResult VersionConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже изменилось",
        detail: "Обновите кризисную очередь перед повторным действием.");

    private static IResult WorkLeaseConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже в работе",
        detail: "Другой оператор уже проверяет этот сигнал. Вернитесь к срочной очереди и выберите свободный.",
        extensions: new Dictionary<string, object?> { ["operatorLeaseConflict"] = true });

    private static string ApplicantTypeText(ApplicantType type) => type switch
    {
        ApplicantType.Student => "Школьник",
        ApplicantType.Parent => "Родитель",
        ApplicantType.Teacher => "Педагог",
        _ => "Заявитель"
    };

    private static string StatusText(AppealStatus status) => status switch
    {
        AppealStatus.New => "Новое",
        AppealStatus.Triaged => "Проверено оператором",
        AppealStatus.Assigned => "Назначено специалисту",
        AppealStatus.InProgress => "В работе",
        AppealStatus.NeedsClarification => "Ждет ответа заявителя",
        AppealStatus.RecommendationReady => "Ответ готов",
        AppealStatus.Returned => "Возвращено оператору",
        _ => "Статус изменен"
    };

    private static string RiskTypeText(string? riskType) => riskType switch
    {
        "PhysicalViolence" => "Возможное физическое насилие",
        "LifeThreat" => "Возможная угроза жизни",
        "SuicideRisk" => "Возможный риск самоповреждения",
        _ => "Сигнал требует проверки оператором"
    };

    private sealed record VersionRequest(int ExpectedVersion, Guid? LeaseId);
    private sealed record DismissRequest(int ExpectedVersion, string? Reason, Guid? LeaseId);
    private sealed record ContactRequest(Guid? LeaseId);
}
