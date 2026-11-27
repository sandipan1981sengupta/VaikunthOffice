// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>    
    /// Separate control from ModernDropdown — adds a label above the
    /// dropdown, with a red asterisk and validation icon when Required is
    /// true and nothing is selected. All internal class names use a
    /// "ddwl-" prefix so this coexists cleanly with ModernDropdown on the
    /// same page with zero CSS/JS collision.
    /// Items can also be passed from the controller as a list of
    /// (value, text) pairs via the Items property instead of writing
    /// <option> children by hand.
    /// </summary>
    [HtmlTargetElement("modern-dropdown-with-label")]
    [RestrictChildren("option")]
    public class ModernDropdownWithLabel : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Placeholder { get; set; } = "Select";
        public string Value { get; set; }          // preselected value, if any
        public string Label { get; set; }           // optional label shown above the dropdown
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;

        // Optional: control popup list height from the view, e.g. menu-height="200px" or menu-height="12rem"
        public string MenuHeight { get; set; }

        // Optional: controls the toggle button's corner rounding. Accepts
        // either a bare number ("4" -> "4px") or a full CSS value
        // ("4px", "999px" for a full pill, "50%"). Defaults to a full pill
        // (999px) if not set. A small value like "4" reads as essentially
        // square corners.
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
                    sb.Append($"<li data-value='{item.Value}' class='ddwl-option{selected}'>{item.Text}</li>");
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
                    sb.Append($"<li data-value='{val}' class='ddwl-option{selected}'>{text}</li>");
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

            var disabledClass = Disabled ? " ddwl-disabled" : "";
            var optionsStyle = !string.IsNullOrEmpty(MenuHeight)
                ? $"style='max-height:{MenuHeight};'"
                : "";

            var labelHtml = !string.IsNullOrWhiteSpace(Label) ? $@"
                <label class='ddwl-label' for='{Id}'>
                    {Label}{(Required ? " <span class='ddwl-required'>*</span>" : "")}
                </label>" : "";

            // Normalize BorderRadius — a bare number like "4" becomes "4px";
            // anything already containing a unit (px, %, rem, ...) passes through as-is.
            var normalizedRadius = BorderRadius;
            if (!string.IsNullOrWhiteSpace(BorderRadius) && double.TryParse(BorderRadius, out _))
            {
                normalizedRadius = BorderRadius + "px";
            }
            var radiusStyleAttr = !string.IsNullOrWhiteSpace(normalizedRadius)
                ? $"style='--ddwl-radius: {normalizedRadius};'"
                : "";

            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";

            // Outer wrapper holds the label + the dropdown itself, so the
            // label can sit above it.
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-dropdown-with-label-wrapper");

            output.Content.SetHtmlContent($@"
                {labelHtml}
                <div class='modern-dropdown-with-label{disabledClass}' id='{Id}' data-name='{Name}' data-required='{Required.ToString().ToLower()}' {radiusStyleAttr}>
                    <button type='button' class='ddwl-toggle' {(Disabled ? "disabled" : "")}>
                        <span class='ddwl-selected'>{initialLabel}</span>
                        <span class='ddwl-icon-error' style='display:none;'>
                            <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                                <circle cx='8' cy='8' r='8' fill='currentColor'/>
                                <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                                <circle cx='8' cy='11' r='0.9' fill='white'/>
                            </svg>
                        </span>
                        <svg class='ddwl-arrow' width='12' height='12' viewBox='0 0 12 12' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <path d='M2.5 4.5L6 8L9.5 4.5' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/>
                        </svg>
                    </button>
                    <div class='ddwl-menu' role='listbox'>
                        <div class='ddwl-search-wrap'>
                            <input type='text' class='ddwl-search' placeholder='🔍 Search...' autocomplete='off' />
                        </div>
                        <ul class='ddwl-options' {optionsStyle}>
                            {sb}
                        </ul>
                        <li class='ddwl-no-results' style='display:none;'>No matches found</li>
                    </div>
                    <input type='hidden' id='{Id}_value' name='{Name}' value='{Value}' {(Disabled ? "disabled" : "")} {dataFieldAttr} />
                </div>
            ");
        }
    }
}