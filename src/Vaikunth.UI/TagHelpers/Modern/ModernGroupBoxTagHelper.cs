// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>
    /// Renders a modern styled group box with a gradient accent bar, header strip,
    /// and content area, matching the "Member photo and signature" style panel.
    ///
    /// Usage:
    /// <modern-group-box title="Member photo and signature"
    ///                    header-bg-color="#ffffff"
    ///                    header-text-color="#1e3a8a"
    ///                    content-bg-color="#fbfbfc"
    ///                    min-height="300px">
    ///     <!-- caller's own inputs / dropdowns / controls go here -->
    ///     <select class="form-select">...</select>
    /// </modern-group-box>
    ///
    /// Two-tone title (optional):
    /// <modern-group-box title="Member photo|and signature" title-accent-color="#a3298f">
    /// A pipe "|" in the title splits it into a primary-color part and an accent-color part,
    /// matching the "Member photo" (blue) / "and signature" (magenta) look.
    /// </summary>
    [HtmlTargetElement("modern-group-box")]
    public class ModernGroupBoxTagHelper : TagHelper
    {
        /// <summary>The header title text. Use "Left|Right" to render a two-tone title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Header strip background color. Default: #ffffff</summary>
        public string HeaderBgColor { get; set; } = "#ffffff";

        /// <summary>Header title text color (also used for the "primary"/left part of a two-tone title). Default: #1f5fa8</summary>
        public string HeaderTextColor { get; set; } = "#2563eb";

        /// <summary>
        /// Optional CSS gradient (e.g. "linear-gradient(90deg, #2563eb, #1e3a8a)") for the title text.
        /// When set, this overrides HeaderTextColor and TitleAccentColor and paints the whole
        /// title — including both parts of a two-tone "Left|Right" title — with the gradient.
        /// </summary>
        public string? HeaderTextGradient { get; set; } 

        /// <summary>Color for the second part of a two-tone title (text after "|"). Default: #c0299a</summary>
        public string TitleAccentColor { get; set; } = "rgba(240, 0, 188, 1)";

        /// <summary>Content/body area background color. Default: #fbfbfc</summary>
        public string ContentBgColor { get; set; } = "#fbfbfc";

        /// <summary>Outer box border color. Default: #c9c9c9</summary>
        public string BorderColor { get; set; } = "#c9c9c9";

        /// <summary>Outer box corner radius. Default: 16px</summary>
        public string Radius { get; set; } = "16px";

        /// <summary>Min-height of the content area, e.g. "300px". Default: auto</summary>
        public string MinHeight { get; set; } = "auto";

        /// <summary>CSS background value (color or gradient) for the top accent bar. Default: blue-to-magenta gradient</summary>
        public string AccentBar { get; set; } 

        /// <summary>
        /// Content layout mode: "center" (default, flex column centered — good for the
        /// photo/browse-row style), "stretch" (flex column, full-width children),
        /// or "block" (plain block layout for arbitrary form controls).
        /// </summary>
        public string ContentLayout { get; set; } = "stretch";

        /// <summary>Extra CSS class(es) to append to the outer element.</summary>
        public string? CssClass { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;

            var outerClass = "cee-group-box" + (string.IsNullOrWhiteSpace(CssClass) ? "" : " " + CssClass);
            output.Attributes.SetAttribute("class", outerClass);

            var style = new StringBuilder();
            style.Append($"--gb-header-bg:{HeaderBgColor};");
            style.Append($"--gb-header-color:{HeaderTextColor};");
            style.Append($"--gb-header-accent-color:{TitleAccentColor};");
            style.Append($"--gb-content-bg:{ContentBgColor};");
            style.Append($"--gb-border-color:{BorderColor};");
            style.Append($"--gb-radius:{Radius};");
            style.Append($"--gb-min-height:{MinHeight};");
            style.Append($"--gb-accent:{AccentBar};");

            var hasGradientTitle = !string.IsNullOrWhiteSpace(HeaderTextGradient);
            if (hasGradientTitle)
            {
                style.Append($"--gb-header-gradient:{HeaderTextGradient};");
            }

            output.Attributes.SetAttribute("style", style.ToString());

            var contentClass = "cee-group-content";
            contentClass += ContentLayout switch
            {
                "stretch" => " align-stretch",
                "block" => " layout-block",
                _ => ""
            };

            var titleClass = "cee-group-title" + (hasGradientTitle ? " gradient-text" : "");

            var encoder = HtmlEncoder.Default;
            var titleHtml = BuildTitleHtml(Title, encoder);

            var sb = new StringBuilder();
            sb.Append("<div class=\"cee-group-header\">");
            sb.Append($"<p class=\"{titleClass}\">");
            sb.Append(titleHtml);
            sb.Append("</p>");
            sb.Append("</div>");
            sb.Append($"<div class=\"{contentClass}\">");

            output.PreContent.SetHtmlContent(sb.ToString());

            // output.Content (the tag's inner body, i.e. the caller's own controls) passes through untouched.

            output.PostContent.SetHtmlContent("</div>"); // closes .cee-group-content
        }

        private static string BuildTitleHtml(string title, HtmlEncoder encoder)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            var pipeIndex = title.IndexOf('|');
            if (pipeIndex < 0)
            {
                return encoder.Encode(title);
            }

            var left = title[..pipeIndex];
            var right = title[(pipeIndex + 1)..];

            return $"<span class=\"part-primary\">{encoder.Encode(left)}</span>" +
                   $"<span class=\"part-accent\">{encoder.Encode(right)}</span>";
        }
    }
}