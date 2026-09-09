using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Infrastructure.Security;

public sealed class DeviceCapabilityService(OtklikDbContext database)
{
    public const string CookieName = "otklik.device";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    public async Task<PreparedDeviceGrant> PrepareGrantAsync(
        HttpContext context,
        Guid threadId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = await ResolveSessionAsync(context, cancellationToken);
        string? rawToken = null;
        if (session is null)
        {
            rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            session = new AppealDeviceSession
            {
                Id = Guid.NewGuid(),
                TokenHash = Hash(rawToken),
                CreatedAt = now,
                LastUsedAt = now,
                ExpiresAt = now.Add(Lifetime)
            };
            database.AppealDeviceSessions.Add(session);
        }
        else
        {
            session.LastUsedAt = now;
            session.ExpiresAt = now.Add(Lifetime);
        }

        var grant = session.Grants.SingleOrDefault(item => item.ThreadId == threadId);
        if (grant is null)
        {
            grant = new AppealDeviceGrant
            {
                DeviceSessionId = session.Id,
                ThreadId = threadId,
                GrantedAt = now
            };
            database.AppealDeviceGrants.Add(grant);
        }
        else
        {
            grant.RevokedAt = null;
            grant.GrantedAt = now;
        }

        return new PreparedDeviceGrant(session, rawToken);
    }

    public void ApplyCookie(HttpContext context, PreparedDeviceGrant prepared)
    {
        var token = prepared.RawToken ?? context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(token)) return;
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = IsSecureRequest(context),
            IsEssential = true,
            Path = "/",
            Expires = prepared.Session.ExpiresAt
        });
    }

    public async Task<DeviceCapability?> ResolveAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var session = await ResolveSessionAsync(context, cancellationToken);
        if (session is null) return null;
        var grant = session.Grants
            .Where(item => item.RevokedAt == null)
            .OrderByDescending(item => item.GrantedAt)
            .FirstOrDefault();
        return grant is null ? null : new DeviceCapability(session, grant.ThreadId);
    }

    public async Task<AppealDeviceSession?> ResolveSessionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var token = context.Request.Cookies[CookieName];
        if (!IsValidToken(token)) return null;
        var hash = Hash(token!);
        return await database.AppealDeviceSessions
            .Include(item => item.Grants)
            .SingleOrDefaultAsync(item => item.TokenHash == hash
                && item.RevokedAt == null
                && item.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);
    }

    public void ClearCookie(HttpContext context) => context.Response.Cookies.Delete(CookieName, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = IsSecureRequest(context),
        IsEssential = true,
        Path = "/"
    });

    private static bool IsSecureRequest(HttpContext context) =>
        context.Request.IsHttps
        || string.Equals(
            context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault(),
            "https",
            StringComparison.OrdinalIgnoreCase);

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static bool IsValidToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;
        try { return WebEncoders.Base64UrlDecode(value).Length == 32; }
        catch (FormatException) { return false; }
    }
}

public sealed record PreparedDeviceGrant(AppealDeviceSession Session, string? RawToken);
public sealed record DeviceCapability(AppealDeviceSession Session, Guid ThreadId);
