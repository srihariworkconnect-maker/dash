namespace Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

public sealed class SnapshotCache : IDisposable
{
    private readonly Dictionary<string, (object Value, DateTimeOffset Expires, DateTimeOffset Created)> entries = [];
    private readonly Dictionary<string, SemaphoreSlim> loads = [];
    private int generation;
    private readonly object gate = new();
    public async Task<T> GetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> collect, CancellationToken ct)
    {
        if (ttl <= TimeSpan.Zero) return await collect(ct);
        bool Try(out T value)
        {
            lock (gate)
            {
                if (entries.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow && entry.Value is T typed)
                { value = typed; return true; }
                entries.Remove(key); value = default!; return false;
            }
        }
        if (Try(out var cached)) return cached;
        SemaphoreSlim load;
        bool retained;
        int epoch;
        lock (gate)
        {
            epoch = generation;
            retained = loads.TryGetValue(key, out load!);
            if (!retained) { load = new(1); if (loads.Count < 128) { loads[key] = load; retained = true; } }
        }
        await load.WaitAsync(ct);
        try
        {
            if (Try(out cached)) return cached;
            var value = await collect(ct); ct.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (epoch != generation) return value;
                foreach (var expired in entries.Where(p => p.Value.Expires <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) entries.Remove(expired);
                if (entries.Count >= 64) entries.Remove(entries.MinBy(p => p.Value.Created).Key);
                entries[key] = (value!, DateTimeOffset.UtcNow.Add(ttl), DateTimeOffset.UtcNow);
            }
            return value;
        }
        finally { load.Release(); if (!retained) load.Dispose(); }
    }
    public void Clear() { lock (gate) { entries.Clear(); generation++; } }
    public void Dispose() { lock (gate) foreach (var value in loads.Values) value.Dispose(); }
}
