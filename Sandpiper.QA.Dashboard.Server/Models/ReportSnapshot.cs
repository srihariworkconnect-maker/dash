namespace Sandpiper.QA.Dashboard.Server.Models;

public record ReportSnapshot(Guid Id, string Kind, DateTimeOffset CreatedAt,
    string CalculationVersion, string Status, string Origin, string Checksum,
    PlanAnalytics CurrentSnapshot, ExecutionIntelligence Activity,
    IReadOnlyDictionary<string, string> Narrative, IReadOnlyList<string> Limitations);
public record ReportRequest(int PlanId, int? SuiteId, string? Tester, string From,
    string To, string BaselineFrom, string Kind, Dictionary<string, string>? Narrative);
public record ReportTable(string Name, string[] Columns, IReadOnlyList<string?[]> Rows);
