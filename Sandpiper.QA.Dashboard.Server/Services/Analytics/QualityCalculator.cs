using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public sealed class QualityRules
{
    public bool Enabled { get; set; }
    public double MinimumProgress { get; set; } = 95;
    public double MinimumPrimaryPass { get; set; } = 95;
    public int MaximumOpenLinkedDefects { get; set; }
    public int MinimumForecastDays { get; set; } = 7;
}

public static class QualityCalculator
{
    public static QualityEvidence Assess(PlanAnalytics snapshot, ExecutionIntelligence activity, QualityEvidence quality, QualityRules rules)
    {
        var reasons = quality.RiskReasons.ToList();
        var readiness = "Not Assessed — approved quality thresholds are not enabled";
        if (rules.Enabled)
        {
            if (!quality.Available || !quality.RelationshipsComplete || snapshot.Metrics.Unknown > 0 ||
                snapshot.Metrics.ExecutionRate == null || snapshot.Metrics.PrimaryPassRate == null || quality.OpenLinkedDefects == null)
                readiness = "Cannot assess — incomplete or unclassified evidence";
            else
            {
                var met = snapshot.Metrics.ExecutionRate >= rules.MinimumProgress && snapshot.Metrics.PrimaryPassRate >= rules.MinimumPrimaryPass &&
                    quality.OpenLinkedDefects <= rules.MaximumOpenLinkedDefects;
                readiness = met ? "Configured indicators met — human review required" : "Configured indicators not met — human review required";
                reasons.Add($"Rules: progress ≥ {rules.MinimumProgress}%; primary pass ≥ {rules.MinimumPrimaryPass}%; open linked defects ≤ {rules.MaximumOpenLinkedDefects}. No automatic sign-off.");
            }
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var days = (TimeZoneInfo.ConvertTime(activity.Window.To, zone).Date - TimeZoneInfo.ConvertTime(activity.Window.From, zone).Date).TotalDays;
        var newPoints = activity.Events.Where(e => e.Qualifies && !e.IsRepeat && e.PointId != null).Select(e => e.PointId).Distinct().Count();
        var remaining = snapshot.Metrics.Applicable - snapshot.Metrics.ExecutedApplicable;
        object? forecast = null;
        if (days >= rules.MinimumForecastDays && newPoints > 0 && activity.Complete && snapshot.Metrics.Unknown == 0)
        {
            var rate = newPoints / days;
            forecast = new { status = "Provisional scenario estimate, not a commitment", remainingApplicable = remaining,
                newlyExecutedBaselinePoints = newPoints, calendarDays = days, averagePerDay = rate,
                estimatedDays = Math.Ceiling(remaining / rate), fasterScenarioDays = Math.Ceiling(remaining / (rate * 1.5)),
                slowerScenarioDays = Math.Ceiling(remaining / (rate * .5)),
                assumptions = "Stable scope, representative UK calendar-day throughput, complete baseline. 50–150% throughput scenarios are not statistical confidence bounds. Corporate reconciliation pending." };
        }
        else reasons.Add("Forecast unavailable: requires complete history, classified outcomes and sufficient baseline first-execution days.");
        return quality with { ReleaseReadiness = readiness, RiskReasons = reasons, Forecast = forecast };
    }
}
