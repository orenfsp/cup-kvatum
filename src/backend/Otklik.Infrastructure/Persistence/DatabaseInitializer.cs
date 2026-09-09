using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Otklik.Infrastructure.Identity;

namespace Otklik.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeOtklikDatabaseAsync(
        this IServiceProvider services,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OtklikDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OtklikDbContext>>();

        await database.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations are up to date");

        if (environment.IsDevelopment())
        {
            await scope.ServiceProvider
                .GetRequiredService<StaffIdentitySeeder>()
                .SeedAsync(cancellationToken);
            await scope.ServiceProvider
                .GetRequiredService<OperatorDemoDataSeeder>()
                .SeedAsync(cancellationToken);
        }
    }
}
