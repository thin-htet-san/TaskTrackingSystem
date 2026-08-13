using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace TaskTrackingSystem.Shared;

public static class SimplePdfReportBuilder
{
    private const float LetterWidth = 612;
    private const float LetterHeight = 792;
    private const float PageMargin = 32;
    private const float FooterHeight = 30;
    private const float TableCellPadding = 7;
    private const float TableLineHeight = 11.5f;
    private const float TableHeaderLineHeight = 11.5f;
    private const int MaximumCellLines = 5;
    private const string FontPathEnvironmentVariable = "TASKTRACKING_PDF_FONT_PATH";

    private static readonly string[] RequiredGlyphSamples = ["ABC0123|:-", "\u1019\u103C\u1014\u103A\u1019\u102C"];
    private static readonly SKColor BrandDark = SKColor.Parse("#12312D");
    private static readonly SKColor BrandPrimary = SKColor.Parse("#0F766E");
    private static readonly SKColor BrandAccent = SKColor.Parse("#B45309");
    private static readonly SKColor BrandLight = SKColor.Parse("#E6F4F1");
    private static readonly SKColor TextMuted = SKColor.Parse("#64706D");
    private static readonly SKColor BorderColor = SKColor.Parse("#D6E2DF");
    private static readonly SKColor StripeColor = SKColor.Parse("#F7FAF9");

    public static byte[] BuildTableReport(
        string title,
        IEnumerable<string>? summaryLines,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        if (headers.Count == 0)
        {
            return BuildTextReport(title, rows.SelectMany(row => row));
        }

        var normalizedHeaders = headers.Select(NormalizeCell).ToList();
        var normalizedRows = rows
            .Select(row => NormalizeRow(row, normalizedHeaders.Count))
            .ToList();
        var summaries = ParseSummaryItems(summaryLines).ToList();
        var landscape = normalizedHeaders.Count >= 6;
        var pageWidth = landscape ? LetterHeight : LetterWidth;
        var pageHeight = landscape ? LetterWidth : LetterHeight;
        var tableWidth = pageWidth - (PageMargin * 2);

        using var typeface = LoadReportTypeface();
        using var shaper = new SKShaper(typeface);
        using var tableFont = new SKFont(typeface, landscape ? 8.2f : 8.6f);
        using var tableHeaderFont = new SKFont(typeface, landscape ? 8.4f : 8.8f);

        var columnWidths = CalculateColumnWidths(
            normalizedHeaders,
            normalizedRows,
            tableWidth,
            shaper,
            tableFont,
            tableHeaderFont);
        var headerCells = normalizedHeaders
            .Select((header, index) => WrapText(header, columnWidths[index] - (TableCellPadding * 2), shaper, tableHeaderFont, 3))
            .ToList();
        var tableHeaderHeight = Math.Max(
            34,
            headerCells.Max(cell => cell.Count) * TableHeaderLineHeight + (TableCellPadding * 2));
        var rowLayouts = normalizedRows
            .Select(row => CreateRowLayout(row, columnWidths, shaper, tableFont))
            .ToList();

        var firstTableTop = CalculateFirstTableTop(summaries.Count, landscape);
        var continuedTableTop = 86f;
        var pages = PaginateRows(
            rowLayouts,
            pageHeight,
            firstTableTop,
            continuedTableTop,
            tableHeaderHeight);

        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("The PDF renderer could not be initialized.");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            using var canvas = document.BeginPage(pageWidth, pageHeight);
            using var pageShaper = new SKShaper(typeface);
            DrawTablePage(
                canvas,
                pageShaper,
                typeface,
                title,
                summaries,
                headerCells,
                columnWidths,
                pages[pageIndex],
                pageIndex,
                pages.Count,
                pageWidth,
                pageHeight,
                firstTableTop,
                continuedTableTop,
                tableHeaderHeight,
                landscape);
            document.EndPage();
        }

