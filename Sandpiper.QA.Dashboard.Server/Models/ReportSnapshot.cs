namespace Sandpiper.QA.Dashboard.Server.Models;

public record ReportSnapshot(Guid Id, string Kind, DateTimeOffset CreatedAt,
    string CalculationVersion, string Status, string Origin, string Checksum,
    PlanAnalytics CurrentSnapshot, ExecutionIntelligence Activity,
    IReadOnlyDictionary<string, string> Narrative, IReadOnlyList<string> Limitations)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public QualityEvidence? Quality { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FrozenReportingScope? Scope { get; init; }
}
public record FrozenReportingScope(string? Category, string? Outcome, string? Tester, string? Preset,
    string? ReleaseReference, DateTimeOffset CurrentStateCollectedAt, DateTimeOffset RequestedAsOf,
    string HistoricalAsOfStatus, string MembershipBasis);
public record ReportRequest(int PlanId, int? SuiteId, string? Tester, string From,
    string To, string BaselineFrom, string Kind, Dictionary<string, string>? Narrative)
{
    public string? Category { get; init; }
    public string? Outcome { get; init; }
    public string? Preset { get; init; }
    public string? ReleaseReference { get; init; }
}
public record ReportTable(string Name, string[] Columns, IReadOnlyList<string?[]> Rows);
