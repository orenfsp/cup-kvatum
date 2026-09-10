using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Otklik.Api.Hubs;
using Otklik.Application.Attachments;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

public static class ExpertEndpoints
{
    private static readonly AppealStatus[] VisibleStatuses =
        [AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.NeedsClarification, AppealStatus.RecommendationReady,
            AppealStatus.Closed];
    private static readonly IReadOnlyDictionary<string, string> QuestionLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["duration"] = "Как давно это происходит?",
            ["place"] = "Где это происходит?",
            ["frequency"] = "Как часто это повторяется?",
            ["safety"] = "Сейчас в безопасности?"
        };

    public static IEndpointRouteBuilder MapExpertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var appeals = endpoints.MapGroup("/api/staff/expert/appeals")
            .RequireAuthorization(StaffPolicies.ExpertOnly);

        appeals.MapGet("", GetAppealsAsync);
        appeals.MapGet("/work-summary", GetWorkSummaryAsync);
        appeals.MapGet("/{appealId:guid}", GetAppealAsync);
        appeals.MapGet("/{appealId:guid}/attachments/{attachmentId:guid}", DownloadAttachmentAsync);
        appeals.MapPost("/{appealId:guid}/accept", AcceptAsync);
        appeals.MapPost("/{appealId:guid}/notes", AddNoteAsync);
        appeals.MapPost("/{appealId:guid}/questions", AskQuestionAsync);
        appeals.MapPost("/{appealId:guid}/recommendations", PublishRecommendationAsync);

        return endpoints;
    }

    private static async Task<IResult> GetWorkSummaryAsync(
        ClaimsPrincipal principal,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();

        var counts = await database.Appeals
            .AsNoTracking()
            .VisibleTo(expertId)
            .Where(appeal => VisibleStatuses.Contains(appeal.Status))
            .GroupBy(appeal => appeal.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var inbox = counts.GetValueOrDefault(AppealStatus.Assigned);
        var active = counts.GetValueOrDefault(AppealStatus.InProgress);
        var waiting = counts.GetValueOrDefault(AppealStatus.NeedsClarification);
        var completed = counts.GetValueOrDefault(AppealStatus.RecommendationReady)
            + counts.GetValueOrDefault(AppealStatus.Closed);
        return Results.Ok(new
        {
            inbox,
            active,
            waiting,
            completed,
            actionRequired = inbox + active
        });
    }

    private static async Task<IResult> GetAppealsAsync(
        ClaimsPrincipal principal,
        string? status,
        string? priority,
        Guid? categoryId,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var expertId))
        {
            return Results.Unauthorized();
        }

        IQueryable<Appeal> query = database.Appeals
            .AsNoTracking()
            .Include(appeal => appeal.Category)
            .VisibleTo(expertId)
            .Where(appeal => VisibleStatuses.Contains(appeal.Status));

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(appeal =>
                    appeal.Status == AppealStatus.RecommendationReady || appeal.Status == AppealStatus.Closed);
            }
            else if (!Enum.TryParse<AppealStatus>(status, true, out var parsedStatus)
                || !VisibleStatuses.Contains(parsedStatus))
            {
                return InvalidFilter("status", "Выберите доступный рабочий статус.");
            }
            else
            {
                query = query.Where(appeal => appeal.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse<AppealPriority>(priority, true, out var parsedPriority)
                || !Enum.IsDefined(parsedPriority))
            {
                return InvalidFilter("priority", "Выберите доступный приоритет.");
            }

            query = query.Where(appeal => appeal.Priority == parsedPriority);
        }

        if (categoryId is not null)
        {
            query = query.Where(appeal => appeal.CategoryId == categoryId);
        }

        var items = await query
            .OrderByDescending(appeal => appeal.Priority == AppealPriority.Urgent)
            .ThenByDescending(appeal => appeal.Priority == AppealPriority.Standard)
            .ThenBy(appeal => appeal.AssignedAt ?? appeal.CreatedAt)
            .Select(appeal => new
            {
                appeal.Id,
                appeal.Version,
                status = appeal.Status.ToString(),
                statusText = StatusText(appeal.Status),
                priority = appeal.Priority.ToString(),
                priorityText = PriorityText(appeal.Priority),
                applicantType = appeal.ApplicantType.ToString(),
                applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
                categoryId = appeal.CategoryId,
                category = appeal.Category != null ? appeal.Category.DisplayName : "Без категории",
                role = appeal.AssignedExpertId == expertId ? AppealExpertRole.Responsible.ToString() : AppealExpertRole.CoExecutor.ToString(),
                roleText = appeal.AssignedExpertId == expertId ? "Ответственный" : "Соисполнитель",
                specialization = database.ExpertGroups
                    .Where(group => group.Id == appeal.AppliedExpertGroupId)
                    .Select(group => group.DisplayName)
                    .FirstOrDefault() ?? "Профильная помощь",
                receivedAt = appeal.CreatedAt,
                assignedAt = appeal.AssignedAt,
                nextAction = NextAction(appeal.Status, appeal.AssignedExpertId == expertId),
                lastActivityAt = appeal.StatusHistory
                    .Select(change => (DateTimeOffset?)change.ChangedAt)
                    .Max() ?? appeal.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var categories = await database.AppealCategories
            .AsNoTracking()
            .Where(candidate => candidate.IsActive)
            .OrderBy(candidate => candidate.SortOrder)
            .Select(candidate => new { candidate.Id, candidate.DisplayName })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            total = items.Count,
            items,
            filters = new
            {
                statuses = new[]
                {
                    new { value = AppealStatus.Assigned.ToString(), label = StatusText(AppealStatus.Assigned) },
                    new { value = AppealStatus.InProgress.ToString(), label = StatusText(AppealStatus.InProgress) },
                    new { value = AppealStatus.NeedsClarification.ToString(), label = StatusText(AppealStatus.NeedsClarification) },
                    new { value = AppealStatus.RecommendationReady.ToString(), label = StatusText(AppealStatus.RecommendationReady) },
                    new { value = AppealStatus.Closed.ToString(), label = StatusText(AppealStatus.Closed) },
                    new { value = "Completed", label = "Все завершённые" }
                },
                priorities = Enum.GetValues<AppealPriority>().Select(value => new
                {
                    value = value.ToString(),
                    label = PriorityText(value)
                }),
                categories
            }
        });
    }

    private static async Task<IResult> GetAppealAsync(
        Guid appealId,
        ClaimsPrincipal principal,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();

        var appeal = await database.Appeals
            .AsNoTracking()
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Answers)
            .Include(candidate => candidate.Attachments)
            .Include(candidate => candidate.StatusHistory)
            .Include(candidate => candidate.Messages)
            .Include(candidate => candidate.InternalNotes)
            .Include(candidate => candidate.Recommendations)
            .Include(candidate => candidate.WorkflowRequests)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appealId
                    && (candidate.AssignedExpertId == expertId || candidate.ExpertParticipants.Any(participant =>
                        participant.ExpertUserId == expertId && participant.RemovedAt == null))
                    && VisibleStatuses.Contains(candidate.Status),
                cancellationToken);
        if (appeal is null) return Results.NotFound();

        var participants = await (
            from participant in database.AppealExpertParticipants.AsNoTracking()
            join user in database.Users.AsNoTracking() on participant.ExpertUserId equals user.Id
            where participant.AppealId == appealId && participant.RemovedAt == null
            orderby participant.Role, participant.AddedAt
            select new
            {
                participant.Id,
                expertId = user.Id,
                user.DisplayName,
                role = participant.Role.ToString(),
                roleText = participant.Role == AppealExpertRole.Responsible ? "Ответственный" : "Соисполнитель"
            }).ToListAsync(cancellationToken);
        var assignmentHistory = await (
            from item in database.AppealAssignmentEvents.AsNoTracking()
            join user in database.Users.AsNoTracking() on item.ExpertUserId equals user.Id
            where item.AppealId == appealId
            orderby item.OccurredAt
            select new
            {
                item.Id,
                item.EventType,
                user.DisplayName,
                role = item.Role.ToString(),
                roleText = item.Role == AppealExpertRole.Responsible ? "Ответственный" : "Соисполнитель",
                item.OccurredAt
            }).ToListAsync(cancellationToken);
        var specialization = appeal.AppliedExpertGroupId is Guid groupId
            ? await database.ExpertGroups.AsNoTracking()
                .Where(group => group.Id == groupId)
                .Select(group => group.DisplayName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var previousCycleEntities = await database.Appeals
            .AsNoTracking()
            .Where(cycle => cycle.ThreadId == appeal.ThreadId && cycle.Sequence < appeal.Sequence)
            .Include(cycle => cycle.Messages)
            .Include(cycle => cycle.Recommendations)
            .OrderBy(cycle => cycle.Sequence)
            .ToListAsync(cancellationToken);
        var previousCycles = previousCycleEntities.Select(cycle => new
        {
            sequence = cycle.Sequence,
            statusText = StatusText(cycle.Status),
            startedAt = cycle.CreatedAt,
            completedAt = cycle.CompletedAt,
            cycle.Narrative,
            messages = cycle.Messages
                .OrderByDescending(message => message.CreatedAt)
                .Take(3)
                .OrderBy(message => message.CreatedAt)
                .Select(MessagePayload)
                .ToArray(),
            recommendations = cycle.Recommendations
                .OrderByDescending(recommendation => recommendation.Version)
                .Take(1)
                .Select(RecommendationPayload)
                .ToArray()
        }).ToArray();
        var lastActivityAt = new[] { appeal.CreatedAt }
            .Concat(appeal.StatusHistory.Select(change => change.ChangedAt))
            .Concat(appeal.Messages.Select(message => message.CreatedAt))
            .Concat(appeal.InternalNotes.Select(note => note.CreatedAt))
            .Concat(appeal.Recommendations.Select(recommendation => recommendation.CreatedAt))
            .Concat(appeal.WorkflowRequests.Select(request => request.DecidedAt ?? request.RequestedAt))
            .Max();
        var responsible = appeal.AssignedExpertId == expertId;

        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            priority = appeal.Priority.ToString(),
            priorityText = PriorityText(appeal.Priority),
            applicantType = appeal.ApplicantType.ToString(),
            applicantTypeText = ApplicantTypeText(appeal.ApplicantType),
            category = appeal.Category?.DisplayName ?? "Без категории",
            role = responsible ? AppealExpertRole.Responsible.ToString() : AppealExpertRole.CoExecutor.ToString(),
            roleText = responsible ? "Ответственный" : "Соисполнитель",
            specialization = specialization ?? "Профильная помощь",
            submissionPath = appeal.SubmissionPath.ToString(),
            submissionPathText = appeal.SubmissionPath == SubmissionPath.FreeText ? "Свободная форма" : "Форма с категорией",
            sequence = appeal.Sequence,
            lastActivityAt,
            nextAction = NextAction(appeal.Status, responsible),
            participants,
            assignmentHistory,
            previousCycles,
            workflowRequests = appeal.WorkflowRequests
                .OrderByDescending(request => request.RequestedAt)
                .Select(request => new
                {
                    request.Id,
                    request.Version,
                    type = request.Type.ToString(),
                    typeText = request.Type == ExpertWorkflowRequestType.Transfer ? "Передать обращение"
                        : request.Type == ExpertWorkflowRequestType.CoExecutor ? "Добавить соисполнителя" : "Пересмотреть приоритет",
                    status = request.Status.ToString(),
                    statusText = request.Status == ExpertWorkflowRequestStatus.Pending ? "Ожидает решения"
                        : request.Status == ExpertWorkflowRequestStatus.Approved ? "Подтвержден" : "Отклонен",
                    request.Reason,
                    request.RequestedAt,
                    request.DecisionReason,
                    request.DecidedAt
                }),
            receivedAt = appeal.CreatedAt,
            assignedAt = appeal.AssignedAt,
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
                .Select(AttachmentPayload),
            timeline = appeal.StatusHistory
                .OrderBy(change => change.ChangedAt)
                .Select(change => new
                {
                    status = change.Status.ToString(),
                    text = StatusText(change.Status),
                    at = change.ChangedAt
                }),
            messages = appeal.Messages
                .OrderBy(message => message.CreatedAt)
                .Select(MessagePayload),
            notes = appeal.InternalNotes
                .OrderBy(note => note.CreatedAt)
                .Select(NotePayload),
            recommendations = appeal.Recommendations
                .OrderBy(recommendation => recommendation.Version)
                .Select(RecommendationPayload)
        });
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid appealId,
        Guid attachmentId,
        ClaimsPrincipal principal,
        HttpResponse response,
        OtklikDbContext database,
        IPrivateAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();

        var attachment = await database.AppealAttachments
            .AsNoTracking()
            .Include(candidate => candidate.Appeal)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == attachmentId
                    && candidate.AppealId == appealId
                    && (candidate.Appeal.AssignedExpertId == expertId
                        || candidate.Appeal.ExpertParticipants.Any(participant =>
                            participant.ExpertUserId == expertId && participant.RemovedAt == null))
                    && VisibleStatuses.Contains(candidate.Appeal.Status),
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

    private static async Task<IResult> AcceptAsync(
        Guid appealId,
        VersionedRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();

        var appeal = await database.Appeals.SingleOrDefaultAsync(
            candidate => candidate.Id == appealId && candidate.AssignedExpertId == expertId,
            cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (appeal.Status == AppealStatus.InProgress)
        {
            return Results.Ok(VersionPayload(appeal));
        }
        if (appeal.Status != AppealStatus.Assigned) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();

        var now = DateTimeOffset.UtcNow;
        appeal.Status = AppealStatus.InProgress;
        appeal.Version++;
        database.AppealStatusChanges.Add(StatusChange(appeal.Id, AppealStatus.InProgress, "Expert", now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();
        await NotifyAsync(updates, database, appeal, cancellationToken);
        return Results.Ok(VersionPayload(appeal));
    }

    private static async Task<IResult> AddNoteAsync(
        Guid appealId,
        NoteRequest request,
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
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        var body = Normalize(request.Body);
        if (request.ClientNoteId == Guid.Empty || body is null || body.Length < 2 || body.Length > 4_000)
        {
            return InvalidFilter("body", "Заметка должна содержать от 2 до 4 000 знаков.");
        }

        var appealExists = await database.Appeals.VisibleTo(expertId).AnyAsync(
            candidate => candidate.Id == appealId
                && VisibleStatuses.Contains(candidate.Status)
                && candidate.Status != AppealStatus.Closed,
            cancellationToken);
        if (!appealExists) return Results.NotFound();
        if (!await ExpertCollaborationAccess.HasValidComposerLeaseAsync(
                database, redis, appealId, expertId, request.LeaseId, cancellationToken)) return ComposerLocked();

        var replay = await database.AppealInternalNotes.AsNoTracking().SingleOrDefaultAsync(
            note => note.AppealId == appealId && note.ClientNoteId == request.ClientNoteId,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Body == body ? Results.Ok(NotePayload(replay)) : IdempotencyConflict();
        }

        var note = new AppealInternalNote
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            ClientNoteId = request.ClientNoteId,
            AuthorUserId = expertId,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };
        database.AppealInternalNotes.Add(note);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            replay = await database.AppealInternalNotes.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.AppealId == appealId && candidate.ClientNoteId == request.ClientNoteId,
                cancellationToken);
            if (replay is null) throw;
            return replay.Body == body ? Results.Ok(NotePayload(replay)) : IdempotencyConflict();
        }

        await NotifyAsync(updates, database, appealId, "NoteAdded", cancellationToken);
        return Results.Created($"/api/staff/expert/appeals/{appealId}/notes/{note.Id}", NotePayload(note));
    }

    private static async Task<IResult> AskQuestionAsync(
        Guid appealId,
        MessageRequest request,
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
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        var body = Normalize(request.Body);
        if (request.ClientMessageId == Guid.Empty || body is null || body.Length < 2 || body.Length > 4_000)
        {
            return InvalidFilter("body", "Вопрос должен содержать от 2 до 4 000 знаков.");
        }

        var appeal = await database.Appeals.VisibleTo(expertId)
            .SingleOrDefaultAsync(candidate => candidate.Id == appealId, cancellationToken);
        if (appeal is null)
        {
            var formerParticipant = await database.AppealExpertParticipants.AsNoTracking().AnyAsync(
                participant => participant.AppealId == appealId
                    && participant.ExpertUserId == expertId
                    && participant.RemovedAt != null,
                cancellationToken);
            return formerParticipant ? Results.Forbid() : Results.NotFound();
        }
        if (!await ExpertCollaborationAccess.HasValidComposerLeaseAsync(
                database, redis, appealId, expertId, request.LeaseId, cancellationToken)) return ComposerLocked();

        var replay = await database.AppealMessages.AsNoTracking().SingleOrDefaultAsync(
            message => message.AppealId == appealId && message.ClientMessageId == request.ClientMessageId,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Author == AppealMessageAuthor.Expert && replay.Body == body
                ? Results.Ok(new { message = MessagePayload(replay), appeal = VersionPayload(appeal) })
                : IdempotencyConflict();
        }

        if (appeal.Status != AppealStatus.InProgress) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        var now = DateTimeOffset.UtcNow;
        var message = new AppealMessage
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            ClientMessageId = request.ClientMessageId,
            Author = AppealMessageAuthor.Expert,
            AuthorUserId = expertId,
            Body = body,
            CreatedAt = now
        };
        appeal.Status = AppealStatus.NeedsClarification;
        appeal.Version++;
        database.AppealMessages.Add(message);
        database.AppealStatusChanges.Add(StatusChange(appealId, AppealStatus.NeedsClarification, "Expert", now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();

        await NotifyAsync(updates, database, appeal, cancellationToken);
        return Results.Created(
            $"/api/staff/expert/appeals/{appealId}/messages/{message.Id}",
            new { message = MessagePayload(message), appeal = VersionPayload(appeal) });
    }

    private static async Task<IResult> PublishRecommendationAsync(
        Guid appealId,
        RecommendationRequest request,
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
        if (!TryActorId(principal, out var expertId)) return Results.Unauthorized();
        var body = Normalize(request.Body);
        if (request.ClientRecommendationId == Guid.Empty || body is null || body.Length < 10 || body.Length > 10_000)
        {
            return InvalidFilter("body", "Рекомендация должна содержать от 10 до 10 000 знаков.");
        }

        var appeal = await database.Appeals.SingleOrDefaultAsync(
            candidate => candidate.Id == appealId && candidate.AssignedExpertId == expertId,
            cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (!await ExpertCollaborationAccess.HasValidComposerLeaseAsync(
                database, redis, appealId, expertId, request.LeaseId, cancellationToken)) return ComposerLocked();

        var replay = await database.AppealRecommendations.AsNoTracking().SingleOrDefaultAsync(
            recommendation => recommendation.AppealId == appealId
                && recommendation.ClientRecommendationId == request.ClientRecommendationId,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Body == body
                ? Results.Ok(new { recommendation = RecommendationPayload(replay), appeal = VersionPayload(appeal) })
                : IdempotencyConflict();
        }

        if (appeal.Status != AppealStatus.InProgress) return InvalidState();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict();
        var recommendationVersion = await database.AppealRecommendations
            .Where(candidate => candidate.AppealId == appealId)
            .Select(candidate => (int?)candidate.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var now = DateTimeOffset.UtcNow;
        var recommendation = new AppealRecommendation
        {
            Id = Guid.NewGuid(),
            AppealId = appealId,
            ClientRecommendationId = request.ClientRecommendationId,
            AuthorUserId = expertId,
            Version = recommendationVersion + 1,
            Body = body,
            CreatedAt = now
        };
        appeal.Status = AppealStatus.RecommendationReady;
        appeal.Version++;
        database.AppealRecommendations.Add(recommendation);
        database.AppealStatusChanges.Add(StatusChange(appealId, AppealStatus.RecommendationReady, "Expert", now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict();

        await NotifyAsync(updates, database, appeal, cancellationToken);
        return Results.Created(
            $"/api/staff/expert/appeals/{appealId}/recommendations/{recommendation.Id}",
            new { recommendation = RecommendationPayload(recommendation), appeal = VersionPayload(appeal) });
    }

    private static object AttachmentPayload(AppealAttachment attachment) => new
    {
        attachment.Id,
        attachment.DisplayName,
        attachment.ContentType,
        attachment.Size,
        attachment.CreatedAt
    };

    private static string NextAction(AppealStatus status, bool responsible) => status switch
    {
        AppealStatus.Assigned => "Взять обращение в работу",
        AppealStatus.InProgress when responsible => "Ответить заявителю или подготовить итог",
        AppealStatus.InProgress => "Поддержать работу ответственного специалиста",
        AppealStatus.NeedsClarification => "Дождаться ответа заявителя",
        AppealStatus.RecommendationReady => "Дождаться решения заявителя",
        AppealStatus.Closed => "Работа завершена; доступен просмотр истории",
        _ => "Проверить актуальное состояние"
    };

    private static object MessagePayload(AppealMessage message) => new
    {
        message.Id,
        author = message.Author.ToString(),
        authorLabel = message.Author == AppealMessageAuthor.Expert ? "Специалист" : "Заявитель",
        message.Body,
        message.CreatedAt
    };

    private static object NotePayload(AppealInternalNote note) => new
    {
        note.Id,
        note.Body,
        note.CreatedAt
    };

    private static object RecommendationPayload(AppealRecommendation recommendation) => new
    {
        recommendation.Id,
        recommendation.Version,
        recommendation.Body,
        recommendation.CreatedAt
    };

    private static object VersionPayload(Appeal appeal) => new
    {
        appeal.Id,
        appeal.Version,
        status = appeal.Status.ToString(),
        statusText = StatusText(appeal.Status)
    };

    private static Task NotifyAsync(
        IHubContext<AppealUpdatesHub> updates,
        OtklikDbContext database,
        Appeal appeal,
        CancellationToken cancellationToken) =>
        NotifyAsync(updates, database, appeal.Id, appeal.Status.ToString(), cancellationToken);

    private static Task NotifyAsync(
        IHubContext<AppealUpdatesHub> updates,
        OtklikDbContext database,
        Guid appealId,
        string change,
        CancellationToken cancellationToken) =>
        ExpertWorkUpdateNotifier.NotifyAsync(updates, database, appealId, change, cancellationToken);

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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult VersionConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Обращение уже изменилось",
        detail: "Обновите карточку перед повторным действием.");

    private static IResult InvalidState() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Действие больше недоступно",
        detail: "Рабочий статус обращения уже изменился.");

    private static IResult IdempotencyConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Команда уже использована",
        detail: "Повторите действие с новым идентификатором.");

    private static IResult ComposerLocked() => Results.Problem(
        statusCode: StatusCodes.Status423Locked,
        title: "Редактор занят",
        detail: "Другой специалист сейчас готовит ответ. Дождитесь освобождения редактора.");

    private static bool TryActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static IResult InvalidFilter(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

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
        AppealStatus.Assigned => "Назначено",
        AppealStatus.InProgress => "В работе",
        AppealStatus.NeedsClarification => "Ждет ответа заявителя",
        AppealStatus.RecommendationReady => "Ответ готов",
        AppealStatus.Closed => "Завершено",
        _ => "Статус изменен"
    };

    private sealed record VersionedRequest(int ExpectedVersion);

    private sealed record NoteRequest(Guid ClientNoteId, string? Body, Guid? LeaseId);

    private sealed record MessageRequest(Guid ClientMessageId, string? Body, int ExpectedVersion, Guid? LeaseId);

    private sealed record RecommendationRequest(Guid ClientRecommendationId, string? Body, int ExpectedVersion, Guid? LeaseId);
}
