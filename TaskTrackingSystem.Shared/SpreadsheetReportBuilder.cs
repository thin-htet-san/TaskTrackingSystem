using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;

namespace TaskTrackingSystem.Shared;

public static class SpreadsheetReportBuilder
{
    private const string DateFormat = "dd-MM-yyyy";
    private const string ContentTypeWorkbook = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string ContentTypeWorksheet = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private const string ContentTypeStyles = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";

    public static byte[] BuildTableWorkbook(
        string worksheetName,
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        IEnumerable<string>? summaryLines = null)
    {
        var rowData = rows.Select(row => row.ToArray()).ToList();
        var summary = summaryLines?
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList() ?? new List<string>();

        var totalRows = 0;
        if (!string.IsNullOrWhiteSpace(title))
        {
            totalRows++;
        }

        totalRows += summary.Count;
        totalRows++; // blank row before header
        totalRows++; // header row
        totalRows += rowData.Count;

        var columnCount = Math.Max(1, headers.Count);
        var lastColumnName = ToColumnName(columnCount);
        var lastRowNumber = Math.Max(1, totalRows);
        var dimension = $"A1:{lastColumnName}{lastRowNumber}";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            AddEntry(archive, "_rels/.rels", BuildRootRelsXml());
            AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml(worksheetName));
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            AddEntry(archive, "xl/styles.xml", BuildStylesXml());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(title, headers, rowData, summary, dimension));
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" />
        </Types>
        """;

    private static string BuildRootRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
        </Relationships>
        """;

    private static string BuildWorkbookXml(string worksheetName)
    {
        var safeName = EscapeXml(SanitizeWorksheetName(worksheetName));
        return $"""
               <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
               <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                         xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                 <sheets>
                   <sheet name="{safeName}" sheetId="1" r:id="rId1" />
                 </sheets>
               </workbook>
               """;
    }

    private static string BuildWorkbookRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml" />
        </Relationships>
        """;

    private static string BuildStylesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="3">
            <font>
              <sz val="11" />
              <color theme="1" />
              <name val="Calibri" />
              <family val="2" />
            </font>
            <font>
              <b />
              <sz val="12" />
              <color rgb="FFFFFFFF" />
              <name val="Calibri" />
              <family val="2" />
            </font>
            <font>
              <i />
              <sz val="11" />
              <color rgb="FF475569" />
              <name val="Calibri" />
              <family val="2" />
            </font>
          </fonts>
          <fills count="3">
            <fill>
              <patternFill patternType="none" />
            </fill>
            <fill>
              <patternFill patternType="gray125" />
            </fill>
            <fill>
              <patternFill patternType="solid">
                <fgColor rgb="FF5B21B6" />
                <bgColor indexed="64" />
              </patternFill>
            </fill>
          </fills>
          <borders count="2">
            <border>
              <left />
              <right />
              <top />
              <bottom />
              <diagonal />
            </border>
            <border>
              <left style="thin"><color rgb="FFE2E8F0" /></left>
              <right style="thin"><color rgb="FFE2E8F0" /></right>
              <top style="thin"><color rgb="FFE2E8F0" /></top>
              <bottom style="thin"><color rgb="FFE2E8F0" /></bottom>
              <diagonal />
            </border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" />
          </cellStyleXfs>
          <cellXfs count="4">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" />
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center" />
            </xf>
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
              <alignment vertical="center" />
            </xf>
            <xf numFmtId="14" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1">
              <alignment vertical="center" />
            </xf>
          </cellXfs>
          <cellStyles count="1">
            <cellStyle name="Normal" xfId="0" builtinId="0" />
          </cellStyles>
          <dxfs count="0" />
          <tableStyles count="0" defaultTableStyle="TableStyleMedium2" defaultPivotStyle="PivotStyleLight16" />
        </styleSheet>
        """;

    private static string BuildWorksheetXml(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<object?[]> rows,
        IReadOnlyList<string> summaryLines,
        string dimension)
    {
        var rowXml = new StringBuilder();
        var rowIndex = 1;
        var columnCount = Math.Max(1, headers.Count);

        if (!string.IsNullOrWhiteSpace(title))
        {
            rowXml.Append(BuildSingleCellRow(rowIndex++, 0, title.Trim(), styleIndex: 1));
        }

        foreach (var line in summaryLines)
        {
            rowXml.Append(BuildSingleCellRow(rowIndex++, 0, line, styleIndex: 2));
        }

        rowIndex++; // blank row

        rowXml.Append("<row r=\"");
        rowXml.Append(rowIndex);
        rowXml.Append("\">");
        for (var col = 1; col <= columnCount; col++)
        {
            var header = col <= headers.Count ? headers[col - 1] : string.Empty;
            rowXml.Append(BuildInlineCell(rowIndex, col, header, styleIndex: 1));
        }
        rowXml.AppendLine("</row>");
        rowIndex++;

        foreach (var row in rows)
        {
            rowXml.Append("<row r=\"");
            rowXml.Append(rowIndex);
            rowXml.Append("\">");

            for (var col = 1; col <= columnCount; col++)
            {
                var value = col <= row.Length ? row[col - 1] : null;
                rowXml.Append(BuildCell(rowIndex, col, value));
            }

            rowXml.AppendLine("</row>");
            rowIndex++;
        }

        var autoFilterRow = 1 + (string.IsNullOrWhiteSpace(title) ? 0 : 1) + summaryLines.Count + 1;

        var worksheetXml = new StringBuilder();
        worksheetXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        worksheetXml.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
        worksheetXml.AppendLine("           xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        worksheetXml.AppendLine($"  <dimension ref=\"{dimension}\" />");
        worksheetXml.AppendLine("  <sheetViews>");
        worksheetXml.AppendLine("    <sheetView workbookViewId=\"0\">");
        worksheetXml.AppendLine($"      <pane ySplit=\"{autoFilterRow}\" topLeftCell=\"A{autoFilterRow + 1}\" activePane=\"bottomLeft\" state=\"frozen\" />");
        worksheetXml.AppendLine($"      <selection pane=\"bottomLeft\" activeCell=\"A{autoFilterRow + 1}\" sqref=\"A{autoFilterRow + 1}\" />");
        worksheetXml.AppendLine("    </sheetView>");
        worksheetXml.AppendLine("  </sheetViews>");
        worksheetXml.AppendLine("  <sheetFormatPr defaultRowHeight=\"15\" />");
        worksheetXml.AppendLine("  <cols>");
        worksheetXml.Append(BuildColumnDefinitions(columnCount));
        worksheetXml.AppendLine("  </cols>");
        worksheetXml.AppendLine("  <sheetData>");
        worksheetXml.Append(rowXml);
        worksheetXml.AppendLine("  </sheetData>");
        worksheetXml.AppendLine($"  <autoFilter ref=\"A{autoFilterRow}:{ToColumnName(columnCount)}{rowIndex - 1}\" />");
        worksheetXml.AppendLine("</worksheet>");
        return worksheetXml.ToString();
    }

    private static string BuildSingleCellRow(int rowIndex, int colIndex, string value, int styleIndex)
    {
        return $"<row r=\"{rowIndex}\">{BuildInlineCell(rowIndex, colIndex + 1, value, styleIndex)}</row>\n";
    }

    private static string BuildColumnDefinitions(int columnCount)
    {
        var builder = new StringBuilder();
        for (var col = 1; col <= columnCount; col++)
        {
            builder.Append($"                   <col min=\"{col}\" max=\"{col}\" width=\"18\" customWidth=\"1\" />\n");
        }
        return builder.ToString();
    }

    private static string BuildCell(int rowIndex, int columnIndex, object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            DateTime dateTime => BuildNumericCell(rowIndex, columnIndex, dateTime.ToOADate().ToString(CultureInfo.InvariantCulture), styleIndex: 3),
            DateTimeOffset dateTimeOffset => BuildNumericCell(rowIndex, columnIndex, dateTimeOffset.DateTime.ToOADate().ToString(CultureInfo.InvariantCulture), styleIndex: 3),
            DateOnly dateOnly => BuildNumericCell(rowIndex, columnIndex, dateOnly.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture), styleIndex: 3),
            bool boolean => BuildBooleanCell(rowIndex, columnIndex, boolean),
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                => BuildNumericCell(rowIndex, columnIndex, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"),
            _ => BuildInlineCell(rowIndex, columnIndex, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, styleIndex: 0)
        };
    }

    private static string BuildInlineCell(int rowIndex, int columnIndex, string value, int styleIndex)
    {
        var cellRef = $"{ToColumnName(columnIndex)}{rowIndex}";
        return $"""
               <c r="{cellRef}" t="inlineStr" s="{styleIndex}">
                 <is><t xml:space="preserve">{EscapeXml(value)}</t></is>
               </c>
               """;
    }

    private static string BuildNumericCell(int rowIndex, int columnIndex, string value, int styleIndex = 0)
    {
        var cellRef = $"{ToColumnName(columnIndex)}{rowIndex}";
        return $"""<c r="{cellRef}" s="{styleIndex}"><v>{value}</v></c>""";
    }

    private static string BuildBooleanCell(int rowIndex, int columnIndex, bool value)
    {
        var cellRef = $"{ToColumnName(columnIndex)}{rowIndex}";
        return $"""<c r="{cellRef}" t="b"><v>{(value ? "1" : "0")}</v></c>""";
    }

    private static string ToColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string SanitizeWorksheetName(string name)
    {
        var invalid = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? ' ' : ch).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Report";
        }

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string EscapeXml(string value) =>
        SecurityElement.Escape(value) ?? string.Empty;
}
