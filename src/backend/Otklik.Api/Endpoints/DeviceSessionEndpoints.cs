using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Appeals;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;
using Otklik.Infrastructure.Security;

namespace Otklik.Api.Endpoints;

public static class DeviceSessionEndpoints
{
    private const string SubscriptionProtectionPurpose = "AppealPushSubscription.v1";

    public static IEndpointRouteBuilder MapDeviceSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var device = endpoints.MapGroup("/api/public/device-session").RequireRateLimiting("public-global");
        device.MapGet("", GetLatestAppealAsync);
        device.MapGet("/csrf", GetCsrf);
        device.MapGet("/push-config", GetPushConfigAsync);
        device.MapPost("/push-subscriptions", SubscribeAsync);
        device.MapPost("/push-subscriptions/revoke", RevokeSubscriptionAsync);
        device.MapPost("/notifications/test", QueueTestNotificationAsync);
        device.MapPost("/remove", RemoveAsync);
        return endpoints;
    }

    private static IResult GetCsrf(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken });
    }

    private static async Task<IResult> GetLatestAppealAsync(
        HttpContext context,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        NoStore(context.Response);
        var capability = await capabilities.ResolveAsync(context, cancellationToken);
        if (capability is null) return Results.NoContent();
        var thread = await database.AppealThreads.AsNoTracking().WithPublicDetails()
            .SingleOrDefaultAsync(item => item.Id == capability.ThreadId, cancellationToken);
        return thread is null
            ? Results.NoContent()
            : Results.Ok(await PublicAppealStatusPayload.CreateAsync(database, thread, cancellationToken));
    }

    private static async Task<IResult> GetPushConfigAsync(
        HttpContext context,
        IConfiguration configuration,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        NoStore(context.Response);
        var capability = await capabilities.ResolveAsync(context, cancellationToken);
        if (capability is null) return MissingCapability();
        var subscribed = await database.AppealPushSubscriptions.AsNoTracking().AnyAsync(
            item => item.DeviceSessionId == capability.Session.Id
                && item.ThreadId == capability.ThreadId
                && item.RevokedAt == null,
            cancellationToken);
        return Results.Ok(new
        {
            publicKey = configuration["Push:PublicKey"] ?? string.Empty,
            subscribed,
            notificationTitle = PushNotificationContract.Title,
            notificationBody = PushNotificationContract.Body
        });
    }

    private static async Task<IResult> SubscribeAsync(
        PushSubscriptionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtection,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        var capability = await capabilities.ResolveAsync(context, cancellationToken);
        if (capability is null) return MissingCapability();
        if (!TryValidateSubscription(request, out var endpoint))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Браузер вернул некорректную подписку. Отключите уведомления и попробуйте снова."]
            });
        }

        var endpointHash = SHA256.HashData(Encoding.UTF8.GetBytes(endpoint));
        var existing = await database.AppealPushSubscriptions.SingleOrDefaultAsync(
            item => item.EndpointHash == endpointHash,
            cancellationToken);
        if (existing is not null && existing.DeviceSessionId != capability.Session.Id)
        {
            return Results.Conflict(new
            {
                detail = "Эта подписка относится к другой локальной сессии. Удалите разрешение в браузере и включите его снова."
            });
        }

        var protector = dataProtection.CreateProtector(SubscriptionProtectionPurpose);
        if (existing is null)
        {
            existing = new AppealPushSubscription
            {
                Id = Guid.NewGuid(),
                DeviceSessionId = capability.Session.Id,
                ThreadId = capability.ThreadId,
                EndpointHash = endpointHash,
                EndpointCiphertext = protector.Protect(endpoint),
                P256dhCiphertext = protector.Protect(request.Keys.P256dh),
                AuthCiphertext = protector.Protect(request.Keys.Auth),
                CreatedAt = DateTimeOffset.UtcNow
            };
            database.AppealPushSubscriptions.Add(existing);
        }
        else
        {
            existing.ThreadId = capability.ThreadId;
            existing.EndpointCiphertext = protector.Protect(endpoint);
            existing.P256dhCiphertext = protector.Protect(request.Keys.P256dh);
            existing.AuthCiphertext = protector.Protect(request.Keys.Auth);
            existing.FailureCount = 0;
            existing.RevokedAt = null;
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { subscribed = true });
    }

    private static async Task<IResult> RevokeSubscriptionAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        var capability = await capabilities.ResolveAsync(context, cancellationToken);
        if (capability is null) return MissingCapability();
        var now = DateTimeOffset.UtcNow;
        var subscriptions = await database.AppealPushSubscriptions
            .Where(item => item.DeviceSessionId == capability.Session.Id
                && item.ThreadId == capability.ThreadId
                && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions) subscription.RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { subscribed = false });
    }

    private static async Task<IResult> QueueTestNotificationAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        var capability = await capabilities.ResolveAsync(context, cancellationToken);
        if (capability is null) return MissingCapability();
        var subscribed = await database.AppealPushSubscriptions.AnyAsync(
            item => item.DeviceSessionId == capability.Session.Id
                && item.ThreadId == capability.ThreadId
                && item.RevokedAt == null,
            cancellationToken);
        if (!subscribed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Уведомления выключены",
                detail: "Сначала разрешите нейтральные уведомления на этом устройстве.");
        }

        var currentCycleId = await database.AppealThreads
            .Where(item => item.Id == capability.ThreadId)
            .Select(item => item.CurrentCycleId)
            .SingleOrDefaultAsync(cancellationToken);
        if (currentCycleId is null) return MissingCapability();
        database.AppealNotificationOutboxes.Add(new AppealNotificationOutbox
        {
            Id = Guid.NewGuid(),
            AppealId = currentCycleId.Value,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted(value: new { queued = true });
    }

    private static async Task<IResult> RemoveAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        OtklikDbContext database,
        DeviceCapabilityService capabilities,
        CancellationToken cancellationToken)
    {
        var csrf = await ValidateAntiforgeryAsync(context, antiforgery);
        if (csrf is not null) return csrf;
        var session = await capabilities.ResolveSessionAsync(context, cancellationToken);
        if (session is not null)
        {
            var now = DateTimeOffset.UtcNow;
            session.RevokedAt = now;
            foreach (var grant in session.Grants.Where(item => item.RevokedAt == null)) grant.RevokedAt = now;
            var subscriptions = await database.AppealPushSubscriptions
                .Where(item => item.DeviceSessionId == session.Id && item.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions) subscription.RevokedAt = now;
            await database.SaveChangesAsync(cancellationToken);
        }

        capabilities.ClearCookie(context);
        return Results.NoContent();
    }

    private static bool TryValidateSubscription(PushSubscriptionRequest request, out string endpoint)
    {
        endpoint = request.Endpoint?.Trim() ?? string.Empty;
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && endpoint.Length <= 2_048
            && request.Keys is not null
            && IsReasonableKey(request.Keys.P256dh)
            && IsReasonableKey(request.Keys.Auth);
    }

    private static bool IsReasonableKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 512;

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

    private static IResult MissingCapability() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Локальный доступ не найден",
        detail: "Введите сохраненный трек-номер, чтобы открыть обращение на этом устройстве.");

    private static void NoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store, max-age=0";
        response.Headers.Pragma = "no-cache";
    }

    private sealed record PushSubscriptionRequest(string? Endpoint, PushSubscriptionKeys Keys);
    private sealed record PushSubscriptionKeys(string P256dh, string Auth);
}
