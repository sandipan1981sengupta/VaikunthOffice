using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The spreadsheet grid: column letters across the top, row numbers down the side, and one cell per address
/// (data-addr="B3"). The workbook script fills values and works out formulas.
///   &lt;vk-sheet-grid id="grid" rows="40" cols="14" /&gt;
/// </summary>
[HtmlTargetElement("vk-sheet-grid", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkSheetGridTagHelper : TagHelper
{
    public string Id { get; set; } = "grid";
    public int Rows { get; set; } = 40;
    public int Cols { get; set; } = 14;
    public int ColWidth { get; set; } = 96;

    public static string ColName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "table";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("class", "vk-grid");
        output.Attributes.SetAttribute("data-rows", Rows);
        output.Attributes.SetAttribute("data-cols", Cols);

        var sb = new StringBuilder("<colgroup><col class='vk-grid-rh' />");
        for (var c = 0; c < Cols; c++) sb.Append($"<col style='width:{(c == 0 ? ColWidth + 50 : ColWidth)}px' />");
        sb.Append("</colgroup><thead><tr><th class='vk-grid-corner'></th>");
        for (var c = 0; c < Cols; c++) sb.Append($"<th data-col='{c}'>{ColName(c)}</th>");
        sb.Append("</tr></thead><tbody>");
        for (var r = 1; r <= Rows; r++)
        {
            sb.Append($"<tr><th data-row='{r}'>{r}</th>");
            for (var c = 0; c < Cols; c++) sb.Append($"<td data-addr='{ColName(c)}{r}'></td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
