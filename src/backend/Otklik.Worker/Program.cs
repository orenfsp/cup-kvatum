using Microsoft.AspNetCore.DataProtection;
using Otklik.Infrastructure;
using Otklik.Worker;

var builder = Host.CreateApplicationBuilder(args);
var dataProtectionKeysPath = builder.Configuration["Security:DataProtectionKeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".keys");
builder.Services
    .AddDataProtection()
    .SetApplicationName("Otklik")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddOtklikInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DependencyProbeWorker>();
builder.Services.AddHostedService<AppealLifecycleWorker>();
builder.Services.AddHostedService<AnalyticsPipelineWorker>();
builder.Services.AddHostedService<PushNotificationWorker>();

var host = builder.Build();
host.Run();
