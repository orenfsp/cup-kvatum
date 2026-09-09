using Otklik.Application.Appeals;
using Xunit;

namespace Otklik.UnitTests;

public sealed class CrisisTextMatcherTests
{
    private static readonly CrisisMarkerDefinition[] Markers =
    [
        new("меня избива*", "PhysicalViolence"),
        new("угрожа* уб*", "LifeThreat"),
        new("хочу умер*", "SuicideRisk"),
        new("не хочу жить", "SuicideRisk")
    ];

    [Theory]
    [InlineData("Меня избивают после школы", "PhysicalViolence")]
    [InlineData("Меня сейчас избивают после школы", "PhysicalViolence")]
    [InlineData("Мне угрожают убить", "LifeThreat")]
    [InlineData("Я хлчу умереть", "SuicideRisk")]
    [InlineData("Я больше не хочу жить", "SuicideRisk")]
    public void Detects_inflections_and_common_single_character_typos(string text, string expectedRisk)
    {
        var result = CrisisTextMatcher.Detect([text], Markers);

        Assert.True(result.IsMatch);
        Assert.Equal(expectedRisk, result.RiskType);
    }

    [Theory]
    [InlineData("Мне нужна помощь с конфликтом в классе")]
    [InlineData("Я хочу спокойно поговорить с учителем")]
    [InlineData("На уроке меня дразнят")]
    public void Neutral_text_is_not_flagged(string text)
    {
        Assert.False(CrisisTextMatcher.Detect([text], Markers).IsMatch);
    }
}
