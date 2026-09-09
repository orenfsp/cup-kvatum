using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Appeals;
using Otklik.Infrastructure.Persistence;
using WebPush;

namespace Otklik.Worker;

public sealed class PushNotificationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IDataProtectionProvider dataProtection,
    ILogger<PushNotificationWorker> logger) : BackgroundService
{
    private const string SubscriptionProtectionPurpose = "AppealPushSubscription.v1";
    private readonly string publicKey = configuration["Push:PublicKey"] ?? string.Empty;
    private readonly string privateKey = configuration["Push:PrivateKey"] ?? string.Empty;
    private readonly string subject = configuration["Push:Subject"] ?? "mailto:dev@otklik.local";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            logger.LogWarning("Push delivery is disabled because VAPID keys are not configured");
            return;
        }

        using var client = new WebPushClient();
        var vapid = new VapidDetails(subject, publicKey, privateKey);
        var protector = dataProtection.CreateProtector(SubscriptionProtectionPurpose);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(client, vapid, protector, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                logger.LogWarning("Push delivery pass failed and will be retried");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(
        WebPushClient client,
        VapidDetails vapid,
        IDataProtector protector,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var items = await database.AppealNotificationOutboxes
            .Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.OccurredAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var threadId = await database.Appeals
                .Where(appeal => appeal.Id == item.AppealId)
                .Select(appeal => (Guid?)appeal.ThreadId)
                .SingleOrDefaultAsync(cancellationToken);
            var subscriptions = await database.AppealPushSubscriptions
                .Where(subscription => threadId != null
                    && subscription.ThreadId == threadId
                    && subscription.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var stored in subscriptions)
            {
                try
                {
                    var subscription = new PushSubscription(
                        protector.Unprotect(stored.EndpointCiphertext),
                        protector.Unprotect(stored.P256dhCiphertext),
                        protector.Unprotect(stored.AuthCiphertext));
                    await client.SendNotificationAsync(
                        subscription,
                        PushNotificationContract.PayloadJson,
                        vapid,
                        cancellationToken);
                    stored.LastDeliveryAt = DateTimeOffset.UtcNow;
                    stored.FailureCount = 0;
                }
                catch (WebPushException exception) when (
                    exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                {
                    stored.FailureCount++;
                    stored.RevokedAt = DateTimeOffset.UtcNow;
                    logger.LogInformation(
                        "Expired push subscription {SubscriptionId} was revoked",
                        stored.Id);
                }
                catch
                {
                    stored.FailureCount++;
                    if (stored.FailureCount >= 5) stored.RevokedAt = DateTimeOffset.UtcNow;
                    logger.LogWarning(
                        "Push delivery failed for subscription {SubscriptionId}; failure {FailureCount}",
                        stored.Id,
                        stored.FailureCount);
                }
            }

            item.ProcessedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
