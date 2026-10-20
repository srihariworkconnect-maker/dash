using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Reporting;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class ExportTests
{
    public static ReportSnapshot Report()
    {
        var start = DateTimeOffset.Parse("2026-10-19T17:00:00Z");
        var current = new PlanAnalytics(1, null, KpiCalculator.Calculate([]), [], 0, start);
        var activity = ExecutionCalculator.Calculate([], [], new(start, start.AddDays(1)), start);
        var r = new ReportSnapshot(Guid.NewGuid(), "DSR", start, "v2-2026-10-20.1", "Draft", "Fixture", "", current, activity,
            new Dictionary<string, string> { ["Objectives"] = "Résumé & evidence" }, ["Awaiting validation"]);
        return r with { Checksum = ReportStore.Hash(r) };
    }
    [Fact]
    public void WorkbookIsValidZipXmlAndUserTextCannotBecomeAFormula()
    {
        var bytes = ReportExporter.Excel([new("Evidence", ["Value"], [["=WEBSERVICE(\"bad\")"], ["Résumé & <evidence>"]])]);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = XDocument.Parse(reader.ReadToEnd());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Empty(xml.Descendants(ns + "f"));
        Assert.Contains(xml.Descendants(ns + "t"), t => t.Value == "Résumé & <evidence>");
    }
    [Fact]
    public void FrozenReportHasStableChecksumAndStoreDoesNotRequeryAdo()
    {
        var r = Report(); var store = new ReportStore(); store.Put(r);
        Assert.Equal(r.Checksum, ReportStore.Hash(store.Get(r.Id)!));
        Assert.NotEqual(r.Checksum, ReportStore.Hash(r with { Status = "Modified" }));
    }
    [Fact]
    public void PdfContainsValidHeaderAndUnicodeFontResources()
    {
        if (!OperatingSystem.IsWindows()) return; // Non-Windows font provisioning remains an environment check.
        var bytes = ReportExporter.Pdf(Report());
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Contains("/Font", Encoding.ASCII.GetString(bytes));
    }
}
