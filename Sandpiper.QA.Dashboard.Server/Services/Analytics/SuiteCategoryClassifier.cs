using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class SuiteCategoryClassifier
{
    public static PlanAnalytics Apply(PlanAnalytics full, IReadOnlyList<SuiteCategoryRule> rules)
    {
        if (rules.Any(r => r.PlanId <= 0 || r.SuiteId <= 0 || string.IsNullOrWhiteSpace(r.Category) || r.Category.Length > 80) ||
            rules.GroupBy(r => (r.PlanId, r.SuiteId)).Any(g => g.Count() > 1))
            throw new InvalidDataException("Suite category configuration requires unique plan/suite IDs and a nonempty category up to 80 characters.");
        var map = full.Suites.ToDictionary(s => s.SuiteId);
        var configured = rules.Where(r => r.PlanId == full.PlanId).ToDictionary(r => r.SuiteId);
        if (configured.Keys.Any(id => !map.ContainsKey(id)))
            throw new InvalidDataException("A configured testing-category suite is absent from the returned plan hierarchy; review configuration and ADO access.");
        string Category(int id)
        {
            var own = id; var seen = new HashSet<int>();
            while (seen.Add(id) && map.TryGetValue(id, out var suite))
            {
                if (configured.TryGetValue(id, out var rule) && (id == own || rule.IncludeDescendants)) return rule.Category.Trim();
                if (suite.ParentSuiteId is not { } parent) break;
                id = parent;
            }
            return "Unclassified";
        }
        return full with { AvailableCategories = configured.Values.Select(r => r.Category.Trim()).Append("Unclassified").Distinct().Order().ToArray(),
            Points = full.Points.Select(p => p with {
            TestingCategories = p.Memberships.Select(m => Category(m.SuiteId)).Distinct().Order().ToArray(),
            Memberships = p.Memberships.Select(m => m with { TestingCategory = Category(m.SuiteId) }).ToArray() }).ToArray() };
    }
}
