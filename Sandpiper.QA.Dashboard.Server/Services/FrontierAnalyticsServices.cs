using System.Text.Json;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

namespace Sandpiper.QA.Dashboard.Server.Services;

// Preserve the original service and Frontier route while accepting additional plans.
public sealed class FrontierAnalyticsService(AzureDevOpsClient ado, IOptions<AzureDevOpsOptions> options, SnapshotCache? cache = null)
{
    public async Task<object> GetPlansAsync(CancellationToken ct) => new
    {
        value = (await ado.ListAsync("testplan/plans", ct))
            .GroupBy(plan => AzureDevOpsClient.Number(plan, "id"))
            .Select(group => new { id = group.Key, name = AzureDevOpsClient.Text(group.First(), "name") }).ToArray()
    };

    public Task<List<JsonElement>> GetSuitesAsync(int planId, CancellationToken ct) =>
        ado.ListAsync($"testplan/Plans/{Positive(planId)}/suites", ct);

    public Task<List<JsonElement>> GetPointsAsync(int planId, int suiteId, CancellationToken ct) =>
        ado.ListAsync($"testplan/Plans/{Positive(planId)}/Suites/{Positive(suiteId)}/TestPoint", ct);

    public async Task<PlanAnalytics> GetAnalyticsAsync(int planId, CancellationToken ct = default,
        int? suiteId = null, string? tester = null, string? category = null, string? outcome = null)
    {
        Positive(planId);
        var key = cache == null ? "" : await ado.ScopeKeyAsync(ct) + $":plan:{planId}";
        var full = cache == null ? await CollectAnalyticsAsync(planId, ct) : await cache.GetAsync(key,
            TimeSpan.FromSeconds(options.Value.CacheSeconds), token => CollectAnalyticsAsync(planId, token), ct);
        return ScopeProjector.Project(full, suiteId, tester, category, outcome) with { CacheAgeSeconds = Math.Max(0, (DateTimeOffset.UtcNow - full.RetrievedAt).TotalSeconds) };
    }

    private async Task<PlanAnalytics> CollectAnalyticsAsync(int planId, CancellationToken ct)
    {
        var snapshot = await LoadScopeAsync(planId, null, ct);
        var metrics = KpiCalculator.Calculate(snapshot.Points.Values.SelectMany(points => points));
        HashSet<int> Descendants(int id)
        {
            HashSet<int> ids = [id];
            var changed = true;
            while (changed) { changed = false; foreach (var s in snapshot.Suites)
                if (s.ParentId is { } parent && ids.Contains(parent)) changed |= ids.Add(s.Id); }
            return ids;
        }
        var descendants = snapshot.Suites.ToDictionary(s => s.Id, s => Descendants(s.Id));
        var suites = snapshot.Suites.Select(suite => new SuiteAnalytics(suite.Id, suite.Name,
            suite.ParentId, KpiCalculator.Calculate(snapshot.Points[suite.Id]))
            { RequirementId = suite.RequirementId, SuiteType = suite.Type,
              SubtreeMetrics = KpiCalculator.Calculate(snapshot.Points.Where(p => descendants[suite.Id].Contains(p.Key)).SelectMany(p => p.Value)) }).ToList();
        var points = TestPointNormalizer.Merge(snapshot.Points.Values.SelectMany(p => p));
        return SuiteCategoryClassifier.Apply(new(planId, null, metrics, suites, suites.Count, DateTimeOffset.UtcNow)
        { Points = points, Reconciliation = TestPointNormalizer.Reconcile(points), SourceBaseUrl = options.Value.BaseUrl }, options.Value.SuiteCategories);
    }

