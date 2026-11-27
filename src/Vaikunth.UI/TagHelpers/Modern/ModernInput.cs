// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers.Modern
{
    /// <summary>
    /// Pill-shaped text input with a label, optional required (*) marker,
    /// optional character limit, and a type switch: "text" (alphanumeric,
    /// unrestricted), "int"/"number" (digits only), "phone" (digits, +, -,
    /// and parentheses), or "currency" (digits and decimal point). Shows a
    /// red validation icon when a mandatory field is left empty on blur.
    /// Set Multiline="true" to render a &lt;textarea&gt; instead — character
    /// filtering (int/phone/currency) is ignored for multiline fields.
    ///
    /// LabelFontSize / LabelFontWeight control the main label's type size —
    /// leave unset to use the CSS defaults (14px / 700).
    ///
    /// SuffixText renders a second, red label immediately after the main
    /// label on the same line (e.g. "Label SuffixText"). SuffixFontSize /
    /// SuffixFontWeight size it independently of the main label.
    /// SuffixVisible toggles whether it's shown at all (default true —
    /// only relevant when SuffixText is also set).
    ///
    /// Autofocus="true" adds the native HTML autofocus attribute, so the
    /// browser focuses this field automatically when the page loads. Only
    /// one element per page should use this — if several modern-input tags
    /// set Autofocus="true" on the same page, browsers generally focus the
    /// last one in DOM order.
    ///
    /// IsPassword="true" turns this into a password field: value is masked
    /// (type="password"), the left icon becomes a key, and a show/hide
    /// "eye" toggle plus a clear ("x") button are added on the right in
    /// place of the mandatory-validation icon slot. Not supported together
    /// with Multiline — IsPassword is ignored if Multiline="true".
    ///
    /// IsCaps="true" turns everything the user types (or pastes) into
    /// CAPITAL letters as they type — the real value is upper-cased, not just
    /// the display, so the saved data is in caps too. Works for single-line
    /// and Multiline fields; ignored for password fields. Default false.
    /// </summary>
    [HtmlTargetElement("modern-input")]
    public class ModernInput : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public bool Mandatory { get; set; } = false;
        public int? MaxLength { get; set; }
        public string InputType { get; set; } = "text";   // "text", "int"/"number", "phone", or "currency"
        public string Placeholder { get; set; } = "";
        public string Value { get; set; } = "";
        public bool Multiline { get; set; } = false;   // renders <textarea> instead of <input>
        public int Rows { get; set; } = 4;              // visible rows when Multiline is true
        public bool Resizable { get; set; } = true;      // only applies when Multiline is true — user can drag to resize
        public string TooltipText { get; set; }            // shown on hover of the mandatory icon; only rendered if Mandatory is true and this is set
        public bool IsReadOnly { get; set; } = false;        // value is visible/selectable but not editable; still submits with the form
        public bool Autofocus { get; set; } = false;        // focuses this field automatically on page load (native autofocus attribute)
        public bool IsPassword { get; set; } = false;        // masks input, adds key icon + show/hide eye + clear button; ignored when Multiline is true
        public bool IsCaps { get; set; } = false;            // true = whatever the user types/pastes becomes CAPITAL letters; ignored for password fields
        // NOTE: cannot be named/bound as "data-field" — Razor tag helpers are not
        // allowed to bind to any data-* attribute (RZ3004). Use field-name="..."
        // in markup instead; it still renders out as data-field='...' on the
        // actual <input>/<textarea> below, which is what JS (e.g. GridBinder) targets.
        public string FieldName { get; set; }

        // ---- Label sizing (point 1) — leave null/0 to fall back to the
        // CSS defaults (14px / weight 700) so existing usages don't change. ----
        public string LabelFontSize { get; set; } = "16px";  // e.g. "16px", "1rem" — any valid CSS size
        public string LabelFontWeight { get; set; } = "600";   // e.g. "600", "bold"

        // ---- Suffix label (point 2) — a second, red label right after the
        // main label on the same line. Only rendered when SuffixText is set. ----
        public string SuffixText { get; set; }
        public string SuffixFontSize { get; set; } = "14px";  // e.g. "12px" — falls back to CSS default if unset
        public string SuffixFontWeight { get; set; } = "500"; // e.g. "500" — falls back to CSS default if unset
        public bool SuffixVisible { get; set; } = false;  // user-controlled show/hide of the suffix label

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "modern-input-wrapper");

            // Inline style for the main label — only emitted when the caller
            // set a value, so untouched usages keep the CSS defaults.
            var labelStyleParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(LabelFontSize)) labelStyleParts.Add($"font-size:{LabelFontSize}");
            if (!string.IsNullOrWhiteSpace(LabelFontWeight)) labelStyleParts.Add($"font-weight:{LabelFontWeight}");
            var labelStyleAttr = labelStyleParts.Count > 0 ? $"style='{string.Join(";", labelStyleParts)}'" : "";

            // Inline style for the suffix label.
            var suffixStyleParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(SuffixFontSize)) suffixStyleParts.Add($"font-size:{SuffixFontSize}");
            if (!string.IsNullOrWhiteSpace(SuffixFontWeight)) suffixStyleParts.Add($"font-weight:{SuffixFontWeight}");
            var suffixStyleAttr = suffixStyleParts.Count > 0 ? $"style='{string.Join(";", suffixStyleParts)}'" : "";

            var suffixClass = "mi-suffix-label" + (SuffixVisible ? "" : " mi-suffix-hidden");
            var suffixHtml = !string.IsNullOrWhiteSpace(SuffixText)
                ? $"<span class='{suffixClass}' {suffixStyleAttr}>{SuffixText}</span>"
                : "";

            var labelHtml = !string.IsNullOrWhiteSpace(Label) ? $@"
                <label class='mi-label' for='{Id}' {labelStyleAttr}>
                    {Label}{(Mandatory ? " <span class='mi-required'>*</span>" : "")}{suffixHtml}
                </label>" : "";

            var maxLengthAttr = MaxLength.HasValue ? $"maxlength='{MaxLength.Value}'" : "";
            var normalizedType = (InputType ?? "text").ToLower();

            string inputModeAttr;
            switch (normalizedType)
            {
                case "int":
                case "number":
                    inputModeAttr = "inputmode='numeric' pattern='[0-9]*'";
                    break;
                case "phone":
                    inputModeAttr = "inputmode='tel'";   // gives mobile users a phone-style keypad
                    break;
                case "currency":
                    inputModeAttr = "inputmode='decimal'";   // gives mobile users a numeric keypad with a decimal point
                    break;
                default:
                    inputModeAttr = "";
                    break;
            }

            var readOnlyAttr = IsReadOnly ? "readonly" : "";
            var autofocusAttr = Autofocus ? "autofocus" : "";
            var dataFieldAttr = !string.IsNullOrWhiteSpace(FieldName) ? $"data-field='{FieldName}'" : "";

            // Password mode only makes sense for a single-line field.
            var isPasswordMode = IsPassword && !Multiline;
            var htmlInputType = isPasswordMode ? "password" : "text";

            // Caps mode: upper-cases the real value on every keystroke / paste,
            // keeping the cursor where it was (so editing in the middle of the
            // text does not jump the cursor to the end). Not for passwords.
            var isCapsMode = IsCaps && !isPasswordMode;
            var capsAttr = isCapsMode
                ? "data-caps=\"true\" autocapitalize=\"characters\" " +
                  "oninput=\"var s=this.selectionStart,e=this.selectionEnd,u=this.value.toUpperCase();" +
                  "if(this.value!==u){this.value=u;try{this.setSelectionRange(s,e);}catch(x){}}\""
                : "";
            var fieldValue = isCapsMode ? (Value ?? "").ToUpperInvariant() : Value;
            // ShankhNaad: encode user-supplied text so a quote or < in a re-shown value can't break the markup.
            fieldValue = System.Net.WebUtility.HtmlEncode(fieldValue ?? "");

            // Multiline fields are always free text — a numeric/phone/currency
            // textarea doesn't make practical sense — so character filtering
            // (handled in JS by data-input-type) is skipped for <textarea>
            // regardless of what InputType was set.
            var fieldHtml = Multiline
                ? $@"<textarea id='{Id}' name='{Name}' class='mi-input mi-textarea' rows='{Rows}'
                               placeholder='{Placeholder}' {maxLengthAttr} {readOnlyAttr} {autofocusAttr} {dataFieldAttr} {capsAttr}>{fieldValue}</textarea>"
                : $@"<input type='{htmlInputType}' id='{Id}' name='{Name}' class='mi-input'
                            placeholder='{Placeholder}' value='{fieldValue}' {maxLengthAttr} {inputModeAttr} {readOnlyAttr} {autofocusAttr} {dataFieldAttr} {capsAttr} />";

            var wrapClass = "mi-input-wrap"
                + (Multiline ? " mi-multiline" : "")
                + (Multiline && !Resizable ? " mi-no-resize" : "")
                + (isPasswordMode ? " mi-password" : "");

            var tooltipAttr = (Mandatory && !string.IsNullOrWhiteSpace(TooltipText))
                ? $"data-tooltip='{TooltipText}'"
                : "";

            // Left icon: key for password fields, pencil/edit for everything else.
            var leftIconHtml = isPasswordMode
                ? @"<svg width='15' height='15' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                        <circle cx='5.5' cy='5.5' r='3' stroke='currentColor' stroke-width='1.3'/>
                        <path d='M7.7 7.7L13.5 13.5M13.5 13.5V11M13.5 13.5H11' stroke='currentColor' stroke-width='1.3' stroke-linecap='round' stroke-linejoin='round'/>
                    </svg>"
                : @"<svg width='15' height='15' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                        <path d='M11.5 2.5L13.5 4.5L5 13H3V11L11.5 2.5Z' stroke='currentColor' stroke-width='1.3' stroke-linejoin='round'/>
                    </svg>";

            // Right-side slot: the mandatory-error icon and a clear ("x")
            // button always share the SAME position (see .mi-icon-error /
            // .mi-clear-btn in the CSS — both sit at right:16px). JS shows
            // at most one at a time: the error icon when the field is
            // mandatory and empty, the clear button once the user has
            // typed something, neither otherwise. Password fields get an
            // extra eye (show/hide) button further to the right, and the
            // clear button substitutes for "mi-clear-btn" at that same
            // right:16px slot exactly as for any other field.
            var clearBtnHtml = $@"<button type='button' class='mi-clear-btn' aria-label='Clear' tabindex='-1' style='display:none;'>
                        <svg width='14' height='14' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <path d='M3 3L13 13M13 3L3 13' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/>
                        </svg>
                    </button>";

            var errorIconHtml = $@"<span class='mi-icon-error' style='display:none;' {tooltipAttr}>
                        <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='8' cy='8' r='8' fill='currentColor'/>
                            <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                            <circle cx='8' cy='11' r='0.9' fill='white'/>
                        </svg>
                    </span>";

            var eyeToggleHtml = isPasswordMode
                ? $@"<button type='button' class='mi-pw-toggle' aria-label='Show password' tabindex='-1'>
                        <svg class='mi-pw-eye-open' width='18' height='18' viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <path d='M1 12s4-7 11-7 11 7 11 7-4 7-11 7-11-7-11-7Z' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/>
                            <circle cx='12' cy='12' r='3' stroke='currentColor' stroke-width='1.6'/>
                        </svg>
                        <svg class='mi-pw-eye-closed' width='18' height='18' viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg' style='display:none;'>
                            <path d='M1 12s4-7 11-7 11 7 11 7-4 7-11 7-11-7-11-7Z' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/>
                            <circle cx='12' cy='12' r='3' stroke='currentColor' stroke-width='1.6'/>
                            <path d='M2 2L22 22' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/>
                        </svg>
                    </button>"
                : "";

            // Password mode wraps the clear-btn + eye-toggle together so CSS
            // can lay them out side by side (see .mi-pw-actions); non-password
            // fields just get the error icon + clear button stacked at the
            // same right:16px position, exactly one visible at a time.
            var rightSlotHtml = isPasswordMode
                ? $@"<span class='mi-pw-actions'>
                        {clearBtnHtml}
                        {eyeToggleHtml}
                    </span>"
                : $@"{errorIconHtml}
                    {clearBtnHtml}";

            output.Content.SetHtmlContent($@"
                {labelHtml}
                <div class='{wrapClass}' data-input-type='{normalizedType}' data-mandatory='{Mandatory.ToString().ToLower()}'>
                    <span class='mi-icon-left'>
                        {leftIconHtml}
                    </span>
                    {fieldHtml}
                    {rightSlotHtml}
                </div>
            ");
        }
    }
}