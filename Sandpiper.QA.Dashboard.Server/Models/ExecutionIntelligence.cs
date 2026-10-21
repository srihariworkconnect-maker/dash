namespace Sandpiper.QA.Dashboard.Server.Models;

public record ExecutionEvent(string Key, int RunId, int ResultId, int? PointId,
    string Outcome, string? State, DateTimeOffset? CompletedAt, SourceIdentity? Performer,
    IReadOnlyList<int> BugIds, string? SourceUrl, bool Qualifies, bool IsRepeat = false,
    string? Transition = null)
{
    public bool BugLinksAvailable { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PointMapping { get; init; }
}
public record TesterActivity(string TesterKey, string Tester, string Day, int AssignedPoints,
    int PendingAssigned, int ExecutionEvents, int UniquePoints, int Passed, int Failed,
    int Blocked, int RepeatEvents, int ReExecutedPoints);
public record DailyActivity(string Day, int ExecutionEvents, int UniquePoints, int RepeatEvents,
    int Passed, int Failed, int Blocked);
public record PointActivity(int PointId, int ExecutionCount, int RepeatCount);
public record ReexecutionPoint(int PointId, int? TestCaseId, string? Title,
    IReadOnlyList<PointMembership> Suites, int ExecutionCount, int ReexecutionCount,
    IReadOnlyList<ExecutionEvent> Attempts);
public record ReexecutionReport(ReportingWindow Window, int? ReexecutedTestPoints,
    int? AdditionalAttempts, int KnownReexecutedTestPoints, int KnownAdditionalAttempts,
    string State, IReadOnlyList<string> Reasons, IReadOnlyList<ReexecutionPoint> Points)
{
    public ReexecutionDiagnostics? Diagnostics { get; init; }
}
public record ReexecutionDiagnostics(int SourceResults, int UniqueResults, int DuplicatesRemoved,
    int QualifyingExecutions, int UniqueTestPoints, int RepeatedTestPoints, int AdditionalAttempts,
    IReadOnlyDictionary<string, int> Excluded, IReadOnlyDictionary<string, int> Mapping);
public record ExecutionIntelligence(ReportingWindow Window, ReportingWindow Baseline,
    IReadOnlyList<ExecutionEvent> Events, IReadOnlyList<TesterActivity> Productivity,
    IReadOnlyList<DailyActivity> Daily, IReadOnlyList<PointActivity> Points,
    int? ReExecutedPoints, int RepeatEvents, int UniquePoints, int MissingPointLinks,
    int MissingDates, int AmbiguousChronology, bool Complete, string Attribution,
    IReadOnlyList<string> Limitations)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReexecutionReport? Reexecution { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReexecutionReport? BaselineReexecution { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CaseRetesting? CaseRetesting { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EvidenceAssessment? Evidence { get; init; }
}
