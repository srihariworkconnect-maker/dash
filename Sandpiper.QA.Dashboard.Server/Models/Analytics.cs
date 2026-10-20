namespace Sandpiper.QA.Dashboard.Server.Models;

public record TestPointSnapshot(int Id, string? Outcome);
public record ExecutionAttempt(int RunId, int ResultId, int? PointId, string? Outcome,
    string? State, DateTimeOffset? CompletedAt);
public record ReportingWindow(DateTimeOffset From, DateTimeOffset To);
public record AssignedWorkload(string TesterKey, string Tester, int AssignedNow, int PendingNow);

public record QaMetrics(int Total, int Passed, int Failed, int Blocked, int Pending,
    int NotApplicable, int InProgress, int OtherCompleted, int Unknown, int Executed,
    int Applicable, int ExecutedApplicable, double? ExecutionRate,
    double? PrimaryPassRate, double? SecondaryPassRate,
    IReadOnlyDictionary<string, int> Statuses);

public record ReexecutionMetrics(int? ReExecuted, int LinkedReExecuted,
    int AdditionalExecutions, int Executions, int UnlinkedResults, int UndatedResults,
    bool Complete, ReportingWindow Window);

public record SuiteAnalytics(int SuiteId, string SuiteName, int? ParentSuiteId,
    QaMetrics Metrics)
{
    public QaMetrics? SubtreeMetrics { get; init; }
    public int Total => Metrics.Total;
    public int Passed => Metrics.Passed;
    public int Failed => Metrics.Failed;
    public int Blocked => Metrics.Blocked;
    public int Pending => Metrics.Pending;
    public int Other => Metrics.NotApplicable + Metrics.InProgress + Metrics.OtherCompleted + Metrics.Unknown;
}

public record PlanAnalytics(int PlanId, int? SuiteId, QaMetrics Metrics,
    IReadOnlyList<SuiteAnalytics> Suites, int SuiteCount, DateTimeOffset RetrievedAt,
    string RulesVersion = "2026-10-19.1", bool Connected = true, bool Complete = true)
{
    // Keep the original Frontier endpoint's aggregate fields for existing consumers.
    public IReadOnlyList<NormalizedPoint> Points { get; init; } = [];
    public ReconciliationStatus? Reconciliation { get; init; }
    public string? SourceBaseUrl { get; init; }
    public double CacheAgeSeconds { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AssignedWorkload>? Assignments { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CaseMetrics? CaseMetrics { get; init; }
    public int Total => Metrics.Total;
    public int Passed => Metrics.Passed;
    public int Failed => Metrics.Failed;
    public int Blocked => Metrics.Blocked;
    public int Pending => Metrics.Pending;
    public int Other => Metrics.NotApplicable + Metrics.InProgress + Metrics.OtherCompleted + Metrics.Unknown;
    public double? ExecutionPercentage => Metrics.ExecutionRate;
    public double? PassPercentage => Metrics.PrimaryPassRate;
}
