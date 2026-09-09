namespace Otklik.Domain.Appeals;

public enum AppealStatus
{
    New,
    Triaged,
    Assigned,
    InProgress,
    NeedsClarification,
    RecommendationReady,
    Returned,
    Closed,
    Rejected
}
