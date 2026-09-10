using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Identity;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Endpoints;

public static class AdminEndpoints
{
    private static readonly AppealStatus[] StuckStatuses =
    [
        AppealStatus.New,
        AppealStatus.Triaged,
        AppealStatus.Assigned,
        AppealStatus.InProgress,
        AppealStatus.NeedsClarification,
        AppealStatus.Returned
    ];

    private static readonly AppealStatus[] AdministrativeStatuses = StuckStatuses;
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/staff/administrator")
            .RequireAuthorization(StaffPolicies.AdministratorOnly);

        admin.MapGet("/configuration", GetConfigurationAsync);
        admin.MapPost("/categories", CreateCategoryAsync);
        admin.MapPost("/categories/{categoryId:guid}/update", UpdateCategoryAsync);
        admin.MapPost("/categories/{categoryId:guid}/deactivate", DeactivateCategoryAsync);
        admin.MapPost("/groups", CreateGroupAsync);
        admin.MapPost("/groups/{groupId:guid}/update", UpdateGroupAsync);
        admin.MapPost("/groups/{groupId:guid}/deactivate", DeactivateGroupAsync);
        admin.MapPost("/rules", SaveRuleAsync);
        admin.MapPost("/rules/{ruleId:guid}/deactivate", DeactivateRuleAsync);

        admin.MapGet("/users", GetUsersAsync);
        admin.MapPost("/users", CreateUserAsync);
        admin.MapPost("/users/{userId:guid}/update", UpdateUserAsync);
        admin.MapPost("/users/{userId:guid}/block", BlockUserAsync);
        admin.MapPost("/users/{userId:guid}/restore", RestoreUserAsync);

        admin.MapGet("/stuck", GetStuckAppealsAsync);
        admin.MapPost("/stuck/{appealId:guid}/intervene", InterveneAsync);

        admin.MapGet("/audit", GetAuditAsync);
        admin.MapGet("/audit/{eventId:guid}", GetAuditEventAsync);

