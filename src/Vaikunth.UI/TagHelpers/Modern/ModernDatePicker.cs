// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
﻿namespace Vaikunth.UI.TagHelpers.Modern
{
    using Microsoft.AspNetCore.Razor.TagHelpers;
    using System.Text.Json;

    [HtmlTargetElement("modern-datepicker")]
    public class ModernDatePicker : TagHelper
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Placeholder { get; set; } = "dd-mm-yyyy";
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;

        // Accepts either a bare number ("4" -> "4px") or a full CSS value
        // ("4px", "999px" for a full pill, "50%"). Defaults to square
        // corners (0) if not set — pass a value explicitly to add curve.
        public string BorderRadius { get; set; } = "30px";

        // New: accepts a list of disabled dates/ranges from the caller
        public object DisabledDates { get; set; }

        // NOTE: cannot be named/bound as "data-field" — Razor tag helpers are not
        // allowed to bind to any data-* attribute (RZ3004). Use field-name="..."
        // in markup instead; it still renders out as data-field='...' on the
        // actual <input> below, which is what JS (e.g. GridBinder) targets.
        public string FieldName { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";

            var wrapperClass = "datepicker-wrapper" + (Disabled ? " dp-disabled" : "");
            output.Attributes.SetAttribute("class", wrapperClass);

            var requiredAttr = Required ? "required" : "";
            var disabledAttr = Disabled ? "disabled" : "";
            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";

            // Serialize disabled dates to JSON so JS can parse it
            var disabledJson = DisabledDates != null
                ? JsonSerializer.Serialize(DisabledDates)
                : "[]";

            // HTML-encode quotes so it's safe inside the attribute
            var disabledDatesAttr = System.Net.WebUtility.HtmlEncode(disabledJson);

            // Normalize BorderRadius — a bare number like "4" becomes "4px";
            // anything already containing a unit passes through as-is.
            var normalizedRadius = BorderRadius;
            if (!string.IsNullOrWhiteSpace(BorderRadius) && double.TryParse(BorderRadius, out _))
            {
                normalizedRadius = BorderRadius + "px";
            }
            if (!string.IsNullOrWhiteSpace(normalizedRadius))
            {
                output.Attributes.SetAttribute("style", $"--dp-radius: {normalizedRadius};");
            }

            output.Content.SetHtmlContent($@"
                <input type='text' id='{ID}' name='{Name}'
                       class='modern-datepicker form-control'
                       value='{Value}' placeholder='{Placeholder}' {requiredAttr} {disabledAttr}
                       data-disabled-dates='{disabledDatesAttr}' {dataFieldAttr} />
                <span class='datepicker-icon'><i class='bi bi-calendar3'></i></span>
            ");
        }
    }
}