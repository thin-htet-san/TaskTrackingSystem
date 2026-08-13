using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace TaskTrackingSystem.Shared;

public static class SimplePdfReportBuilder
{
    private const float PageWidth = 612;
    private const float PageHeight = 792;
    private const float LeftMargin = 48;
    private const float TopMargin = 52;
    private const float RightMargin = 48;
    private const float TitleFontSize = 16;
    private const float BodyFontSize = 10;
    private const float LineSpacing = 14;
    private const int BodyLinesPerPage = 42;
    private const string FontPathEnvironmentVariable = "TASKTRACKING_PDF_FONT_PATH";

    private static readonly string[] RequiredGlyphSamples = ["ABC0123|:-", "မြန်မာ"];

    public static byte[] BuildTableReport(
        string title,
        IEnumerable<string>? summaryLines,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var lines = new List<string>();

        if (summaryLines != null)
        {
            lines.AddRange(summaryLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
        }

        if (headers.Count > 0)
        {
            lines.Add(string.Join(" | ", headers));
            lines.Add(new string('-', 72));
        }

        lines.AddRange(rows.Select(row => string.Join(" | ", row.Select(NormalizeCell))));

        if (lines.Count == 0)
        {
            lines.Add("No data available.");
        }

        return BuildTextReport(title, lines);
    }

    public static byte[] BuildTextReport(string title, IEnumerable<string> bodyLines)
    {
        using var typeface = LoadReportTypeface();
        using var shaper = new SKShaper(typeface);
        using var bodyFont = new SKFont(typeface, BodyFontSize);

        var availableWidth = PageWidth - LeftMargin - RightMargin;
        var normalizedLines = bodyLines
            .SelectMany(line => WrapLine(line, availableWidth, shaper, bodyFont))
            .ToList();

        var pages = normalizedLines
            .Chunk(BodyLinesPerPage)
            .Select(chunk => chunk.ToList())
            .ToList();

        if (pages.Count == 0)
        {
            pages.Add(["No data available."]);
        }

        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("The PDF renderer could not be initialized.");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            using var canvas = document.BeginPage(PageWidth, PageHeight);
            DrawPage(canvas, shaper, typeface, title, pages[pageIndex], pageIndex + 1, pages.Count);
            document.EndPage();
        }

        document.Close();
        return output.ToArray();
    }

    private static void DrawPage(
        SKCanvas canvas,
        SKShaper shaper,
        SKTypeface typeface,
        string title,
        IReadOnlyList<string> lines,
        int pageNumber,
        int totalPages)
    {
        canvas.Clear(SKColors.White);

        using var titleFont = new SKFont(typeface, TitleFontSize);
        using var bodyFont = new SKFont(typeface, BodyFontSize);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        canvas.DrawShapedText(shaper, title, LeftMargin, TopMargin, SKTextAlign.Left, titleFont, paint);
        canvas.DrawShapedText(
            shaper,
            $"Page {pageNumber} of {totalPages}",
            LeftMargin,
            TopMargin + 22,
            SKTextAlign.Left,
            bodyFont,
            paint);

        var baseline = TopMargin + 22 + LineSpacing;
        foreach (var line in lines)
        {
            baseline += LineSpacing;

            if (!string.IsNullOrEmpty(line))
            {
                canvas.DrawShapedText(shaper, line, LeftMargin, baseline, SKTextAlign.Left, bodyFont, paint);
            }
        }
    }

    private static IEnumerable<string> WrapLine(string? line, float maxWidth, SKShaper shaper, SKFont font)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield return string.Empty;
            yield break;
        }

        var remaining = line.Trim();
        while (MeasureText(remaining, shaper, font) > maxWidth)
        {
            var breakIndex = FindBreakIndex(remaining, maxWidth, shaper, font);
            var wrappedLine = remaining[..breakIndex].TrimEnd();

            if (wrappedLine.Length == 0)
            {
                break;
            }

            yield return wrappedLine;
            remaining = remaining[breakIndex..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
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

    private static float MeasureText(string text, SKShaper shaper, SKFont font)
        => string.IsNullOrEmpty(text) ? 0 : shaper.Shape(text, font).Width;

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
}
