using Otklik.Domain.Appeals;

namespace Otklik.Application.Appeals;

public enum AppealAutoCloseAction
{
    None,
    Close,
    CreateCrisisReview
}

public static class AppealAutoClosePolicy
{
    public static AppealAutoCloseAction Evaluate(
        AppealStatus status,
        bool crisisFlag,
        DateTimeOffset? recommendationAt,
        DateTimeOffset now,
        int autoCloseDays)
    {
        if (status != AppealStatus.RecommendationReady || recommendationAt is null) return AppealAutoCloseAction.None;
        if (recommendationAt.Value > now.AddDays(-Math.Clamp(autoCloseDays, 1, 365))) return AppealAutoCloseAction.None;
        return crisisFlag ? AppealAutoCloseAction.CreateCrisisReview : AppealAutoCloseAction.Close;
    }
}
