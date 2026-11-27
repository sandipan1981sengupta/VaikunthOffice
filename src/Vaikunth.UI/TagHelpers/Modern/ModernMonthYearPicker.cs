// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
namespace Vaikunth.UI.TagHelpers.Modern
{
    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>
    /// Month + Year only picker — same control as ModernDatePickerWithLabel
    /// (label, mandatory red asterisk + validation icon, configurable
    /// border-radius, disabled state, DisableTyping) but the calendar
    /// popup only lets the user pick a month and year, no day. Uses the
    /// "myp-" class prefix so it coexists with ModernDatePickerWithLabel's
    /// "mdpwl-" prefix on the same page without any CSS collision.
    ///
    /// The stored/submitted Value is "MM-yyyy" (e.g. "08-2026") by
    /// default; set DateFormat to change it (any flatpickr-compatible
    /// format token string, e.g. "yyyy-MM" for "2026-08").
    ///
    /// DisableTyping (default false): when true, the user cannot type
    /// into the field at all — they must pick from the month/year
    /// popup. Same convention as ModernDatePickerWithLabel.
    ///
    /// IsLeftLabel (default false): when true, the label sits to the
    /// left of the field on the same row instead of stacked above it.
    /// The label column sizes itself automatically to its own text —
    /// no manual width needed.
    /// </summary>
    [HtmlTargetElement("modern-month-year-picker")]
    public class ModernMonthYearPicker : TagHelper
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Label { get; set; }
        public string Placeholder { get; set; } = "mm-yyyy";
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public bool DisableTyping { get; set; } = true;
        public string DateFormat { get; set; } = "m-Y";   // flatpickr format tokens: m = 01-12, Y = full year
        public bool IsLeftLabel { get; set; } = false;

        // Accepts either a bare number ("4" -> "4px") or a full CSS value.
        public string BorderRadius { get; set; } = "30px";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            var outerClass = "myp-outer" + (IsLeftLabel ? " myp-outer-left-label" : "");
            output.Attributes.SetAttribute("class", outerClass);

            var labelHtml = !string.IsNullOrWhiteSpace(Label) ? $@"
                <label class='myp-label' for='{ID}'>
                    {Label}{(Required ? " <span class='myp-required'>*</span>" : "")}
                </label>" : "";

            var fieldWrapClass = "myp-field" + (Disabled ? " myp-disabled" : "");

            var requiredAttr = Required ? "required" : "";
            var disabledAttr = Disabled ? "disabled" : "";

            var normalizedRadius = BorderRadius;
            if (!string.IsNullOrWhiteSpace(BorderRadius) && double.TryParse(BorderRadius, out _))
            {
                normalizedRadius = BorderRadius + "px";
            }
            var radiusStyleAttr = !string.IsNullOrWhiteSpace(normalizedRadius)
                ? $"style='--myp-radius: {normalizedRadius};'"
                : "";

            var disableTypingAttr = DisableTyping ? "data-disable-typing='true' readonly" : "";

            output.Content.SetHtmlContent($@"
                {labelHtml}
                <div class='{fieldWrapClass}' data-required='{Required.ToString().ToLower()}' {radiusStyleAttr}>
                    <input type='text' id='{ID}' name='{Name}'
                           class='myp-input'
                           value='{Value}' placeholder='{Placeholder}' {requiredAttr} {disabledAttr} {disableTypingAttr}
                           data-date-format='{DateFormat}' />
                    <span class='myp-icon-error' style='display:none;'>
                        <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='8' cy='8' r='8' fill='currentColor'/>
                            <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                            <circle cx='8' cy='11' r='0.9' fill='white'/>
                        </svg>
                    </span>
                    <span class='myp-icon'><i class='bi bi-calendar3'></i></span>
                </div>
            ");
        }
    }
}