        return endpoints;
    }

    private static async Task<IResult> GetConfigurationAsync(
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var categories = await database.AppealCategories
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.DisplayName,
                item.SortOrder,
                item.IsActive,
                item.Version,
                item.UpdatedAt,
                usageCount = database.Appeals.Count(appeal => appeal.CategoryId == item.Id)
            })
            .ToListAsync(cancellationToken);

        var groups = await database.ExpertGroups
            .AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.DisplayName,
                item.ActiveAppealLimit,
                item.IsActive,
                item.Version,
                item.UpdatedAt,
                memberIds = database.ExpertGroupMemberships
                    .Where(membership => membership.ExpertGroupId == item.Id)
                    .Select(membership => membership.ExpertUserId)
                    .ToArray()
            })
            .ToListAsync(cancellationToken);

        var rules = await database.AppealRoutingRules
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ExpertGroup)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Category.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.CategoryId,
                category = item.Category.DisplayName,
                item.ExpertGroupId,
                expertGroup = item.ExpertGroup.DisplayName,
                item.Version,
                item.IsActive,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var expertRoleId = await database.Roles
            .Where(role => role.Name == StaffRoles.Expert)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);
        var experts = await (
            from user in database.Users.AsNoTracking()
            join userRole in database.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            where userRole.RoleId == expertRoleId
            orderby user.DisplayName
            select new
            {
                user.Id,
                user.UserName,
                user.DisplayName,
                user.IsActive,
                user.IsAvailable
            }).ToListAsync(cancellationToken);

        return Results.Ok(new { categories, groups, rules, experts });
    }

    private static async Task<IResult> CreateCategoryAsync(
        CategoryCreateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateConfiguration(request.Code, request.DisplayName, request.SortOrder);
        if (validation is not null) return validation;

        var code = request.Code.Trim().ToLowerInvariant();
        if (await database.AppealCategories.AnyAsync(item => item.Code == code, cancellationToken))
        {
            return Validation("code", "Категория с таким кодом уже существует.");
        }

        var now = DateTimeOffset.UtcNow;
        var category = new AppealCategory
        {
            Id = Guid.NewGuid(),
            Code = code,
            DisplayName = request.DisplayName.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            Version = 1,
            UpdatedAt = now
        };
        database.AppealCategories.Add(category);
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "CategoryCreated", "Category", category.Id, null, CategoryMetadata(category), null, now));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/staff/administrator/categories/{category.Id}", CategoryPayload(category, 0));
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        CategoryUpdateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateConfiguration(request.Code, request.DisplayName, request.SortOrder);
        if (validation is not null) return validation;
        var category = await database.AppealCategories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null) return Results.NotFound();
        if (category.Version != request.ExpectedVersion) return VersionConflict("Категория");
        var code = request.Code.Trim().ToLowerInvariant();
        if (await database.AppealCategories.AnyAsync(
                item => item.Id != categoryId && item.Code == code,
                cancellationToken))
        {
            return Validation("code", "Категория с таким кодом уже существует.");
        }

        var before = CategoryMetadata(category);
        category.Code = code;
        category.DisplayName = request.DisplayName.Trim();
        category.SortOrder = request.SortOrder;
        category.Version++;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "CategoryUpdated", "Category", category.Id, before, CategoryMetadata(category), null, category.UpdatedAt));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Категория");
        var usageCount = await database.Appeals.CountAsync(item => item.CategoryId == category.Id, cancellationToken);
        return Results.Ok(CategoryPayload(category, usageCount));
    }

    private static async Task<IResult> DeactivateCategoryAsync(
        Guid categoryId,
        VersionRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        if (categoryId == AppealCategory.UnsureId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Категория обязательна",
                detail: "Вариант «Не знаю, как это назвать» должен оставаться доступным заявителю.");
        }
        var category = await database.AppealCategories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        if (category is null) return Results.NotFound();
        if (category.Version != request.ExpectedVersion) return VersionConflict("Категория");
        if (!category.IsActive) return Results.Ok(new { category.Id, category.IsActive, category.Version });

        var before = CategoryMetadata(category);
        category.IsActive = false;
        category.Version++;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "CategoryDeactivated", "Category", category.Id, before, CategoryMetadata(category), null, category.UpdatedAt));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Категория");
        return Results.Ok(new { category.Id, category.IsActive, category.Version });
    }

    private static async Task<IResult> CreateGroupAsync(
        GroupCreateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateConfiguration(request.Code, request.DisplayName, request.ActiveAppealLimit);
        if (validation is not null) return validation;
        if (request.ActiveAppealLimit is < 1 or > 100)
        {
            return Validation("activeAppealLimit", "Лимит должен быть от 1 до 100 обращений.");
        }
        var code = request.Code.Trim().ToLowerInvariant();
        if (await database.ExpertGroups.AnyAsync(item => item.Code == code, cancellationToken))
        {
            return Validation("code", "Группа с таким кодом уже существует.");
        }
        var expertError = await ValidateExpertsAsync(database, request.ExpertIds, cancellationToken);
        if (expertError is not null) return expertError;

        var now = DateTimeOffset.UtcNow;
        var group = new ExpertGroup
        {
            Id = Guid.NewGuid(),
            Code = code,
            DisplayName = request.DisplayName.Trim(),
            ActiveAppealLimit = request.ActiveAppealLimit,
            IsActive = true,
            Version = 1,
            UpdatedAt = now
        };
        database.ExpertGroups.Add(group);
        AddMemberships(database, group.Id, request.ExpertIds);
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "ExpertGroupCreated", "ExpertGroup", group.Id, null,
            GroupMetadata(group, request.ExpertIds), null, now));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/staff/administrator/groups/{group.Id}", GroupPayload(group, request.ExpertIds));
    }

    private static async Task<IResult> UpdateGroupAsync(
        Guid groupId,
        GroupUpdateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateConfiguration(request.Code, request.DisplayName, request.ActiveAppealLimit);
        if (validation is not null) return validation;
        if (request.ActiveAppealLimit is < 1 or > 100)
        {
            return Validation("activeAppealLimit", "Лимит должен быть от 1 до 100 обращений.");
        }
        var group = await database.ExpertGroups.SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken);
        if (group is null) return Results.NotFound();
        if (group.Version != request.ExpectedVersion) return VersionConflict("Группа");
        var code = request.Code.Trim().ToLowerInvariant();
        if (await database.ExpertGroups.AnyAsync(item => item.Id != groupId && item.Code == code, cancellationToken))
        {
            return Validation("code", "Группа с таким кодом уже существует.");
        }
        var expertError = await ValidateExpertsAsync(database, request.ExpertIds, cancellationToken);
        if (expertError is not null) return expertError;
        var currentMemberships = await database.ExpertGroupMemberships
            .Where(item => item.ExpertGroupId == groupId)
            .ToListAsync(cancellationToken);
        var before = GroupMetadata(group, currentMemberships.Select(item => item.ExpertUserId));

        group.Code = code;
        group.DisplayName = request.DisplayName.Trim();
        group.ActiveAppealLimit = request.ActiveAppealLimit;
        group.Version++;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        database.ExpertGroupMemberships.RemoveRange(currentMemberships);
        AddMemberships(database, group.Id, request.ExpertIds);
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "ExpertGroupUpdated", "ExpertGroup", group.Id, before,
            GroupMetadata(group, request.ExpertIds), null, group.UpdatedAt));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Группа");
        return Results.Ok(GroupPayload(group, request.ExpertIds));
    }

    private static async Task<IResult> DeactivateGroupAsync(
        Guid groupId,
        VersionRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var group = await database.ExpertGroups.SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken);
        if (group is null) return Results.NotFound();
        if (group.Version != request.ExpectedVersion) return VersionConflict("Группа");
        if (!group.IsActive) return Results.Ok(new { group.Id, group.IsActive, group.Version });
        var memberIds = await database.ExpertGroupMemberships
            .Where(item => item.ExpertGroupId == groupId)
            .Select(item => item.ExpertUserId)
            .ToListAsync(cancellationToken);
        var before = GroupMetadata(group, memberIds);
        group.IsActive = false;
        group.Version++;
        group.UpdatedAt = DateTimeOffset.UtcNow;
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "ExpertGroupDeactivated", "ExpertGroup", group.Id, before,
            GroupMetadata(group, memberIds), null, group.UpdatedAt));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Группа");
        return Results.Ok(new { group.Id, group.IsActive, group.Version });
    }

    private static async Task<IResult> SaveRuleAsync(
        RuleSaveRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var category = await database.AppealCategories.SingleOrDefaultAsync(
            item => item.Id == request.CategoryId && item.IsActive,
            cancellationToken);
        if (category is null) return Validation("categoryId", "Выберите активную категорию.");
        var group = await database.ExpertGroups.SingleOrDefaultAsync(
            item => item.Id == request.ExpertGroupId && item.IsActive,
            cancellationToken);
        if (group is null) return Validation("expertGroupId", "Выберите активную группу.");

        var now = DateTimeOffset.UtcNow;
        var rule = await database.AppealRoutingRules.SingleOrDefaultAsync(
            item => item.CategoryId == request.CategoryId,
            cancellationToken);
        object? before = null;
        string action;
        if (rule is null)
        {
            rule = new AppealRoutingRule
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                ExpertGroupId = group.Id,
                IsActive = true,
                Version = 1,
                UpdatedAt = now
            };
            database.AppealRoutingRules.Add(rule);
            action = "RoutingRuleCreated";
        }
        else
        {
            if (request.ExpectedVersion is null || rule.Version != request.ExpectedVersion)
            {
                return VersionConflict("Правило");
            }
            before = RuleMetadata(rule);
            rule.ExpertGroupId = group.Id;
            rule.IsActive = true;
            rule.Version++;
            rule.UpdatedAt = now;
            action = "RoutingRuleUpdated";
        }

        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, action, "RoutingRule", rule.Id, before, RuleMetadata(rule), null, now));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Правило");
        return Results.Ok(new
        {
            rule.Id,
            rule.CategoryId,
            category = category.DisplayName,
            rule.ExpertGroupId,
            expertGroup = group.DisplayName,
            rule.Version,
            rule.IsActive,
            rule.UpdatedAt
        });
    }

    private static async Task<IResult> DeactivateRuleAsync(
        Guid ruleId,
        VersionRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var rule = await database.AppealRoutingRules.SingleOrDefaultAsync(item => item.Id == ruleId, cancellationToken);
        if (rule is null) return Results.NotFound();
        if (rule.Version != request.ExpectedVersion) return VersionConflict("Правило");
        if (!rule.IsActive) return Results.Ok(new { rule.Id, rule.IsActive, rule.Version });
        var before = RuleMetadata(rule);
        rule.IsActive = false;
        rule.Version++;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "RoutingRuleDeactivated", "RoutingRule", rule.Id, before,
            RuleMetadata(rule), null, rule.UpdatedAt));
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Правило");
        return Results.Ok(new { rule.Id, rule.IsActive, rule.Version });
    }

    private static async Task<IResult> GetUsersAsync(
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var users = await database.Users.AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);
        var payload = new List<object>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            payload.Add(UserPayload(user, roles.SingleOrDefault()));
        }
        return Results.Ok(new { total = payload.Count, items = payload });
    }

    private static async Task<IResult> CreateUserAsync(
        UserCreateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateUser(request.UserName, request.DisplayName, request.Role);
        if (validation is not null) return validation;
        if (string.IsNullOrWhiteSpace(request.Password)) return Validation("password", "Укажите временный пароль.");
        var userName = request.UserName.Trim().ToLowerInvariant();
        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return Validation("userName", "Сотрудник с таким логином уже существует.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new StaffUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            IsAvailable = request.Role == StaffRoles.Expert && request.IsAvailable,
            CreatedAt = now,
            UpdatedAt = now,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) return IdentityValidation(created, "password");
        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return IdentityValidation(roleResult, "role");
        }
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor!, "StaffAccountCreated", "StaffUser", user.Id, null,
            UserMetadata(user, request.Role), null, now));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/staff/administrator/users/{user.Id}", UserPayload(user, request.Role));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UserUpdateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var validation = ValidateUser("valid.user", request.DisplayName, request.Role);
        if (validation is not null) return validation;
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Results.NotFound();
        var currentRoles = await userManager.GetRolesAsync(user);
        var currentRole = currentRoles.SingleOrDefault();
        if (user.Id == guard.Actor!.Id && request.Role != StaffRoles.Administrator)
        {
            return Validation("role", "Нельзя снять роль администратора у собственной учетной записи.");
        }
        var before = UserMetadata(user, currentRole);
        user.DisplayName = request.DisplayName.Trim();
        user.IsAvailable = request.Role == StaffRoles.Expert && request.IsAvailable;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityValidation(update, "displayName");
        if (currentRole != request.Role)
        {
            if (currentRoles.Count > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removed.Succeeded) return IdentityValidation(removed, "role");
            }
            var added = await userManager.AddToRoleAsync(user, request.Role);
            if (!added.Succeeded) return IdentityValidation(added, "role");
            await userManager.UpdateSecurityStampAsync(user);
        }
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor, "StaffAccountUpdated", "StaffUser", user.Id, before,
            UserMetadata(user, request.Role), null, user.UpdatedAt));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(UserPayload(user, request.Role));
    }

    private static Task<IResult> BlockUserAsync(
        Guid userId,
        AccountStateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken) => ChangeUserStateAsync(
            userId, false, request.Reason, principal, context, antiforgery, userManager, database, cancellationToken);

    private static Task<IResult> RestoreUserAsync(
        Guid userId,
        AccountStateRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken) => ChangeUserStateAsync(
            userId, true, request.Reason, principal, context, antiforgery, userManager, database, cancellationToken);

    private static async Task<IResult> ChangeUserStateAsync(
        Guid userId,
        bool isActive,
        string? reason,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        if (userId == guard.Actor!.Id && !isActive)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Учетная запись используется",
                detail: "Нельзя заблокировать собственную активную сессию.");
        }
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Results.NotFound();
        var role = (await userManager.GetRolesAsync(user)).SingleOrDefault();
        if (user.IsActive == isActive) return Results.Ok(UserPayload(user, role));
        var before = UserMetadata(user, role);
        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return IdentityValidation(updated, "user");
        await userManager.UpdateSecurityStampAsync(user);
        database.AdministrativeAuditEvents.Add(Audit(
            guard.Actor, isActive ? "StaffAccountRestored" : "StaffAccountBlocked", "StaffUser", user.Id,
            before, UserMetadata(user, role), Normalize(reason), user.UpdatedAt));
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(UserPayload(user, role));
    }

    private static async Task<IResult> GetStuckAppealsAsync(
        string? status,
        string? priority,
        IConfiguration configuration,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var stuckHours = Math.Max(1, configuration.GetValue("Administrator:StuckHours", 4));
        var cutoff = DateTimeOffset.UtcNow.AddHours(-stuckHours);
        var query = database.Appeals.AsNoTracking()
            .Where(appeal => StuckStatuses.Contains(appeal.Status))
            .Select(appeal => new
            {
                appeal.Id,
                appeal.Version,
                appeal.CategoryId,
                category = appeal.Category == null ? "Категория не подтверждена" : appeal.Category.DisplayName,
                appeal.Status,
                appeal.Priority,
                appeal.AssignedExpertId,
                assignedExpert = database.Users
                    .Where(user => user.Id == appeal.AssignedExpertId)
                    .Select(user => user.DisplayName)
                    .SingleOrDefault(),
                appeal.CreatedAt,
                lastStatusChangedAt = appeal.StatusHistory
                    .OrderByDescending(change => change.ChangedAt)
                    .Select(change => (DateTimeOffset?)change.ChangedAt)
                    .FirstOrDefault() ?? appeal.CreatedAt,
                hasOpenAlert = database.AdminAlerts.Any(alert => alert.AppealId == appeal.Id && alert.ResolvedAt == null)
            })
            .Where(item => item.hasOpenAlert || item.lastStatusChangedAt <= cutoff);

        if (Enum.TryParse<AppealStatus>(status, true, out var parsedStatus) && StuckStatuses.Contains(parsedStatus))
        {
            query = query.Where(item => item.Status == parsedStatus);
        }
        if (Enum.TryParse<AppealPriority>(priority, true, out var parsedPriority))
        {
            query = query.Where(item => item.Priority == parsedPriority);
        }

        var items = await query
            .OrderByDescending(item => item.hasOpenAlert)
            .ThenBy(item => item.lastStatusChangedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var ids = items.Select(item => item.Id).ToArray();
        var alerts = await database.AdminAlerts.AsNoTracking()
            .Where(item => ids.Contains(item.AppealId) && item.ResolvedAt == null)
            .GroupBy(item => item.AppealId)
            .Select(group => new { AppealId = group.Key, Types = group.Select(item => item.Type).Distinct().ToArray() })
            .ToDictionaryAsync(item => item.AppealId, item => item.Types, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return Results.Ok(new
        {
            total = items.Count,
            stuckHours,
            items = items.Select(item => new
            {
                item.Id,
                item.Version,
                item.CategoryId,
                item.category,
                status = item.Status.ToString(),
                priority = item.Priority.ToString(),
                item.AssignedExpertId,
                item.assignedExpert,
                item.CreatedAt,
                item.lastStatusChangedAt,
                waitingHours = Math.Max(0, (int)(now - item.lastStatusChangedAt).TotalHours),
                alertTypes = alerts.GetValueOrDefault(item.Id) ?? []
            })
        });
    }

    private static async Task<IResult> InterveneAsync(
        Guid appealId,
        StuckInterventionRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        IConfiguration configuration,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(context, antiforgery, principal, userManager);
        if (guard.Error is not null) return guard.Error;
        var reason = Normalize(request.Reason);
        if ((reason?.Length ?? 0) < 10) return Validation("reason", "Укажите причину разблокирования — не менее 10 знаков.");
        if (!Enum.TryParse<AppealStatus>(request.Status, true, out var status)
            || !AdministrativeStatuses.Contains(status))
        {
            return Validation("status", "Выберите рабочий статус; администратор не завершает обращения.");
        }
        if (!Enum.TryParse<AppealPriority>(request.Priority, true, out var priority))
        {
            return Validation("priority", "Выберите доступный приоритет.");
        }

        var appeal = await database.Appeals
            .Include(item => item.StatusHistory)
            .Include(item => item.ExpertParticipants.Where(participant => participant.RemovedAt == null))
            .SingleOrDefaultAsync(item => item.Id == appealId, cancellationToken);
        if (appeal is null) return Results.NotFound();
        if (appeal.Version != request.ExpectedVersion) return VersionConflict("Обращение");
        var stuckHours = Math.Max(1, configuration.GetValue("Administrator:StuckHours", 4));
        if (!await IsStuckAsync(database, appeal, stuckHours, cancellationToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Обращение уже не зависло",
                detail: "Состояние изменилось после открытия списка. Обновите раздел.");
        }

        StaffUser? expert = null;
        if (request.AssignedExpertId is not null)
        {
            expert = await database.Users.SingleOrDefaultAsync(
                item => item.Id == request.AssignedExpertId && item.IsActive && item.IsAvailable,
                cancellationToken);
            if (expert is null || !await userManager.IsInRoleAsync(expert, StaffRoles.Expert))
            {
                return Validation("assignedExpertId", "Выберите активного доступного эксперта.");
            }
        }
        var requiresExpert = status is AppealStatus.Assigned or AppealStatus.InProgress or AppealStatus.NeedsClarification;
        if (requiresExpert != (expert is not null))
        {
            return Validation(
                "assignedExpertId",
                requiresExpert
                    ? "Для выбранного статуса нужен ответственный эксперт."
                    : "Для возврата в очередь снимите назначение эксперта.");
        }

        var before = AppealMetadata(appeal);
        var now = DateTimeOffset.UtcNow;
        if (appeal.Status != status)
        {
            appeal.Status = status;
            database.AppealStatusChanges.Add(new AppealStatusChange
            {
                Id = Guid.NewGuid(),
                AppealId = appeal.Id,
                Status = status,
                Source = "Administrator",
                ChangedAt = now
            });
        }
        appeal.Priority = priority;
        if (appeal.AssignedExpertId != request.AssignedExpertId)
        {
            foreach (var participant in appeal.ExpertParticipants.Where(item => item.Role == AppealExpertRole.Responsible))
            {
                participant.RemovedAt = now;
                participant.RemovedByUserId = guard.Actor!.Id;
            }
            appeal.AssignedExpertId = request.AssignedExpertId;
            appeal.AssignedAt = expert is null ? null : now;
            if (expert is not null)
            {
                database.AppealExpertParticipants.Add(new AppealExpertParticipant
                {
                    Id = Guid.NewGuid(),
                    AppealId = appeal.Id,
                    ExpertUserId = expert.Id,
                    Role = AppealExpertRole.Responsible,
                    AddedByUserId = guard.Actor!.Id,
                    AddedAt = now
                });
                database.AppealAssignmentEvents.Add(new AppealAssignmentEvent
                {
                    Id = Guid.NewGuid(),
                    AppealId = appeal.Id,
                    EventType = "AdminReassigned",
                    ExpertUserId = expert.Id,
                    Role = AppealExpertRole.Responsible,
                    ActorUserId = guard.Actor!.Id,
                    OccurredAt = now
                });
            }
        }
        appeal.Version++;
        var alerts = await database.AdminAlerts
            .Where(item => item.AppealId == appeal.Id && item.ResolvedAt == null)
            .ToListAsync(cancellationToken);
        alerts.ForEach(item => item.ResolvedAt = now);
        var auditEvent = Audit(
            guard.Actor!, "StuckAppealIntervened", "Appeal", appeal.Id, before,
            AppealMetadata(appeal), reason, now);
        database.AdministrativeAuditEvents.Add(auditEvent);
        if (!await SaveAsync(database, cancellationToken)) return VersionConflict("Обращение");
        return Results.Ok(new
        {
            appeal.Id,
            appeal.Version,
            status = appeal.Status.ToString(),
            priority = appeal.Priority.ToString(),
            appeal.AssignedExpertId,
            assignedExpert = expert?.DisplayName,
            changedAt = now,
            auditEventId = auditEvent.Id
        });
    }

    private static async Task<IResult> GetAuditAsync(
        string? action,
        string? targetType,
        Guid? actorId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var query = database.AdministrativeAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action == action);
        if (!string.IsNullOrWhiteSpace(targetType)) query = query.Where(item => item.TargetType == targetType);
        if (actorId is not null) query = query.Where(item => item.ActorUserId == actorId);
        if (from is not null) query = query.Where(item => item.OccurredAt >= from);
        if (to is not null) query = query.Where(item => item.OccurredAt <= to);
        var take = Math.Clamp(limit ?? 100, 1, 200);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAt).Take(take)
            .Select(item => new
            {
                item.Id,
                item.ActorUserId,
                item.ActorDisplayName,
                item.Action,
                item.TargetType,
                item.TargetId,
                item.Reason,
                item.OccurredAt
            })
            .ToListAsync(cancellationToken);
        var filters = await database.AdministrativeAuditEvents.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                actions = group.Select(item => item.Action).Distinct().OrderBy(item => item).ToArray(),
                targetTypes = group.Select(item => item.TargetType).Distinct().OrderBy(item => item).ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return Results.Ok(new
        {
            total,
            items,
            filters = filters ?? new { actions = Array.Empty<string>(), targetTypes = Array.Empty<string>() }
        });
    }

    private static async Task<IResult> GetAuditEventAsync(
        Guid eventId,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var item = await database.AdministrativeAuditEvents.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (item is null) return Results.NotFound();
        return Results.Ok(new
        {
            item.Id,
            item.ActorUserId,
            item.ActorDisplayName,
            item.Action,
            item.TargetType,
            item.TargetId,
            before = ParseMetadata(item.BeforeMetadataJson),
            after = ParseMetadata(item.AfterMetadataJson),
            item.Reason,
            item.OccurredAt
        });
    }

    private static async Task<IResult?> ValidateExpertsAsync(
        OtklikDbContext database,
        IReadOnlyCollection<Guid> expertIds,
        CancellationToken cancellationToken)
    {
        if (expertIds.Count != expertIds.Distinct().Count())
        {
            return Validation("expertIds", "Список экспертов содержит повторения.");
        }
        if (expertIds.Count == 0) return null;
        var expertRoleId = await database.Roles
            .Where(role => role.Name == StaffRoles.Expert)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);
        var validCount = await database.UserRoles.CountAsync(
            item => expertIds.Contains(item.UserId) && item.RoleId == expertRoleId,
            cancellationToken);
        return validCount == expertIds.Count
            ? null
            : Validation("expertIds", "В группу можно добавить только учетные записи экспертов.");
    }

    private static void AddMemberships(
        OtklikDbContext database,
        Guid groupId,
        IEnumerable<Guid> expertIds)
    {
        foreach (var expertId in expertIds.Distinct())
        {
            database.ExpertGroupMemberships.Add(new ExpertGroupMembership
            {
                ExpertGroupId = groupId,
                ExpertUserId = expertId
            });
        }
    }

    private static async Task<bool> IsStuckAsync(
        OtklikDbContext database,
        Appeal appeal,
        int stuckHours,
        CancellationToken cancellationToken)
    {
        if (!StuckStatuses.Contains(appeal.Status)) return false;
        var hasAlert = await database.AdminAlerts.AnyAsync(
            item => item.AppealId == appeal.Id && item.ResolvedAt == null,
            cancellationToken);
        var lastChange = appeal.StatusHistory.OrderByDescending(item => item.ChangedAt).FirstOrDefault()?.ChangedAt
            ?? appeal.CreatedAt;
        return hasAlert || lastChange <= DateTimeOffset.UtcNow.AddHours(-stuckHours);
    }

    private static IResult? ValidateConfiguration(string code, string displayName, int numericValue)
    {
        if (!Regex.IsMatch(code.Trim(), "^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant))
        {
            return Validation("code", "Код: 3–64 строчные латинские буквы, цифры или дефисы.");
        }
        if (displayName.Trim().Length is < 2 or > 160)
        {
            return Validation("displayName", "Название должно содержать от 2 до 160 знаков.");
        }
        if (numericValue is < 0 or > 10_000)
        {
            return Validation("sortOrder", "Числовое значение вне допустимого диапазона.");
        }
        return null;
    }

    private static IResult? ValidateUser(string userName, string displayName, string role)
    {
        if (!Regex.IsMatch(userName.Trim(), "^[a-z0-9][a-z0-9._-]{2,63}$", RegexOptions.CultureInvariant))
        {
            return Validation("userName", "Логин: 3–64 строчные латинские буквы, цифры, точка, дефис или подчеркивание.");
        }
        if (displayName.Trim().Length is < 2 or > 160)
        {
            return Validation("displayName", "Имя сотрудника должно содержать от 2 до 160 знаков.");
        }
        if (!StaffRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return Validation("role", "Выберите одну рабочую роль.");
        }
        return null;
    }

    private static async Task<AdminGuard> GuardAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        ClaimsPrincipal principal,
        UserManager<StaffUser> userManager)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return new AdminGuard(null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Запрос устарел",
                detail: "Обновите страницу и повторите действие."));
        }
        var user = await userManager.GetUserAsync(principal);
        return user is null || !user.IsActive
            ? new AdminGuard(null, Results.Unauthorized())
            : new AdminGuard(new AdminActor(user.Id, user.DisplayName), null);
    }

    private static AdministrativeAuditEvent Audit(
        AdminActor actor,
        string action,
        string targetType,
        Guid targetId,
        object? before,
        object? after,
        string? reason,
        DateTimeOffset occurredAt) => new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actor.Id,
            ActorDisplayName = actor.DisplayName,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            BeforeMetadataJson = before is null ? null : JsonSerializer.Serialize(before, AuditJsonOptions),
            AfterMetadataJson = after is null ? null : JsonSerializer.Serialize(after, AuditJsonOptions),
            Reason = reason,
            OccurredAt = occurredAt
        };

    private static object CategoryMetadata(AppealCategory item) => new
    {
        item.Code,
        item.DisplayName,
        item.SortOrder,
        item.IsActive,
        item.Version
    };

    private static object GroupMetadata(ExpertGroup item, IEnumerable<Guid> memberIds) => new
    {
        item.Code,
        item.DisplayName,
        item.ActiveAppealLimit,
        item.IsActive,
        item.Version,
        memberIds = memberIds.Order().ToArray()
    };

    private static object RuleMetadata(AppealRoutingRule item) => new
    {
        item.CategoryId,
        item.ExpertGroupId,
        item.IsActive,
        item.Version
    };

    private static object UserMetadata(StaffUser item, string? role) => new
    {
        item.UserName,
        item.DisplayName,
        role,
        item.IsActive,
        item.IsAvailable
    };

    private static object AppealMetadata(Appeal item) => new
    {
        item.CategoryId,
        status = item.Status.ToString(),
        priority = item.Priority.ToString(),
        item.AssignedExpertId,
        item.Version
    };

    private static object CategoryPayload(AppealCategory item, int usageCount) => new
    {
        item.Id,
        item.Code,
        item.DisplayName,
        item.SortOrder,
        item.IsActive,
        item.Version,
        item.UpdatedAt,
        usageCount
    };

    private static object GroupPayload(ExpertGroup item, IEnumerable<Guid> memberIds) => new
    {
        item.Id,
        item.Code,
        item.DisplayName,
        item.ActiveAppealLimit,
        item.IsActive,
        item.Version,
        item.UpdatedAt,
        memberIds = memberIds.Order().ToArray()
    };

    private static object UserPayload(StaffUser item, string? role) => new
    {
        item.Id,
        item.UserName,
        item.DisplayName,
        role,
        item.IsActive,
        item.IsAvailable,
        item.CreatedAt,
        item.UpdatedAt
    };

    private static object? ParseMetadata(string? json)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<bool> SaveAsync(OtklikDbContext database, CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult IdentityValidation(IdentityResult result, string field) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = result.Errors.Select(error => error.Description).ToArray()
        });

    private static IResult VersionConflict(string target) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: $"{target} уже изменилось",
        detail: "Обновите раздел перед повторным сохранением.");

    private sealed record AdminActor(Guid Id, string DisplayName);
    private sealed record AdminGuard(AdminActor? Actor, IResult? Error);
    private sealed record CategoryCreateRequest(string Code, string DisplayName, int SortOrder);
    private sealed record CategoryUpdateRequest(string Code, string DisplayName, int SortOrder, int ExpectedVersion);
    private sealed record VersionRequest(int ExpectedVersion);
    private sealed record GroupCreateRequest(
        string Code,
        string DisplayName,
        int ActiveAppealLimit,
        Guid[] ExpertIds);
    private sealed record GroupUpdateRequest(
        string Code,
        string DisplayName,
        int ActiveAppealLimit,
        Guid[] ExpertIds,
        int ExpectedVersion);
    private sealed record RuleSaveRequest(Guid CategoryId, Guid ExpertGroupId, int? ExpectedVersion);
    private sealed record UserCreateRequest(
        string UserName,
        string DisplayName,
        string Password,
        string Role,
        bool IsAvailable);
    private sealed record UserUpdateRequest(string DisplayName, string Role, bool IsAvailable);
    private sealed record AccountStateRequest(string? Reason);
    private sealed record StuckInterventionRequest(
        string Status,
        string Priority,
        Guid? AssignedExpertId,
        int ExpectedVersion,
        string? Reason);
}
