using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class BugActivityTests
{
    [Fact]
    public void DailyCountsUseCreatorAndEachAttemptActorAndKeepUnknownReopens()
    {
        var day = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
        var creator = new SourceIdentity("creator", "Creator");
        var developer = new SourceIdentity("developer", "Developer");
        var tester = new SourceIdentity("tester", "Tester");
        var first = new ExecutionEvent("1:1", 1, 1, 10, "failed", "Completed", day.AddHours(5), tester, [20], null, true);
        var bug = new DefectEvidence(20, "Fixture", creator, day, developer, "Active", "InProgress", null, null,
            [new(1, "Active", "InProgress", day, creator), new(2, "Resolved", "Resolved", day.AddHours(1), developer),
             new(3, "Active", "InProgress", day.AddDays(1), null)], [first, first], "Reopened", null, null, null, [], 2);
        var activity = BugActivityCalculator.Calculate([bug], new(day, day.AddDays(2)), null);
        Assert.Equal(1, activity.Single(a => a.TesterKey == "creator").Raised);
        Assert.Equal(1, activity.Single(a => a.TesterKey == "developer").Resolved);
        Assert.Equal(1, activity.Single(a => a.TesterKey == "tester").VerificationAttempts);
        Assert.Equal("2026-10-20", activity.Single(a => a.TesterKey == "unattributed").Day);
        Assert.Equal(1, activity.Single(a => a.TesterKey == "unattributed").Reopened);
        Assert.Single(BugActivityCalculator.Calculate([bug], new(day, day.AddDays(2)), "tester"));
    }
}
