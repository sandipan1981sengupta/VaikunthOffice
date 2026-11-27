// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Json;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>
    /// Donut chart — a pie chart with a hollow center. Standalone control —
    /// requires Chart.js loaded in _Layout.cshtml:
    /// <script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
    /// </summary>
    [HtmlTargetElement("modern-donut-chart")]
    public class ModernDonutChart : TagHelper
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public IEnumerable<string> Labels { get; set; }
        public IEnumerable<double> Data { get; set; }
        public string ChartHeight { get; set; } = "350px";
        public IEnumerable<string> Colors { get; set; }   // optional — one color per slice; falls back to auto-palette if not set

        public bool ShowBorder { get; set; } = true;
        public bool ShowLegend { get; set; } = true;
        public bool ShowLegendBox { get; set; } = true;
        public int LegendFontSize { get; set; } = 14;
        public string LegendFontWeight { get; set; } = "700";
        public int LegendGap { get; set; } = 16;
        public string LegendPosition { get; set; } = "bottom";   // "top", "bottom", "left", "right"

        public string CutoutPercentage { get; set; } = "60%";      // how big the center hole is — "0%" would make it a full pie

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-chart-wrapper" + (ShowBorder ? "" : " mc-no-border"));

            var labelsJson = JsonSerializer.Serialize(Labels ?? Enumerable.Empty<string>());
            var dataJson = JsonSerializer.Serialize(Data ?? Enumerable.Empty<double>());
            var colorsJson = JsonSerializer.Serialize(Colors ?? Enumerable.Empty<string>());

            var titleHtml = !string.IsNullOrWhiteSpace(Title)
                ? $"<div class='mc-title'>{Title}</div>"
                : "";

            var normalizedPosition = (LegendPosition ?? "bottom").ToLower();
            if (normalizedPosition != "top" && normalizedPosition != "bottom" &&
                normalizedPosition != "left" && normalizedPosition != "right")
            {
                normalizedPosition = "bottom";
            }

            var legendBoxHtml = ShowLegend
                ? $@"<div class='mc-legend-box{(ShowLegendBox ? "" : " mc-legend-plain")}' id='{Id}_legend'
                          style='--mc-legend-font-size:{LegendFontSize}px; --mc-legend-font-weight:{LegendFontWeight}; --mc-legend-gap:{LegendGap}px;'></div>"
                : "";

            output.Content.SetHtmlContent($@"
                {titleHtml}
                <div class='mc-chart-body mc-legend-{normalizedPosition}'>
                    <div class='mc-canvas-wrap' style='height:{ChartHeight};'>
                        <canvas id='{Id}' class='modern-donut-chart-canvas'
                                data-labels='{System.Net.WebUtility.HtmlEncode(labelsJson)}'
                                data-values='{System.Net.WebUtility.HtmlEncode(dataJson)}'
                                data-colors='{System.Net.WebUtility.HtmlEncode(colorsJson)}'
                                data-cutout='{CutoutPercentage}'></canvas>
                    </div>
                    {legendBoxHtml}
                </div>
            ");
        }
    }
}
