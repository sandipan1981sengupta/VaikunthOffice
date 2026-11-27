// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>
    /// One radio option, written as a CHILD tag inside a
    /// &lt;modern-radio-group&gt;. This version renders itself as a real
    /// (invisible) HTML marker element instead of calling SuppressOutput()
    /// and relying on TagHelperContext.Items — that approach measured 0
    /// items reaching the parent when this partial is loaded via AJAX,
    /// even though the child tag helper itself was executing (confirmed
    /// via rendered-length debug output), which points to Items not
    /// flowing reliably across this execution context. Rendering a real
    /// marker element and having the PARENT read it back from its own
    /// actual rendered child content sidesteps that entirely — it only
    /// depends on GetChildContentAsync() returning correct HTML, which
    /// was already confirmed working (rendered-length was non-zero).
    /// </summary>
    [HtmlTargetElement("modern-radio", ParentTag = "modern-radio-group")]
    public class ModernRadio : TagHelper
    {
        public string Value { get; set; }
        public string Label { get; set; }
        public bool Disabled { get; set; } = false;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Renders as a hidden marker <i> element carrying its data in
            // attributes, instead of SuppressOutput(). The parent parses
            // these back out of its own rendered child content string.
            output.TagName = "i";
            output.Attributes.SetAttribute("class", "modern-radio-marker");
            output.Attributes.SetAttribute("style", "display:none");
            output.Attributes.SetAttribute("data-value", Value ?? "");
            output.Attributes.SetAttribute("data-label", Label ?? "");
            output.Attributes.SetAttribute("data-disabled", Disabled ? "true" : "false");
            output.TagMode = Microsoft.AspNetCore.Razor.TagHelpers.TagMode.StartTagAndEndTag;
            output.Content.SetContent(string.Empty);
        }
    }

    public class ModernRadioOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public bool Disabled { get; set; } = false;
    }

    [HtmlTargetElement("modern-radio-group")]
    public class ModernRadioGroup : TagHelper
    {
        public string Name { get; set; }
        public string Label { get; set; }

        // Only used if no <modern-radio> children are nested inside.
        public List<ModernRadioOption> Options { get; set; }

        public string SelectedValue { get; set; }
        public bool Mandatory { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public bool Inline { get; set; } = true;
        public string TooltipText { get; set; }
        public string FieldName { get; set; }
        public string LabelFontSize { get; set; } = "16px";
        public string LabelFontWeight { get; set; } = "600";
        public string OptionFontSize { get; set; }
        public string OptionFontWeight { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();

            // Parse the marker elements back out of the ACTUAL rendered
            // child HTML (not context.Items). We render into a temporary
            // container and read its own child <i class="modern-radio-marker">
            // elements' attributes — this only depends on GetChildContentAsync()
            // producing correct markup, which we've confirmed it does.
            var options = ParseMarkers(childContent);
            if (options.Count == 0 && Options != null)
            {
                options = Options; // fall back to code-supplied list if no markers found
            }

            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-radio-wrapper");

            var labelStyleParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(LabelFontSize)) labelStyleParts.Add($"font-size:{LabelFontSize}");
            if (!string.IsNullOrWhiteSpace(LabelFontWeight)) labelStyleParts.Add($"font-weight:{LabelFontWeight}");
            var labelStyleAttr = labelStyleParts.Count > 0 ? $"style='{string.Join(";", labelStyleParts)}'" : "";

            var optionStyleParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(OptionFontSize)) optionStyleParts.Add($"font-size:{OptionFontSize}");
            if (!string.IsNullOrWhiteSpace(OptionFontWeight)) optionStyleParts.Add($"font-weight:{OptionFontWeight}");
            var optionStyleAttr = optionStyleParts.Count > 0 ? $"style='{string.Join(";", optionStyleParts)}'" : "";

            var groupLabelHtml = !string.IsNullOrWhiteSpace(Label) ? $@"
                <label class='mr-group-label' {labelStyleAttr}>
                    {Label}{(Mandatory ? " <span class='mr-required'>*</span>" : "")}
                </label>" : "";

            var tooltipAttr = (Mandatory && !string.IsNullOrWhiteSpace(TooltipText))
                ? $"data-tooltip='{TooltipText}'"
                : "";

            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";
            var layoutClass = Inline ? "mr-options-inline" : "mr-options-stacked";

            var optionsHtml = string.Join("\n", options.Select((opt, i) =>
            {
                var optionId = $"{Name}_{i}";
                var isChecked = !string.IsNullOrEmpty(SelectedValue) && SelectedValue == opt.Value;
                var isDisabled = Disabled || opt.Disabled;
                var checkedAttr = isChecked ? "checked" : "";
                var disabledAttr = isDisabled ? "disabled" : "";

                return $@"
                    <label class='mr-cbx'>
                        <input type='radio' id='{optionId}' name='{Name}' value='{opt.Value}'
                               {checkedAttr} {disabledAttr} {dataFieldAttr} />
                        <span class='mr-indicator'>
                            <span class='mr-dot'></span>
                        </span>
                        <span class='mr-option-text' {optionStyleAttr}>{opt.Text}</span>
                    </label>";
            }));

            output.Content.SetHtmlContent($@"
                {groupLabelHtml}
                <div class='mr-control {layoutClass}' data-mandatory='{Mandatory.ToString().ToLower()}' data-group-name='{Name}'>
                    {optionsHtml}
                    <span class='mr-icon-error' style='display:none;' {tooltipAttr}>
                        <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='8' cy='8' r='8' fill='currentColor'/>
                            <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                            <circle cx='8' cy='11' r='0.9' fill='white'/>
                        </svg>
                    </span>
                </div>
            ");
        }

        // Extracts data-value / data-label / data-disabled from each
        // <i class="modern-radio-marker" ...></i> in the rendered child
        // HTML using a lightweight regex scan — no external HTML parser
        // dependency required for this narrow, controlled markup shape.
        private static List<ModernRadioOption> ParseMarkers(Microsoft.AspNetCore.Razor.TagHelpers.TagHelperContent content)
        {
            var html = content.GetContent();
            var result = new List<ModernRadioOption>();

            var regex = new System.Text.RegularExpressions.Regex(
                "<i class=\"modern-radio-marker\"[^>]*data-value=\"([^\"]*)\"[^>]*data-label=\"([^\"]*)\"[^>]*data-disabled=\"([^\"]*)\"[^>]*>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in regex.Matches(html))
            {
                result.Add(new ModernRadioOption
                {
                    Value = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value),
                    Text = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value),
                    Disabled = m.Groups[3].Value == "true"
                });
            }

            return result;
        }
    }
}