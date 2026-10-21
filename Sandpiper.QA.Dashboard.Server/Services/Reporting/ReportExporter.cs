using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Reporting;

public static class ReportExporter
{
    private static readonly Lazy<bool> Fonts = new(() =>
    { if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true; return true; });
    public static byte[] Excel(IReadOnlyList<ReportTable> tables)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Add(string path, string text)
            {
                var entry = zip.CreateEntry(path); entry.LastWriteTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(text);
            }
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Add("[Content_Types].xml", new XElement(ct + "Types",
                new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                tables.Select((_, i) => new XElement(ct + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))).ToString());
            Add("_rels/.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", office.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))).ToString());
            Add("xl/workbook.xml", new XElement(main + "workbook", new XAttribute(XNamespace.Xmlns + "r", office), new XElement(main + "sheets", tables.Select((table, i) => new XElement(main + "sheet", new XAttribute("name", table.Name[..Math.Min(31, table.Name.Length)]), new XAttribute("sheetId", i + 1), new XAttribute(office + "id", $"rId{i + 1}"))))).ToString());
            Add("xl/_rels/workbook.xml.rels", new XElement(rel + "Relationships", tables.Select((_, i) => new XElement(rel + "Relationship", new XAttribute("Id", $"rId{i + 1}"), new XAttribute("Type", office.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")))).ToString());
            for (var i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                // Inline strings prevent formula injection and preserve Unicode without another dependency.
                var data = new[] { table.Columns }.Concat(table.Rows);
                Add($"xl/worksheets/sheet{i + 1}.xml", new XElement(main + "worksheet", new XElement(main + "sheetData",
                    data.Select((row, n) => new XElement(main + "row", new XAttribute("r", n + 1), row.Select(value =>
                        new XElement(main + "c", new XAttribute("t", "inlineStr"), new XElement(main + "is", new XElement(main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Clean(value ?? "Unavailable"))))))))).ToString());
            }
        }
        return memory.ToArray();
    }
    private static string Clean(string text) => string.Concat(text.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t'));

    public static byte[] Pdf(ReportSnapshot report)
        => PdfTables(ReportService.Tables(report), $"Sandpiper {report.Kind} evidence draft", report.CreatedAt, report.Status);

    public static byte[] PdfTables(IReadOnlyList<ReportTable> tables, string title, DateTimeOffset createdAt, string status)
    {
        _ = Fonts.Value;
        using var document = new PdfDocument();
        document.Info.Title = title;
        document.Info.CreationDate = createdAt.UtcDateTime;
        var font = new XFont("Arial", 9, XFontStyleEx.Regular);
        XGraphics? graphics = null;
        double y = 0;
        void Line(string value)
        {
            // Wrap to fit, rather than clipping confidential case titles or source references.
            foreach (var rawLine in Clean(value).Replace("\r", "").Split('\n'))
            {
                var remaining = rawLine;
                do
                {
                    if (graphics == null || y > 790)
                    {
                        graphics?.Dispose(); var page = document.AddPage(); page.Size = PdfSharp.PageSize.A4;
                        graphics = XGraphics.FromPdfPage(page); y = 35;
                    }
                    var length = Math.Min(remaining.Length, 100);
                    while (length > 1 && graphics.MeasureString(remaining[..length], font).Width > 520) length--;
                    graphics.DrawString(remaining[..length], font, XBrushes.Black, new XPoint(35, y)); y += 13;
                    remaining = remaining[length..];
                } while (remaining.Length > 0);
            }
        }
        Line(title); Line(status);
        foreach (var table in tables)
        {
            Line(""); Line(table.Name.ToUpperInvariant()); Line(string.Join(" | ", table.Columns));
            foreach (var row in table.Rows) Line(string.Join(" | ", row.Select(v => v ?? "Unavailable")));
        }
        graphics?.Dispose();
        using var memory = new MemoryStream(); document.Save(memory, false); return memory.ToArray();
    }
}
