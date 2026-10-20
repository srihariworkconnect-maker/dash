namespace Sandpiper.QA.Dashboard.Server.Models;

public record CaseMetrics(int? TotalTestCases, int? ExecutedTestCases, double? ExecutionCoverage,
    int KnownTestCases, int KnownExecutedTestCases, int MissingCaseMappings, string State)
{
    public IReadOnlyList<int> TestCaseIds { get; init; } = [];
    public IReadOnlyList<int> ExecutedCaseIds { get; init; } = [];
    public int UnclassifiedCases { get; init; }
}
public record EvidenceAssessment(string State, IReadOnlyList<string> Reasons);
public record ReexecutedCase(int CaseId, IReadOnlyList<int> PointIds, IReadOnlyList<ExecutionEvent> Attempts);
public record CaseRetesting(int? ReExecutedTestCases, int KnownReExecutedTestCases, int MissingCaseMappings,
    IReadOnlyList<ReexecutedCase> Cases, string State);
