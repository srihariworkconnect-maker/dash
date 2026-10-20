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
        var m = snapshot.Metrics;
        string readiness;
        if (!rules.Enabled)
        {
            // Never silently enable an unapproved release gate. Readiness stays "Not assessed".
            readiness = "Not Assessed — approved release-gate thresholds are not configured";
            reasons.Add("Release-gate thresholds are disabled (QualityRules.Enabled = false). Configure and approve " +
                "coverage, pass-rate and open-defect thresholds before an automatic Green/Amber/Red gate is shown.");
        }
        else if (!quality.Available || !quality.RelationshipsComplete || m.Unknown > 0 ||
            m.ExecutionRate == null || m.PrimaryPassRate == null || quality.OpenLinkedDefects == null || !activity.Complete)
        {
            readiness = "Cannot assess — mandatory evidence is incomplete or unclassified";
            if (!quality.Available) reasons.Add("Work-item/defect schema evidence is unavailable.");
            if (!quality.RelationshipsComplete) reasons.Add("Result-to-defect links are incomplete; the open-defect total is withheld.");
            if (m.Unknown > 0) reasons.Add($"{m.Unknown} test points have unclassified outcomes.");
            if (m.ExecutionRate == null || m.PrimaryPassRate == null) reasons.Add("Execution or pass-rate denominator is zero.");
            if (quality.OpenLinkedDefects == null) reasons.Add("Open linked-defect count is unavailable.");
            if (!activity.Complete) reasons.Add("Execution history is incomplete (missing result links or dates).");
        }
        else
        {
            var coverageOk = m.ExecutionRate >= rules.MinimumProgress;
            var passOk = m.PrimaryPassRate >= rules.MinimumPrimaryPass;
            var defectsOk = quality.OpenLinkedDefects <= rules.MaximumOpenLinkedDefects;
            var blockedOk = m.Blocked == 0;
            var rag = coverageOk && passOk && defectsOk && blockedOk ? "Green"
                : !passOk || !defectsOk ? "Red" : "Amber";
            readiness = $"{rag} — human review required";
            reasons.Add($"Execution coverage {Fmt(m.ExecutionRate)} vs ≥ {rules.MinimumProgress}% → {(coverageOk ? "met" : "not met")}.");
            reasons.Add($"Primary pass {Fmt(m.PrimaryPassRate)} vs ≥ {rules.MinimumPrimaryPass}% → {(passOk ? "met" : "not met")}.");
            reasons.Add($"Open linked defects {quality.OpenLinkedDefects} vs ≤ {rules.MaximumOpenLinkedDefects} → {(defectsOk ? "met" : "not met")}.");
            reasons.Add($"Blocked points {m.Blocked} → {(blockedOk ? "none" : "present (treated as release risk, Amber)")}.");
            reasons.Add("Thresholds are configured indicators only; no automatic sign-off is performed.");
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

    private static string Fmt(double? value) => value == null ? "Unavailable" : $"{value:0.##}%";
}
