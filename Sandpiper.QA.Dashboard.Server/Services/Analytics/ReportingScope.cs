using System.Globalization;
using System.Text.RegularExpressions;
using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;
public static class ReportingScope
{
    public static DateTimeOffset Instant(string value) => Regex.IsMatch(value, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
        ? parsed.ToUniversalTime() : throw new ArgumentException("Reporting dates require an explicit UTC offset.");
    public static ReportingWindow Window(string from, string to)
    {
        var window = new ReportingWindow(Instant(from), Instant(to));
        if (window.From >= window.To) throw new ArgumentException("Start must precede end.");
        return window;
    }
}
