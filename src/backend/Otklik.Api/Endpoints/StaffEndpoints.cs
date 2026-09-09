using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Otklik.Application.Security;
using Otklik.Infrastructure.Identity;

namespace Otklik.Api.Endpoints;

public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapGroup("/api/staff");

        staff.MapGet("/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        staff.MapPost("/auth/login", LoginAsync)
            .AllowAnonymous();

        staff.MapPost("/auth/logout", LogoutAsync)
            .RequireAuthorization();

        staff.MapGet("/auth/me", CurrentUserAsync)
            .RequireAuthorization();

        staff.MapGet("/operator/overview", () => RoleOverview(StaffRoles.Operator, "Очередь обращений"))
            .RequireAuthorization(StaffPolicies.OperatorOnly);
        staff.MapGet("/expert/overview", () => RoleOverview(StaffRoles.Expert, "Назначенные обращения"))
            .RequireAuthorization(StaffPolicies.ExpertOnly);
        staff.MapGet("/administrator/overview", () => RoleOverview(StaffRoles.Administrator, "Настройки платформы"))
            .RequireAuthorization(StaffPolicies.AdministratorOnly);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        SignInManager<StaffUser> signInManager,
        ILoggerFactory loggerFactory)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        var logger = loggerFactory.CreateLogger("StaffAuthentication");
        var normalizedUserName = request.UserName.Trim();
        var user = await userManager.FindByNameAsync(normalizedUserName);

        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Staff login failed for user {UserName}", normalizedUserName);
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Staff login failed for user {UserName}; locked out: {IsLockedOut}",
                normalizedUserName,
                result.IsLockedOut);
            return InvalidCredentials();
        }

        var roles = await userManager.GetRolesAsync(user);
        logger.LogInformation("Staff login succeeded for user {UserId} with roles {Roles}", user.Id, roles);
        return Results.Ok(ToPayload(user, roles));
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<StaffUser> signInManager,
        UserManager<StaffUser> userManager,
        ILoggerFactory loggerFactory)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        var user = await userManager.GetUserAsync(principal);
        await signInManager.SignOutAsync();

        loggerFactory
            .CreateLogger("StaffAuthentication")
            .LogInformation("Staff logout completed for user {UserId}", user?.Id);

        return Results.NoContent();
    }

    private static async Task<IResult> CurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<StaffUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(ToPayload(user, await userManager.GetRolesAsync(user)));
    }

    private static IResult RoleOverview(string role, string title) => Results.Ok(new { role, title });

    private static object ToPayload(StaffUser user, IEnumerable<string> roles) => new
    {
        id = user.Id,
        userName = user.UserName,
        displayName = user.DisplayName,
        roles
    };

    private static IResult InvalidCredentials() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Не удалось войти",
        detail: "Проверьте логин и пароль.");

    private static async Task<IResult?> ValidateAntiforgeryAsync(
        HttpContext context,
        IAntiforgery antiforgery)
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

    private sealed record LoginRequest(string UserName, string Password);
}
