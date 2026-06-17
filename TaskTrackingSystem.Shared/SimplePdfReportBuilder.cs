using System.Text;

namespace TaskTrackingSystem.Shared;

public static class SimplePdfReportBuilder
{
    private const int PageWidth = 612;
    private const int PageHeight = 792;
    private const int LeftMargin = 48;
    private const int TopMargin = 52;
    private const int BottomMargin = 48;
    private const int TitleFontSize = 16;
    private const int BodyFontSize = 10;
    private const int LineSpacing = 14;
    private const int BodyLinesPerPage = 42;

    public static byte[] BuildTableReport(
        string title,
        IEnumerable<string>? summaryLines,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var lines = new List<string>();

        if (summaryLines != null)
        {
            foreach (var line in summaryLines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                lines.AddRange(WrapLine(line.Trim()));
            }
        }

        if (headers.Count > 0)
        {
            lines.Add(string.Join(" | ", headers));
            lines.Add(new string('-', Math.Min(96, Math.Max(24, headers.Sum(h => h.Length + 3)))));
        }

        foreach (var row in rows)
        {
            lines.AddRange(WrapLine(string.Join(" | ", row.Select(NormalizeCell))));
        }

        if (lines.Count == 0)
        {
            lines.Add("No data available.");
        }

        return BuildTextReport(title, lines);
    }

    public static byte[] BuildTextReport(string title, IEnumerable<string> bodyLines)
    {
        var normalizedLines = bodyLines
            .SelectMany(line => WrapLine(line))
            .ToList();

        var pages = normalizedLines
            .Chunk(BodyLinesPerPage)
            .Select(chunk => chunk.ToList())
            .ToList();

        if (pages.Count == 0)
        {
            pages.Add(new List<string> { "No data available." });
        }

        var objects = new List<string>();
        objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        var pageObjectIds = new List<int>();
        var contentObjectIds = new List<int>();
        var nextObjectId = 4;

        foreach (var _ in pages)
        {
            pageObjectIds.Add(nextObjectId++);
            contentObjectIds.Add(nextObjectId++);
        }

        var pagesKids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        objects.Add($"2 0 obj\n<< /Type /Pages /Kids [{pagesKids}] /Count {pages.Count} >>\nendobj\n");
        objects.Add("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageObjectId = pageObjectIds[pageIndex];
            var contentObjectId = contentObjectIds[pageIndex];
            var pageLines = pages[pageIndex];
            var content = BuildContentStream(title, pageLines, pageIndex + 1, pages.Count);
            var contentBytes = Encoding.ASCII.GetBytes(content);

            objects.Add(
                $"{pageObjectId} 0 obj\n" +
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>\n" +
                "endobj\n");

            objects.Add(
                $"{contentObjectId} 0 obj\n" +
                $"<< /Length {contentBytes.Length} >>\n" +
                "stream\n" +
                content +
                "\nendstream\nendobj\n");
        }

        var buffer = new StringBuilder();
        buffer.Append("%PDF-1.4\n");

        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(buffer.ToString()));
            buffer.Append(obj);
        }

        var xrefStart = Encoding.ASCII.GetByteCount(buffer.ToString());
        var xref = new StringBuilder();
        xref.Append("xref\n");
        xref.Append($"0 {objects.Count + 1}\n");
        xref.Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            xref.Append($"{offset:D10} 00000 n \n");
        }

        xref.Append("trailer\n");
        xref.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        xref.Append("startxref\n");
        xref.Append($"{xrefStart}\n");
        xref.Append("%%EOF");

        buffer.Append(xref);
        return Encoding.ASCII.GetBytes(buffer.ToString());
    }

    private static string BuildContentStream(string title, IReadOnlyList<string> lines, int pageNumber, int totalPages)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 16 Tf");
        content.AppendLine($"{LeftMargin} {PageHeight - TopMargin} Td");
        content.AppendLine($"({EscapeText(title)}) Tj");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine($"0 -22 Td");
        content.AppendLine($"({EscapeText($"Page {pageNumber} of {totalPages}")}) Tj");

        foreach (var line in lines)
        {
            content.AppendLine($"0 -{LineSpacing} Td");
            content.AppendLine($"({EscapeText(line)}) Tj");
        }

        content.AppendLine("ET");
        return content.ToString();
    }

    private static IEnumerable<string> WrapLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield return string.Empty;
            yield break;
        }

        const int maxWidth = 92;
        var remaining = line.Trim();

        while (remaining.Length > maxWidth)
        {
            var breakIndex = remaining.LastIndexOf(' ', maxWidth);
            if (breakIndex <= 0)
            {
                breakIndex = maxWidth;
            }

            yield return remaining[..breakIndex].Trim();
            remaining = remaining[breakIndex..].TrimStart();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            yield return remaining;
        }
    }

    private static string NormalizeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string EscapeText(string value)
    {
        var ascii = new string(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
        return ascii
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}
