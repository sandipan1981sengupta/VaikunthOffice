// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>
    /// Checkbox with a label, optional required (*) marker, and two visual
    /// styles selected by IsToggleButton:
    ///   - false (default): classic rounded checkbox with an animated
    ///     checkmark, gradient fill when checked.
    ///   - true: pill-shaped iOS-style toggle switch with a sliding thumb.
    /// Both styles share the same markup shape, JS API and data-field
    /// wiring — only the CSS classes differ, so callers can flip
    /// IsToggleButton without touching any other markup or JS.
    ///
    /// OnText / OffText (toggle-only, default "ON"/"OFF") set the text
    /// shown inside the toggle track for the checked/unchecked states —
    /// e.g. OnText="Yes" OffText="No", or OnText="" OffText="" to hide
    /// the text entirely and show a plain sliding switch.
    ///
    /// LabelFontSize / LabelFontWeight control the label's type size —
    /// leave unset to use the CSS defaults (15px / 500), same convention
    /// as ModernInput.
    ///
    /// DescText renders a second, muted line under the label (e.g. a short
    /// explanation of what the option does).
    /// </summary>
    [HtmlTargetElement("modern-checkbox")]
    public class ModernCheckbox : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public string DescText { get; set; }
        public bool Mandatory { get; set; } = false;
        public bool Checked { get; set; } = false;
        public bool IsReadOnly { get; set; } = false;   // rendered but not interactive; still submits with the form
        public bool Disabled { get; set; } = false;
        public bool IsToggleButton { get; set; } = false;   // false = checkbox look, true = toggle-switch look
        public string OnText { get; set; } = "ON";          // toggle-only: text shown inside the track when checked
        public string OffText { get; set; } = "OFF";         // toggle-only: text shown inside the track when unchecked
        public string TooltipText { get; set; }            // shown on hover of the mandatory icon; only rendered if Mandatory is true and this is set

        // NOTE: cannot be named/bound as "data-field" — Razor tag helpers are not
        // allowed to bind to any data-* attribute (RZ3004). Use field-name="..."
        // in markup instead; it still renders out as data-field='...' on the
        // actual <input> below, which is what JS (e.g. GridBinder) targets.
        public string FieldName { get; set; }

        // ShankhNaad addition: the value posted when checked. Default "true" so the
        // box binds straight to a bool model property. When Value is "true" a hidden
        // "false" is written AFTER the checkbox (same as ASP.NET's own CheckBox helper),
        // so an unticked box posts false instead of nothing. For a list (e.g. picking
        // people) set value="@person.Id" and no hidden field is written.
        public string Value { get; set; } = "true";

        // ---- Label sizing — leave null/empty to fall back to the CSS
        // defaults (15px / weight 500), same convention as ModernInput. ----
        public string LabelFontSize { get; set; } = "16px";
        public string LabelFontWeight { get; set; } = "600";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-checkbox-wrapper");

            var labelStyleParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(LabelFontSize)) labelStyleParts.Add($"font-size:{LabelFontSize}");
            if (!string.IsNullOrWhiteSpace(LabelFontWeight)) labelStyleParts.Add($"font-weight:{LabelFontWeight}");
            var labelStyleAttr = labelStyleParts.Count > 0 ? $"style='{string.Join(";", labelStyleParts)}'" : "";

            var tooltipAttr = (Mandatory && !string.IsNullOrWhiteSpace(TooltipText))
                ? $"data-tooltip='{TooltipText}'"
                : "";

            var checkedAttr = Checked ? "checked" : "";
            var postedValue = System.Net.WebUtility.HtmlEncode(string.IsNullOrEmpty(Value) ? "true" : Value);
            var hiddenFalse = (!string.IsNullOrWhiteSpace(Name) && (Value ?? "true") == "true" && !Disabled)
                ? $"<input type='hidden' name='{Name}' value='false' />"
                : "";
            var disabledAttr = (Disabled || IsReadOnly) ? "disabled" : "";
            var readOnlyDataAttr = IsReadOnly ? "data-readonly='true'" : "";
            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";

            var variantClass = IsToggleButton ? "mc-toggle" : "mc-box";

            // No-desc marker: when DescText is empty there's only one line
            // of text, so mc-text should center it vertically against the
            // checkbox/toggle instead of anchoring it to the top (which is
            // what flex-column does when a second line is expected).
            // ShankhNaad: labels can be people's names, so encode them.
            var safeLabel = System.Net.WebUtility.HtmlEncode(Label ?? "");
            var safeDesc = System.Net.WebUtility.HtmlEncode(DescText ?? "");
            var textClass = "mc-text" + (string.IsNullOrWhiteSpace(DescText) ? " mc-text-single" : "");

            var textHtml = (!string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(DescText)) ? $@"
                <label class='{textClass}' for='{Id}'>
                    {(!string.IsNullOrWhiteSpace(Label) ? $@"<span class='mc-title' {labelStyleAttr}>{safeLabel}{(Mandatory ? " <span class='mc-required'>*</span>" : "")}</span>" : "")}
                    {(!string.IsNullOrWhiteSpace(DescText) ? $"<span class='mc-desc'>{safeDesc}</span>" : "")}
                </label>" : "";

            // The switch/box visual is built from the same two elements
            // (input + span.mc-indicator) for both variants; CSS alone
            // reshapes span.mc-indicator into a checkmark box or a sliding
            // pill depending on which of mc-box / mc-toggle is present on
            // the wrapper, so no JS or markup branching is needed to
            // support both looks.
            var indicatorInner = IsToggleButton
                ? $@"<span class='mc-toggle-text mc-toggle-text-off'>{OffText}</span>
                     <span class='mc-toggle-text mc-toggle-text-on'>{OnText}</span>
                     <span class='mc-thumb'></span>"
                : @"<svg viewBox='0 0 24 24' class='mc-check'><polyline points='4 12 9 17 20 6'></polyline></svg>";

            output.Content.SetHtmlContent($@"
                <div class='mc-control {variantClass}' data-mandatory='{Mandatory.ToString().ToLower()}'>
                    <label class='mc-cbx'>
                        <input type='checkbox' id='{Id}' name='{Name}' value='{postedValue}' {checkedAttr} {disabledAttr} {readOnlyDataAttr} {dataFieldAttr} />{hiddenFalse}
                        <span class='mc-indicator'>
                            {indicatorInner}
                        </span>
                    </label>
                    {textHtml}
                    <span class='mc-icon-error' style='display:none;' {tooltipAttr}>
                        <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='8' cy='8' r='8' fill='currentColor'/>
                            <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                            <circle cx='8' cy='11' r='0.9' fill='white'/>
                        </svg>
                    </span>
                </div>
            ");
        }
    }
}