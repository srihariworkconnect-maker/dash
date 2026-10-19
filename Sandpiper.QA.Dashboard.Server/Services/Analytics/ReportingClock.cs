using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class ReportingClock
{
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
