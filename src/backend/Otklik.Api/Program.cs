using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Otklik.Api.Endpoints;
using Otklik.Api.Hubs;
using Otklik.Api.Security;
using Otklik.Application.Security;
using Otklik.Infrastructure;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
var dataProtectionKeysPath = builder.Configuration["Security:DataProtectionKeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".keys");
builder.Services
    .AddDataProtection()
    .SetApplicationName("Otklik")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddOtklikInfrastructure(builder.Configuration);
builder.Services.AddOtklikStaffIdentity();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("public-global", context =>
    {
        var limiter = context.RequestServices.GetRequiredService<TrackLookupRateLimiter>();
        var partitionKey = limiter.GetAddressDigest(ClientRequestIdentity.Address(context));
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 600,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.ApplicationScheme,
    options =>
    {
        options.Cookie.Name = "otklik.staff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "otklik.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(StaffPolicies.OperatorOnly, policy => policy.RequireRole(StaffRoles.Operator))
    .AddPolicy(StaffPolicies.ExpertOnly, policy => policy.RequireRole(StaffRoles.Expert))
    .AddPolicy(StaffPolicies.AdministratorOnly, policy => policy.RequireRole(StaffRoles.Administrator));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        var forwardedHttps = string.Equals(
            context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault(),
            "https",
            StringComparison.OrdinalIgnoreCase);
        if (!app.Environment.IsDevelopment() && (context.Request.IsHttps || forwardedHttps))
        {
            headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        }
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers.CacheControl = "no-store, max-age=0";
            headers.Pragma = "no-cache";
        }

        return Task.CompletedTask;
    });
    await next();
});

app.UseExceptionHandler();

await app.Services.InitializeOtklikDatabaseAsync(app.Environment);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

app.MapGet("/api", () => Results.Ok(new
{
    service = "Отклик API",
    status = "running",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "development"
}));

app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
});

app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

app.MapGet("/api/system/status", async (
    HealthCheckService healthChecks,
    CancellationToken cancellationToken) =>
{
    var report = await healthChecks.CheckHealthAsync(
        registration => registration.Tags.Contains("infrastructure"),
        cancellationToken);

    return Results.Json(ToPayload(report), statusCode: report.Status == HealthStatus.Healthy ? 200 : 503);
});

app.MapStaffEndpoints();
app.MapOperatorWorkEndpoints();
app.MapOperatorEndpoints();
app.MapExpertEndpoints();
app.MapCollaborationEndpoints();
app.MapApplicantOutcomeEndpoints();
app.MapOperatorLifecycleEndpoints();
app.MapOperatorCrisisEndpoints();
app.MapAdminEndpoints();
app.MapAnalyticsEndpoints();
app.MapPublicAppealEndpoints();
app.MapDeviceSessionEndpoints();
app.MapHub<AppealUpdatesHub>("/hubs/appeals");

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    return context.Response.WriteAsync(JsonSerializer.Serialize(ToPayload(report)));
}

static object ToPayload(HealthReport report) => new
{
    status = report.Status.ToString().ToLowerInvariant(),
    checkedAt = DateTimeOffset.UtcNow,
    durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
    components = report.Entries.ToDictionary(
        pair => pair.Key,
        pair => new
        {
            status = pair.Value.Status.ToString().ToLowerInvariant(),
            description = pair.Value.Description,
            durationMs = Math.Round(pair.Value.Duration.TotalMilliseconds, 1),
            data = pair.Value.Data
        })
};

public partial class Program;
