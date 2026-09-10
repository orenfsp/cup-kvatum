using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Otklik.Api.Hubs;
using Otklik.Api.Security;
using Otklik.Application.Appeals;
using Otklik.Application.Attachments;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;

namespace Otklik.Api.Endpoints;

public static class PublicAppealEndpoints
{
    private const string RecoveryPurpose = "AppealTrackRecovery.v1";
    private const string CrisisContactPurpose = "AppealCrisisContact.v1";
    private const int MaxAttachmentCount = 5;
    private const long MaxUploadRequestSize = 11 * 1024 * 1024;
    private static readonly HashSet<string> AllowedQuestionCodes =
        new(StringComparer.Ordinal) { "duration", "place", "frequency", "safety" };

    public static IEndpointRouteBuilder MapPublicAppealEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var intake = endpoints.MapGroup("/api/public").RequireRateLimiting("public-global");

        intake.MapGet("/intake/options", GetOptionsAsync);
        intake.MapPost("/appeals", CreateAppealAsync);
        intake.MapPost("/appeals/open", GetStatusAsync);
        intake.MapPost("/appeals/status", GetStatusAsync);
        intake.MapPost("/appeals/continue", ContinueAppealAsync);
        intake.MapPost("/appeals/messages", AddApplicantMessageAsync);
        intake.MapPost("/appeals/attachments", UploadAttachmentAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestSize));
        intake.MapPost("/appeals/attachments/{attachmentId:guid}/download", DownloadAttachmentAsync);

        return endpoints;
    }

    private static async Task<IResult> GetOptionsAsync(
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var categories = await database.AppealCategories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .Select(category => new
            {
                category.Id,
                category.Code,
                category.DisplayName
            })
            .ToListAsync(cancellationToken);
        var crisisMarkers = await database.CrisisMarkers
            .AsNoTracking()
            .Where(marker => marker.IsActive)
            .OrderBy(marker => marker.SortOrder)
            .Select(marker => marker.Pattern)
            .ToListAsync(cancellationToken);
        var crisisSupport = await GetCrisisSupportAsync(database, cancellationToken);

        return Results.Ok(new
        {
            applicantTypes = new[]
            {
                new { value = "Student", label = "Школьник" },
                new { value = "Parent", label = "Родитель" },
                new { value = "Teacher", label = "Педагог" }
            },
            submissionPaths = new[]
            {
                new { value = "FreeText", label = "Рассказать своими словами" },
                new { value = "Category", label = "Выбрать категорию" }
            },
            categories,
            crisisMarkers,
            crisisSupport,
            questions = new[]
            {
                new { code = "duration", studentText = "Как давно это происходит?", adultText = "Как давно это происходит?" },
                new { code = "place", studentText = "Где это происходит?", adultText = "Где это происходит?" },
                new { code = "frequency", studentText = "Как часто это повторяется?", adultText = "Как часто это повторяется?" },
                new { code = "safety", studentText = "Ты сейчас в безопасности?", adultText = "Вы сейчас в безопасности?" }
            }
        });
    }

    private static async Task<IResult> CreateAppealAsync(
        CreateAppealRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        IDataProtectionProvider dataProtection,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, database, cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return Results.ValidationProblem(validation.Errors);
        }

        var protector = dataProtection.CreateProtector(RecoveryPurpose);
        var contactProtector = dataProtection.CreateProtector(CrisisContactPurpose);
        var existing = await database.Appeals
            .AsNoTracking()
            .Include(appeal => appeal.Thread)
            .SingleOrDefaultAsync(
                appeal => appeal.ClientRequestId == request.ClientRequestId,
                cancellationToken);

        if (existing is not null)
        {
            var prepared = await deviceCapabilities.PrepareGrantAsync(context, existing.ThreadId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            deviceCapabilities.ApplyCookie(context, prepared);
            return Created(existing, protector.Unprotect(existing.Thread.TrackRecoveryCiphertext));
        }

        var trackNumber = await GenerateUniqueTrackNumberAsync(database, trackNumbers, cancellationToken);
        var createdAt = DateTimeOffset.UtcNow;
        var appliedRule = validation.Category is null
            ? null
            : await database.AppealRoutingRules
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    rule => rule.CategoryId == validation.Category.Id && rule.IsActive,
                    cancellationToken);
        var threadId = Guid.NewGuid();
        var thread = new AppealThread
        {
            Id = threadId,
            TrackHash = trackNumbers.Hash(trackNumber),
            TrackRecoveryCiphertext = protector.Protect(trackNumber),
            Version = 1,
            CreatedAt = createdAt,
            LastActivityAt = createdAt
        };
        var appeal = new Appeal
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            Sequence = 1,
            ClientRequestId = request.ClientRequestId,
            ApplicantType = validation.ApplicantType,
            SubmissionPath = validation.SubmissionPath,
            CategoryId = validation.Category?.Id,
            AppliedRoutingRuleId = appliedRule?.Id,
            AppliedRoutingRuleVersion = appliedRule?.Version,
            AppliedExpertGroupId = appliedRule?.ExpertGroupId,
            Narrative = NormalizeOptional(request.Narrative),
            Status = AppealStatus.New,
            CrisisFlag = validation.Crisis.IsMatch,
            CrisisDetectedAt = validation.Crisis.IsMatch ? createdAt : null,
            CreatedAt = createdAt,
            Answers = NormalizeAnswers(request.Answers),
            StatusHistory =
            [
                new AppealStatusChange
                {
                    Id = Guid.NewGuid(),
                    Status = AppealStatus.New,
                    ChangedAt = createdAt,
                    Source = "Applicant"
                }
            ]
        };

        var crisisContact = NormalizeOptional(request.CrisisContact);
        if (validation.Crisis.IsMatch && crisisContact is not null)
        {
            appeal.CrisisContact = new AppealCrisisContact
            {
                AppealId = appeal.Id,
                Ciphertext = contactProtector.Protect(crisisContact),
                CreatedAt = createdAt
            };
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        PreparedDeviceGrant deviceGrant;
        try
        {
            database.AppealThreads.Add(thread);
            await database.SaveChangesAsync(cancellationToken);
            database.Appeals.Add(appeal);
            if (validation.Crisis.IsMatch)
            {
                database.AdminAlerts.Add(new AdminAlert
                {
                    Id = Guid.NewGuid(),
                    AppealId = appeal.Id,
                    Type = "CrisisDetectedAtIntake",
                    CreatedAt = createdAt
                });
            }
            await database.SaveChangesAsync(cancellationToken);
            thread.CurrentCycleId = appeal.Id;
            deviceGrant = await deviceCapabilities.PrepareGrantAsync(context, thread.Id, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var replay = await database.Appeals
                .AsNoTracking()
                .Include(candidate => candidate.Thread)
                .SingleOrDefaultAsync(
                    candidate => candidate.ClientRequestId == request.ClientRequestId,
                    cancellationToken);

            if (replay is null)
            {
                throw;
            }

            deviceGrant = await deviceCapabilities.PrepareGrantAsync(context, replay.ThreadId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            deviceCapabilities.ApplyCookie(context, deviceGrant);
            return Created(replay, protector.Unprotect(replay.Thread.TrackRecoveryCiphertext));
        }

        deviceCapabilities.ApplyCookie(context, deviceGrant);
        return Created(appeal, trackNumber);
    }

    private static async Task<IResult> GetStatusAsync(
        TrackLookupRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        TrackLookupRateLimiter rateLimiter,
        DeviceCapabilityService deviceCapabilities,
        CancellationToken cancellationToken)
    {
        var clientAddress = ClientRequestIdentity.Address(context);
        var attempt = await rateLimiter.CheckBeforeAttemptAsync(clientAddress, cancellationToken);
        if (!attempt.IsAllowed) return TooManyTrackAttempts(context.Response, attempt.RetryAfterSeconds);
        if (!trackNumbers.TryNormalize(request.TrackNumber, out var normalized))
        {
            await rateLimiter.RecordFailureAsync(clientAddress, cancellationToken);
            return TrackNotFound();
        }

        var hash = trackNumbers.Hash(normalized);
        var thread = await database.AppealThreads.AsNoTracking().WithPublicDetails()
            .SingleOrDefaultAsync(candidate => candidate.TrackHash == hash, cancellationToken);

        if (thread is null)
        {
            await rateLimiter.RecordFailureAsync(clientAddress, cancellationToken);
            return TrackNotFound();
        }

        var prepared = await deviceCapabilities.PrepareGrantAsync(context, thread.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        deviceCapabilities.ApplyCookie(context, prepared);
        return Results.Ok(await PublicAppealStatusPayload.CreateAsync(database, thread, cancellationToken));
    }

    private static async Task<IResult> ContinueAppealAsync(
        ContinueAppealRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        IDataProtectionProvider dataProtection,
        CancellationToken cancellationToken)
    {
        var body = NormalizeOptional(request.Body);
        if (request.ClientContinuationId == Guid.Empty || body is null || body.Length < 10 || body.Length > 10_000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["Расскажите, что изменилось. Нужно от 10 до 10 000 знаков."]
            });
        }

        var threadId = await ApplicantAppealAccess.ResolveThreadIdAsync(
            request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (threadId is null) return TrackNotFound();

        var thread = await database.AppealThreads
            .Include(item => item.Cycles)
            .SingleOrDefaultAsync(item => item.Id == threadId, cancellationToken);
        if (thread is null) return TrackNotFound();

        var replay = thread.Cycles.SingleOrDefault(cycle =>
            cycle.ClientContinuationId == request.ClientContinuationId);
        if (replay is not null)
        {
            return replay.Narrative == body
                ? Continued(thread, replay)
                : IdempotencyConflict();
        }

        var current = thread.Cycles.SingleOrDefault(cycle => cycle.Id == thread.CurrentCycleId)
            ?? thread.Cycles.OrderByDescending(cycle => cycle.Sequence).First();
        if (current.Status != AppealStatus.Closed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Текущий цикл ещё открыт",
                detail: "Продолжайте диалог в текущем цикле. Новый цикл можно начать после завершения работы специалиста.");
        }
        if (request.ExpectedThreadVersion is > 0 && request.ExpectedThreadVersion != thread.Version)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Обращение уже обновилось",
                detail: "Откройте обращение заново перед продолжением.");
        }

        var crisis = await DetectCrisisAsync(database, [body], cancellationToken);
        var crisisContact = NormalizeOptional(request.CrisisContact);
        if (crisisContact is not null && !crisis.IsMatch)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["crisisContact"] = ["Способ связи не нужен для обычного обращения и не был сохранен."]
            });
        }
        if (crisisContact is not null && (crisisContact.Length < 5 || crisisContact.Length > 200))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["crisisContact"] = ["Укажите от 5 до 200 знаков или продолжите без контакта."]
            });
        }

        var now = DateTimeOffset.UtcNow;
        var next = new Appeal
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            Sequence = current.Sequence + 1,
            ClientRequestId = Guid.NewGuid(),
            ClientContinuationId = request.ClientContinuationId,
            ApplicantType = current.ApplicantType,
            SubmissionPath = SubmissionPath.FreeText,
            CategoryId = current.CategoryId,
            AppliedRoutingRuleId = current.AppliedRoutingRuleId,
            AppliedRoutingRuleVersion = current.AppliedRoutingRuleVersion,
            AppliedExpertGroupId = current.AppliedExpertGroupId,
            Narrative = body,
            Status = AppealStatus.New,
            Priority = AppealPriority.Standard,
            CrisisFlag = crisis.IsMatch,
            CrisisDetectedAt = crisis.IsMatch ? now : null,
            CreatedAt = now,
            StatusHistory =
            [
                new AppealStatusChange
                {
                    Id = Guid.NewGuid(),
                    Status = AppealStatus.New,
                    ChangedAt = now,
                    Source = "Applicant"
                }
            ]
        };
        if (crisis.IsMatch && crisisContact is not null)
        {
            next.CrisisContact = new AppealCrisisContact
            {
                AppealId = next.Id,
                Ciphertext = dataProtection.CreateProtector(CrisisContactPurpose).Protect(crisisContact),
                CreatedAt = now
            };
        }

        thread.CurrentCycleId = next.Id;
        thread.LastActivityAt = now;
        thread.Version++;
        database.Appeals.Add(next);
        if (crisis.IsMatch)
        {
            database.AdminAlerts.Add(new AdminAlert
            {
                Id = Guid.NewGuid(),
                AppealId = next.Id,
                Type = "CrisisDetectedAtContinuation",
                CreatedAt = now
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            var duplicate = await database.Appeals.AsNoTracking()
                .SingleOrDefaultAsync(cycle => cycle.ThreadId == threadId
                    && cycle.ClientContinuationId == request.ClientContinuationId,
                    cancellationToken);
            if (duplicate is not null && duplicate.Narrative == body)
            {
                var currentThread = await database.AppealThreads.AsNoTracking()
                    .SingleAsync(item => item.Id == threadId, cancellationToken);
                return Continued(currentThread, duplicate);
            }
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Обращение уже обновилось",
                detail: "Откройте обращение заново перед продолжением.");
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var duplicate = await database.Appeals.AsNoTracking()
                .SingleOrDefaultAsync(cycle => cycle.ThreadId == threadId
                    && cycle.ClientContinuationId == request.ClientContinuationId,
                    cancellationToken);
            if (duplicate is null) throw;
            var currentThread = await database.AppealThreads.AsNoTracking()
                .SingleAsync(item => item.Id == threadId, cancellationToken);
            return duplicate.Narrative == body
                ? Continued(currentThread, duplicate)
                : IdempotencyConflict();
        }

        return Continued(thread, next);
    }

    private static IResult Continued(AppealThread thread, Appeal cycle) => Results.Ok(new
    {
        threadVersion = thread.Version,
        cycleNumber = cycle.Sequence,
        status = cycle.Status.ToString(),
        statusText = StatusText(cycle.Status),
        createdAt = cycle.CreatedAt
    });

    private static async Task<IResult> AddApplicantMessageAsync(
        ApplicantMessageRequest request,
        HttpContext context,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        IDataProtectionProvider dataProtection,
        IHubContext<AppealUpdatesHub> updates,
        CancellationToken cancellationToken)
    {
        var body = NormalizeOptional(request.Body);
        if (request.ClientMessageId == Guid.Empty || body is null || body.Length < 2 || body.Length > 4_000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["Ответ должен содержать от 2 до 4 000 знаков."]
            });
        }

        var appealId = await ApplicantAppealAccess.ResolveAppealIdAsync(
            request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (appealId is null) return TrackNotFound();
        var appeal = await database.Appeals
            .Include(candidate => candidate.Messages)
            .Include(candidate => candidate.CrisisContact)
            .SingleOrDefaultAsync(candidate => candidate.Id == appealId, cancellationToken);
        if (appeal is null) return TrackNotFound();

        var replay = appeal.Messages.SingleOrDefault(message => message.ClientMessageId == request.ClientMessageId);
        if (replay is not null)
        {
            return replay.Author == AppealMessageAuthor.Applicant && replay.Body == body
                ? Results.Ok(new
                {
                    message = ApplicantMessagePayload(replay, appeal.ApplicantType),
                    status = appeal.Status.ToString(),
                    statusText = StatusText(appeal.Status)
                })
                : IdempotencyConflict();
        }

        if (appeal.Status != AppealStatus.NeedsClarification)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Ответ сейчас не ожидается",
                detail: "Обновите статус обращения перед отправкой.");
        }

        var crisis = await DetectCrisisAsync(database, [body], cancellationToken);
        var crisisContact = NormalizeOptional(request.CrisisContact);
        if (crisisContact is not null && !crisis.IsMatch && !appeal.CrisisFlag)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["crisisContact"] = ["Способ связи не нужен для обычного ответа и не был сохранен."]
            });
        }
        if (crisisContact is not null && (crisisContact.Length < 5 || crisisContact.Length > 200))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["crisisContact"] = ["Укажите от 5 до 200 знаков или отправьте ответ без контакта."]
            });
        }

        var now = DateTimeOffset.UtcNow;
        var message = new AppealMessage
        {
            Id = Guid.NewGuid(),
            AppealId = appeal.Id,
            ClientMessageId = request.ClientMessageId,
            Author = AppealMessageAuthor.Applicant,
            Body = body,
            CreatedAt = now
        };
        appeal.Status = AppealStatus.InProgress;
        appeal.Version++;
        database.AppealMessages.Add(message);
        database.AppealStatusChanges.Add(new AppealStatusChange
        {
            Id = Guid.NewGuid(),
            AppealId = appeal.Id,
            Status = AppealStatus.InProgress,
            Source = "Applicant",
            ChangedAt = now
        });
        if (crisis.IsMatch)
        {
            appeal.CrisisFlag = true;
            appeal.CrisisDetectedAt ??= now;
            var hasUnresolvedMessageAlert = await database.AdminAlerts.AnyAsync(
                alert => alert.AppealId == appeal.Id
                    && alert.Type == "CrisisDetectedInMessage"
                    && alert.ResolvedAt == null,
                cancellationToken);
            if (!hasUnresolvedMessageAlert)
            {
                database.AdminAlerts.Add(new AdminAlert
                {
                    Id = Guid.NewGuid(),
                    AppealId = appeal.Id,
                    Type = "CrisisDetectedInMessage",
                    CreatedAt = now
                });
            }
        }
        if ((crisis.IsMatch || appeal.CrisisFlag) && crisisContact is not null && appeal.CrisisContact is null)
        {
            appeal.CrisisContact = new AppealCrisisContact
            {
                AppealId = appeal.Id,
                Ciphertext = dataProtection.CreateProtector(CrisisContactPurpose).Protect(crisisContact),
                CreatedAt = now
            };
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Обращение уже обновилось",
                detail: "Проверьте новые сообщения перед повторной отправкой.");
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var duplicate = await database.AppealMessages.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.AppealId == appeal.Id
                    && candidate.ClientMessageId == request.ClientMessageId,
                cancellationToken);
            if (duplicate is null) throw;
            return duplicate.Author == AppealMessageAuthor.Applicant && duplicate.Body == body
                ? Results.Ok(new
                {
                    message = ApplicantMessagePayload(duplicate, appeal.ApplicantType),
                    status = AppealStatus.InProgress.ToString(),
                    statusText = StatusText(AppealStatus.InProgress)
                })
                : IdempotencyConflict();
        }

        await ExpertWorkUpdateNotifier.NotifyAsync(
            updates,
            database,
            appeal.Id,
            appeal.Status.ToString(),
            cancellationToken);
        return Results.Created(
            $"/api/public/appeals/messages/{message.Id}",
            new
            {
                message = ApplicantMessagePayload(message, appeal.ApplicantType),
                status = appeal.Status.ToString(),
                statusText = StatusText(appeal.Status)
            });
    }

    private static object ApplicantMessagePayload(AppealMessage message, ApplicantType applicantType) => new
    {
        message.Id,
        author = message.Author.ToString(),
        authorLabel = message.Author == AppealMessageAuthor.Expert
            ? "Специалист"
            : applicantType == ApplicantType.Student ? "Ты" : "Вы",
        message.Body,
        message.CreatedAt
    };

    private static IResult IdempotencyConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Команда уже использована",
        detail: "Повторите отправку с новым идентификатором.");

    private static async Task<IResult> UploadAttachmentAsync(
        HttpRequest request,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        IAttachmentSanitizer sanitizer,
        IPrivateAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return AttachmentProblem("Выберите файл для загрузки.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var trackNumber = form["trackNumber"].ToString();
        var file = form.Files.GetFile("file");
        if (!trackNumbers.TryNormalize(trackNumber, out var normalized))
        {
            return TrackNotFound();
        }

        if (!Guid.TryParse(form["clientUploadId"].ToString(), out var clientUploadId)
            || clientUploadId == Guid.Empty
            || file is null)
        {
            return AttachmentProblem("Не удалось подготовить файл. Выберите его еще раз.");
        }

        var hash = trackNumbers.Hash(normalized);
        var currentAppealId = await database.AppealThreads
            .Where(thread => thread.TrackHash == hash)
            .Select(thread => thread.CurrentCycleId)
            .SingleOrDefaultAsync(cancellationToken);
        var appeal = currentAppealId is null
            ? null
            : await database.Appeals
                .Include(candidate => candidate.Attachments)
                .SingleOrDefaultAsync(candidate => candidate.Id == currentAppealId, cancellationToken);
        if (appeal is null)
        {
            return TrackNotFound();
        }

        var replay = appeal.Attachments.SingleOrDefault(attachment => attachment.ClientUploadId == clientUploadId);
        if (replay is not null)
        {
            return Results.Created(
                $"/api/public/appeals/attachments/{replay.Id}/download",
                AttachmentResponse(replay));
        }

        if (appeal.Attachments.Count >= MaxAttachmentCount)
        {
            return AttachmentProblem("К одному обращению можно прикрепить не больше пяти файлов.");
        }

        SanitizedAttachment sanitized;
        try
        {
            await using var source = file.OpenReadStream();
            sanitized = await sanitizer.SanitizeAsync(
                source,
                file.Length,
                file.FileName,
                file.ContentType,
                cancellationToken);
        }
        catch (AttachmentValidationException exception)
        {
            return AttachmentProblem(exception.Message);
        }

        var storageKey = await storage.StoreAsync(
            sanitized.Content,
            sanitized.Extension,
            cancellationToken);
        var attachment = new AppealAttachment
        {
            Id = Guid.NewGuid(),
            AppealId = appeal.Id,
            ClientUploadId = clientUploadId,
            DisplayName = GenericDisplayName(
                sanitized.ContentType,
                sanitized.Extension,
                appeal.Attachments.Count + 1),
            ContentType = sanitized.ContentType,
            Size = sanitized.Content.LongLength,
            StorageKey = storageKey,
            CreatedAt = DateTimeOffset.UtcNow
        };
        database.AppealAttachments.Add(attachment);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return Results.Created(
            $"/api/public/appeals/attachments/{attachment.Id}/download",
            AttachmentResponse(attachment));
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid attachmentId,
        TrackLookupRequest request,
        HttpContext context,
        HttpResponse response,
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        DeviceCapabilityService deviceCapabilities,
        IPrivateAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        var threadId = await ApplicantAppealAccess.ResolveThreadIdAsync(
            request.TrackNumber, context, database, trackNumbers, deviceCapabilities, cancellationToken);
        if (threadId is null) return TrackNotFound();
        var attachment = await database.AppealAttachments
            .AsNoTracking()
            .Include(candidate => candidate.Appeal)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == attachmentId && candidate.Appeal.ThreadId == threadId,
                cancellationToken);
        if (attachment is null)
        {
            return TrackNotFound();
        }

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

    private static async Task<ValidationResult> ValidateAsync(
        CreateAppealRequest request,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ClientRequestId == Guid.Empty)
        {
            errors["clientRequestId"] = ["Не удалось подготовить отправку. Обновите страницу и попробуйте снова."];
        }

        var hasApplicantType = Enum.TryParse<ApplicantType>(request.ApplicantType, true, out var applicantType)
            && Enum.IsDefined(applicantType);
        if (!hasApplicantType)
        {
            errors["applicantType"] = ["Выберите, кто оставляет обращение."];
        }

        var hasSubmissionPath = Enum.TryParse<SubmissionPath>(request.SubmissionPath, true, out var submissionPath)
            && Enum.IsDefined(submissionPath);
        if (!hasSubmissionPath)
        {
            errors["submissionPath"] = ["Выберите удобный способ рассказать о ситуации."];
        }

        AppealCategory? category = null;
        if (hasSubmissionPath && submissionPath == SubmissionPath.Category)
        {
            category = await database.AppealCategories
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == request.CategoryId && candidate.IsActive,
                    cancellationToken);
            if (category is null)
            {
                errors["categoryId"] = ["Выберите подходящую категорию."];
            }
        }

        var narrative = request.Narrative?.Trim();
        var narrativeRequired = hasSubmissionPath && submissionPath == SubmissionPath.FreeText
            || category?.Code == "unsure";
        if (narrativeRequired && (narrative is null || narrative.Length < 10))
        {
            errors["narrative"] = ["Расскажите немного подробнее — достаточно нескольких предложений."];
        }
        else if (narrative?.Length > 10_000)
        {
            errors["narrative"] = ["Текст слишком длинный. Оставьте, пожалуйста, не больше 10 000 знаков."];
        }

        if (request.Answers is not null)
        {
            var meaningfulAnswers = request.Answers
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToArray();
            if (meaningfulAnswers.Length > AllowedQuestionCodes.Count
                || meaningfulAnswers.Any(pair => !AllowedQuestionCodes.Contains(pair.Key)))
            {
                errors["answers"] = ["В уточняющих ответах есть неизвестный вопрос."];
            }
            else if (meaningfulAnswers.Any(pair => pair.Value.Trim().Length > 1_000))
            {
                errors["answers"] = ["Один из уточняющих ответов слишком длинный."];
            }
        }

        var crisis = await DetectCrisisAsync(
            database,
            [narrative, .. request.Answers?.Values ?? []],
            cancellationToken);
        var crisisContact = NormalizeOptional(request.CrisisContact);
        if (crisisContact is not null && !crisis.IsMatch)
        {
            errors["crisisContact"] = ["Способ связи не нужен для обычного обращения и не был сохранен."];
        }
        else if (crisisContact is not null && (crisisContact.Length < 5 || crisisContact.Length > 200))
        {
            errors["crisisContact"] = ["Укажите от 5 до 200 знаков или отправьте обращение без контакта."];
        }

        return new ValidationResult(errors, applicantType, submissionPath, category, crisis);
    }

    private static async Task<CrisisDetectionResult> DetectCrisisAsync(
        OtklikDbContext database,
        IEnumerable<string?> textParts,
        CancellationToken cancellationToken)
    {
        var markers = await database.CrisisMarkers
            .AsNoTracking()
            .Where(marker => marker.IsActive)
            .OrderBy(marker => marker.SortOrder)
            .Select(marker => new CrisisMarkerDefinition(marker.Pattern, marker.RiskType))
            .ToListAsync(cancellationToken);
        return CrisisTextMatcher.Detect(textParts, markers);
    }

    private static async Task<object[]> GetCrisisSupportAsync(
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var configured = await database.CrisisSupportContacts
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
            .ToArrayAsync(cancellationToken);
        return configured.Cast<object>().ToArray();
    }

    private static async Task<string> GenerateUniqueTrackNumberAsync(
        OtklikDbContext database,
        ITrackNumberService trackNumbers,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = trackNumbers.Generate();
            var hash = trackNumbers.Hash(candidate);
            if (!await database.AppealThreads.AnyAsync(thread => thread.TrackHash == hash, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique track number.");
    }

    private static List<AppealAnswer> NormalizeAnswers(IReadOnlyDictionary<string, string>? answers) =>
        answers?
            .Where(pair => AllowedQuestionCodes.Contains(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new AppealAnswer
            {
                Id = Guid.NewGuid(),
                QuestionCode = pair.Key,
                Value = pair.Value.Trim()
            })
            .ToList() ?? [];

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult Created(Appeal appeal, string trackNumber) => Results.Created(
        "/api/public/appeals/status",
        new
        {
            appealId = appeal.Id,
            trackNumber,
            status = appeal.Status.ToString(),
            statusText = StatusText(appeal.Status),
            createdAt = appeal.CreatedAt
        });

    private static IResult TrackNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Не удалось найти обращение",
        detail: "Проверьте трек-номер. Если номер потерян, его нельзя восстановить — можно оставить новое обращение.");

    private static IResult TooManyTrackAttempts(HttpResponse response, int retryAfterSeconds)
    {
        response.Headers.RetryAfter = Math.Max(1, retryAfterSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Слишком много попыток",
            detail: "Подождите немного и попробуйте снова. Это защищает обращения от подбора номера.");
    }

    private static IResult AttachmentProblem(string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["attachment"] = [message] });

    private static object AttachmentResponse(AppealAttachment attachment) => new
    {
        attachment.Id,
        attachment.DisplayName,
        attachment.ContentType,
        attachment.Size,
        attachment.CreatedAt
    };

    private static string GenericDisplayName(string contentType, string extension, int number) =>
        contentType.StartsWith("image/", StringComparison.Ordinal)
            ? $"Изображение {number}{extension}"
            : $"Документ {number}.pdf";

    private static string StatusText(AppealStatus status) => status switch
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

    private sealed record CreateAppealRequest(
        Guid ClientRequestId,
        string? ApplicantType,
        string? SubmissionPath,
        Guid? CategoryId,
        string? Narrative,
        IReadOnlyDictionary<string, string>? Answers,
        string? CrisisContact);

    private sealed record TrackLookupRequest(string? TrackNumber);

    private sealed record ContinueAppealRequest(
        string? TrackNumber,
        Guid ClientContinuationId,
        int? ExpectedThreadVersion,
        string? Body,
        string? CrisisContact);

    private sealed record ApplicantMessageRequest(
        string? TrackNumber,
        Guid ClientMessageId,
        string? Body,
        string? CrisisContact);

    private sealed record ValidationResult(
        Dictionary<string, string[]> Errors,
        ApplicantType ApplicantType,
        SubmissionPath SubmissionPath,
        AppealCategory? Category,
        CrisisDetectionResult Crisis);
}
