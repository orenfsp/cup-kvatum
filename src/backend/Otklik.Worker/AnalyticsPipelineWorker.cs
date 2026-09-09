using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Analytics;
using Otklik.Domain.Analytics;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Worker;

public sealed class AnalyticsPipelineWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AnalyticsPipelineWorker> logger) : BackgroundService
{
    private const string DefaultTopic = "otklik.appeal-analytics.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string bootstrapServers = configuration.GetConnectionString("Kafka")
        ?? throw new InvalidOperationException("Connection string 'Kafka' is required.");
    private readonly string topic = configuration["Analytics:Topic"] ?? DefaultTopic;
    private bool historicalBackfillComplete;
    private DateTimeOffset nextExportCleanupAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureTopicAsync(stoppingToken);
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = "otklik-analytics-outbox",
            EnableIdempotence = true,
            Acks = Acks.All
        }).Build();
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = configuration["Analytics:ConsumerGroup"] ?? "otklik-analytics-projection-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        }).Build();
        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!historicalBackfillComplete)
                {
                    historicalBackfillComplete = await QueueHistoricalSnapshotsAsync(stoppingToken);
                }
                await PublishOutboxAsync(producer, stoppingToken);
                await ConsumeAvailableAsync(consumer, stoppingToken);
                if (DateTimeOffset.UtcNow >= nextExportCleanupAt)
                {
                    await CleanupExpiredExportsAsync(stoppingToken);
                    nextExportCleanupAt = DateTimeOffset.UtcNow.AddMinutes(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Analytics pipeline pass failed and will be retried");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        consumer.Close();
    }

    private async Task EnsureTopicAsync(CancellationToken cancellationToken)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = "otklik-analytics-topic"
        }).Build();
        try
        {
            await admin.CreateTopicsAsync([
                new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }
            ]);
        }
        catch (CreateTopicsException exception) when (
            exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Existing topic is the expected state after the first start.
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<bool> QueueHistoricalSnapshotsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var appeals = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.Messages)
            .Include(item => item.Recommendations)
            .Include(item => item.OperatorActions)
            .Where(appeal => !database.AnalyticsOutboxMessages.Any(message => message.AppealId == appeal.Id))
            .OrderBy(item => item.CreatedAt)
            .Take(100)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        if (appeals.Count == 0) return true;

        foreach (var appeal in appeals)
        {
            var eventId = Guid.NewGuid();
            var firstOperatorAction = appeal.OperatorActions.OrderBy(item => item.OccurredAt).FirstOrDefault();
            var analyticsEvent = new AppealAnalyticsEvent
            {
                EventId = eventId,
                AppealId = appeal.Id,
                AppealVersion = appeal.Version,
                OccurredAt = appeal.CreatedAt,
                ApplicantType = appeal.ApplicantType.ToString(),
                CategoryId = appeal.CategoryId,
                ExpertGroupId = appeal.AppliedExpertGroupId,
                Status = appeal.Status.ToString(),
                Priority = appeal.Priority.ToString(),
                CreatedAt = appeal.CreatedAt,
                OperatorAcceptedAt = appeal.StatusHistory
                    .Where(item => item.Source == "Operator")
                    .Select(item => (DateTimeOffset?)item.ChangedAt)
                    .Min(),
                FirstExpertResponseAt = appeal.Messages
                    .Where(item => item.Author == AppealMessageAuthor.Expert)
                    .Select(item => (DateTimeOffset?)item.CreatedAt)
                    .Concat(appeal.Recommendations.Select(item => (DateTimeOffset?)item.CreatedAt))
                    .Min(),
                ClosedAt = appeal.CompletedAt,
                OperatorUserId = firstOperatorAction?.ActorUserId,
                AssignedExpertId = appeal.AssignedExpertId,
                CrisisFlag = appeal.CrisisFlag,
                ReturnCount = appeal.ReturnCount
            };
            database.AnalyticsOutboxMessages.Add(new AnalyticsOutboxMessage
            {
                Id = eventId,
                AppealId = appeal.Id,
                AppealVersion = appeal.Version,
                EventType = AppealAnalyticsEvent.Type,
                PayloadJson = JsonSerializer.Serialize(analyticsEvent, Json),
                OccurredAt = appeal.CreatedAt
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued {Count} historical analytics snapshots", appeals.Count);
        return appeals.Count < 100;
    }

    private async Task PublishOutboxAsync(
        IProducer<string, string> producer,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var messages = await database.AnalyticsOutboxMessages
            .Where(item => item.PublishedAt == null)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = message.AppealId.ToString("D"),
                    Value = message.PayloadJson,
                    Headers = new Headers
                    {
                        { "event-type", System.Text.Encoding.UTF8.GetBytes(message.EventType) },
                        { "event-id", System.Text.Encoding.UTF8.GetBytes(message.Id.ToString("D")) }
                    }
                }, cancellationToken);
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.PublishAttempts++;
                message.LastPublishError = null;
            }
            catch (ProduceException<string, string> exception)
            {
                message.PublishAttempts++;
                message.LastPublishError = exception.Error.Reason.Length > 1_000
                    ? exception.Error.Reason[..1_000]
                    : exception.Error.Reason;
                break;
            }
        }

        if (messages.Count > 0) await database.SaveChangesAsync(cancellationToken);
    }

    private async Task ConsumeAvailableAsync(
        IConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < 100; index++)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(50));
            if (result is null) return;
            var analyticsEvent = JsonSerializer.Deserialize<AppealAnalyticsEvent>(result.Message.Value, Json)
                ?? throw new InvalidOperationException("Analytics event payload is empty.");
            if (analyticsEvent.SchemaVersion != AppealAnalyticsEvent.CurrentSchemaVersion
                || analyticsEvent.EventId == Guid.Empty
                || analyticsEvent.AppealId == Guid.Empty)
            {
                throw new InvalidOperationException("Analytics event schema is not supported.");
            }

            await ApplyProjectionAsync(analyticsEvent, cancellationToken);
            consumer.Commit(result);
        }
    }

    private async Task ApplyProjectionAsync(
        AppealAnalyticsEvent analyticsEvent,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await database.ProcessedAnalyticsEvents.AnyAsync(
                item => item.EventId == analyticsEvent.EventId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var projection = await database.AppealAnalyticsProjections.SingleOrDefaultAsync(
            item => item.AppealId == analyticsEvent.AppealId,
            cancellationToken);
        if (projection is null)
        {
            projection = AppealAnalyticsProjector.Apply(null, analyticsEvent);
            database.AppealAnalyticsProjections.Add(projection);
        }
        else
        {
            AppealAnalyticsProjector.Apply(projection, analyticsEvent);
        }

        database.ProcessedAnalyticsEvents.Add(new ProcessedAnalyticsEvent
        {
            EventId = analyticsEvent.EventId,
            AppealId = analyticsEvent.AppealId,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CleanupExpiredExportsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var expired = await database.AnalyticsExports
            .Where(item => item.DownloadedAt != null || item.ExpiresAt < DateTimeOffset.UtcNow)
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var export in expired)
        {
            if (File.Exists(export.FilePath)) File.Delete(export.FilePath);
            export.DownloadedAt ??= export.ExpiresAt;
        }
        if (expired.Count > 0) await database.SaveChangesAsync(cancellationToken);
    }
}
