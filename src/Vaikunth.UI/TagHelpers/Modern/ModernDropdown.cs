// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>    
    /// Items can also be passed from the controller as a list of (value, text) pairs
    /// via the Items property instead of writing <option> children by hand.
    /// </summary>
    [HtmlTargetElement("modern-dropdown")]
    [RestrictChildren("option")]
    public class ModernDropdown : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Placeholder { get; set; } = "Select";
        public string Value { get; set; }          // preselected value, if any
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;

        // Optional: control popup list height from the view, e.g. menu-height="200px" or menu-height="12rem"
        public string MenuHeight { get; set; }

        // Optional: controls the toggle button's corner rounding. Accepts
        // either a bare number ("4" -> "4px") or a full CSS value
        // ("4px", "999px" for a full pill, "50%"). Defaults to square
        // corners (0) if not set — pass a value explicitly to add curve.
        public string BorderRadius { get; set; }

        // Optional: pass items programmatically instead of child <option> tags
        public IEnumerable<(string Value, string Text)> Items { get; set; }

        // NOTE: cannot be named/bound as "data-field" — Razor tag helpers are not
        // allowed to bind to any data-* attribute (RZ3004). Use field-name="..."
        // in markup instead; it still renders out as data-field='...' on the
        // hidden input holding the actual selected value (the visible button/
        // span is just display), which is what JS (e.g. GridBinder) targets.
        public string FieldName { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var innerOptionsHtml = childContent.GetContent();

            var sb = new StringBuilder();

            // If Items was supplied in code-behind, build <li> from it instead of <option> children
            if (Items != null && Items.Any())
            {
                foreach (var item in Items)
                {
                    var selected = item.Value == Value ? " selected" : "";
                    sb.Append($"<li data-value='{item.Value}' class='dd-option{selected}'>{item.Text}</li>");
                }
            }
            else
            {
                // Parse simple <option value="..">Text</option> children into <li> markup
                // (kept intentionally simple — assumes well-formed <option> tags)
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    innerOptionsHtml,
                    "<option\\s+value=[\"']?([^\"'>]*)[\"']?>(.*?)</option>",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var val = m.Groups[1].Value.Trim();
                    var text = m.Groups[2].Value.Trim();
                    var selected = val == Value ? " selected" : "";
                    sb.Append($"<li data-value='{val}' class='dd-option{selected}'>{text}</li>");
                }
            }

            var initialLabel = Placeholder;
            if (!string.IsNullOrEmpty(Value))
            {
                // Try to find matching text for the preselected value
                var match = System.Text.RegularExpressions.Regex.Match(
                    sb.ToString(),
                    $"data-value='{System.Text.RegularExpressions.Regex.Escape(Value)}'[^>]*>(.*?)</li>");
                if (match.Success) initialLabel = match.Groups[1].Value;
            }

            var disabledClass = Disabled ? " dd-disabled" : "";
            var optionsStyle = !string.IsNullOrEmpty(MenuHeight)
                ? $"style='max-height:{MenuHeight};'"
                : "";

            // Normalize BorderRadius — a bare number like "4" becomes "4px";
            // anything already containing a unit (px, %, rem, ...) passes through as-is.
            var normalizedRadius = BorderRadius;
            if (!string.IsNullOrWhiteSpace(BorderRadius) && double.TryParse(BorderRadius, out _))
            {
                normalizedRadius = BorderRadius + "px";
            }

            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";

            output.TagName = "div";
            output.Attributes.SetAttribute("class", $"modern-dropdown{disabledClass}");
            output.Attributes.SetAttribute("id", Id);
            output.Attributes.SetAttribute("data-name", Name);

            // FIX: the previous version did SetAttribute(requiredAttr, null)
            // where requiredAttr was already the full string "data-required='true'" —
            // that set an attribute literally NAMED "data-required='true'" instead
            // of setting data-required="true" properly. Fixed below.
            if (Required)
            {
                output.Attributes.SetAttribute("data-required", "true");
            }

            if (!string.IsNullOrWhiteSpace(normalizedRadius))
            {
                output.Attributes.SetAttribute("style", $"--dd-radius: {normalizedRadius};");
            }

            output.Content.SetHtmlContent($@"
    <button type='button' class='dd-toggle' {(Disabled ? "disabled" : "")}>
        <span class='dd-selected'>{initialLabel}</span>
        <svg class='dd-arrow' width='12' height='12' viewBox='0 0 12 12' fill='none' xmlns='http://www.w3.org/2000/svg'>
            <path d='M2.5 4.5L6 8L9.5 4.5' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/>
        </svg>
    </button>
    <div class='dd-menu' role='listbox'>
        <div class='dd-search-wrap'>
            <input type='text' class='dd-search' placeholder='🔍 Search...' autocomplete='off' />
        </div>
        <ul class='dd-options' {optionsStyle}>
            {sb}
        </ul>
        <li class='dd-no-results' style='display:none;'>No matches found</li>
    </div>
    <input type='hidden' id='{Id}_value' name='{Name}' value='{Value}' {(Disabled ? "disabled" : "")} {dataFieldAttr} />
");
        }
    }
}