        document.Close();
        return output.ToArray();
    }

    public static byte[] BuildTextReport(string title, IEnumerable<string> bodyLines)
    {
        using var typeface = LoadReportTypeface();
        using var shaper = new SKShaper(typeface);
        using var bodyFont = new SKFont(typeface, 10);

        var availableWidth = LetterWidth - (PageMargin * 2);
        var normalizedLines = bodyLines
            .SelectMany(line => WrapText(line, availableWidth, shaper, bodyFont, int.MaxValue))
            .ToList();
        var pages = normalizedLines.Chunk(43).Select(chunk => chunk.ToList()).ToList();

        if (pages.Count == 0)
        {
            pages.Add(["No data available."]);
        }

        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("The PDF renderer could not be initialized.");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            using var canvas = document.BeginPage(LetterWidth, LetterHeight);
            using var pageShaper = new SKShaper(typeface);
            canvas.Clear(SKColors.White);
            DrawReportHeader(canvas, pageShaper, typeface, title, pageIndex > 0, LetterWidth);

            using var paint = CreatePaint(BrandDark);
            var baseline = 104f;
            foreach (var line in pages[pageIndex])
            {
                DrawShapedText(canvas, pageShaper, line, PageMargin, baseline, SKTextAlign.Left, bodyFont, paint);
                baseline += 15;
            }

            DrawFooter(canvas, pageShaper, typeface, pageIndex + 1, pages.Count, LetterWidth, LetterHeight);
            document.EndPage();
        }

        document.Close();
        return output.ToArray();
    }

    private static void DrawTablePage(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        string title,
        IReadOnlyList<SummaryItem> summaries,
        IReadOnlyList<IReadOnlyList<string>> headerCells,
        IReadOnlyList<float> columnWidths,
        TablePage page,
        int pageIndex,
        int totalPages,
        float pageWidth,
        float pageHeight,
        float firstTableTop,
        float continuedTableTop,
        float tableHeaderHeight,
        bool landscape)
    {
        canvas.Clear(SKColors.White);
        DrawReportHeader(canvas, shaper, typeface, title, pageIndex > 0, pageWidth);

        if (pageIndex == 0 && summaries.Count > 0)
        {
            DrawSummaryCards(canvas, shaper, typeface, summaries, pageWidth, landscape);
        }

        var tableTop = pageIndex == 0 ? firstTableTop : continuedTableTop;
        DrawTableHeader(canvas, shaper, typeface, headerCells, columnWidths, tableTop, tableHeaderHeight);

        var rowTop = tableTop + tableHeaderHeight;
        if (page.Rows.Count == 0)
        {
            DrawEmptyTableRow(canvas, shaper, typeface, rowTop, columnWidths.Sum());
        }
        else
        {
            for (var rowIndex = 0; rowIndex < page.Rows.Count; rowIndex++)
            {
                DrawTableRow(
                    canvas,
                    shaper,
                    typeface,
                    page.Rows[rowIndex],
                    columnWidths,
                    rowTop,
                    page.StartRowIndex + rowIndex);
                rowTop += page.Rows[rowIndex].Height;
            }
        }

        DrawFooter(canvas, shaper, typeface, pageIndex + 1, totalPages, pageWidth, pageHeight);
    }

    private static void DrawReportHeader(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        string title,
        bool continued,
        float pageWidth)
    {
        using var kickerFont = new SKFont(typeface, 7.5f);
        using var titleFont = new SKFont(typeface, continued ? 15 : 19);
        using var kickerPaint = CreatePaint(BrandPrimary);
        using var titlePaint = CreatePaint(BrandDark);
        using var accentPaint = CreatePaint(BrandAccent);

        canvas.DrawRoundRect(new SKRect(PageMargin, 30, PageMargin + 4, 68), 2, 2, accentPaint);
        DrawShapedText(canvas, shaper, "TASKIFY / REPORT", PageMargin + 14, 39, SKTextAlign.Left, kickerFont, kickerPaint, emphasized: true);
        DrawShapedText(canvas, shaper, title, PageMargin + 14, 62, SKTextAlign.Left, titleFont, titlePaint, emphasized: true);

        if (continued)
        {
            using var continuedPaint = CreatePaint(TextMuted);
            DrawShapedText(canvas, shaper, "CONTINUED", pageWidth - PageMargin, 42, SKTextAlign.Right, kickerFont, continuedPaint);
        }
    }

    private static void DrawSummaryCards(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        IReadOnlyList<SummaryItem> summaries,
        float pageWidth,
        bool landscape)
    {
        var columns = Math.Min(landscape ? 4 : 2, summaries.Count);
        var gap = 8f;
        var cardWidth = (pageWidth - (PageMargin * 2) - (gap * (columns - 1))) / columns;
        const float cardHeight = 42;
        const float top = 82;

        using var labelFont = new SKFont(typeface, 7.1f);
        using var valueFont = new SKFont(typeface, 8.6f);
        using var backgroundPaint = CreatePaint(BrandLight);
        using var labelPaint = CreatePaint(BrandPrimary);
        using var valuePaint = CreatePaint(BrandDark);

        for (var index = 0; index < summaries.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var left = PageMargin + column * (cardWidth + gap);
            var cardTop = top + row * (cardHeight + gap);
            var rect = new SKRect(left, cardTop, left + cardWidth, cardTop + cardHeight);
            canvas.DrawRoundRect(rect, 5, 5, backgroundPaint);

            DrawShapedText(canvas, shaper, summaries[index].Label, left + 9, cardTop + 13, SKTextAlign.Left, labelFont, labelPaint, emphasized: true);
            var valueLines = WrapText(summaries[index].Value, cardWidth - 18, shaper, valueFont, 2);
            for (var lineIndex = 0; lineIndex < valueLines.Count; lineIndex++)
            {
                DrawShapedText(
                    canvas,
                    shaper,
                    valueLines[lineIndex],
                    left + 9,
                    cardTop + 27 + lineIndex * 9.5f,
                    SKTextAlign.Left,
                    valueFont,
                    valuePaint,
                    emphasized: lineIndex == 0);
            }
        }
    }

    private static void DrawTableHeader(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        IReadOnlyList<IReadOnlyList<string>> headerCells,
        IReadOnlyList<float> columnWidths,
        float top,
        float height)
    {
        using var backgroundPaint = CreatePaint(BrandPrimary);
        using var textPaint = CreatePaint(SKColors.White);
        using var font = new SKFont(typeface, 8.4f);

        var tableWidth = columnWidths.Sum();
        canvas.DrawRoundRect(new SKRect(PageMargin, top, PageMargin + tableWidth, top + height), 5, 5, backgroundPaint);

        var left = PageMargin;
        for (var column = 0; column < headerCells.Count; column++)
        {
            DrawCellLines(
                canvas,
                shaper,
                headerCells[column],
                new SKRect(left, top, left + columnWidths[column], top + height),
                font,
                textPaint,
                TableHeaderLineHeight,
                emphasized: true);
            left += columnWidths[column];
        }
    }

    private static void DrawTableRow(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        TableRowLayout row,
        IReadOnlyList<float> columnWidths,
        float top,
        int absoluteRowIndex)
    {
        var tableWidth = columnWidths.Sum();
        if (absoluteRowIndex % 2 == 1)
        {
            using var stripePaint = CreatePaint(StripeColor);
            canvas.DrawRect(new SKRect(PageMargin, top, PageMargin + tableWidth, top + row.Height), stripePaint);
        }

        using var borderPaint = CreateStrokePaint(BorderColor, 0.6f);
        using var textPaint = CreatePaint(BrandDark);
        using var font = new SKFont(typeface, 8.2f);

        canvas.DrawLine(PageMargin, top + row.Height, PageMargin + tableWidth, top + row.Height, borderPaint);

        var left = PageMargin;
        for (var column = 0; column < row.Cells.Count; column++)
        {
            if (column > 0)
            {
                canvas.DrawLine(left, top, left, top + row.Height, borderPaint);
            }

            DrawCellLines(
                canvas,
                shaper,
                row.Cells[column],
                new SKRect(left, top, left + columnWidths[column], top + row.Height),
                font,
                textPaint,
                TableLineHeight);
            left += columnWidths[column];
        }
    }

    private static void DrawCellLines(
        SKCanvas canvas,
        SKShaper shaper,
        IReadOnlyList<string> lines,
        SKRect cell,
        SKFont font,
        SKPaint paint,
        float lineHeight,
        bool emphasized = false)
    {
        canvas.Save();
        canvas.ClipRect(cell);
        var baseline = cell.Top + TableCellPadding + font.Size;

        foreach (var line in lines)
        {
            DrawShapedText(
                canvas,
                shaper,
                line,
                cell.Left + TableCellPadding,
                baseline,
                SKTextAlign.Left,
                font,
                paint,
                emphasized);
            baseline += lineHeight;
        }

        canvas.Restore();
    }

    private static void DrawEmptyTableRow(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        float top,
        float tableWidth)
    {
        using var backgroundPaint = CreatePaint(StripeColor);
        using var textPaint = CreatePaint(TextMuted);
        using var font = new SKFont(typeface, 9);
        var rect = new SKRect(PageMargin, top, PageMargin + tableWidth, top + 42);
        canvas.DrawRect(rect, backgroundPaint);
        DrawShapedText(canvas, shaper, "No data available.", rect.MidX, top + 25, SKTextAlign.Center, font, textPaint);
    }

    private static void DrawFooter(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        int pageNumber,
        int totalPages,
        float pageWidth,
        float pageHeight)
    {
        using var linePaint = CreateStrokePaint(BorderColor, 0.7f);
        using var textPaint = CreatePaint(TextMuted);
        using var brandPaint = CreatePaint(BrandPrimary);
        using var font = new SKFont(typeface, 7.5f);

        var lineY = pageHeight - FooterHeight;
        canvas.DrawLine(PageMargin, lineY, pageWidth - PageMargin, lineY, linePaint);
        DrawShapedText(canvas, shaper, "TASKIFY", PageMargin, pageHeight - 13, SKTextAlign.Left, font, brandPaint, emphasized: true);
        DrawShapedText(
            canvas,
            shaper,
            $"Page {pageNumber} of {totalPages}",
            pageWidth - PageMargin,
            pageHeight - 13,
            SKTextAlign.Right,
            font,
            textPaint);
    }

    private static TableRowLayout CreateRowLayout(
        IReadOnlyList<string> row,
        IReadOnlyList<float> columnWidths,
        SKShaper shaper,
        SKFont font)
    {
        var cells = row
            .Select((cell, index) => (IReadOnlyList<string>)WrapText(
                cell,
                columnWidths[index] - (TableCellPadding * 2),
                shaper,
                font,
                MaximumCellLines))
            .ToList();
        var lineCount = Math.Max(1, cells.Max(cell => cell.Count));
        var height = Math.Max(28, lineCount * TableLineHeight + (TableCellPadding * 2));
        return new TableRowLayout(cells, height);
    }

    private static List<TablePage> PaginateRows(
        IReadOnlyList<TableRowLayout> rows,
        float pageHeight,
        float firstTableTop,
        float continuedTableTop,
        float tableHeaderHeight)
    {
        var pages = new List<TablePage>();
        var currentRows = new List<TableRowLayout>();
        var startRowIndex = 0;
        var usedHeight = firstTableTop + tableHeaderHeight;
        var pageBottom = pageHeight - FooterHeight - 10;

        foreach (var row in rows)
        {
            if (currentRows.Count > 0 && usedHeight + row.Height > pageBottom)
            {
                pages.Add(new TablePage(currentRows, startRowIndex));
                startRowIndex += currentRows.Count;
                currentRows = new List<TableRowLayout>();
                usedHeight = continuedTableTop + tableHeaderHeight;
            }

            currentRows.Add(row);
            usedHeight += row.Height;
        }

        if (currentRows.Count > 0 || pages.Count == 0)
        {
            pages.Add(new TablePage(currentRows, startRowIndex));
        }

        return pages;
    }

    private static float CalculateFirstTableTop(int summaryCount, bool landscape)
    {
        if (summaryCount == 0)
        {
            return 84;
        }

        var columns = Math.Min(landscape ? 4 : 2, summaryCount);
        var rows = (int)Math.Ceiling(summaryCount / (double)columns);
        return 82 + rows * 42 + Math.Max(0, rows - 1) * 8 + 16;
    }

    private static IReadOnlyList<float> CalculateColumnWidths(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        float tableWidth,
        SKShaper shaper,
        SKFont bodyFont,
        SKFont headerFont)
    {
        var count = headers.Count;
        var minimumWidth = Math.Min(52, tableWidth / count * 0.72f);
        var desired = new float[count];

        for (var column = 0; column < count; column++)
        {
            var measured = rows
                .Take(250)
                .Select(row => MeasureText(row[column], shaper, bodyFont) + (TableCellPadding * 2))
                .OrderBy(width => width)
                .ToList();
            var percentileWidth = measured.Count == 0
                ? 0
                : measured[(int)Math.Floor((measured.Count - 1) * 0.72)];
            desired[column] = Math.Clamp(
                Math.Max(MeasureText(headers[column], shaper, headerFont) + (TableCellPadding * 2), percentileWidth),
                minimumWidth,
                tableWidth * 0.30f);
        }

        var remainingWidth = Math.Max(0, tableWidth - minimumWidth * count);
        var flexibleWeights = desired.Select(width => Math.Max(1, width - minimumWidth)).ToArray();
        var totalWeight = flexibleWeights.Sum();
        var result = flexibleWeights
            .Select(weight => minimumWidth + remainingWidth * weight / totalWeight)
            .ToArray();

        result[^1] += tableWidth - result.Sum();
        return result;
    }

    private static List<string> WrapText(
        string? value,
        float maxWidth,
        SKShaper shaper,
        SKFont font,
        int maxLines)
    {
        var text = NormalizeCell(value);
        var lines = new List<string>();
        var remaining = text;

        while (remaining.Length > 0 && lines.Count < maxLines)
        {
            if (MeasureText(remaining, shaper, font) <= maxWidth)
            {
                lines.Add(remaining);
                remaining = string.Empty;
                break;
            }

            var breakIndex = FindBreakIndex(remaining, maxWidth, shaper, font);
            if (breakIndex <= 0)
            {
                breakIndex = Math.Min(1, remaining.Length);
            }

            lines.Add(remaining[..breakIndex].TrimEnd());
            remaining = remaining[breakIndex..].TrimStart();
        }

        if (remaining.Length > 0 && lines.Count > 0)
        {
            lines[^1] = Ellipsize(lines[^1] + " " + remaining, maxWidth, shaper, font);
        }

        return lines.Count == 0 ? ["-"] : lines;
    }

    private static int FindBreakIndex(string text, float maxWidth, SKShaper shaper, SKFont font)
    {
        var textElementIndexes = StringInfo.ParseCombiningCharacters(text);
        var low = 1;
        var high = textElementIndexes.Length;
        var fittingElementCount = 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var endIndex = middle < textElementIndexes.Length ? textElementIndexes[middle] : text.Length;

            if (MeasureText(text[..endIndex], shaper, font) <= maxWidth)
            {
                fittingElementCount = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        var fittingEndIndex = fittingElementCount < textElementIndexes.Length
            ? textElementIndexes[fittingElementCount]
            : text.Length;
        var whitespaceIndex = text.LastIndexOf(' ', Math.Max(0, fittingEndIndex - 1), fittingEndIndex);
        return whitespaceIndex > 0 ? whitespaceIndex : fittingEndIndex;
    }

    private static string Ellipsize(string text, float maxWidth, SKShaper shaper, SKFont font)
    {
        const string suffix = "...";
        var indexes = StringInfo.ParseCombiningCharacters(text);
        var low = 0;
        var high = indexes.Length;
        var fittingCount = 0;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var end = middle < indexes.Length ? indexes[middle] : text.Length;
            if (MeasureText(text[..end].TrimEnd() + suffix, shaper, font) <= maxWidth)
            {
                fittingCount = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        var fittingEnd = fittingCount < indexes.Length ? indexes[fittingCount] : text.Length;
        return text[..fittingEnd].TrimEnd() + suffix;
    }

    private static float MeasureText(string text, SKShaper shaper, SKFont font)
        => string.IsNullOrEmpty(text) ? 0 : shaper.Shape(text, font).Width;

    private static IEnumerable<SummaryItem> ParseSummaryItems(IEnumerable<string>? summaryLines)
    {
        if (summaryLines == null)
        {
            yield break;
        }

        foreach (var rawLine in summaryLines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator > 0)
            {
                yield return new SummaryItem(line[..separator].Trim(), NormalizeCell(line[(separator + 1)..]));
            }
            else
            {
                yield return new SummaryItem("DETAIL", NormalizeCell(line));
            }
        }
    }

    private static IReadOnlyList<string> NormalizeRow(IReadOnlyList<string> row, int columnCount)
    {
        var normalized = new string[columnCount];
        for (var index = 0; index < columnCount; index++)
        {
            normalized[index] = index < row.Count ? NormalizeCell(row[index]) : "-";
        }

        return normalized;
    }

    private static SKTypeface LoadReportTypeface()
    {
        foreach (var path in GetFontCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var typeface = SKTypeface.FromFile(path);
            if (typeface != null && SupportsReportGlyphs(typeface))
            {
                return typeface;
            }

            typeface?.Dispose();
        }

        foreach (var family in new[] { "Noto Sans Myanmar", "Myanmar Text", "Nirmala UI", "Padauk" })
        {
            var typeface = SKTypeface.FromFamilyName(family);
            if (typeface != null && SupportsReportGlyphs(typeface))
            {
                return typeface;
            }

            typeface?.Dispose();
        }

        throw new InvalidOperationException(
            "No Unicode font capable of rendering Burmese was found. " +
            $"Install Noto Sans Myanmar or set {FontPathEnvironmentVariable} to a compatible .ttf file.");
    }

    private static IEnumerable<string> GetFontCandidates()
    {
        var configuredPath = Environment.GetEnvironmentVariable(FontPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            yield return Path.Combine(windowsDirectory, "Fonts", "mmrtext.ttf");
            yield return Path.Combine(windowsDirectory, "Fonts", "Nirmala.ttf");
        }

        yield return "/usr/share/fonts/truetype/noto/NotoSansMyanmar-Regular.ttf";
        yield return "/usr/share/fonts/truetype/padauk/Padauk-Regular.ttf";
        yield return "/usr/share/fonts/opentype/noto/NotoSansMyanmar-Regular.ttf";
    }

    private static bool SupportsReportGlyphs(SKTypeface typeface)
        => RequiredGlyphSamples.All(sample => typeface.GetGlyphs(sample).All(glyph => glyph != 0));

    private static string NormalizeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static SKPaint CreatePaint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static SKPaint CreateStrokePaint(SKColor color, float width) => new()
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width
    };

    private static void DrawShapedText(
        SKCanvas canvas,
        SKShaper shaper,
        string text,
        float x,
        float y,
        SKTextAlign alignment,
        SKFont font,
        SKPaint paint,
        bool emphasized = false)
    {
        canvas.DrawShapedText(shaper, text, x, y, alignment, font, paint);
        if (emphasized)
        {
            canvas.DrawShapedText(shaper, text, x + 0.28f, y, alignment, font, paint);
        }
    }

    private sealed record SummaryItem(string Label, string Value);
    private sealed record TableRowLayout(IReadOnlyList<IReadOnlyList<string>> Cells, float Height);
    private sealed record TablePage(IReadOnlyList<TableRowLayout> Rows, int StartRowIndex);
}
