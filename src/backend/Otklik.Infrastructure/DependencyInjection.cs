using Confluent.Kafka;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Otklik.Application.Appeals;
using Otklik.Application.Attachments;
using Otklik.Application.Security;
using Otklik.Infrastructure.Attachments;
using Otklik.Infrastructure.Diagnostics;
using Otklik.Infrastructure.Identity;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;
using StackExchange.Redis;

namespace Otklik.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOtklikInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgres = RequiredConnectionString(configuration, "Postgres");
        var redis = RequiredConnectionString(configuration, "Redis");
        var kafka = RequiredConnectionString(configuration, "Kafka");

        services.AddSingleton(_ => NpgsqlDataSource.Create(postgres));
        services.AddDbContext<OtklikDbContext>(options =>
        {
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
            options.UseNpgsql(
                postgres,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "otklik"));
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(redis)));

        services.AddSingleton<IAdminClient>(_ => new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = kafka,
            ClientId = "otklik-platform-health"
        }).Build());
        services.AddSingleton<ITrackNumberService, TrackNumberService>();
        services.AddSingleton<TrackLookupRateLimiter>();
        services.AddScoped<DeviceCapabilityService>();
        services.AddSingleton<ICategorySuggestionService, CategorySuggestionService>();
        services.AddSingleton<IAttachmentSanitizer, AttachmentSanitizer>();
        services.AddSingleton<IPrivateAttachmentStorage, FileSystemAttachmentStorage>();

        services
            .AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready", "infrastructure"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready", "infrastructure"])
            .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready", "infrastructure"]);

        return services;
    }

    public static IdentityBuilder AddOtklikStaffIdentity(this IServiceCollection services)
    {
        services.AddScoped<StaffIdentitySeeder>();
        services.AddScoped<OperatorDemoDataSeeder>();

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.Zero;
        });

        return services
            .AddIdentityCore<StaffUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<OtklikDbContext>()
            .AddSignInManager();
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is required.");
}
