using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class ReportingClock
{
    public static ReportingWindow Preset(DateTimeOffset now, string preset, TimeOnly cutoff)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var date = TimeZoneInfo.ConvertTime(now, zone).Date;
        if (preset == "Yesterday") date = date.AddDays(-1);
        if (preset is not ("Today" or "Yesterday" or "Last 7 Days" or "Last Month" or "Last 2 Months"))
            throw new ArgumentException("Use explicit offset-aware dates for custom/release scope.");
        var end = DateTime.SpecifyKind(date.Add(cutoff.ToTimeSpan()), DateTimeKind.Unspecified);
        if (preset is "Last Month" or "Last 2 Months")
        {
            // Complete calendar months in UK local time, bounded by the configured cutoff.
            var monthEnd = DateTime.SpecifyKind(new DateTime(date.Year, date.Month, 1).Add(cutoff.ToTimeSpan()), DateTimeKind.Unspecified);
            return new(ToInstant(monthEnd.AddMonths(preset == "Last Month" ? -1 : -2), zone), ToInstant(monthEnd, zone));
        }
        return new(ToInstant(end.AddDays(preset == "Last 7 Days" ? -7 : -1), zone), ToInstant(end, zone));
    }
    public static ReportingWindow LastDsrPeriod(DateTimeOffset now, TimeOnly cutoff)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var end = DateTime.SpecifyKind(localNow.Date.Add(cutoff.ToTimeSpan()), DateTimeKind.Unspecified);
        if (localNow.DateTime < end) end = end.AddDays(-1);
        return new(ToInstant(end.AddDays(-1), zone), ToInstant(end, zone));
    }

    private static DateTimeOffset ToInstant(DateTime value, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(value) || zone.IsAmbiguousTime(value))
            throw new ArgumentException("Configured cutoff falls in an ambiguous or invalid UK clock interval.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(value, zone));
    }
}