    public async Task<object> GetReexecutionAsync(int planId, int? suiteId,
        ReportingWindow window, CancellationToken ct)
    {
        if (window.From >= window.To) throw new ArgumentException("Reporting start must precede end.");
        var snapshot = await LoadScopeAsync(planId, suiteId, ct);
        var points = snapshot.Points.Values.SelectMany(values => values).Select(point => point.Id).ToHashSet();
        var runs = await ado.HistoryAsync("test/runs", new() { ["planId"] = planId.ToString() }, ct);
        var attempts = new List<ExecutionAttempt>();
        foreach (var run in runs)
        {
            if (run.TryGetProperty("plan", out var plan) && AzureDevOpsClient.Number(plan, "id") != planId)
                throw new InvalidDataException("History belongs to a different plan.");
            var runId = AzureDevOpsClient.Number(run, "id");
            var results = await ado.HistoryAsync($"test/Runs/{runId}/results", [], ct);
            foreach (var result in results)
            {
                var pointId = result.TryGetProperty("testPoint", out var point)
                    ? AzureDevOpsClient.Number(point, "id") : 0;
                var date = AzureDevOpsClient.Text(result, "completedDate");
                attempts.Add(new(runId, AzureDevOpsClient.Number(result, "id"), pointId > 0 ? pointId : null,
                    AzureDevOpsClient.Text(result, "outcome"), AzureDevOpsClient.Text(result, "state"),
                    TestPointNormalizer.ExecutionDate(date)));
            }
        }
        return new { planId, suiteId, metrics = KpiCalculator.Reexecution(attempts, points, window),
            runs = runs.Count, retrievedAt = DateTimeOffset.UtcNow, rulesVersion = "2026-10-19.1",
            scope = "Current selected points; retained accessible results in [from, to). Not an audit of deleted history." };
    }

    private async Task<ScopeSnapshot> LoadScopeAsync(int planId, int? selectedSuite, CancellationToken ct)
    {
        var elements = await GetSuitesAsync(planId, ct);
        var map = new Dictionary<int, SuiteInfo>();
        foreach (var element in elements) Collect(element, map, null);
        foreach (var suite in map.Values)
        {
            HashSet<int> seen = [suite.Id];
            var ancestor = suite.ParentId;
            while (ancestor != null && map.TryGetValue(ancestor.Value, out var parent))
            {
                if (!seen.Add(ancestor.Value)) throw new InvalidDataException("Cyclic suite hierarchy.");
                ancestor = parent.ParentId;
            }
        }
        if (selectedSuite != null && !map.ContainsKey(selectedSuite.Value))
            throw new ArgumentException("Selected suite does not belong to this plan.");
        var included = map.Keys.ToHashSet();
        if (selectedSuite != null)
        {
            included = [selectedSuite.Value];
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var suite in map.Values)
                    if (suite.ParentId != null && included.Contains(suite.ParentId.Value))
                        changed |= included.Add(suite.Id);
            }
        }
        var suites = map.Values.Where(suite => included.Contains(suite.Id)).ToList();
        string Path(SuiteInfo suite) => suite.ParentId is { } parent && map.TryGetValue(parent, out var node)
            ? Path(node) + " / " + suite.Name : suite.Name;
        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, List<NormalizedPoint>>();
        await Parallel.ForEachAsync(suites, new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxConcurrency, CancellationToken = ct
        }, async (suite, token) =>
        {
            var raw = await GetPointsAsync(planId, suite.Id, token);
            results[suite.Id] = raw.Select(point => TestPointNormalizer.Normalize(point, planId, suite.Id, Path(suite))).ToList();
        });
        return new(suites, results.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private static void Collect(JsonElement element, Dictionary<int, SuiteInfo> suites, int? parent)
    {
        var id = AzureDevOpsClient.Number(element, "id");
        if (id <= 0) throw new InvalidDataException("Invalid suite identity.");
        if (element.TryGetProperty("parentSuite", out var reference))
        {
            var parentId = AzureDevOpsClient.Number(reference, "id");
            if (parentId > 0) parent = parentId;
        }
        var requirement = AzureDevOpsClient.Number(element, "requirementId");
        var suite = new SuiteInfo(id, AzureDevOpsClient.Text(element, "name") ?? "", parent,
            requirement > 0 ? requirement : null, AzureDevOpsClient.Text(element, "suiteType"));
        if (suites.TryGetValue(id, out var existing) && existing != suite)
            throw new InvalidDataException("Conflicting suite metadata.");
        suites[id] = suite;
        if (element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray()) Collect(child, suites, id);
    }

    private static int Positive(int value) => value > 0 ? value : throw new ArgumentException("Invalid plan or suite ID.");
    private record SuiteInfo(int Id, string Name, int? ParentId, int? RequirementId, string? Type);
    private record ScopeSnapshot(List<SuiteInfo> Suites, Dictionary<int, List<NormalizedPoint>> Points);
}
