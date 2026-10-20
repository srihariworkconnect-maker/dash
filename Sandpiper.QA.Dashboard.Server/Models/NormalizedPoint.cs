namespace Sandpiper.QA.Dashboard.Server.Models;

public record SourceIdentity(string Key, string DisplayName);
public record PointMembership(int SuiteId, string SuitePath);
public record NormalizedPoint(int Id, string? Outcome, int PlanId,
    int? TestCaseId, string? Title, int? ConfigurationId, string? ConfigurationName,
    SourceIdentity? AssignedTester, SourceIdentity? LatestExecutedBy,
    int? LatestRunId, int? LatestResultId, DateTimeOffset? LatestExecutionDate,
    string OutcomeJsonType, string? RawOutcome, string? ResultState, bool? IsActive,
    IReadOnlyList<PointMembership> Memberships) : TestPointSnapshot(Id, Outcome);

public record ReconciliationStatus(bool CollectionComplete, string ValidationState,
    int Unspecified, int MissingOutcome, int UnsupportedOutcomeType,
    int MissingCaseIdentity, IReadOnlyDictionary<string, int> RawOutcomeTypes,
    IReadOnlyList<string> Warnings);

public record ReferenceCounts(int Total, int Passed, int Failed, int Blocked, int Pending,
    int NotApplicable, string Reference, DateTimeOffset CapturedAt);
