using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Api.Hubs;
using Otklik.Application.Appeals;
using Otklik.Application.Attachments;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Identity;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

public static class OperatorEndpoints
{
    private static readonly AppealStatus[] QueueStatuses = [AppealStatus.New, AppealStatus.Triaged];
    private static readonly AppealStatus[] ActiveExpertStatuses =
        [AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.NeedsClarification];
    private static readonly IReadOnlyDictionary<string, string> QuestionLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["duration"] = "Как давно это происходит?",
            ["place"] = "Где это происходит?",
            ["frequency"] = "Как часто это повторяется?",
            ["safety"] = "Сейчас в безопасности?"
        };

    public static IEndpointRouteBuilder MapOperatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var queue = endpoints.MapGroup("/api/staff/operator/queue")
            .RequireAuthorization(StaffPolicies.OperatorOnly);

        queue.MapGet("", GetQueueAsync);
        queue.MapGet("/{appealId:guid}", GetAppealAsync);
        queue.MapGet("/{appealId:guid}/attachments/{attachmentId:guid}", DownloadAttachmentAsync);
        queue.MapPost("/{appealId:guid}/triage", TriageAsync);
        queue.MapPost("/{appealId:guid}/assign", AssignAsync);
        queue.MapPost("/{appealId:guid}/reject", RejectAsync);
        queue.MapPost("/{appealId:guid}/resolve", ResolveWithoutExpertAsync);

        return endpoints;
    }

    private static async Task<IResult> GetQueueAsync(
        string? sort,
        ClaimsPrincipal principal,
        HttpRequest request,
        OtklikDbContext database,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var operatorId)) return Results.Unauthorized();
        var overdueHours = Math.Max(1, configuration.GetValue("Operator:OverdueHours", 4));
        var now = DateTimeOffset.UtcNow;
        IQueryable<Appeal> query = database.Appeals
            .AsNoTracking()
            .Where(appeal => QueueStatuses.Contains(appeal.Status))
            .Include(appeal => appeal.Category)
            .Include(appeal => appeal.Attachments)
            .Include(appeal => appeal.StatusHistory);

        query = sort?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(appeal => appeal.CreatedAt),
            "priority" => query
                .OrderByDescending(appeal => appeal.Priority == AppealPriority.Urgent ? 3
                    : appeal.Priority == AppealPriority.Standard ? 2 : 1)
                .ThenBy(appeal => appeal.CreatedAt),
            _ => query
                .OrderByDescending(appeal => appeal.CrisisFlag)
                .ThenByDescending(appeal => appeal.Priority == AppealPriority.Urgent ? 3
                    : appeal.Priority == AppealPriority.Standard ? 2 : 1)
                .ThenBy(appeal => appeal.CreatedAt)
        };

        var appeals = await query.ToListAsync(cancellationToken);
        var workStates = await OperatorWorkLease.ReadStatesAsync(
            redis,
            appeals.Select(appeal => appeal.Id).ToArray(),
            operatorId,
            OperatorWorkLease.ReadLeaseId(request));
        var items = appeals.Select(appeal => new
        {
            appeal.Id,
            caseNumber = CaseNumber(appeal.Id),
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            priority = appeal.Priority.ToString(),
            priorityText = PriorityText(appeal.Priority),
            appeal.CrisisFlag,
            receivedAt = appeal.CreatedAt,
            waitingMinutes = Math.Max(0, (int)(now - appeal.CreatedAt).TotalMinutes),
            isOverdue = now - appeal.CreatedAt >= TimeSpan.FromHours(overdueHours),
            applicantType = appeal.ApplicantType.ToString(),
            applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
            category = appeal.Category?.DisplayName ?? "Категория не подтверждена",
            workState = workStates.GetValueOrDefault(appeal.Id, OperatorWorkState.Available).ToString(),
            nextAction = appeal.Status == AppealStatus.New ? "Провести разбор" : "Назначить специалиста",
            blockingReason = appeal.CategoryId is null
                ? "Нужно выбрать категорию"
                : appeal.Status == AppealStatus.Triaged && appeal.AppliedExpertGroupId is null
                    ? "Нет активного правила маршрутизации"
                    : null,
            hasAttachments = appeal.Attachments.Count > 0,
            lastActivityAt = appeal.StatusHistory
                .Select(change => change.ChangedAt)
                .DefaultIfEmpty(appeal.CreatedAt)
                .Max()
        }).ToArray();

        return Results.Ok(new
        {
            total = items.Length,
            overdueCount = items.Count(item => item.isOverdue),
            overdueHours,
            sort = sort?.ToLowerInvariant() ?? "default",
            items
        });
    }

    private static async Task<IResult> GetAppealAsync(
        Guid appealId,
        OtklikDbContext database,
        ICategorySuggestionService suggestionService,
        CancellationToken cancellationToken)
    {
        var appeal = await database.Appeals
            .AsNoTracking()
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Answers)
            .Include(candidate => candidate.Attachments)
            .SingleOrDefaultAsync(candidate => candidate.Id == appealId, cancellationToken);
        if (appeal is null || !QueueStatuses.Contains(appeal.Status))
        {
            return Results.NotFound();
        }

        var suggestion = suggestionService.Suggest(
            appeal.Narrative,
            appeal.Answers.Select(answer => answer.Value));
        var suggestedCategory = suggestion is null
            ? null
            : await database.AppealCategories
                .AsNoTracking()
                .SingleOrDefaultAsync(category => category.Id == suggestion.CategoryId, cancellationToken);
        var categories = await database.AppealCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .Select(category => new { category.Id, category.DisplayName })
            .ToListAsync(cancellationToken);
        var routing = await BuildRoutingAsync(
            database,
            appeal.CategoryId ?? suggestion?.CategoryId,
            cancellationToken);

        return Results.Ok(new
        {
            appeal.Id,
            caseNumber = CaseNumber(appeal.Id),
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            priority = appeal.Priority.ToString(),
            priorityText = PriorityText(appeal.Priority),
            appeal.CrisisFlag,
            receivedAt = appeal.CreatedAt,
            applicantType = appeal.ApplicantType.ToString(),
            applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
            submissionPath = appeal.SubmissionPath.ToString(),
            appeal.CategoryId,
            category = appeal.Category?.DisplayName,
            appeal.Narrative,
            answers = appeal.Answers
                .OrderBy(answer => answer.QuestionCode)
                .Select(answer => new
                {
                    answer.QuestionCode,
                    question = QuestionLabels.GetValueOrDefault(answer.QuestionCode, answer.QuestionCode),
                    answer.Value
                }),
            attachments = appeal.Attachments
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new
                {
                    attachment.Id,
                    attachment.DisplayName,
                    attachment.ContentType,
                    attachment.Size,
                    attachment.CreatedAt
                }),
            categories,
            suggestion = suggestedCategory is null ? null : new
            {
                categoryId = suggestedCategory.Id,
                category = suggestedCategory.DisplayName,
                suggestion!.Reason
            },
            appliedRouting = appeal.AppliedRoutingRuleId is null ? null : new
            {
                ruleId = appeal.AppliedRoutingRuleId,
                ruleVersion = appeal.AppliedRoutingRuleVersion,
                groupId = appeal.AppliedExpertGroupId
            },
            routing
        });
    }

    private static async Task<IResult> TriageAsync(
        Guid appealId,
        TriageRequest request,
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
        if (!Enum.TryParse<AppealPriority>(request.Priority, true, out var priority)
            || !Enum.IsDefined(priority))
        {
            return Validation("priority", "Выберите доступный приоритет.");
        }

        var category = await database.AppealCategories
            .SingleOrDefaultAsync(item => item.Id == request.CategoryId && item.IsActive, cancellationToken);
        if (category is null)
        {
            return Validation("categoryId", "Выберите доступную категорию.");
        }

        var appeal = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.OperatorActions)
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!QueueStatuses.Contains(appeal.Status)) return InvalidState();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        var now = DateTimeOffset.UtcNow;
        var categoryChanged = appeal.CategoryId != category.Id;
        if (categoryChanged)
        {
            database.OperatorActionLogs.Add(Action(
                appeal.Id,
                actorId,
                "CategoryChanged",
                appeal.CategoryId?.ToString(),
                category.Id.ToString(),
                null,
                now));
            appeal.CategoryId = category.Id;
        }

        if (appeal.Priority != priority)
        {
            database.OperatorActionLogs.Add(Action(
                appeal.Id,
                actorId,
                "PriorityChanged",
                appeal.Priority.ToString(),
                priority.ToString(),
                Normalize(request.Reason),
                now));
            appeal.Priority = priority;
        }

        if (appeal.Status == AppealStatus.New)
        {
            appeal.Status = AppealStatus.Triaged;
            database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Triaged, "Operator", now));
        }

        if (categoryChanged || appeal.AppliedRoutingRuleId is null)
        {
            await ApplyRoutingSnapshotAsync(database, appeal, category.Id, cancellationToken);
        }

        appeal.Version++;
        await SynchronizeRoutingAlertAsync(database, appeal.Id, category.Id, now, cancellationToken);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            categoryId = category.Id,
            category = category.DisplayName,
            priority = appeal.Priority.ToString(),
            priorityText = PriorityText(appeal.Priority)
        });
    }

    private static async Task<IResult> AssignAsync(
        Guid appealId,
        AssignRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IConnectionMultiplexer redis,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        if (!TryActorId(principal, out var actorId)) return Results.Unauthorized();

        var appeal = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.OperatorActions)
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!QueueStatuses.Contains(appeal.Status)) return InvalidState();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        if (appeal.CategoryId is null)
        {
            return Validation("categoryId", "Сначала подтвердите категорию обращения.");
        }

        var routing = await BuildRoutingAsync(database, appeal.CategoryId, cancellationToken);
        var expert = routing.Experts.SingleOrDefault(candidate => candidate.Id == request.ExpertId);
        if (expert is null)
        {
            await SynchronizeRoutingAlertAsync(
                database,
                appeal.Id,
                appeal.CategoryId.Value,
                DateTimeOffset.UtcNow,
                cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Эксперт недоступен",
                detail: "Обращение осталось в очереди. Выберите специалиста из рекомендованной группы.");
        }

        var overrideReason = Normalize(request.OverrideReason);
        if (expert.AtCapacity && !request.AllowOverCapacity)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Достигнут лимит нагрузки",
                detail: "Оставьте обращение в очереди или подтвердите ручное превышение лимита с причиной.",
                extensions: new Dictionary<string, object?> { ["capacityOverrideRequired"] = true });
        }

        if (expert.AtCapacity && (overrideReason?.Length ?? 0) < 10)
        {
            return Validation("overrideReason", "Укажите причину ручного превышения лимита — не менее 10 символов.");
        }

        var now = DateTimeOffset.UtcNow;
        appeal.AssignedExpertId = expert.Id;
        appeal.AssignedAt = now;
        appeal.Status = AppealStatus.Assigned;
        database.AppealExpertParticipants.Add(new AppealExpertParticipant
        {
            Id = Guid.NewGuid(),
            AppealId = appeal.Id,
            ExpertUserId = expert.Id,
            Role = AppealExpertRole.Responsible,
            AddedByUserId = actorId,
            AddedAt = now
        });
        database.AppealAssignmentEvents.Add(new AppealAssignmentEvent
        {
            Id = Guid.NewGuid(),
            AppealId = appeal.Id,
            EventType = "Assigned",
            ExpertUserId = expert.Id,
            Role = AppealExpertRole.Responsible,
            ActorUserId = actorId,
            OccurredAt = now
        });
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Assigned, "Operator", now));
        database.OperatorActionLogs.Add(Action(
            appeal.Id,
            actorId,
            "Assigned",
            null,
            expert.Id.ToString(),
            null,
            now));
        if (expert.AtCapacity)
        {
            database.OperatorActionLogs.Add(Action(
                appeal.Id,
                actorId,
                "CapacityOverride",
                expert.ActiveCount.ToString(),
                expert.Limit.ToString(),
                overrideReason,
                now));
        }

        appeal.Version++;
        var alerts = await database.AdminAlerts
            .Where(alert => alert.AppealId == appeal.Id && alert.ResolvedAt == null)
            .ToListAsync(cancellationToken);
        alerts.ForEach(alert => alert.ResolvedAt = now);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        if (request.LeaseId is { } leaseId)
        {
            await OperatorWorkLease.ReleaseAsync(redis, appeal.Id, actorId, leaseId);
        }
        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates,
            database,
            appeal.Id,
            appeal.Status.ToString(),
            cancellationToken);

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            assignedExpertId = expert.Id,
            assignedExpert = expert.DisplayName
        });
    }

    private static async Task<IResult> RejectAsync(
        Guid appealId,
        RejectRequest request,
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
        if (!Enum.TryParse<AppealRejectionReason>(request.ReasonCode, true, out var reasonCode)
            || !Enum.IsDefined(reasonCode))
        {
            return Validation("reasonCode", "Выберите причину завершения.");
        }

        var internalReason = Normalize(request.InternalReason);
        if ((internalReason?.Length ?? 0) < 5)
        {
            return Validation("internalReason", "Кратко укажите внутреннюю причину.");
        }

        var appeal = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.OperatorActions)
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!QueueStatuses.Contains(appeal.Status)) return InvalidState();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        var now = DateTimeOffset.UtcNow;
        appeal.Status = AppealStatus.Rejected;
        appeal.RejectionReason = reasonCode;
        appeal.RejectionInternalReason = internalReason;
        appeal.PublicResolution = reasonCode == AppealRejectionReason.Spam
            ? "Обращение завершено, потому что не содержит запроса о помощи. Если помощь нужна, можно оставить новое обращение и описать ситуацию подробнее."
            : "Этот вопрос находится вне задач сервиса. Если ситуация связана с травлей, конфликтом или давлением, можно оставить новое обращение и описать ее подробнее.";
        appeal.CompletedAt = now;
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Rejected, "Operator", now));
        database.OperatorActionLogs.Add(Action(
            appeal.Id,
            actorId,
            "Rejected",
            null,
            reasonCode.ToString(),
            internalReason,
            now));
        appeal.Version++;
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        if (request.LeaseId is { } leaseId)
        {
            await OperatorWorkLease.ReleaseAsync(redis, appeal.Id, actorId, leaseId);
        }

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status)
        });
    }

    private static async Task<IResult> ResolveWithoutExpertAsync(
        Guid appealId,
        ResolveRequest request,
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
        var message = Normalize(request.Message);
        if ((message?.Length ?? 0) < 10 || message!.Length > 4_000)
        {
            return Validation("message", "Ответ должен содержать от 10 до 4 000 знаков.");
        }

        var appeal = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.OperatorActions)
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!QueueStatuses.Contains(appeal.Status)) return InvalidState();
        if (!await OperatorWorkLease.CanWriteAsync(redis, appeal.Id, actorId, request.LeaseId)) return WorkLeaseConflict();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        var now = DateTimeOffset.UtcNow;
        appeal.Status = AppealStatus.Closed;
        appeal.PublicResolution = message;
        appeal.CompletedAt = now;
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.Closed, "Operator", now));
        database.OperatorActionLogs.Add(Action(
            appeal.Id,
            actorId,
            "ResolvedWithoutExpert",
            null,
            "PublicResponse",
            null,
            now));
        appeal.Version++;
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        if (request.LeaseId is { } leaseId)
        {
            await OperatorWorkLease.ReleaseAsync(redis, appeal.Id, actorId, leaseId);
        }

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status)
        });
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid appealId,
        Guid attachmentId,
        HttpResponse response,
        OtklikDbContext database,
        IPrivateAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        var attachment = await database.AppealAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId && item.AppealId == appealId,
                cancellationToken);
        if (attachment is null) return Results.NotFound();

        response.Headers.CacheControl = "no-store";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = "sandbox";
        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return Results.File(
            content,
            attachment.ContentType,
            fileDownloadName: attachment.DisplayName,
            enableRangeProcessing: false);
    }

    private static async Task<RoutingEvaluation> BuildRoutingAsync(
        OtklikDbContext database,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return new RoutingEvaluation(null, null, "Подтвердите категорию, чтобы увидеть подходящую группу.", []);
        }

        var rule = await database.AppealRoutingRules
            .AsNoTracking()
            .Include(item => item.ExpertGroup)
            .SingleOrDefaultAsync(
                item => item.CategoryId == categoryId && item.IsActive && item.ExpertGroup.IsActive,
                cancellationToken);
        if (rule is null)
        {
            return new RoutingEvaluation(null, null, "Для категории пока не настроена группа специалистов.", []);
        }

        var experts = await (
            from membership in database.ExpertGroupMemberships.AsNoTracking()
            join user in database.Users.AsNoTracking() on membership.ExpertUserId equals user.Id
            where membership.ExpertGroupId == rule.ExpertGroupId && user.IsActive && user.IsAvailable
            select new { user.Id, user.DisplayName })
            .ToListAsync(cancellationToken);
        var expertIds = experts.Select(expert => expert.Id).ToArray();
        var activeCounts = await database.Appeals
            .AsNoTracking()
            .Where(appeal => appeal.AssignedExpertId != null
                && expertIds.Contains(appeal.AssignedExpertId.Value)
                && ActiveExpertStatuses.Contains(appeal.Status))
            .GroupBy(appeal => appeal.AssignedExpertId!.Value)
            .Select(group => new { ExpertId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ExpertId, item => item.Count, cancellationToken);
        return RoutingPolicy.Evaluate(
            rule.ExpertGroupId,
            rule.ExpertGroup.DisplayName,
            rule.ExpertGroup.ActiveAppealLimit,
            experts.Select(expert => new ExpertLoad(
                expert.Id,
                expert.DisplayName,
                activeCounts.GetValueOrDefault(expert.Id))));
    }

    private static async Task SynchronizeRoutingAlertAsync(
        OtklikDbContext database,
        Guid appealId,
        Guid categoryId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var routing = await BuildRoutingAsync(database, categoryId, cancellationToken);
        var existing = await database.AdminAlerts
            .SingleOrDefaultAsync(
                alert => alert.AppealId == appealId
                    && alert.Type == "NoEligibleExpert"
                    && alert.ResolvedAt == null,
                cancellationToken);
        if (routing.Experts.Count == 0 && existing is null)
        {
            database.AdminAlerts.Add(new AdminAlert
            {
                Id = Guid.NewGuid(),
                AppealId = appealId,
                Type = "NoEligibleExpert",
                CreatedAt = now
            });
        }
        else if (routing.Experts.Count > 0 && existing is not null)
        {
            existing.ResolvedAt = now;
        }
    }

    private static async Task ApplyRoutingSnapshotAsync(
        OtklikDbContext database,
        Appeal appeal,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var rule = await database.AppealRoutingRules
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CategoryId == categoryId && item.IsActive,
                cancellationToken);
        appeal.AppliedRoutingRuleId = rule?.Id;
        appeal.AppliedRoutingRuleVersion = rule?.Version;
        appeal.AppliedExpertGroupId = rule?.ExpertGroupId;
    }

    private static async Task<bool> SaveAsync(
        OtklikDbContext database,
        CancellationToken cancellationToken)
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

    private static async Task<IResult?> ValidateAntiforgeryAsync(
        HttpContext context,
        IAntiforgery antiforgery)
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

    private static OperatorActionLog Action(
        Guid appealId,
        Guid actorId,
        string action,
        string? fromValue,
        string? toValue,
        string? reason,
        DateTimeOffset occurredAt) => new()
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            ActorUserId = actorId,
            Action = action,
            FromValue = fromValue,
            ToValue = toValue,
            Reason = reason,
            OccurredAt = occurredAt
        };

    private static AppealStatusChange StatusChange(
        Guid appealId,
        AppealStatus status,
        string source,
        DateTimeOffset changedAt) => new()
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            Status = status,
            Source = source,
            ChangedAt = changedAt
        };

    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CaseNumber(Guid appealId) =>
        $"ОБР-{appealId.ToString("N")[..8].ToUpperInvariant()}";

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult VersionConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже изменилось",
        detail: "Другой оператор успел обновить обращение. Перезагрузите карточку перед повторным действием.");

    private static IResult WorkLeaseConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже в работе",
        detail: "Другой оператор уже разбирает это обращение. Вернитесь к очереди и выберите свободное.",
        extensions: new Dictionary<string, object?> { ["operatorLeaseConflict"] = true });

    private static IResult InvalidState() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Действие больше недоступно",
        detail: "Статус обращения уже изменился. Обновите очередь.");

    private static string ApplicantTypeText(ApplicantType type) => type switch
    {
        ApplicantType.Student => "Школьник",
        ApplicantType.Parent => "Родитель",
        ApplicantType.Teacher => "Педагог",
        _ => "Заявитель"
    };

    private static string PriorityText(AppealPriority priority) => priority switch
    {
        AppealPriority.Urgent => "Срочный",
        AppealPriority.Low => "Низкий",
        _ => "Обычный"
    };

    private static string StatusText(AppealStatus status) => status switch
    {
        AppealStatus.New => "Новое",
        AppealStatus.Triaged => "Проверено оператором",
        AppealStatus.Assigned => "Распределено",
        AppealStatus.Rejected => "Отклонено",
        AppealStatus.Closed => "Завершено оператором",
        _ => "Статус изменен"
    };

    private sealed record TriageRequest(
        Guid CategoryId,
        string? Priority,
        int ExpectedVersion,
        string? Reason,
        Guid? LeaseId);

    private sealed record AssignRequest(
        Guid ExpertId,
        int ExpectedVersion,
        bool AllowOverCapacity,
        string? OverrideReason,
        Guid? LeaseId);

    private sealed record RejectRequest(
        string? ReasonCode,
        string? InternalReason,
        int ExpectedVersion,
        Guid? LeaseId);

    private sealed record ResolveRequest(string? Message, int ExpectedVersion, Guid? LeaseId);

}
