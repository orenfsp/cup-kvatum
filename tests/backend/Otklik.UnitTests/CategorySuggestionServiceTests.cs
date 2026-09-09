using Otklik.Application.Appeals;
using Otklik.Domain.Appeals;
using Xunit;

namespace Otklik.UnitTests;

public sealed class CategorySuggestionServiceTests
{
    private readonly CategorySuggestionService service = new();

    [Fact]
    public void Suggests_bullying_without_changing_the_appeal()
    {
        var suggestion = service.Suggest(
            "Меня постоянно обзывают и прячут вещи",
            []);

        Assert.NotNull(suggestion);
        Assert.Equal(AppealCategory.BullyingId, suggestion.CategoryId);
    }

    [Fact]
    public void Uses_optional_answers_for_a_suggestion()
    {
        var suggestion = service.Suggest(null, ["На меня оказывают давление каждый день"]);

        Assert.NotNull(suggestion);
        Assert.Equal(AppealCategory.PressureId, suggestion.CategoryId);
    }

    [Fact]
    public void Returns_no_suggestion_when_dictionary_has_no_match()
    {
        Assert.Null(service.Suggest("Хочу поговорить о ситуации", []));
    }
}
