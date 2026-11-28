using ClosedXML.Excel;
using Vaikunth.DtoLibrary;

namespace Vaikunth.DhruvaPlanner.Export;

/// <summary>
/// Dhruva Planner → Microsoft Excel (.xlsx): a Tasks sheet (one row per task, with a filter and frozen header)
/// and a Summary sheet (project dates, tasks per column, overdue, overall progress as live formulas).
/// </summary>
public static class PlannerXlsx
{
    public static byte[] Build(PlannerContent plan, string title)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var columns = plan.Columns.Count == 0 ? PlannerColumnDto.Defaults() : plan.Columns;
        string ColumnName(string key) => columns.FirstOrDefault(c => c.Key == key)?.Name ?? key;

        using var wb = new XLWorkbook();
        wb.Properties.Title = title;
        wb.Properties.Author = "Vaikunth Desk · Dhruva Planner";

        // ---- Tasks ----
        var ws = wb.Worksheets.Add("Tasks");
        string[] head = ["Task", "Status", "Priority", "Owner", "Start", "Due", "Days", "Progress", "Milestone", "Overdue", "Notes"];
        for (var i = 0; i < head.Length; i++) ws.Cell(1, i + 1).Value = head[i];
        var header = ws.Range(1, 1, 1, head.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#5B2A86");

        var row = 2;
        foreach (var t in plan.Tasks)
        {
            ws.Cell(row, 1).Value = t.Title;
            ws.Cell(row, 2).Value = ColumnName(t.Status);
            ws.Cell(row, 3).Value = t.Priority switch { "high" => "High", "low" => "Low", _ => "Medium" };
            ws.Cell(row, 4).Value = t.Owner;
            if (t.Start is DateOnly s) ws.Cell(row, 5).Value = s.ToDateTime(TimeOnly.MinValue);
            if (t.Due is DateOnly d) ws.Cell(row, 6).Value = d.ToDateTime(TimeOnly.MinValue);
            if (t.Start is not null && t.Due is not null) ws.Cell(row, 7).FormulaA1 = $"IF(AND(E{row}<>\"\",F{row}<>\"\"),F{row}-E{row}+1,\"\")";
            ws.Cell(row, 8).Value = Math.Clamp(t.Progress, 0, 100) / 100.0;
            ws.Cell(row, 9).Value = t.Milestone ? "Yes" : "";
            var overdue = t.Due is DateOnly due && due < today && t.Status != "done" && t.Progress < 100;
            ws.Cell(row, 10).Value = overdue ? "Yes" : "";
            ws.Cell(row, 11).Value = t.Notes;
            if (overdue) ws.Range(row, 1, row, head.Length).Style.Font.FontColor = XLColor.FromHtml("#B42318");
            if (t.Status == "done") ws.Range(row, 1, row, head.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F4EC");
            row++;
        }
        var last = Math.Max(2, row - 1);
        ws.Range(2, 5, last, 6).Style.DateFormat.Format = "d mmm yyyy";
        ws.Range(2, 8, last, 8).Style.NumberFormat.Format = "0%";
        ws.Range(1, 1, last, head.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, head.Length).AdjustToContents(1, last, 8, 60);
        ws.Column(11).Width = Math.Max(ws.Column(11).Width, 30);
        ws.Column(11).Style.Alignment.WrapText = true;

        // ---- Summary ----
        var sum = wb.Worksheets.Add("Summary");
        sum.Cell(1, 1).Value = title;
        sum.Cell(1, 1).Style.Font.Bold = true;
        sum.Cell(1, 1).Style.Font.FontSize = 16;
        sum.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#5B2A86");
        var r = 3;
        void Line(string label, Action<IXLCell> set) { sum.Cell(r, 1).Value = label; sum.Cell(r, 1).Style.Font.Bold = true; set(sum.Cell(r, 2)); r++; }
        if (!string.IsNullOrWhiteSpace(plan.Description)) Line("About", c => { c.Value = plan.Description; c.Style.Alignment.WrapText = true; });
        if (plan.Start is DateOnly ps) Line("Starts", c => { c.Value = ps.ToDateTime(TimeOnly.MinValue); c.Style.DateFormat.Format = "d mmm yyyy"; });
        if (plan.End is DateOnly pe) Line("Ends", c => { c.Value = pe.ToDateTime(TimeOnly.MinValue); c.Style.DateFormat.Format = "d mmm yyyy"; });
        Line("Tasks", c => c.FormulaA1 = $"COUNTA(Tasks!A2:A{last})");
        foreach (var col in columns)
            Line(col.Name, c => c.FormulaA1 = $"COUNTIF(Tasks!B2:B{last},\"{col.Name.Replace("\"", "\"\"")}\")");
        Line("Overdue", c => c.FormulaA1 = $"COUNTIF(Tasks!J2:J{last},\"Yes\")");
        Line("Progress", c => { c.FormulaA1 = $"IFERROR(AVERAGE(Tasks!H2:H{last}),0)"; c.Style.NumberFormat.Format = "0%"; });
        sum.Column(1).Width = 18;
        sum.Column(2).Width = 48;

        ws.SetTabActive();
        wb.FullCalculationOnLoad = true;
        try { wb.RecalculateAllFormulas(); } catch (Exception) { /* Excel will calculate on open */ }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
