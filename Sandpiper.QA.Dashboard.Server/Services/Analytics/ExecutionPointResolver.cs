using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public sealed class ExecutionPointResolver(IReadOnlyList<NormalizedPoint> points)
{
    private readonly Dictionary<int, NormalizedPoint> byId = TestPointNormalizer.Merge(points).ToDictionary(p => p.Id);
    private readonly Dictionary<(int Case, int Suite, int Configuration), int[]> byIdentity = points
        .Where(p => p.TestCaseId > 0 && p.ConfigurationId > 0)
        .SelectMany(p => p.Memberships.Select(s => (Key: (p.TestCaseId!.Value, s.SuiteId, p.ConfigurationId!.Value), p.Id)))
        .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Select(p => p.Id).Distinct().ToArray());

    public (int? PointId, string Status) Resolve(JsonElement result)
    {
        static int Reference(JsonElement parent, string name) => parent.TryGetProperty(name, out var item) &&
            item.ValueKind == JsonValueKind.Object ? AzureDevOpsClient.Number(item, "id") : 0;
        var id = Reference(result, "testPoint");
        var testCase = Reference(result, "testCase");
        var suite = Reference(result, "testSuite");
        var configuration = Reference(result, "configuration");
        if (id > 0)
        {
            if (!byId.TryGetValue(id, out var point)) return (id, "UnknownPoint");
            if ((testCase > 0 && point.TestCaseId != testCase) ||
                (configuration > 0 && point.ConfigurationId != configuration) ||
                (suite > 0 && !point.Memberships.Any(s => s.SuiteId == suite)))
                return (null, "ConflictingIdentity");
            return (id, "Direct");
        }
        // Never guess by case alone: a case can have many suite/configuration points.
        if (testCase <= 0 || suite <= 0 || configuration <= 0) return (null, "MissingIdentity");
        if (!byIdentity.TryGetValue((testCase, suite, configuration), out var candidates)) return (null, "UnmappedIdentity");
        return candidates.Length == 1 ? (candidates[0], "ResolvedByIdentity") : (null, "AmbiguousIdentity");
    }
}
