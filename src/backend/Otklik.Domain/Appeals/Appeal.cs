namespace Otklik.Domain.Appeals;

public sealed class Appeal
{
    public Guid Id { get; set; }

    public Guid ThreadId { get; set; }

    public AppealThread Thread { get; set; } = null!;

    public int Sequence { get; set; } = 1;

    public Guid ClientRequestId { get; set; }

    public Guid? ClientContinuationId { get; set; }

    public ApplicantType ApplicantType { get; set; }

    public SubmissionPath SubmissionPath { get; set; }

    public Guid? CategoryId { get; set; }

    public AppealCategory? Category { get; set; }

    public Guid? AppliedRoutingRuleId { get; set; }

    public int? AppliedRoutingRuleVersion { get; set; }

    public Guid? AppliedExpertGroupId { get; set; }

    public string? Narrative { get; set; }

    public AppealStatus Status { get; set; } = AppealStatus.New;

    public AppealPriority Priority { get; set; } = AppealPriority.Standard;

    public bool CrisisFlag { get; set; }

    public DateTimeOffset? CrisisDetectedAt { get; set; }

    public Guid? AssignedExpertId { get; set; }

    public DateTimeOffset? AssignedAt { get; set; }

    public AppealRejectionReason? RejectionReason { get; set; }

    public string? RejectionInternalReason { get; set; }

    public string? PublicResolution { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int Version { get; set; } = 1;

    public int ReturnCount { get; set; }

    public DateTimeOffset? ReturnedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<AppealAnswer> Answers { get; set; } = [];

    public List<AppealStatusChange> StatusHistory { get; set; } = [];

    public List<AppealAttachment> Attachments { get; set; } = [];

    public List<OperatorActionLog> OperatorActions { get; set; } = [];

    public List<AppealMessage> Messages { get; set; } = [];

    public List<AppealInternalNote> InternalNotes { get; set; } = [];

    public List<AppealRecommendation> Recommendations { get; set; } = [];

    public List<ExpertWorkflowRequest> WorkflowRequests { get; set; } = [];

    public List<AppealExpertParticipant> ExpertParticipants { get; set; } = [];

    public List<AppealAssignmentEvent> AssignmentHistory { get; set; } = [];

    public List<ApplicantOutcomeAction> OutcomeActions { get; set; } = [];

    public AppealResultFeedback? ResultFeedback { get; set; }

    public List<AppealComplaint> Complaints { get; set; } = [];

    public AppealCrisisContact? CrisisContact { get; set; }

    public List<CrisisContactAccessLog> CrisisContactAccesses { get; set; } = [];
}
