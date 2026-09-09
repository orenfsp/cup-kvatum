using Microsoft.EntityFrameworkCore;
using Otklik.Application.Appeals;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Worker;

public sealed class AppealLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AppealLifecycleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Appeal lifecycle pass could not be completed and will be retried");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var setting = await database.PlatformSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == PlatformSetting.AutoCloseDaysKey, cancellationToken);
        var autoCloseDays = int.TryParse(setting?.Value, out var configured) ? Math.Clamp(configured, 1, 365) : 7;
        var appeals = await database.Appeals
            .Include(item => item.Recommendations)
            .Where(item => item.Status == AppealStatus.RecommendationReady)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var closed = 0;
        var reviews = 0;
        foreach (var appeal in appeals)
        {
            var recommendationAt = appeal.Recommendations.MaxBy(item => item.Version)?.CreatedAt;
            switch (AppealAutoClosePolicy.Evaluate(
                appeal.Status, appeal.CrisisFlag, recommendationAt, now, autoCloseDays))
            {
                case AppealAutoCloseAction.Close:
                    appeal.Status = AppealStatus.Closed;
                    appeal.CompletedAt = now;
                    appeal.PublicResolution = "Обращение закрыто, потому что в течение установленного срока не поступило ответа. Если помощь все еще нужна, можно написать снова.";
                    appeal.Version++;
                    database.AppealStatusChanges.Add(new AppealStatusChange
                    {
                        Id = Guid.NewGuid(), AppealId = appeal.Id, Status = AppealStatus.Closed,
                        Source = "System", ChangedAt = now
                    });
                    closed++;
                    break;
                case AppealAutoCloseAction.CreateCrisisReview:
                    if (!await database.AdminAlerts.AnyAsync(alert => alert.AppealId == appeal.Id
                        && alert.Type == "CrisisNoResponseReview" && alert.ResolvedAt == null, cancellationToken))
                    {
                        database.AdminAlerts.Add(new AdminAlert
                        {
                            Id = Guid.NewGuid(), AppealId = appeal.Id,
                            Type = "CrisisNoResponseReview", CreatedAt = now
                        });
                        reviews++;
                    }
                    break;
            }
        }
        if (closed > 0 || reviews > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Appeal lifecycle pass closed {ClosedCount} and created {ReviewCount} manual reviews", closed, reviews);
        }
    }
}
