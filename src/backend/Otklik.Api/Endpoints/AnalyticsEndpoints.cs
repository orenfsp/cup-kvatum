using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Security;
using Otklik.Domain.Analytics;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Identity;
using Otklik.Infrastructure.Persistence;

namespace Otklik.Api.Endpoints;

public static class AnalyticsEndpoints
{
    private static readonly string[] ActiveStatuses =
    [
        "New", "Triaged", "Assigned", "InProgress", "NeedsClarification", "RecommendationReady", "Returned"
    ];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var analytics = endpoints.MapGroup("/api/staff/analytics").RequireAuthorization();
        analytics.MapGet("/dashboard", DashboardAsync);
        analytics.MapPost("/exports", CreateExportAsync);
        analytics.MapPost("/exports/{exportId:guid}/download", DownloadExportAsync);
        return endpoints;
    }

    private static async Task<IResult> DashboardAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        ClaimsPrincipal principal,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(principal, userManager);
        if (actor is null) return Results.Unauthorized();
        var period = NormalizePeriod(from, to);
        if (period.Error is not null) return period.Error;

        var rows = await Scoped(database.AppealAnalyticsProjections.AsNoTracking(), actor)
            .Where(item => item.CreatedAt >= period.From && item.CreatedAt < period.To)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var categoryNames = await CategoryNamesAsync(database, rows, cancellationToken);
        var workload = await WorkloadAsync(database, rows, actor, cancellationToken);
        var operatorDurations = rows.Where(item => item.OperatorAcceptedAt is not null)
            .Select(item => (item.OperatorAcceptedAt!.Value - item.CreatedAt).TotalMinutes);
        var expertDurations = rows.Where(item => item.FirstExpertResponseAt is not null)
            .Select(item => (item.FirstExpertResponseAt!.Value - item.CreatedAt).TotalMinutes);
        var closeDurations = rows.Where(item => item.ClosedAt is not null)
            .Select(item => (item.ClosedAt!.Value - item.CreatedAt).TotalMinutes);

        return Results.Ok(new
        {
            scope = new { role = actor.Role, actor.Id, label = actor.Role == StaffRoles.Administrator ? "Все обращения" : "Только ваш рабочий срез" },
            period = new { from = period.From, to = period.To },
            generatedAt = DateTimeOffset.UtcNow,
            total = rows.Count,
            active = rows.Count(item => ActiveStatuses.Contains(item.Status)),
            urgentSharePercent = Share(rows.Count(item => item.Priority == AppealPriority.Urgent.ToString()), rows.Count),
            returnedSharePercent = Share(rows.Count(item => item.ReturnCount > 0), rows.Count),
            averageMinutes = new
            {
                operatorAccepted = Average(operatorDurations),
                firstExpertResponse = Average(expertDurations),
                closed = Average(closeDurations)
            },
            distributions = new
            {
                status = Distribution(rows.Select(item => item.Status)),
                priority = Distribution(rows.Select(item => item.Priority)),
                applicantType = Distribution(rows.Select(item => item.ApplicantType)),
                category = rows.GroupBy(item => item.CategoryId)
                    .Select(group => new
                    {
                        key = group.Key?.ToString("D") ?? "unconfirmed",
                        label = group.Key is Guid id && categoryNames.TryGetValue(id, out var name)
                            ? name
                            : "Категория не подтверждена",
                        count = group.Count()
                    })
                    .OrderByDescending(item => item.count)
                    .ThenBy(item => item.label)
            },
            workload,
            definitions = new
            {
                total = "Обращения, созданные в выбранном периоде.",
                operatorAccepted = "Календарное время от создания до первого изменения оператором.",
                firstExpertResponse = "Календарное время от создания до первого сообщения или рекомендации эксперта.",
                closed = "Календарное время от создания до закрытия или отклонения.",
                active = "Обращения периода, которые сейчас не закрыты и не отклонены.",
                urgentShare = "Доля обращений периода с подтвержденным приоритетом Urgent.",
                returnedShare = "Доля обращений периода, которые заявитель хотя бы раз вернул на доработку."
            }
        });
    }

    private static async Task<IResult> CreateExportAsync(
        ExportRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        var actor = await GetActorAsync(principal, userManager);
        if (actor is null) return Results.Unauthorized();
        var format = request.Format?.Trim().ToLowerInvariant();
        if (format is not ("csv" or "xlsx"))
            return Validation("format", "Выберите CSV или XLSX.");
        var period = NormalizePeriod(request.From, request.To);
        if (period.Error is not null) return period.Error;

        var rows = await Scoped(database.AppealAnalyticsProjections.AsNoTracking(), actor)
            .Where(item => item.CreatedAt >= period.From && item.CreatedAt < period.To)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var categoryNames = await CategoryNamesAsync(database, rows, cancellationToken);
        var exportRows = rows.Select(item => ToExportRow(item, categoryNames)).ToList();
        var exportId = Guid.NewGuid();
        var directory = Path.Combine(Path.GetTempPath(), "otklik-analytics-exports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{exportId:N}.{format}");
        try
        {
            if (format == "csv") await File.WriteAllBytesAsync(path, BuildCsv(exportRows), cancellationToken);
            else await File.WriteAllBytesAsync(path, BuildXlsx(exportRows), cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var export = new AnalyticsExport
            {
                Id = exportId,
                RequestedByUserId = actor.Id,
                RequestedByRole = actor.Role,
                Format = format,
                PeriodFrom = period.From,
                PeriodTo = period.To,
                FilePath = path,
                RowCount = exportRows.Count,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(15)
            };
            database.AnalyticsExports.Add(export);
            database.AdministrativeAuditEvents.Add(Audit(actor, "AnalyticsExportCreated", export.Id, new
            {
                format,
                period.From,
                period.To,
                rowCount = exportRows.Count,
                scopeRole = actor.Role
            }, now));
            await database.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/staff/analytics/exports/{export.Id}", new
            {
                export.Id,
                export.Format,
                export.RowCount,
                export.CreatedAt,
                export.ExpiresAt,
                downloadUrl = $"/api/staff/analytics/exports/{export.Id}/download"
            });
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private static async Task<IResult> DownloadExportAsync(
        Guid exportId,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<StaffUser> userManager,
        OtklikDbContext database,
        CancellationToken cancellationToken)
    {
        var antiforgeryError = await ValidateAntiforgeryAsync(context, antiforgery);
        if (antiforgeryError is not null) return antiforgeryError;
        var actor = await GetActorAsync(principal, userManager);
        if (actor is null) return Results.Unauthorized();
        var export = await database.AnalyticsExports.SingleOrDefaultAsync(
            item => item.Id == exportId && item.RequestedByUserId == actor.Id,
            cancellationToken);
        if (export is null) return Results.NotFound();
        if (export.DownloadedAt is not null || export.ExpiresAt < DateTimeOffset.UtcNow || !File.Exists(export.FilePath))
            return Results.Problem(statusCode: StatusCodes.Status410Gone, title: "Выгрузка больше недоступна", detail: "Создайте новую выгрузку.");

        var bytes = await File.ReadAllBytesAsync(export.FilePath, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        export.DownloadedAt = now;
        database.AdministrativeAuditEvents.Add(Audit(actor, "AnalyticsExportDownloaded", export.Id, new
        {
            export.Format,
            export.RowCount,
            export.PeriodFrom,
            export.PeriodTo,
            scopeRole = actor.Role
        }, now));
        await database.SaveChangesAsync(cancellationToken);
        try { File.Delete(export.FilePath); }
        catch (IOException) { /* The worker retries cleanup without exposing a server path. */ }

        return Results.File(
            bytes,
            export.Format == "csv"
                ? "text/csv; charset=utf-8"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"otklik-analytics-{now:yyyyMMdd-HHmm}.{export.Format}");
    }

    private static IQueryable<AppealAnalyticsProjection> Scoped(
        IQueryable<AppealAnalyticsProjection> query,
        AnalyticsActor actor) => actor.Role switch
    {
        StaffRoles.Administrator => query,
        StaffRoles.Operator => query.Where(item => item.OperatorUserId == actor.Id),
        StaffRoles.Expert => query.Where(item => item.LastAssignedExpertId == actor.Id),
        _ => query.Where(_ => false)
    };

    private static async Task<AnalyticsActor?> GetActorAsync(
        ClaimsPrincipal principal,
        UserManager<StaffUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive) return null;
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.SingleOrDefault();
        return role is StaffRoles.Operator or StaffRoles.Expert or StaffRoles.Administrator
            ? new AnalyticsActor(user.Id, user.DisplayName, role)
            : null;
    }

    private static async Task<Dictionary<Guid, string>> CategoryNamesAsync(
        OtklikDbContext database,
        IReadOnlyCollection<AppealAnalyticsProjection> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Where(item => item.CategoryId is not null).Select(item => item.CategoryId!.Value).Distinct().ToArray();
        return await database.AppealCategories.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
    }

    private static async Task<object[]> WorkloadAsync(
        OtklikDbContext database,
        IReadOnlyCollection<AppealAnalyticsProjection> rows,
        AnalyticsActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Role != StaffRoles.Administrator)
        {
            return [new { staffUserId = actor.Id, displayName = actor.DisplayName, activeCount = rows.Count(item => ActiveStatuses.Contains(item.Status)) }];
        }

        var counts = rows.Where(item => ActiveStatuses.Contains(item.Status) && item.LastAssignedExpertId is not null)
            .GroupBy(item => item.LastAssignedExpertId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var ids = counts.Keys.ToArray();
        var users = await database.Users.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.DisplayName })
            .OrderBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);
        return users.Select(user => (object)new
        {
            staffUserId = user.Id,
            user.DisplayName,
            activeCount = counts[user.Id]
        }).ToArray();
    }

    private static ExportRow ToExportRow(
        AppealAnalyticsProjection item,
        IReadOnlyDictionary<Guid, string> categories) => new(
        item.AppealId,
        item.ApplicantType,
        item.CategoryId is Guid categoryId && categories.TryGetValue(categoryId, out var category)
            ? category
            : "Категория не подтверждена",
        item.Status,
        item.Priority,
        item.CreatedAt,
        item.OperatorAcceptedAt,
        item.FirstExpertResponseAt,
        item.ClosedAt,
        item.ReturnCount,
        item.CrisisFlag);

    private static byte[] BuildCsv(IReadOnlyCollection<ExportRow> rows)
    {
        var result = new StringBuilder();
        result.AppendLine("appeal_id,applicant_type,category,status,priority,created_at_utc,operator_accepted_at_utc,first_expert_response_at_utc,closed_at_utc,return_count,crisis_flag");
        foreach (var row in rows)
        {
            result.AppendLine(string.Join(',', new[]
            {
                Csv(row.AppealId.ToString("D")), Csv(row.ApplicantType), Csv(row.Category), Csv(row.Status), Csv(row.Priority),
                Csv(Iso(row.CreatedAt)), Csv(Iso(row.OperatorAcceptedAt)), Csv(Iso(row.FirstExpertResponseAt)), Csv(Iso(row.ClosedAt)),
                row.ReturnCount.ToString(CultureInfo.InvariantCulture), row.CrisisFlag ? "true" : "false"
            }));
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(result.ToString());
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<ExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Аналитика");
        var headers = new[]
        {
            "appeal_id", "applicant_type", "category", "status", "priority", "created_at_utc",
            "operator_accepted_at_utc", "first_expert_response_at_utc", "closed_at_utc", "return_count", "crisis_flag"
        };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var rowNumber = 2;
        foreach (var row in rows)
        {
            var values = new[]
            {
                row.AppealId.ToString("D"), row.ApplicantType, SpreadsheetSafe(row.Category), row.Status, row.Priority,
                Iso(row.CreatedAt), Iso(row.OperatorAcceptedAt), Iso(row.FirstExpertResponseAt), Iso(row.ClosedAt),
                row.ReturnCount.ToString(CultureInfo.InvariantCulture), row.CrisisFlag ? "true" : "false"
            };
            for (var column = 0; column < values.Length; column++) sheet.Cell(rowNumber, column + 1).Value = values[column];
            rowNumber++;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(8, 48);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SpreadsheetSafe(string value) => value.Length > 0 && "=+-@".Contains(value[0]) ? $"'{value}" : value;
    private static string Csv(string value) => $"\"{SpreadsheetSafe(value).Replace("\"", "\"\"")}\"";
    private static string Iso(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    private static string Iso(DateTimeOffset? value) => value is null ? string.Empty : Iso(value.Value);
    private static double? Average(IEnumerable<double> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? null : Math.Round(materialized.Average(), 1);
    }
    private static double Share(int count, int total) => total == 0 ? 0 : Math.Round(count * 100d / total, 1);
    private static DistributionItem[] Distribution(IEnumerable<string> values) => values.GroupBy(value => value)
        .Select(group => new DistributionItem(group.Key, Label(group.Key), group.Count()))
        .OrderByDescending(item => item.Count)
        .ThenBy(item => item.Label)
        .ToArray();
    private static string Label(string value) => value switch
    {
        "New" => "Новое", "Triaged" => "Разобрано оператором", "Assigned" => "Назначено",
        "InProgress" => "В работе", "NeedsClarification" => "Ждет ответа заявителя",
        "RecommendationReady" => "Рекомендация готова", "Returned" => "Возвращено",
        "Closed" => "Закрыто", "Rejected" => "Отклонено", "Urgent" => "Срочный",
        "Standard" => "Обычный", "Low" => "Низкий", "Student" => "Ученик",
        "Parent" => "Родитель", "Teacher" => "Педагог", "OtherAdult" => "Другой взрослый",
        _ => value
    };

    private static PeriodResult NormalizePeriod(DateTimeOffset? from, DateTimeOffset? to)
    {
        var normalizedTo = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var normalizedFrom = (from ?? normalizedTo.AddDays(-30)).ToUniversalTime();
        if (normalizedFrom >= normalizedTo || normalizedTo - normalizedFrom > TimeSpan.FromDays(366))
            return new(default, default, Validation("period", "Период должен быть от одного момента до другого и не длиннее 366 дней."));
        return new(normalizedFrom, normalizedTo, null);
    }

    private static AdministrativeAuditEvent Audit(
        AnalyticsActor actor,
        string action,
        Guid targetId,
        object metadata,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ActorUserId = actor.Id,
        ActorDisplayName = actor.DisplayName,
        Action = action,
        TargetType = "AnalyticsExport",
        TargetId = targetId,
        AfterMetadataJson = JsonSerializer.Serialize(metadata, Json),
        OccurredAt = now
    };

    private static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return null; }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "Запрос устарел", detail: "Обновите страницу и повторите действие.");
        }
    }

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private sealed record AnalyticsActor(Guid Id, string DisplayName, string Role);
    private sealed record DistributionItem(string Key, string Label, int Count);
    private sealed record PeriodResult(DateTimeOffset From, DateTimeOffset To, IResult? Error);
    private sealed record ExportRequest(string? Format, DateTimeOffset? From, DateTimeOffset? To);
    private sealed record ExportRow(
        Guid AppealId,
        string ApplicantType,
        string Category,
        string Status,
        string Priority,
        DateTimeOffset CreatedAt,
        DateTimeOffset? OperatorAcceptedAt,
        DateTimeOffset? FirstExpertResponseAt,
        DateTimeOffset? ClosedAt,
        int ReturnCount,
        bool CrisisFlag);
}
