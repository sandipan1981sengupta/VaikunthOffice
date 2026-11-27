// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Json;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>One series/dataset within a multi-series bar chart.</summary>
    public class BarChartSeries
    {
        public string Label { get; set; }
        public List<double> Values { get; set; }
        public string Color { get; set; }   // optional — falls back to an auto palette if not set
    }

    /// <summary>
    /// Horizontal bar chart. Standalone control — requires Chart.js loaded
    /// in _Layout.cshtml: <script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
    /// Set Series (bound from a C# list, e.g. a ViewModel property) for
    /// multi-series charts — 4-5 grouped bars per category is fine.
    /// If Series is set, Data/LegendLabel/Colors are ignored.
    /// </summary>
    [HtmlTargetElement("modern-bar-chart")]
    public class ModernBarChart : TagHelper
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public IEnumerable<string> Labels { get; set; }
        public IEnumerable<double> Data { get; set; }               // single-series only
        public string LegendLabel { get; set; } = "Value";           // single-series only
        public IEnumerable<string> Colors { get; set; }                // single-series only
        public string ChartHeight { get; set; } = "350px";
        public IEnumerable<BarChartSeries> Series { get; set; }          // set this for multi-series (4-5+ datasets)
        public bool ShowBorder { get; set; } = true;   // set false to remove the card border and shadow
        public bool ShowLegend { get; set; } = true;     // multi-series only — set false to hide the legend box
        public int LegendFontSize { get; set; } = 14;
        public string LegendFontWeight { get; set; } = "700";
        public int LegendGap { get; set; } = 16;
        public string LegendPosition { get; set; } = "bottom";   // "top", "bottom", "left", "right"
        public bool ShowDataLabels { get; set; } = false;          // set true to print each value inside its bar, near the right edge
        public bool ShowCrosshair { get; set; } = false;             // horizontal guide line on hover, for comparing series at the same category
        public bool ShowLegendBox { get; set; } = true;             // set false to remove the border/background/shadow, showing plain legend items

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-chart-wrapper" + (ShowBorder ? "" : " mc-no-border"));

            var labelsJson = JsonSerializer.Serialize(Labels ?? Enumerable.Empty<string>());

            var titleHtml = !string.IsNullOrWhiteSpace(Title)
                ? $"<div class='mc-title'>{Title}</div>"
                : "";

            string seriesAttr = "";
            string dataAttr = "";
            string colorsAttr = "";
            bool isMultiSeries = Series != null && Series.Any();

            if (isMultiSeries)
            {
                var seriesJson = JsonSerializer.Serialize(Series.Select(s => new
                {
                    label = s.Label,
                    data = s.Values,
                    color = s.Color
                }));
                seriesAttr = $"data-series='{System.Net.WebUtility.HtmlEncode(seriesJson)}'";
            }
            else
            {
                var dataJson = JsonSerializer.Serialize(Data ?? Enumerable.Empty<double>());
                var colorsJson = JsonSerializer.Serialize(Colors ?? Enumerable.Empty<string>());
                dataAttr = $"data-values='{System.Net.WebUtility.HtmlEncode(dataJson)}'";
                colorsAttr = $"data-colors='{System.Net.WebUtility.HtmlEncode(colorsJson)}'";
            }

            var normalizedPosition = (LegendPosition ?? "bottom").ToLower();
            if (normalizedPosition != "top" && normalizedPosition != "bottom" &&
                normalizedPosition != "left" && normalizedPosition != "right")
            {
                normalizedPosition = "bottom";
            }

            // FIXED — was (isMultiSeries && ShowLegend); see column chart for
            // full explanation. Always renders when ShowLegend is true so
            // JS-populated multi-series charts have somewhere to render into.
            var legendBoxHtml = ShowLegend
                ? $@"<div class='mc-legend-box{(ShowLegendBox ? "" : " mc-legend-plain")}' id='{Id}_legend'
                          style='--mc-legend-font-size:{LegendFontSize}px; --mc-legend-font-weight:{LegendFontWeight}; --mc-legend-gap:{LegendGap}px;'></div>"
                : "";

            output.Content.SetHtmlContent($@"
                {titleHtml}
                <div class='mc-chart-body mc-legend-{normalizedPosition}'>
                    <div class='mc-canvas-wrap' style='height:{ChartHeight};'>
                        <canvas id='{Id}' class='modern-bar-chart-canvas'
                                data-labels='{System.Net.WebUtility.HtmlEncode(labelsJson)}'
                                {dataAttr}
                                {colorsAttr}
                                {seriesAttr}
                                data-dataset-label='{LegendLabel}'
                                data-show-data-labels='{ShowDataLabels.ToString().ToLower()}'
                                data-show-crosshair='{ShowCrosshair.ToString().ToLower()}'></canvas>
                    </div>
                    {legendBoxHtml}
                </div>
            ");
        }
    }
}