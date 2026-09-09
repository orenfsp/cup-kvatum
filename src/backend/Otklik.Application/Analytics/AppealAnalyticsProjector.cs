using Otklik.Domain.Analytics;

namespace Otklik.Application.Analytics;

public static class AppealAnalyticsProjector
{
    public static AppealAnalyticsProjection Apply(
        AppealAnalyticsProjection? projection,
        AppealAnalyticsEvent analyticsEvent)
    {
        if (projection is null)
        {
            return new AppealAnalyticsProjection
            {
                AppealId = analyticsEvent.AppealId,
                ApplicantType = analyticsEvent.ApplicantType,
                CategoryId = analyticsEvent.CategoryId,
                ExpertGroupId = analyticsEvent.ExpertGroupId,
                Status = analyticsEvent.Status,
                Priority = analyticsEvent.Priority,
                CreatedAt = analyticsEvent.CreatedAt,
                OperatorAcceptedAt = analyticsEvent.OperatorAcceptedAt,
                FirstExpertResponseAt = analyticsEvent.FirstExpertResponseAt,
                ClosedAt = analyticsEvent.ClosedAt,
                OperatorUserId = analyticsEvent.OperatorUserId,
                AssignedExpertId = analyticsEvent.AssignedExpertId,
                LastAssignedExpertId = analyticsEvent.AssignedExpertId,
                CrisisFlag = analyticsEvent.CrisisFlag,
                ReturnCount = analyticsEvent.ReturnCount,
                LastAppealVersion = analyticsEvent.AppealVersion,
                LastEventAt = analyticsEvent.OccurredAt
            };
        }

        if (analyticsEvent.AppealVersion < projection.LastAppealVersion) return projection;
        projection.ApplicantType = analyticsEvent.ApplicantType;
        projection.CategoryId = analyticsEvent.CategoryId;
        projection.ExpertGroupId = analyticsEvent.ExpertGroupId;
        projection.Status = analyticsEvent.Status;
        projection.Priority = analyticsEvent.Priority;
        projection.OperatorAcceptedAt ??= analyticsEvent.OperatorAcceptedAt;
        projection.FirstExpertResponseAt ??= analyticsEvent.FirstExpertResponseAt;
        projection.ClosedAt = analyticsEvent.ClosedAt;
        projection.OperatorUserId ??= analyticsEvent.OperatorUserId;
        projection.AssignedExpertId = analyticsEvent.AssignedExpertId;
        projection.LastAssignedExpertId = analyticsEvent.AssignedExpertId ?? projection.LastAssignedExpertId;
        projection.CrisisFlag = analyticsEvent.CrisisFlag;
        projection.ReturnCount = analyticsEvent.ReturnCount;
        projection.LastAppealVersion = analyticsEvent.AppealVersion;
        projection.LastEventAt = analyticsEvent.OccurredAt > projection.LastEventAt
            ? analyticsEvent.OccurredAt
            : projection.LastEventAt;
        return projection;
    }
}
