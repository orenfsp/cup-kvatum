using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Otklik.Application.Security;

namespace Otklik.Infrastructure.Identity;

public sealed class StaffIdentitySeeder(
    UserManager<StaffUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IConfiguration configuration,
    ILogger<StaffIdentitySeeder> logger)
{
    private static readonly SeedAccount[] Accounts =
    [
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "operator",
            "Оператор демо",
            StaffRoles.Operator,
            "SeedAccounts:Operator:Password"),
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "expert",
            "Эксперт демо",
            StaffRoles.Expert,
            "SeedAccounts:Expert:Password"),
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "administrator",
            "Администратор демо",
            StaffRoles.Administrator,
            "SeedAccounts:Administrator:Password"),
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "expert.mediator",
            "Эксперт по медиации",
            StaffRoles.Expert,
            "SeedAccounts:Expert:Password")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in StaffRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)), $"create role {roleName}");
            }
        }

        foreach (var account in Accounts)
        {
            var password = configuration[account.PasswordConfigurationKey];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Development seed password '{account.PasswordConfigurationKey}' is required.");
            }

            var user = await userManager.FindByNameAsync(account.UserName);
            if (user is null)
            {
                user = new StaffUser
                {
                    Id = account.Id,
                    UserName = account.UserName,
                    DisplayName = account.DisplayName,
                    IsActive = true,
                    IsAvailable = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                EnsureSucceeded(await userManager.CreateAsync(user, password), $"create user {account.UserName}");
            }

            if (!await userManager.IsInRoleAsync(user, account.Role))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, account.Role), $"assign role {account.Role}");
            }
        }

        logger.LogInformation("Development staff seed is ready for {AccountCount} accounts", Accounts.Length);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Identity seed failed during '{operation}': {errors}");
    }

    private sealed record SeedAccount(
        Guid Id,
        string UserName,
        string DisplayName,
        string Role,
        string PasswordConfigurationKey);
}
