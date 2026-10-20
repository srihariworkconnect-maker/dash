namespace Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

public record AuditEntry(DateTimeOffset At, string Method, string Route, int Status,
    string TraceId, long DurationMs);
public sealed class AuditTrail
{
    private readonly Queue<AuditEntry> entries = new();
    private readonly object gate = new();
    public void Add(AuditEntry entry)
    {
        lock (gate) { entries.Enqueue(entry); while (entries.Count > 500) entries.Dequeue(); }
    }
    public IReadOnlyList<AuditEntry> List() { lock (gate) return entries.Reverse().ToArray(); }
}
