namespace Otklik.Application.Appeals;

public static class RoutingPolicy
{
    public static RoutingEvaluation Evaluate(
        Guid groupId,
        string groupName,
        int activeAppealLimit,
        IEnumerable<ExpertLoad> experts)
    {
        var candidates = experts
            .Select(expert => new RoutingExpert(
                expert.Id,
                expert.DisplayName,
                expert.ActiveCount,
                activeAppealLimit,
                expert.ActiveCount >= activeAppealLimit))
            .OrderBy(expert => expert.ActiveCount)
            .ThenBy(expert => expert.DisplayName)
            .ToArray();
        var warning = candidates.Length == 0
            ? "В группе нет доступных специалистов. Обращение останется в очереди, администратор получит уведомление."
            : candidates.All(expert => expert.AtCapacity)
                ? "Все специалисты группы достигли лимита. Для ручного назначения потребуется причина."
                : null;
        return new RoutingEvaluation(groupId, groupName, warning, candidates);
    }
}

public sealed record ExpertLoad(Guid Id, string DisplayName, int ActiveCount);

public sealed record RoutingEvaluation(
    Guid? GroupId,
    string? GroupName,
    string? Warning,
    IReadOnlyList<RoutingExpert> Experts);

public sealed record RoutingExpert(
    Guid Id,
    string DisplayName,
    int ActiveCount,
    int Limit,
    bool AtCapacity);
