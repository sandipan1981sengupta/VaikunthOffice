// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Json;

namespace Vaikunth.UI.TagHelpers.Modern
{
    public class TableColumn
    {
        public string Field { get; set; }
        public string Header { get; set; }
        public bool Sortable { get; set; } = true;
        public bool Filterable { get; set; } = true;
        public bool Visible { get; set; } = true;
        public string Width { get; set; }   // optional, e.g. "120px"
        public string Type { get; set; } = "text";   // "text" (default), "toggle", "image", or "badge"
        public string ImageSize { get; set; }         // e.g. "70px" — only applies to type="image" columns; falls back to 32px if not set
        public string HeaderAlign { get; set; }        // "left" (default), "center", "right"
        public string CellAlign { get; set; }           // "left" (default), "center", "right"
        public string Format { get; set; }               // display format applied client-side (modern-table.js)

        // ---- type="badge" only ----
        public Dictionary<string, string> BadgeColors { get; set; }
        public string BadgeDefaultColor { get; set; }
    }

    [HtmlTargetElement("modern-table")]
    [RestrictChildren("column")]
    public class ModernTable : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }               // hidden input name for selected row id
        public int PageSize { get; set; } = 50;
        public bool Searchable { get; set; } = true;
        public bool Selectable { get; set; } = true;
        public bool EnablePaging { get; set; } = true;
        public bool ColumnToggle { get; set; } = true;
        // Caps the column-chooser popup's height (e.g. "220px", "16rem"). Bare numbers
        // auto-get "px". When the column list is taller than this, it scrolls internally
        // instead of the popup growing past the viewport. Defaults to 260px — enough for
        // roughly 8-9 column rows before scrolling kicks in; pass a value explicitly to
        // override, or an empty string to remove the cap entirely (grow to fit content).
        public string ColumnPanelMaxHeight { get; set; } = "260px";
        public string SearchPlaceholder { get; set; } = "Search...";

        // ---- "Filters" toggle button (shows/hides the per-column filter row) ----
        // When true, a Filters button renders BEFORE the search box and the column
        // filter row starts collapsed; clicking the button opens/closes it.
        // When false, the filter row is always visible (original behavior).
        // The button is never rendered if no column is filterable.
        public bool ShowFilterButton { get; set; } = true;
        public string FilterButtonText { get; set; } = "Filters";
        public bool FilterRowOpen { get; set; } = false;   // start with the filter row expanded

        // ---- Mobile layout breakpoint ----
        // Screen width (in px) at or below which the toolbar switches to the
        // hamburger "Options" popup layout (Filters/Search/date-range/Columns
        // move into the sheet) instead of the normal desktop toolbar row.
        // CSS media features can't read a custom property (no browser
        // supports @media (max-width: var(--x))), so this can't be a plain
        // @media query — modern-table.css's mobile rules are instead all
        // scoped under a plain ".mt-mobile-mode" class (see its "MOBILE
        // LAYOUT" section) rather than @media (max-width: 950px). This value
        // is written out as data-mobile-breakpoint below, and
        // modern-table.js reads that attribute (falling back to 950 if
        // missing) to build its own window.matchMedia check, which is what
        // actually adds/removes .mt-mobile-mode — on both the wrapper and
        // the sheet/backdrop, since the latter get portaled to <body> and
        // need the class themselves once outside the wrapper.
        public int MobileBreakpoint { get; set; } = 950;

        public string RowIdField { get; set; } = "id";
        public string TableHeight { get; set; } = "100%";       // e.g. "100%", "400px", "80vh"
        public string TableWidth { get; set; } = "100%";          // e.g. "800px" or "100%"
        public bool ShowVerticalLines { get; set; } = false;
        public bool IsAnimatedScrolling { get; set; } = false;
        public string HeaderHeight { get; set; } = "40px";
        public string HeaderFontSize { get; set; } = "12px";
        public string HeaderFontWeight { get; set; } = "700";
        public string RowHeight { get; set; } = "50px";
        public string RowFontSize { get; set; } = "14px";
        public string RowFontWeight { get; set; } = "500";
        public string BadgeWidth { get; set; }
        public string GridName { get; set; }
        public string Icon { get; set; }
        public string IconColor { get; set; }
        public string IconSize { get; set; }
        public string IconBackgroundColor { get; set; }
        public string IconMargin { get; set; }
        public string DangerColor { get; set; }
        public string DangerBackgroundColor { get; set; }
        public string WarningColor { get; set; }
        public string WarningBackgroundColor { get; set; }
        public string SuccessColor { get; set; }
        public string SuccessBackgroundColor { get; set; }
        public bool ShowGridNameAndIcon { get; set; } = true;

        // ---- Toolbar date-range filter (two modern-datepicker instances + search button) ----
        public bool ShowFirstDatePicker { get; set; } = false;
        public string FirstDateLabel { get; set; } = "From";
        public string FirstDateName { get; set; }        // form field name for first date picker
        public string FirstDateValue { get; set; }

        public bool ShowSecondDatePicker { get; set; } = false;
        public string SecondDateLabel { get; set; } = "To";
        public string SecondDateName { get; set; }        // form field name for second date picker
        public string SecondDateValue { get; set; }

        // Shared border radius applied to both date pickers.
        // Accepts a bare number ("4" -> "4px") or a full CSS value ("4px", "999px", "50%").
        // Defaults to whatever modern-datepicker's own default is (square corners) if not set.
        public string DatePickerBorderRadius { get; set; }

        // Shared font size/weight applied to both "From"/"To" labels.
        public string DateLabelFontSize { get; set; }
        public string DateLabelFontWeight { get; set; }

        // ---- Search button: round icon-only button (search glyph), no text ----
        // Shared by the date-range block. Also usable standalone if a caller wants
        // just the round button with neither dates, dropdowns, nor the search input.
        public bool ShowSearchButton { get; set; } = false;
        public string SearchButtonBackgroundColor { get; set; }
        public string SearchButtonIconColor { get; set; }
        public string SearchButtonSize { get; set; }   // diameter, e.g. "37px". Accepts bare number too.

        // ---- Toolbar dropdown filters (two modern-dropdown instances, independent of date-range) ----
        // Reproduces modern-dropdown's own markup shape (button + menu + hidden input) inline,
        // for the same reason the date-range block reproduces modern-datepicker's markup: runtime
        // HTML built via output.Content.SetHtmlContent() is not re-run through the Razor tag helper
        // pipeline, so a literal <modern-dropdown> tag inside this string would render unprocessed.
        // Wiring (open/close, search-filter, option select) is handled by modern-table.js, which
        // already ships the same dd-* behavior alongside its own table logic.
        public bool ShowFirstDropdown { get; set; } = false;
        public string FirstDropdownName { get; set; }
        public string FirstDropdownPlaceholder { get; set; } = "Select";
        public string FirstDropdownValue { get; set; }
        public IEnumerable<(string Value, string Text)> FirstDropdownItems { get; set; }

        public bool ShowSecondDropdown { get; set; } = false;
        public string SecondDropdownName { get; set; }
        public string SecondDropdownPlaceholder { get; set; } = "Select";
        public string SecondDropdownValue { get; set; }
        public IEnumerable<(string Value, string Text)> SecondDropdownItems { get; set; }

        // Shared border radius + width applied to both toolbar dropdowns.
        public string DropdownBorderRadius { get; set; }
        public string DropdownWidth { get; set; }   // e.g. "160px"; falls back to modern-dropdown's own default width if unset

        // ---- Toolbar input-with-search (single modern-input-with-search instance) ----
        // Same inline-reproduction approach as above, matching modern-input-with-search's
        // own markup (label + pill input + attached round search button). Fires the same
        // "mi:search" event on its input, so existing page-level handlers keep working
        // whether the control lives standalone or inside this toolbar.
        public bool ShowSearchInput { get; set; } = false;
        public string SearchInputId { get; set; }
        public string SearchInputName { get; set; }
        public string SearchInputLabel { get; set; }
        public bool SearchInputMandatory { get; set; } = false;
        public string SearchInputType { get; set; } = "text";   // "text", "int"/"number", "phone", or "currency"
        public string SearchInputPlaceholder { get; set; } = "";
        public string SearchInputValue { get; set; } = "";
        public string SearchInputTooltipText { get; set; }
        public string SearchInputBorderRadius { get; set; }
        public bool SearchInputReadOnly { get; set; } = false;

        public IEnumerable<TableColumn> Columns { get; set; }
        public IEnumerable<Dictionary<string, object>> Items { get; set; }

        // Normalizes a bare number ("4") to a pixel value ("4px"); passes anything
        // that already has a unit (e.g. "4px", "999px", "50%") through unchanged.
        // Mirrors the same normalization used by ModernDatePicker's own BorderRadius.
        private static string NormalizeCssLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return double.TryParse(value, out _) ? value + "px" : value;
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var declaredColumns = new List<TableColumn>();
            context.Items[typeof(ModernTable)] = declaredColumns;

            // IMPORTANT: Await child content FIRST so all <column> TagHelpers finish running
            // and populate declaredColumns before we serialize them to JSON.
            var childContent = await output.GetChildContentAsync();

            var columns = declaredColumns.Count > 0
                ? declaredColumns
                : (Columns ?? Enumerable.Empty<TableColumn>()).ToList();

            // Only render the Filters button if at least one column is actually filterable.
            var renderFilterButton = ShowFilterButton && columns.Any(c => c.Filterable);

            output.TagName = "div";

            var wrapperClass = "modern-table-wrapper"
                + (ShowVerticalLines ? " mt-vertical-lines" : "")
                + (IsAnimatedScrolling ? " mt-animated-scroll" : "")
                + (renderFilterButton ? " mt-has-filter-btn" : "")
                + (renderFilterButton && FilterRowOpen ? " mt-filters-open" : "");
            output.Attributes.SetAttribute("class", wrapperClass);
            output.Attributes.SetAttribute("id", Id);
            output.Attributes.SetAttribute("data-page-size", PageSize.ToString());
            output.Attributes.SetAttribute("data-selectable", Selectable.ToString().ToLower());
            output.Attributes.SetAttribute("data-enable-paging", EnablePaging.ToString().ToLower());
            output.Attributes.SetAttribute("data-row-id-field", RowIdField);
            output.Attributes.SetAttribute("data-filters-open", (renderFilterButton && FilterRowOpen).ToString().ToLower());
            output.Attributes.SetAttribute("data-mobile-breakpoint", MobileBreakpoint.ToString());

            var styleParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(TableHeight))
            {
                styleParts.Add($"--mt-wrapper-height: {TableHeight};");
                styleParts.Add($"--mt-body-height: {TableHeight};");
            }
            if (!string.IsNullOrWhiteSpace(TableWidth))
                styleParts.Add($"--mt-wrapper-width: {TableWidth};");
            if (!string.IsNullOrWhiteSpace(HeaderHeight))
                styleParts.Add($"--mt-header-height: {HeaderHeight};");
            if (!string.IsNullOrWhiteSpace(HeaderFontSize))
                styleParts.Add($"--mt-header-font-size: {HeaderFontSize};");
            if (!string.IsNullOrWhiteSpace(HeaderFontWeight))
                styleParts.Add($"--mt-header-font-weight: {HeaderFontWeight};");
            if (!string.IsNullOrWhiteSpace(RowHeight))
                styleParts.Add($"--mt-row-height: {RowHeight};");
            if (!string.IsNullOrWhiteSpace(RowFontSize))
                styleParts.Add($"--mt-row-font-size: {RowFontSize};");
            if (!string.IsNullOrWhiteSpace(RowFontWeight))
                styleParts.Add($"--mt-row-font-weight: {RowFontWeight};");
            if (!string.IsNullOrWhiteSpace(BadgeWidth))
                styleParts.Add($"--mt-badge-width: {BadgeWidth};");
            var normalizedColPanelMaxHeight = NormalizeCssLength(ColumnPanelMaxHeight);
            if (!string.IsNullOrWhiteSpace(normalizedColPanelMaxHeight))
                styleParts.Add($"--mt-col-panel-max-height: {normalizedColPanelMaxHeight};");
            if (!string.IsNullOrWhiteSpace(IconColor))
                styleParts.Add($"--mt-icon-color: {IconColor};");
            if (!string.IsNullOrWhiteSpace(IconSize))
                styleParts.Add($"--mt-icon-size: {IconSize};");
            if (!string.IsNullOrWhiteSpace(IconBackgroundColor))
                styleParts.Add($"--mt-icon-bg: {IconBackgroundColor};");
            if (!string.IsNullOrWhiteSpace(IconMargin))
                styleParts.Add($"--mt-icon-margin: {IconMargin};");
            if (!string.IsNullOrWhiteSpace(DangerColor))
                styleParts.Add($"--mt-danger-color: {DangerColor};");
            if (!string.IsNullOrWhiteSpace(DangerBackgroundColor))
                styleParts.Add($"--mt-danger-bg: {DangerBackgroundColor};");
            if (!string.IsNullOrWhiteSpace(WarningColor))
                styleParts.Add($"--mt-warning-color: {WarningColor};");
            if (!string.IsNullOrWhiteSpace(WarningBackgroundColor))
                styleParts.Add($"--mt-warning-bg: {WarningBackgroundColor};");
            if (!string.IsNullOrWhiteSpace(SuccessColor))
                styleParts.Add($"--mt-success-color: {SuccessColor};");
            if (!string.IsNullOrWhiteSpace(SuccessBackgroundColor))
                styleParts.Add($"--mt-success-bg: {SuccessBackgroundColor};");

            var normalizedDatePickerRadius = NormalizeCssLength(DatePickerBorderRadius);
            if (!string.IsNullOrWhiteSpace(normalizedDatePickerRadius))
                styleParts.Add($"--mt-daterange-radius: {normalizedDatePickerRadius};");
            if (!string.IsNullOrWhiteSpace(DateLabelFontSize))
                styleParts.Add($"--mt-daterange-label-size: {DateLabelFontSize};");
            if (!string.IsNullOrWhiteSpace(DateLabelFontWeight))
                styleParts.Add($"--mt-daterange-label-weight: {DateLabelFontWeight};");
            if (!string.IsNullOrWhiteSpace(SearchButtonBackgroundColor))
                styleParts.Add($"--mt-daterange-search-bg: {SearchButtonBackgroundColor};");
            if (!string.IsNullOrWhiteSpace(SearchButtonIconColor))
                styleParts.Add($"--mt-daterange-search-icon-color: {SearchButtonIconColor};");
            var normalizedSearchBtnSize = NormalizeCssLength(SearchButtonSize);
            if (!string.IsNullOrWhiteSpace(normalizedSearchBtnSize))
                styleParts.Add($"--mt-daterange-search-size: {normalizedSearchBtnSize};");

            var normalizedDropdownRadius = NormalizeCssLength(DropdownBorderRadius);
            if (!string.IsNullOrWhiteSpace(normalizedDropdownRadius))
                styleParts.Add($"--mt-toolbar-dd-radius: {normalizedDropdownRadius};");
            if (!string.IsNullOrWhiteSpace(DropdownWidth))
                styleParts.Add($"--mt-toolbar-dd-width: {DropdownWidth};");

            var normalizedSearchInputRadius = NormalizeCssLength(SearchInputBorderRadius);
            if (!string.IsNullOrWhiteSpace(normalizedSearchInputRadius))
                styleParts.Add($"--mt-toolbar-mis-radius: {normalizedSearchInputRadius};");

            if (styleParts.Count > 0)
            {
                output.Attributes.SetAttribute("style", string.Join(" ", styleParts));
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var columnsJson = JsonSerializer.Serialize(columns, jsonOptions);
            var itemsJson = JsonSerializer.Serialize(Items ?? Enumerable.Empty<Dictionary<string, object>>());

            var columnToggleHtml = ColumnToggle ? @"
        <div class='mt-col-toggle-wrap'>
            <button type='button' class='mt-col-toggle-btn'>
                <svg width='15' height='15' viewBox='0 0 15 15' fill='none' xmlns='http://www.w3.org/2000/svg'>
                    <rect x='1.5' y='2' width='3.5' height='11' rx='1' stroke='currentColor' stroke-width='1.3'/>
                    <rect x='5.75' y='2' width='3.5' height='11' rx='1' stroke='currentColor' stroke-width='1.3'/>
                    <rect x='10' y='2' width='3.5' height='11' rx='1' stroke='currentColor' stroke-width='1.3'/>
                </svg>
                Columns
            </button>
            <div class='mt-col-panel'><div class='mt-col-list'></div><div class='mt-col-resize-handle' title='Drag to resize' aria-hidden='true'><svg width='10' height='10' viewBox='0 0 10 10' fill='none' xmlns='http://www.w3.org/2000/svg'><path d='M9 1L1 9M9 5L5 9M9 9L9 9' stroke='currentColor' stroke-width='1.3' stroke-linecap='round'/></svg></div></div>
        </div>" : "";

            var titleIconHtml = ShowGridNameAndIcon ? (!string.IsNullOrWhiteSpace(Icon) ? $"<span class='mt-title-icon'><i class='fa fa-{Icon}'></i></span>" : "") : "";
            var titleHtml = ShowGridNameAndIcon ? (!string.IsNullOrWhiteSpace(GridName) ? $@" <div class='mt-title-wrap'>{titleIconHtml}<span class='mt-title-text'>{GridName}</span></div>" : "") : "";

            // ---- "Filters" toggle button: rendered BEFORE the search box ----
            // Clicking it toggles .mt-filters-open on the wrapper, which shows/hides the
            // per-column filter row (see modern-table.css / modern-table.js). The count
            // badge shows how many column filters are currently active. The funnel icon
            // and trailing chevron are wrapped in their own spans so modern-table.css can
            // animate/recolor each independently (chevron rotates on open, funnel just
            // recolors) without fighting the button's own color transition.
            var filterButtonHtml = renderFilterButton ? $@"
        <button type='button' class='mt-filter-toggle-btn' aria-expanded='{(FilterRowOpen ? "true" : "false")}' title='Show column filters'>
            <span class='mt-filter-toggle-icon'>
                <svg width='14' height='14' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                    <path d='M2.5 3.5h11c.4 0 .7.44.44.78L9.75 9.9V13l-3.5-1.75V9.9L2.06 4.28c-.26-.34.04-.78.44-.78z' stroke='currentColor' stroke-width='1.35' stroke-linejoin='round' stroke-linecap='round'/>
                </svg>
            </span>
            <span class='mt-filter-toggle-text'>{FilterButtonText}</span>
            <span class='mt-filter-count' style='display:none;'></span>
            <span class='mt-filter-toggle-chevron'>
                <svg width='10' height='10' viewBox='0 0 12 12' fill='none' xmlns='http://www.w3.org/2000/svg'>
                    <path d='M2.5 4.5L6 8L9.5 4.5' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/>
                </svg>
            </span>
        </button>" : "";

            var searchBoxHtml = Searchable ? $@"
        <div class='mt-search-wrap'>
            <i class='mt-search-icon fa fa-search'></i>
            <input type='text' class='mt-search' placeholder='{SearchPlaceholder}' autocomplete='off' />
        </div>" : "";

            // ---- Date-range block: first picker, second picker ----
            // NOTE: <modern-datepicker> is expanded here as plain markup (not emitted as a literal
            // tag) because runtime-built HTML from output.Content.SetHtmlContent() is not re-run
            // through the Razor tag helper pipeline — a literal <modern-datepicker> tag inside this
            // string would render unprocessed. So we reproduce ModernDatePicker's own output shape
            // (input + icon span, wrapped in .datepicker-wrapper) directly.
            // Both pickers share one --mt-daterange-radius custom property (set at wrapper level
            // above), applied via CSS to .mt-daterange-field .datepicker-wrapper.
            var firstDateHtml = ShowFirstDatePicker ? $@"
            <div class='mt-daterange-field'>
                <label class='mt-daterange-label'>{FirstDateLabel}</label>
                <div class='datepicker-wrapper'>
                    <input type='text' id='{Id}_firstDate' name='{FirstDateName}'
                           class='modern-datepicker form-control' value='{FirstDateValue}'
                           placeholder='dd-mm-yyyy' data-disabled-dates='[]' />
                    <span class='datepicker-icon'><i class='bi bi-calendar3'></i></span>
                </div>
            </div>" : "";

            var secondDateHtml = ShowSecondDatePicker ? $@"
            <div class='mt-daterange-field'>
                <label class='mt-daterange-label'>{SecondDateLabel}</label>
                <div class='datepicker-wrapper'>
                    <input type='text' id='{Id}_secondDate' name='{SecondDateName}'
                           class='modern-datepicker form-control' value='{SecondDateValue}'
                           placeholder='dd-mm-yyyy' data-disabled-dates='[]' />
                    <span class='datepicker-icon'><i class='bi bi-calendar3'></i></span>
                </div>
            </div>" : "";

            // ---- Toolbar dropdown block: first dropdown, second dropdown ----
            // Reproduces modern-dropdown's own markup (toggle button + searchable menu + hidden
            // input) inline, same rationale as the datepicker block above. Options come from the
            // typed FirstDropdownItems/SecondDropdownItems (Value, Text) pairs — no child <option>
            // tag parsing here, since <column> is the only permitted child of <modern-table>.
            string BuildToolbarDropdown(string fieldId, string name, string placeholder, string value,
                                          IEnumerable<(string Value, string Text)> items)
            {
                var optionsList = items ?? Enumerable.Empty<(string Value, string Text)>();
                var optionsHtml = new System.Text.StringBuilder();
                var initialLabel = placeholder;

                foreach (var item in optionsList)
                {
                    var selected = item.Value == value ? " selected" : "";
                    optionsHtml.Append($"<li data-value='{item.Value}' class='dd-option{selected}'>{item.Text}</li>");
                    if (item.Value == value) initialLabel = item.Text;
                }

                return $@"
            <div class='modern-dropdown mt-toolbar-dd' id='{fieldId}' data-name='{name}'>
                <button type='button' class='dd-toggle'>
                    <span class='dd-selected'>{initialLabel}</span>
                    <svg class='dd-arrow' width='12' height='12' viewBox='0 0 12 12' fill='none' xmlns='http://www.w3.org/2000/svg'>
                        <path d='M2.5 4.5L6 8L9.5 4.5' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/>
                    </svg>
                </button>
                <div class='dd-menu' role='listbox'>
                    <div class='dd-search-wrap'>
                        <input type='text' class='dd-search' placeholder='🔍 Search...' autocomplete='off' />
                    </div>
                    <ul class='dd-options'>
                        {optionsHtml}
                    </ul>
                    <li class='dd-no-results' style='display:none;'>No matches found</li>
                </div>
                <input type='hidden' id='{fieldId}_value' name='{name}' value='{value}' />
            </div>";
            }

            var firstDropdownHtml = ShowFirstDropdown
                ? BuildToolbarDropdown($"{Id}_firstDropdown", FirstDropdownName, FirstDropdownPlaceholder, FirstDropdownValue, FirstDropdownItems)
                : "";

            var secondDropdownHtml = ShowSecondDropdown
                ? BuildToolbarDropdown($"{Id}_secondDropdown", SecondDropdownName, SecondDropdownPlaceholder, SecondDropdownValue, SecondDropdownItems)
                : "";

            // ---- Toolbar search-input block: single modern-input-with-search instance ----
            // Reproduces modern-input-with-search's own markup inline, same rationale as above.
            // Keeps the "mis-" class prefix and fires the same "mi:search" event on its input,
            // so page-level $("#id").on("mi:search", ...) handlers work whether the control is
            // standalone or embedded in this toolbar.
            var searchInputFieldId = string.IsNullOrWhiteSpace(SearchInputId) ? $"{Id}_searchInput" : SearchInputId;
            var normalizedSearchInputType = (SearchInputType ?? "text").ToLower();

            string searchInputModeAttr;
            switch (normalizedSearchInputType)
            {
                case "int":
                case "number":
                    searchInputModeAttr = "inputmode='numeric' pattern='[0-9]*'";
                    break;
                case "phone":
                    searchInputModeAttr = "inputmode='tel'";
                    break;
                case "currency":
                    searchInputModeAttr = "inputmode='decimal'";
                    break;
                default:
                    searchInputModeAttr = "";
                    break;
            }

            var searchInputTooltipAttr = (SearchInputMandatory && !string.IsNullOrWhiteSpace(SearchInputTooltipText))
                ? $"data-tooltip='{SearchInputTooltipText}'"
                : "";

            var searchInputLabelHtml = !string.IsNullOrWhiteSpace(SearchInputLabel) ? $@"
                <label class='mi-label' for='{searchInputFieldId}'>
                    {SearchInputLabel}{(SearchInputMandatory ? " <span class='mi-required'>*</span>" : "")}
                </label>" : "";

            var searchInputReadOnlyAttr = SearchInputReadOnly ? "readonly" : "";

            var searchInputHtml = ShowSearchInput ? $@"
            <div class='modern-input-wrapper mt-toolbar-mis'>
                {searchInputLabelHtml}
                <div class='mis-row'>
                    <div class='mi-input-wrap' data-input-type='{normalizedSearchInputType}' data-mandatory='{SearchInputMandatory.ToString().ToLower()}'>
                        <span class='mi-icon-left'>
                            <svg width='15' height='15' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                                <path d='M11.5 2.5L13.5 4.5L5 13H3V11L11.5 2.5Z' stroke='currentColor' stroke-width='1.3' stroke-linejoin='round'/>
                            </svg>
                        </span>
                        <input type='text' id='{searchInputFieldId}' name='{SearchInputName}' class='mi-input'
                               placeholder='{SearchInputPlaceholder}' value='{SearchInputValue}' {searchInputModeAttr} {searchInputReadOnlyAttr} />
                        <span class='mi-icon-error' style='display:none;' {searchInputTooltipAttr}>
                            <svg width='12' height='12' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                                <circle cx='8' cy='8' r='8' fill='currentColor'/>
                                <rect x='7.25' y='4' width='1.5' height='5' rx='0.75' fill='white'/>
                                <circle cx='8' cy='11' r='0.9' fill='white'/>
                            </svg>
                        </span>
                    </div>
                    <button type='button' class='mis-search-btn' data-target='{searchInputFieldId}' aria-label='Search'>
                        <svg width='18' height='18' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                            <circle cx='7' cy='7' r='5.5' stroke='white' stroke-width='1.8'/>
                            <path d='M11 11L14.5 14.5' stroke='white' stroke-width='1.8' stroke-linecap='round'/>
                        </svg>
                    </button>
                </div>
            </div>" : "";

            // Round, icon-only search button (no text) — background/icon color/size all
            // come from user-supplied --mt-daterange-search-* CSS vars set on the wrapper.
            // Shared across the date-range and dropdown groups; harmless if both are shown
            // at once (caller is responsible for choosing one combination in practice).
            var searchButtonHtml = ShowSearchButton ? $@"
            <button type='button' class='mt-daterange-search-btn' aria-label='Search' title='Search'>
                <svg width='16' height='16' viewBox='0 0 24 24' fill='none' xmlns='http://www.w3.org/2000/svg'>
                    <circle cx='11' cy='11' r='7' stroke='currentColor' stroke-width='2'/>
                    <line x1='21' y1='21' x2='16.65' y2='16.65' stroke='currentColor' stroke-width='2' stroke-linecap='round'/>
                </svg>
            </button>" : "";

            var dateRangeHtml = (ShowFirstDatePicker || ShowSecondDatePicker || ShowFirstDropdown || ShowSecondDropdown || ShowSearchButton) ? $@"
        <div class='mt-daterange-wrap'>
            {firstDateHtml}
            {secondDateHtml}
            {firstDropdownHtml}
            {secondDropdownHtml}
            {searchButtonHtml}
        </div>" : "";

            // ---- Mobile "Options" trigger + popup sheet ----
            // On narrow screens (see modern-table.css's .mt-mobile-mode rules,
            // switched on at or below this table's own MobileBreakpoint), the Filters
            // button, Search box, date-range group (From/To date pickers, toolbar
            // dropdowns and their search button), and Columns toggle are all hidden
            // from their normal toolbar spot and a single hamburger-icon "Options"
            // button takes their place instead. Tapping it opens .mt-mobile-sheet,
            // and modern-table.js MOVES (not clones) those existing elements into
            // the sheet body — so every handler already wired to them (search
            // input, filter toggle, column checkboxes, datepickers, dropdowns)
            // keeps working untouched, with nothing duplicated. On a resize back
            // to desktop width, the same elements are moved back to their original
            // spot in .mt-toolbar-left / next to it.
            var hasMobileOptions = renderFilterButton || Searchable || ColumnToggle
                || ShowFirstDatePicker || ShowSecondDatePicker || ShowFirstDropdown || ShowSecondDropdown || ShowSearchButton;
            var mobileOptionsHtml = hasMobileOptions ? @"
        <button type='button' class='mt-mobile-options-btn' aria-haspopup='true' aria-expanded='false' title='Search, filter and column options'>
            <svg width='18' height='18' viewBox='0 0 18 18' fill='none' xmlns='http://www.w3.org/2000/svg'>
                <path d='M2.5 5h13M2.5 9h13M2.5 13h13' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/>
            </svg>
        </button>
        <div class='mt-mobile-sheet-backdrop'></div>
        <div class='mt-mobile-sheet' role='dialog' aria-modal='true' aria-label='Table options'>
            <div class='mt-mobile-sheet-header'>
                <span class='mt-mobile-sheet-title'>Table options</span>
                <button type='button' class='mt-mobile-sheet-close' aria-label='Close'>
                    <svg width='16' height='16' viewBox='0 0 16 16' fill='none' xmlns='http://www.w3.org/2000/svg'>
                        <path d='M3.5 3.5l9 9M12.5 3.5l-9 9' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/>
                    </svg>
                </button>
            </div>
            <div class='mt-mobile-sheet-body'></div>
        </div>" : "";

            var toolbarHtml = (renderFilterButton || Searchable || ColumnToggle || !string.IsNullOrWhiteSpace(GridName) || ShowFirstDatePicker || ShowSecondDatePicker || ShowFirstDropdown || ShowSecondDropdown || ShowSearchButton || ShowSearchInput) ? $@"
        <div class='mt-toolbar'>
            <div class='mt-toolbar-left'>
                {filterButtonHtml}
                {searchBoxHtml}
                {titleHtml}
                {dateRangeHtml}
                {searchInputHtml}
            </div>
            {columnToggleHtml}
            {mobileOptionsHtml}
        </div>" : "";

            output.Content.SetHtmlContent($@"
        {toolbarHtml}
        <div class='mt-scroll'>
            <table class='mt-table'>
                <thead class='mt-thead'></thead>
                <tbody class='mt-tbody'></tbody>
            </table>
            <div class='mt-empty' style='display:none;'>No records found</div>
        </div>
        <div class='mt-footer'>
            <div class='mt-info'></div>
            <div class='mt-pagination'></div>
        </div>
        <input type='hidden' id='{Id}_selected' name='{Name}' value='' />
        <script type='application/json' class='mt-columns-data'>{columnsJson}</script>
        <script type='application/json' class='mt-items-data'>{itemsJson}</script>
    ");
        }
    }

    [HtmlTargetElement("column", ParentTag = "modern-table")]
    [RestrictChildren("badge")]
    public class ModernTableColumn : TagHelper
    {
        public string Field { get; set; }
        public string Header { get; set; }
        public bool Sortable { get; set; } = true;
        public bool Filterable { get; set; } = true;
        public bool Visible { get; set; } = true;
        public string Width { get; set; }
        public string Type { get; set; } = "text";
        public string ImageSize { get; set; }
        public string HeaderAlign { get; set; }
        public string CellAlign { get; set; }
        public string Format { get; set; }
        public string BadgeDefaultColor { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var columns = (List<TableColumn>)context.Items[typeof(ModernTable)];

            var badgeColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            context.Items[typeof(ModernTableColumn)] = badgeColors;

            await output.GetChildContentAsync();

            columns.Add(new TableColumn
            {
                Field = Field,
                Header = Header ?? Field,
                Sortable = Sortable,
                Filterable = Filterable,
                Visible = Visible,
                Width = Width,
                Type = Type,
                ImageSize = ImageSize,
                HeaderAlign = HeaderAlign,
                CellAlign = CellAlign,
                Format = Format,
                BadgeColors = badgeColors.Count > 0 ? badgeColors : null,
                BadgeDefaultColor = BadgeDefaultColor
            });

            output.SuppressOutput();
        }
    }

    [HtmlTargetElement("badge", ParentTag = "column")]
    public class ModernTableBadge : TagHelper
    {
        public string Value { get; set; }
        public string Color { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (context.Items.TryGetValue(typeof(ModernTableColumn), out var raw)
                && raw is Dictionary<string, string> badgeColors
                && !string.IsNullOrWhiteSpace(Value))
            {
                badgeColors[Value] = Color;
            }

            output.SuppressOutput();
        }
    }
}