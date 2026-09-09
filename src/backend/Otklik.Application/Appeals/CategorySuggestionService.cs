using Otklik.Domain.Appeals;

namespace Otklik.Application.Appeals;

public interface ICategorySuggestionService
{
    CategorySuggestion? Suggest(string? narrative, IEnumerable<string> answers);
}

public sealed record CategorySuggestion(Guid CategoryId, string Reason);

public sealed class CategorySuggestionService : ICategorySuggestionService
{
    private static readonly SuggestionRule[] Rules =
    [
        new(AppealCategory.BullyingId, ["травл", "булл", "обзыва", "дразн", "издева", "унижа"]),
        new(AppealCategory.PressureId, ["давлен", "угрожа", "шантаж", "заставля", "принужда"]),
        new(AppealCategory.ConflictId, ["конфликт", "ссор", "спор", "поруг", "разноглас"])
    ];

    public CategorySuggestion? Suggest(string? narrative, IEnumerable<string> answers)
    {
        var text = string.Join(' ', new[] { narrative }.Concat(answers))
            .ToLowerInvariant();
        foreach (var rule in Rules)
        {
            if (rule.Markers.Any(text.Contains))
            {
                return new CategorySuggestion(rule.CategoryId, "По словам в обращении");
            }
        }

        return null;
    }

    private sealed record SuggestionRule(Guid CategoryId, string[] Markers);
}
