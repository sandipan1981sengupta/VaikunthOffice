using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Vaikunth.DtoLibrary;

namespace Vaikunth.HrishikeshaWorkbook.Export;

/// <summary>
/// Hrishikesha Workbook → Microsoft Excel (.xlsx). Every sheet, value and formula (=SUM(B2:B9) stays a live formula),
/// bold / italic, fill and text colours, alignment and number formats.
/// </summary>
public static partial class XlsxExport
{
    [GeneratedRegex(@"^[A-Z]{1,3}[1-9][0-9]{0,6}$")]
    private static partial Regex Address();

    public static byte[] Build(WorkbookContent book, string title)
    {
        using var wb = new XLWorkbook();
        wb.Properties.Title = title;
        wb.Properties.Author = "Vaikunth Desk · Hrishikesha Workbook";
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in book.Sheets.Count == 0 ? [new SheetDto()] : book.Sheets)
        {
            var ws = wb.Worksheets.Add(SheetName(sheet.Name, used));
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;
            ws.Columns(1, Math.Clamp(sheet.Cols, 1, 16384)).Width = 12;

            foreach (var (key, cell) in sheet.Cells)
            {
                var address = key.Trim().ToUpperInvariant();
                if (!Address().IsMatch(address) || cell is null) continue;
                var x = ws.Cell(address);
                var input = cell.Input ?? "";

                if (input.StartsWith('=') && input.Length > 1) x.FormulaA1 = input[1..];
                else if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) x.Value = number;
                else if (input.EndsWith('%') && double.TryParse(input[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                {
                    x.Value = pct / 100;
                    x.Style.NumberFormat.Format = "0.0%";
                }
                else if (input.Length > 0) x.Value = input;

                if (cell.Bold) x.Style.Font.Bold = true;
                if (cell.Italic) x.Style.Font.Italic = true;
                if (Color(cell.Fill) is XLColor fill) x.Style.Fill.BackgroundColor = fill;
                if (Color(cell.Color) is XLColor ink) x.Style.Font.FontColor = ink;
                x.Style.Alignment.Horizontal = cell.Align switch
                {
                    "center" => XLAlignmentHorizontalValues.Center,
                    "right" => XLAlignmentHorizontalValues.Right,
                    "left" => XLAlignmentHorizontalValues.Left,
                    _ => XLAlignmentHorizontalValues.General
                };
                switch (cell.Format)
                {
                    case "number": x.Style.NumberFormat.Format = "#,##0.00"; break;
                    case "currency": x.Style.NumberFormat.Format = "[$₹-4009] #,##,##0"; break;   // Indian rupee, lakh grouping
                    case "percent": x.Style.NumberFormat.Format = "0.0%"; break;
                }
            }
        }

        // Excel works out every formula when the file opens; store the answers too for viewers that don't calculate
        wb.FullCalculationOnLoad = true;
        try { wb.RecalculateAllFormulas(); } catch (Exception) { /* Excel will calculate on open */ }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Excel sheet names: at most 31 characters, none of : \ / ? * [ ], and no two the same.</summary>
    private static string SheetName(string? name, HashSet<string> used)
    {
        var clean = new string((name ?? "").Where(c => ":\\/?*[]".IndexOf(c) < 0).ToArray()).Trim().Trim('\'');
        if (clean.Length == 0) clean = "Sheet";
        if (clean.Length > 31) clean = clean[..31];
        var candidate = clean;
        for (var i = 2; !used.Add(candidate); i++)
        {
            var suffix = $" ({i})";
            candidate = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }
        return candidate;
    }

    private static XLColor? Color(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return null;
        try { return XLColor.FromHtml(css.Trim().Length == 4 && css.Trim()[0] == '#' ? "#" + string.Concat(css.Trim()[1..].Select(c => $"{c}{c}")) : css.Trim()); }
        catch (Exception) { return null; }
    }
}
