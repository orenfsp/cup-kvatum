using Otklik.Application.Appeals;
using Otklik.Domain.Appeals;
using Xunit;

namespace Otklik.UnitTests;

public sealed class AppealAutoClosePolicyTests
{
    [Fact]
    public void Old_regular_recommendation_is_closed()
    {
        var now = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        Assert.Equal(AppealAutoCloseAction.Close, AppealAutoClosePolicy.Evaluate(
            AppealStatus.RecommendationReady, false, now.AddDays(-8), now, 7));
    }

    [Fact]
    public void Old_crisis_recommendation_requires_manual_review()
    {
        var now = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        Assert.Equal(AppealAutoCloseAction.CreateCrisisReview, AppealAutoClosePolicy.Evaluate(
            AppealStatus.RecommendationReady, true, now.AddDays(-8), now, 7));
    }

    [Theory]
    [InlineData(AppealStatus.InProgress)]
    [InlineData(AppealStatus.RecommendationReady)]
    public void Active_or_recent_appeal_is_unchanged(AppealStatus status)
    {
        var now = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        Assert.Equal(AppealAutoCloseAction.None, AppealAutoClosePolicy.Evaluate(status, false, now.AddDays(-2), now, 7));
    }
}
