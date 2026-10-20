namespace Sandpiper.QA.Dashboard.Server.Models;

public record ExecutionEvent(string Key, int RunId, int ResultId, int? PointId,
    string Outcome, string? State, DateTimeOffset? CompletedAt, SourceIdentity? Performer,
    IReadOnlyList<int> BugIds, string? SourceUrl, bool Qualifies, bool IsRepeat = false,
    string? Transition = null)
{
    public bool BugLinksAvailable { get; init; }
}
public record TesterActivity(string TesterKey, string Tester, string Day, int AssignedPoints,
    int PendingAssigned, int ExecutionEvents, int UniquePoints, int Passed, int Failed,
    int Blocked, int RepeatEvents, int ReExecutedPoints);
public record DailyActivity(string Day, int ExecutionEvents, int UniquePoints, int RepeatEvents,
    int Passed, int Failed, int Blocked);
public record PointActivity(int PointId, int ExecutionCount, int RepeatCount);
public record ExecutionIntelligence(ReportingWindow Window, ReportingWindow Baseline,
    IReadOnlyList<ExecutionEvent> Events, IReadOnlyList<TesterActivity> Productivity,
    IReadOnlyList<DailyActivity> Daily, IReadOnlyList<PointActivity> Points,
    int? ReExecutedPoints, int RepeatEvents, int UniquePoints, int MissingPointLinks,
    int MissingDates, int AmbiguousChronology, bool Complete, string Attribution,
    IReadOnlyList<string> Limitations);
