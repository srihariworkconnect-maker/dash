namespace Sandpiper.QA.Dashboard.Server.Models;

public record WorkItemRevision(int Revision, string? State, string? Category,
    DateTimeOffset? ChangedAt, SourceIdentity? ChangedBy);
public record CaseEnrichment(int CaseId, string? Priority, IReadOnlyList<int> RequirementIds,
    IReadOnlyList<string> EvidenceLinks);
public record DefectEvidence(int BugId, string? Title, SourceIdentity? CreatedBy,
    DateTimeOffset? CreatedAt, SourceIdentity? AssignedTo, string? State, string? Category,
    string? Severity, string? Priority, IReadOnlyList<WorkItemRevision> Revisions,
    IReadOnlyList<ExecutionEvent> RelatedResults, string VerificationStatus,
    SourceIdentity? RetestedBy, DateTimeOffset? RetestedAt, string? VerificationOutcome,
    IReadOnlyList<string> VerificationEvidence, double? AgeDays)
{
    public DateTimeOffset? VerifiedAt { get; init; }
}
public record BugActivity(string TesterKey, string Tester, int Raised, int Resolved, int Reopened,
    int VerifiedWithEvidence, int VerificationAttempts)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Day { get; init; }
}
public record QualityEvidence(bool Available, string SchemaStatus, DateTimeOffset RetrievedAt,
    IReadOnlyList<string> InspectedFields, IReadOnlyDictionary<string, string> StateCategories,
    IReadOnlyList<CaseEnrichment> Cases, IReadOnlyList<DefectEvidence> Defects,
    IReadOnlyList<BugActivity> BugActivity, int? OpenLinkedDefects, int? RequirementGaps,
    string ReleaseReadiness, IReadOnlyList<string> RiskReasons, object? Forecast,
    IReadOnlyList<string> Limitations)
{
    public bool RelationshipsComplete { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BlockerEvidence>? Blockers { get; init; }
}
public record BlockerEvidence(int PointId, DateTimeOffset? ObservedSince, double? ObservedAgeDays, string EvidenceStatus);
public record WorkspaceEvidence(PlanAnalytics Snapshot, ExecutionIntelligence Activity,
    QualityEvidence Quality, DateTimeOffset RetrievedAt);
