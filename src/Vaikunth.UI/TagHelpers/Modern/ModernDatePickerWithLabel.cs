// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
namespace Vaikunth.UI.TagHelpers.Modern
{
    using Microsoft.AspNetCore.Razor.TagHelpers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;

    /// <summary>
    /// Separate control from ModernDatePicker — adds a label above (or to the
    /// left of) the field, with a red asterisk and validation icon when
    /// Required is true and the field is empty, plus a configurable
    /// border-radius, height and width. All wrapper/input-specific class
    /// names use an "mdpwl-" prefix so this coexists cleanly with
    /// ModernDatePicker on the same page.
    ///
    /// DisableTyping (default true): when true, the user cannot type into
    /// the field — they must pick a date from the calendar popup.
    ///
    /// IsLeftLabel (default false): when true, the label sits on the left
    /// side of the date picker on the same row instead of above it.
    /// Use LabelWidth to give every left label the same width so a column
    /// of pickers lines up.
    ///
    /// Height / Width / LabelWidth / BorderRadius all accept either a bare
    /// number ("40" -> "40px") or a full CSS value ("40px", "50%", "12rem").
    /// </summary>
    [HtmlTargetElement("modern-datepicker-with-label")]
    public class ModernDatePickerWithLabel : TagHelper
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Label { get; set; }
        public string Placeholder { get; set; } = "dd-mm-yyyy";
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public bool DisableTyping { get; set; } = true;   // true = must pick from calendar, no manual typing

        public string BorderRadius { get; set; } = "30px";

        // NEW — input height (default 40px, from CSS) and overall control width (default 100%)
        public string Height { get; set; } = "40px";
        public string Width { get; set; }

        // NEW — label on the left side of the picker instead of above it
        public bool IsLeftLabel { get; set; } = false;

        // NEW — optional fixed label width, only used when IsLeftLabel = true
        public string LabelWidth { get; set; }

        // Accepts a list of disabled dates/ranges from the caller
        public object DisabledDates { get; set; }

        // NEW — same as ModernInput: field-name="DateOfBirth" in markup renders as
        // data-field='DateOfBirth' on the <input>. Needed so Edit fills the date in
        // (GridBinder.populateFormFromSelection) and Save sends it (CollectFormData).
        // (Razor tag helpers cannot bind data-* attributes directly, hence field-name.)
        public string FieldName { get; set; }

        // NEW — alias for Required, so date pickers can use mandatory="true" like the other controls.
        public bool Mandatory { get => Required; set => Required = value; }

        /// <summary>"40" -> "40px"; "40px" / "50%" / "10rem" pass through; blank -> null.</summary>
        private static string ToCssSize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                ? value + "px"
                : value;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";

            // ---- Outer wrapper: layout class + size variables ----
            var outerClass = "mdpwl-outer" + (IsLeftLabel ? " mdpwl-label-left" : "");
            output.Attributes.SetAttribute("class", outerClass);

            var outerStyles = new List<string>();
            var width = ToCssSize(Width);
            var height = ToCssSize(Height);
            var labelWidth = ToCssSize(LabelWidth);
            if (width != null) outerStyles.Add($"--mdpwl-width: {width};");
            if (height != null) outerStyles.Add($"--mdpwl-height: {height};");
            if (IsLeftLabel && labelWidth != null) outerStyles.Add($"--mdpwl-label-width: {labelWidth};");
            if (outerStyles.Count > 0)
            {
                output.Attributes.SetAttribute("style", string.Join(" ", outerStyles));
            }

            var labelHtml = !string.IsNullOrWhiteSpace(Label) ? $@"
                <label class='mdpwl-label' for='{ID}'>
                    {Label}{(Required ? " <span class='mdpwl-required'>*</span>" : "")}
                </label>" : "";

            var fieldWrapClass = "mdpwl-field" + (Disabled ? " mdpwl-disabled" : "");

            var requiredAttr = Required ? "required" : "";
            var disabledAttr = Disabled ? "disabled" : "";

            var disabledJson = DisabledDates != null
                ? JsonSerializer.Serialize(DisabledDates)
                : "[]";
            var disabledDatesAttr = System.Net.WebUtility.HtmlEncode(disabledJson);

            var normalizedRadius = ToCssSize(BorderRadius);
            var radiusStyleAttr = normalizedRadius != null
                ? $"style='--mdpwl-radius: {normalizedRadius};'"
                : "";

            var disableTypingAttr = DisableTyping ? "data-disable-typing='true' readonly" : "";
            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName)
                ? $"data-field='{System.Net.WebUtility.HtmlEncode(FieldName)}'"
                : "";

            output.Content.SetHtmlContent($@"
                {labelHtml}
                <div class='{fieldWrapClass}' data-required='{Required.ToString().ToLower()}' {radiusStyleAttr}>
                    <input type='text' id='{ID}' name='{Name}'
                           class='mdpwl-input'
                           value='{Value}' placeholder='{Placeholder}' {requiredAttr} {disabledAttr} {disableTypingAttr} {dataFieldAttr}
                           data-disabled-dates='{disabledDatesAttr}' />
                    <span class='mdpwl-icon-error' style='display:none;'>
                        <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='8' cy='8' r='8' fill='currentColor'/>
                            <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                            <circle cx='8' cy='11' r='0.9' fill='white'/>
                        </svg>
                    </span>
                    <span class='mdpwl-icon'><i class='bi bi-calendar3'></i></span>
                </div>
            ");
        }
    }
}