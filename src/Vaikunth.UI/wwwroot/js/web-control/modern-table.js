var CURRENCY_CODE = "INR";   // Vaikunth: Indian rupees (ISO code)

function padZeros(value, length) {
    var s = String(value);
    while (s.length < length) s = "0" + s;
    return s;
}

function formatNumericValue(value, format) {
    var cur = /^(.*)[cC](\d*)$/.exec(format);
    if (cur) {
        if (value === "" || isNaN(Number(value))) return null;
        var curNum = Number(value);
        var curDigits = cur[2] === "" ? 2 : parseInt(cur[2], 10);
        var symbol = cur[1];

        if (symbol === "") {
            return curNum.toLocaleString(undefined, { style: "currency", currency: CURRENCY_CODE, minimumFractionDigits: curDigits, maximumFractionDigits: curDigits });
        }
        var curSign = curNum < 0 ? "-" : "";
        var curBody = Math.abs(curNum).toLocaleString(symbol.indexOf("₹") >= 0 ? "en-IN" : undefined, {   // Vaikunth: ₹1,49,500
            minimumFractionDigits: curDigits, maximumFractionDigits: curDigits });
        return curSign + symbol + curBody;
    }

    var m = /^([nNpPfFdD])(\d*)$/.exec(format);
    if (!m || value === "" || isNaN(Number(value))) return null;

    var num = Number(value);
    var kind = m[1].toLowerCase();
    var hasDigits = m[2] !== "";
    var digits = hasDigits ? parseInt(m[2], 10) : 2;

    switch (kind) {
        case "n":
            return num.toLocaleString(undefined, { minimumFractionDigits: digits, maximumFractionDigits: digits });
        case "p":
            return num.toLocaleString(undefined, { style: "percent", minimumFractionDigits: digits, maximumFractionDigits: digits });
        case "f":
            return num.toFixed(digits);
        case "d":
            var intVal = Math.trunc(num);
            var sign = intVal < 0 ? "-" : "";
            return sign + padZeros(Math.abs(intVal), hasDigits ? digits : 1);
        default:
            return null;
    }
}

function parseRawDate(value) {
    if (value instanceof Date) return value;
    if (typeof value === "number") return new Date(value);
    if (typeof value !== "string") return new Date(value);

    var s = value.trim();

    var m = /^(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{4})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?$/.exec(s);
    if (m) {
        var a = parseInt(m[1], 10), b = parseInt(m[2], 10), year = parseInt(m[3], 10);
        var day, month;
        if (a > 12) { day = a; month = b; }
        else if (b > 12) { day = b; month = a; }
        else { day = a; month = b; }
        var hh = m[4] ? parseInt(m[4], 10) : 0;
        var mi = m[5] ? parseInt(m[5], 10) : 0;
        var ss = m[6] ? parseInt(m[6], 10) : 0;
        return new Date(year, month - 1, day, hh, mi, ss);
    }

    var iso = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
    if (iso) {
        return new Date(parseInt(iso[1], 10), parseInt(iso[2], 10) - 1, parseInt(iso[3], 10));
    }

    return new Date(s);
}

function formatDateValue(value, format) {
    var date = parseRawDate(value);
    if (isNaN(date.getTime())) return null;

    var year = date.getFullYear();
    var month = date.getMonth() + 1;
    var day = date.getDate();

    return format.replace(/yyyy|yy|mm|dd|m|d/gi, function (token) {
        switch (token.toLowerCase()) {
            case "yyyy": return String(year);
            case "yy": return padZeros(year % 100, 2);
            case "mm": return padZeros(month, 2);
            case "dd": return padZeros(day, 2);
            case "m": return String(month);
            case "d": return String(day);
            default: return token;
        }
    });
}

function formatCellValue(value, format) {
    if (value === null || value === undefined || value === "") return "";
    if (!format) return value;

    var numeric = formatNumericValue(value, format);
    if (numeric !== null) return numeric;

    var dated = formatDateValue(value, format);
    if (dated !== null) return dated;

    return value;
}

function getReadableTextColor(bgColor) {
    var rgb = colorToRgb(bgColor);
    if (!rgb) return "#111827";

    var brightness = (rgb.r * 299 + rgb.g * 587 + rgb.b * 114) / 1000;
    return brightness > 150 ? "#111827" : "#ffffff";
}

function colorToRgb(color) {
    var probe = document.createElement("span");
    probe.style.color = color;
    document.body.appendChild(probe);
    var computed = getComputedStyle(probe).color;
    document.body.removeChild(probe);

    var m = /^rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(computed);
    if (!m) return null;
    return { r: parseInt(m[1], 10), g: parseInt(m[2], 10), b: parseInt(m[3], 10) };
}

function resolveBadgeColor(col, val) {
    if (col.badgeColors) {
        var matchKey = Object.keys(col.badgeColors).find(function (k) {
            return k.toLowerCase() === String(val).toLowerCase();
        });
        if (matchKey) return col.badgeColors[matchKey];
    }
    return col.badgeDefaultColor || null;
}

// ---------------------------------------------------------------------
// window.ModernTable.init(context)
//
// Initializes every ".modern-table-wrapper" found within `context`
// (defaults to the whole document). Safe to call multiple times:
// already-initialized wrappers are skipped via the "mt-inited" data
// guard, so re-running this after a partial view is injected (AJAX,
// tab switch, menu load, etc.) will only initialize the NEW table(s)
// that weren't present when $(document).ready() first ran.
//
// Call this explicitly after injecting any HTML that contains a
// <modern-table>, e.g.:
//
//   $("#content").load("/Designation/DesignationPartial", function () {
//       window.ModernTable.init();
//   });
// ---------------------------------------------------------------------
window.ModernTable = window.ModernTable || {};

window.ModernTable.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $wrappers = ($scope.hasClass && $scope.hasClass("modern-table-wrapper"))
        ? $scope
        : $scope.find(".modern-table-wrapper");

    $wrappers.each(function () {
        var $wrapper = $(this);
        if ($wrapper.data("mt-inited")) return; // already initialized, skip
        $wrapper.data("mt-inited", true);
        initTable($wrapper);
    });
};

$(document).on("click", function (e) {
    if (!$(e.target).closest(".mt-col-toggle-wrap").length) {
        $(".mt-col-toggle-wrap.open").removeClass("open");
    }
    // Toolbar dropdowns (mirrors modern-dropdown's own document-level close-on-outside-click)
    if (!$(e.target).closest(".mt-toolbar-dd").length) {
        $(".mt-toolbar-dd.dd-open").removeClass("dd-open");
    }
});

$(document).ready(function () {
    window.ModernTable.init();
});

// ---- type="image" cell with no picture: picture icon + "No image", like the image box ----
// Sized to the column's image-size (default 40px); the styles are added to the page once.
function buildNoImage(imageSize) {
    if (!document.getElementById("mt-noimage-style")) {
        $("<style id='mt-noimage-style'>" +
            ".mt-noimage{display:inline-flex;flex-direction:column;align-items:center;justify-content:center;gap:1px;" +
            "box-sizing:border-box;color:#8a8f98;background:#f4f5f7;border-radius:6px;vertical-align:middle;" +
            "line-height:1;overflow:hidden;font-weight:600;white-space:nowrap;}" +
            ".mt-noimage svg{display:block;flex:0 0 auto;}" +
            "</style>").appendTo("head");
    }
    var px = parseFloat(imageSize) || 40;
    var icon = Math.round(px * 0.5);
    var font = Math.max(7, Math.round(px * 0.2));
    return $(
        "<div class='mt-row-image mt-row-image-placeholder mt-noimage' title='No image'>" +
        "<svg width='" + icon + "' height='" + icon + "' viewBox='0 0 48 48' fill='none' xmlns='http://www.w3.org/2000/svg'>" +
        "<rect x='4' y='8' width='40' height='32' rx='1' stroke='currentColor' stroke-width='2.6'/>" +
        "<circle cx='15' cy='18' r='3.2' fill='currentColor'/>" +
        "<path d='M6 34L18 22L26 30L34 20L44 32' stroke='currentColor' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/>" +
        "</svg>" +
        "<span style='font-size:" + font + "px'>No image</span>" +
        "</div>"
    ).css({ width: px + "px", height: px + "px", minWidth: px + "px" });
}

function initTable($wrapper) {
    var pageSize = parseInt($wrapper.attr("data-page-size")) || 10;
    var selectable = $wrapper.attr("data-selectable") === "true";
    var enablePaging = $wrapper.attr("data-enable-paging") !== "false";
    var rowIdField = $wrapper.attr("data-row-id-field") || "id";

    function parseJsonScript($el) {
        var raw = $el.html() || $el.text() || "";
        if (!raw.trim()) return [];
        try {
            return JSON.parse(raw);
        } catch (e) {
            return [];
        }
    }

    var columns = parseJsonScript($wrapper.find(".mt-columns-data"));
    var allRows = parseJsonScript($wrapper.find(".mt-items-data"));

    var $thead = $wrapper.find(".mt-thead");
    var $tbody = $wrapper.find(".mt-tbody");
    var $empty = $wrapper.find(".mt-empty");
    var $scrollEl = $wrapper.find(".mt-scroll");
    var $search = $wrapper.find(".mt-search");
    var $info = $wrapper.find(".mt-info");
    var $pagination = $wrapper.find(".mt-pagination");
    // Direct child only: the selected-row hidden input sits at the wrapper's top level.
    // (A plain .find("input[type=hidden]") would also match the toolbar dropdowns'
    // hidden inputs and overwrite their values whenever a row is selected.)
    var $hiddenInput = $wrapper.children("input[type=hidden]");

    var $colToggleWrap = $wrapper.find(".mt-col-toggle-wrap");
    var $colToggleBtn = $wrapper.find(".mt-col-toggle-btn");
    var $colPanel = $wrapper.find(".mt-col-list"); // inner scrollable list inside .mt-col-panel

    // ---- Toolbar date-range: first picker / second picker / search button (optional) ----
    // Rendered by ModernTable only when ShowFirstDatePicker / ShowSecondDatePicker /
    // ShowSearchButton are true. Clicking Search fires "table:daterange-search" on the
    // wrapper with { firstDate, secondDate, firstDropdown, secondDropdown } so page-level
    // code can filter/re-fetch rows using whichever toolbar controls are actually present.
    var $dateRangeWrap = $wrapper.find(".mt-daterange-wrap");
    var $firstDateInput = $dateRangeWrap.find(".mt-daterange-field").eq(0).find("input");
    var $secondDateInput = $dateRangeWrap.find(".mt-daterange-field").eq(1).find("input");
    var $daterangeSearchBtn = $wrapper.find(".mt-daterange-search-btn");

    $daterangeSearchBtn.off("click").on("click", function () {
        var $dropdowns = $wrapper.find(".mt-toolbar-dd");
        var payload = {
            firstDate: $firstDateInput.length ? $firstDateInput.val() : null,
            secondDate: $secondDateInput.length ? $secondDateInput.val() : null,
            firstDropdown: $dropdowns.eq(0).length ? $dropdowns.eq(0).find("input[type=hidden]").val() : null,
            secondDropdown: $dropdowns.eq(1).length ? $dropdowns.eq(1).find("input[type=hidden]").val() : null
        };
        $wrapper.trigger("table:daterange-search", [payload]);
    });

    // ---- Toolbar dropdowns (optional: ShowFirstDropdown / ShowSecondDropdown) ----
    // Mirrors modern-dropdown's own client behavior (toggle open/close, type-to-filter
    // the option list, click-to-select updates the visible label + hidden input and
    // fires a change event on that hidden input so page code can react to it).
    // Wiring is extracted into wireDropdown() so it can be re-run after setDropdownItems()
    // replaces a dropdown's <li> options at runtime (e.g. Branch change -> reload Status).
    function wireDropdown($dd) {
        var $toggleBtn = $dd.find(".dd-toggle");
        var $selectedLabel = $dd.find(".dd-selected");
        var $ddSearch = $dd.find(".dd-search");
        var $noResults = $dd.find(".dd-no-results");
        var $ddHidden = $dd.find("input[type=hidden]");

        $toggleBtn.off("click").on("click", function (e) {
            e.stopPropagation();
            var willOpen = !$dd.hasClass("dd-open");
            $wrapper.find(".mt-toolbar-dd").not($dd).removeClass("dd-open");
            $dd.toggleClass("dd-open", willOpen);
            if (willOpen) {
                $ddSearch.val("").trigger("input");
                $ddSearch.focus();
            }
        });

        $ddSearch.off("input").on("input", function () {
            var term = $(this).val().trim().toLowerCase();
            var anyVisible = false;
            $dd.find(".dd-option").each(function () {
                var match = $(this).text().trim().toLowerCase().indexOf(term) > -1;
                $(this).toggle(match);
                if (match) anyVisible = true;
            });
            $noResults.toggle(!anyVisible);
        });

        // Delegated so options added later by setDropdownItems() are clickable
        // without re-binding: no .off()/.on() needed per option element.
        $dd.find(".dd-options").off("click", ".dd-option").on("click", ".dd-option", function () {
            var val = $(this).attr("data-value");
            var text = $(this).text().trim();
            $dd.find(".dd-option").removeClass("dd-option-selected");
            $(this).addClass("dd-option-selected");
            $selectedLabel.text(text);
            $ddHidden.val(val).trigger("change");
            $dd.removeClass("dd-open");
        });
    }

    $wrapper.find(".mt-toolbar-dd").each(function () {
        wireDropdown($(this));
    });

    // Rebuilds one toolbar dropdown's option list and resets its selection/search state.
    // which: "first" | "second". items: array of { value, text } (or { Value, Text }).
    // Returns true if the target dropdown exists, false otherwise (e.g. ShowFirstDropdown
    // was false so it was never rendered).
    function setDropdownItemsInternal(which, items) {
        var $dd = which === "second"
            ? $wrapper.find(".mt-toolbar-dd").eq(1)
            : $wrapper.find(".mt-toolbar-dd").eq(0);
        if (!$dd.length) return false;

        var $options = $dd.find(".dd-options");
        var $selectedLabel = $dd.find(".dd-selected");
        var $ddHidden = $dd.find("input[type=hidden]");
        var $ddSearch = $dd.find(".dd-search");
        var $noResults = $dd.find(".dd-no-results");

        var placeholder = $ddHidden.attr("data-placeholder") || $selectedLabel.attr("data-placeholder") || $selectedLabel.text();
        // First call: remember the original placeholder text so repeated refreshes
        // (e.g. clearing back to "Select") don't lose it after the first selection.
        if (!$selectedLabel.attr("data-placeholder")) {
            $selectedLabel.attr("data-placeholder", $selectedLabel.text());
            placeholder = $selectedLabel.text();
        }

        $options.empty();
        (items || []).forEach(function (item) {
            var val = item.value !== undefined ? item.value : item.Value;
            var text = item.text !== undefined ? item.text : item.Text;
            $("<li>").addClass("dd-option").attr("data-value", val).text(text).appendTo($options);
        });

        $selectedLabel.text(placeholder);
        $ddHidden.val("").trigger("change");
        $ddSearch.val("");
        $noResults.hide();
        $dd.find(".dd-option").show();

        return true;
    }

    // ---- Toolbar search input (optional: ShowSearchInput) ----
    // Mirrors modern-input-with-search's own behavior: clicking the attached round
    // button fires "mi:search" on the text input, passing its current value, so
    // existing $("#id").on("mi:search", function(e, value) {...}) handlers work the
    // same whether the control is standalone or embedded in this toolbar.
    $wrapper.find(".mt-toolbar-mis .mis-search-btn").off("click").on("click", function () {
        var targetId = $(this).attr("data-target");
        var $targetInput = $("#" + targetId);
        $targetInput.trigger("mi:search", [$targetInput.val()]);
    });

    var columnVisibility = {};
    columns.forEach(function (col) {
        var isVis = col.visible;
        columnVisibility[col.field] = !(isVis === false || isVis === "false" || isVis === "False");
    });

    var rowClassCallback = null;
    var isAnimatedScrolling = $wrapper.hasClass("mt-animated-scroll");

    function animateScrollToTop(el, duration) {
        var start = el.scrollTop;
        if (start === 0) return;
        var startTime = null;

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var progress = Math.min((timestamp - startTime) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            el.scrollTop = start * (1 - eased);
            if (progress < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }

    var state = {
        searchTerm: "",
        columnFilters: {},
        sortField: null,
        sortDir: "asc",
        currentPage: 1,
        selectedId: null
    };

    // ---- "Filters" toggle button (ShowFilterButton) ----
    // Toggles .mt-filters-open on the wrapper, which shows/hides the per-column
    // filter row via CSS. Collapsing the row does NOT clear typed filters — they
    // stay applied, and the count pill on the button shows how many are active.
    // The server already skips rendering the button when no column is filterable;
    // the anyFilterableCol check below is just a client-side fallback.
    var $filterToggleBtn = $wrapper.find(".mt-filter-toggle-btn");
    var $filterCount = $filterToggleBtn.find(".mt-filter-count");

    function setFiltersOpen(open, focusFirst) {
        $wrapper.toggleClass("mt-filters-open", !!open);
        $filterToggleBtn.attr("aria-expanded", open ? "true" : "false");
        if (open && focusFirst) {
            setTimeout(function () {
                $wrapper.find(".mt-filter-row .mt-filter-input:visible").first().trigger("focus");
            }, 0);
        }
    }

    function updateFilterCount() {
        var n = Object.keys(state.columnFilters).length;
        $filterCount.text(n).toggle(n > 0);
        $filterToggleBtn.toggleClass("mt-filter-active", n > 0);
    }

    if ($filterToggleBtn.length) {
        var anyFilterableCol = columns.some(function (col) { return col.filterable !== false; });
        if (!anyFilterableCol) {
            $filterToggleBtn.hide(); // nothing to filter; don't show a dead button
        } else {
            setFiltersOpen($wrapper.attr("data-filters-open") === "true", false);
            $filterToggleBtn.off("click").on("click", function (e) {
                e.stopPropagation();
                setFiltersOpen(!$wrapper.hasClass("mt-filters-open"), true);
            });
        }
    }

    renderHeader();
    renderColumnPanel();
    render();

    // ---- Mobile "Options" popup sheet (Filters + Search + Columns) ----
    // Below the CSS breakpoint (see modern-table.css's .mt-mobile-options-btn /
    // .mt-mobile-mode rules, switched on at or below this table's own
    // MobileBreakpoint — see setupMobileOptionsSheet() below), the Filters button, the search
    // box, and the Columns toggle are pulled OUT of their normal toolbar spot and
    // MOVED (not cloned) into a slide-up sheet, reachable via one hamburger-style
    // "Options" button. This keeps every existing handler (search input, filter
    // toggle open/close, column visibility checkboxes) wired to the SAME DOM
    // elements — nothing is duplicated or re-bound, so behavior stays identical
    // whether a control lives in the toolbar or inside the sheet.
    //
    // On resize back above the breakpoint, the same elements are moved back to
    // their original position (tracked via a placeholder comment-free approach:
    // each moved element's original next-sibling is remembered so it can be
    // re-inserted in the exact same spot).
    (function setupMobileOptionsSheet() {
        var $optionsBtn = $wrapper.find(".mt-mobile-options-btn");
        var $sheet = $wrapper.find(".mt-mobile-sheet");
        var $sheetBody = $sheet.find(".mt-mobile-sheet-body");
        var $backdrop = $wrapper.find(".mt-mobile-sheet-backdrop");
        var $closeBtn = $sheet.find(".mt-mobile-sheet-close");

        if (!$optionsBtn.length) return; // nothing to collapse (no filter/search/columns at all)

        // ---- Escape any transformed/filtered ancestor ("portal" to <body>) ----
        // .mt-mobile-sheet / .mt-mobile-sheet-backdrop use position:fixed so they
        // pin to the VIEWPORT edge regardless of where the table sits on the page.
        // But CSS position:fixed is only fixed relative to the viewport as long as
        // every ancestor has the default (untransformed) rendering context — any
        // ancestor with a `transform`, `filter`, `perspective`, or certain
        // `will-change` values becomes the fixed-position containing block INSTEAD
        // of the viewport (this is standard CSS spec behavior, not a bug in this
        // component). A page layout wrapper, an animated container, a modal host,
        // or any card/panel using a transform for its own effects will silently
        // break "stick to the bottom of the screen" and the sheet ends up
        // positioned relative to that ancestor's box instead — showing up
        // mid-page, cut off, or requiring a scroll to see, rather than sliding up
        // from the true bottom of the screen.
        //
        // The reliable fix used by virtually every modal/sheet library (Bootstrap,
        // MUI, Radix, etc.) is to move the sheet + backdrop to be DIRECT CHILDREN
        // OF <body> the first time they're needed, so no ancestor of
        // .modern-table-wrapper can ever trap them. This is a one-time relocation
        // in the DOM, not a copy — event handlers bound below keep working
        // normally since jQuery selections stay live regardless of DOM location.
        if ($sheet.parent().is(document.body) === false) {
            // ---- Carry over the wrapper's CSS custom properties ----
            // ModernTable.cs writes all of this table's per-instance CSS
            // variables (--mt-daterange-radius, --mt-daterange-search-bg,
            // --mt-toolbar-dd-radius, etc.) as an inline style="..." on
            // .modern-table-wrapper itself, and modern-table.css reads them
            // with var(--mt-whatever, someDefault). That works fine for
            // controls still inside .modern-table-wrapper, but Filters/
            // Search/date-range/Columns get MOVED into this sheet, and the
            // sheet is portaled to be a direct child of <body> (see the
            // comment above) — outside .modern-table-wrapper entirely. Once
            // outside it, those var() lookups no longer find the variables
            // and silently fall back to their CSS defaults (e.g. the date
            // pickers losing their rounded corners because
            // --mt-daterange-radius stops resolving and border-radius
            // falls back to its 0 default). Copying the wrapper's inline
            // style onto the sheet itself keeps every one of those
            // variables resolving correctly regardless of where in the DOM
            // the sheet ends up.
            var wrapperInlineStyle = $wrapper.attr("style");
            if (wrapperInlineStyle) {
                var existingSheetStyle = $sheet.attr("style") || "";
                $sheet.attr("style", existingSheetStyle + (existingSheetStyle && existingSheetStyle.slice(-1) !== ";" ? ";" : "") + wrapperInlineStyle);
            }

            $backdrop.appendTo(document.body);
            $sheet.appendTo(document.body);
        }

        // ---- Clean up the portal'd sheet when the table itself is torn down ----
        // This app loads pages/tabs via AJAX and swaps them in with plain
        // $(container).html(newHtml) (see global.js's loadIntoMainPanel /
        // tabpanel:loaded handling). That call tears down and garbage-collects
        // everything INSIDE `container` — but .mt-mobile-sheet and its backdrop
        // were just moved OUT to be direct children of <body>, so a plain
        // .html() swap of the table's container would silently orphan them:
        // invisible (display:none via the base CSS rule) but still sitting in
        // <body>, permanently, and a duplicate would pile up next to it every
        // time the user navigates to another page containing a mobile table.
        //
        // MutationObserver on the wrapper's parent chain is the standard way to
        // detect "this element was removed from the document" from outside a
        // framework lifecycle. When it fires, remove the portal'd elements too.
        if (window.MutationObserver) {
            var wrapperEl = $wrapper[0];
            var bodyObserver = new MutationObserver(function () {
                if (!document.body.contains(wrapperEl)) {
                    $sheet.remove();
                    $backdrop.remove();
                    bodyObserver.disconnect();
                }
            });
            bodyObserver.observe(document.body, { childList: true, subtree: true });
        }

        // Elements that move into the sheet on mobile. Each entry remembers
        // where it came from so it can be moved back to the exact same spot.
        //
        // IMPORTANT: a plain "remember my previous sibling, re-insert after
        // it" approach breaks here, because two of these three elements are
        // ADJACENT to each other in the original toolbar (Filters is
        // immediately followed by Search). If Search's remembered "previous
        // sibling" is the Filters button — itself one of the elements being
        // moved — then whichever order moveBackToToolbar() processes them in,
        // one element's anchor is still sitting inside the sheet (not yet
        // moved back) at the moment we try to re-insert relative to it. That
        // silently produces the wrong position (this is exactly what caused
        // the search box to go missing from the toolbar after closing the
        // sheet on a desktop-width resize).
        //
        // A single shared marker doesn't work either: .mt-filter-toggle-btn
        // and .mt-search-wrap live INSIDE .mt-toolbar-left, while
        // .mt-col-toggle-wrap is a SIBLING of .mt-toolbar-left (a direct
        // child of .mt-toolbar). Anchoring one marker after the last movable
        // element collapses everyone to that single container, which moves
        // Filters/Search out of .mt-toolbar-left into .mt-toolbar.
        //
        // Instead, each movable element gets its OWN stable, never-moved
        // marker <span> inserted right after its own original position, once,
        // at setup time. Restoring is then always "put this element back
        // immediately before its own marker" — every anchor is a plain,
        // never-relocated <span>, never another movable element and never a
        // marker shared across containers, so both the ordering bug and the
        // wrong-container bug are ruled out.
        var $filterEl = $wrapper.find(".mt-filter-toggle-btn");
        var $searchEl = $wrapper.find(".mt-search-wrap");
        var $dateRangeEl = $wrapper.find(".mt-daterange-wrap");
        var $colEl = $wrapper.find(".mt-col-toggle-wrap");

        var movable = [
            { $el: $filterEl },
            { $el: $searchEl },
            { $el: $dateRangeEl },
            { $el: $colEl }
        ].filter(function (m) { return m.$el.length > 0; });

        movable.forEach(function (m) {
            var $marker = $("<span>", { "class": "mt-mobile-sheet-restore-marker", "aria-hidden": "true" })
                .css({ display: "none" });
            m.$el.after($marker);
            m.$marker = $marker;
        });

        var inSheet = false;

        function moveIntoSheet() {
            if (inSheet || !movable.length) return;
            movable.forEach(function (m) {
                $sheetBody.append(m.$el);
            });
            inSheet = true;
        }

        function moveBackToToolbar() {
            if (!inSheet || !movable.length) return;
            // Insert each element back immediately before its OWN marker,
            // which never moved from its original position — so this is
            // immune both to other movable elements not being restored yet,
            // and to different movable elements living in different parent
            // containers.
            movable.forEach(function (m) {
                m.$marker.before(m.$el);
            });
            inSheet = false;
        }

        function openSheet() {
            moveIntoSheet();
            $sheet.addClass("mt-sheet-open");
            $backdrop.addClass("mt-sheet-open");
            $optionsBtn.attr("aria-expanded", "true");
            // Column panel/filter dropdowns etc. use position:absolute relative
            // to their own wrapper; inside the sheet they still anchor correctly
            // since .mt-col-toggle-wrap keeps its own position:relative.
        }

        function closeSheet() {
            $sheet.removeClass("mt-sheet-open");
            $backdrop.removeClass("mt-sheet-open");
            $optionsBtn.attr("aria-expanded", "false");
        }

        $optionsBtn.off("click").on("click", function (e) {
            e.stopPropagation();
            openSheet();
        });

        $closeBtn.off("click").on("click", function (e) {
            e.stopPropagation();
            closeSheet();
        });

        $backdrop.off("click").on("click", function () {
            closeSheet();
        });

        // ---- Mobile breakpoint (configurable per table via MobileBreakpoint) ----
        // modern-table.css has NO @media query for the mobile layout — CSS
        // can't read a variable inside a media-feature (no browser supports
        // @media (max-width: var(--x))), so a per-table breakpoint can only
        // work if the layout switch is driven from JS instead. Every mobile
        // rule in modern-table.css is written as ".mt-mobile-mode <selector>"
        // rather than inside @media, and this is the one place that decides
        // whether that class is present — driven by ModernTable.cs's
        // data-mobile-breakpoint attribute (default 950, same as the old
        // hardcoded value) rather than a fixed number.
        var mobileBreakpointPx = parseInt($wrapper.attr("data-mobile-breakpoint"), 10);
        if (!mobileBreakpointPx || mobileBreakpointPx <= 0) mobileBreakpointPx = 950;
        var mobileBreakpoint = window.matchMedia ? window.matchMedia("(max-width: " + mobileBreakpointPx + "px)") : null;

        // .mt-mobile-mode has to be present on whatever element each mobile
        // rule's selector is actually scoped under. Most rules target
        // descendants of the wrapper, but the sheet/backdrop (and anything
        // opened from inside them, like a flatpickr calendar) get portaled
        // to be direct children of <body> — outside the wrapper entirely —
        // the first time the sheet opens (see the portal code above). So the
        // class is set on $wrapper AND on $sheet/$backdrop together, whether
        // or not the portal has happened yet, keeping every ".mt-mobile-mode
        // ..." selector matching regardless of where in the DOM the sheet
        // currently lives.
        function setMobileMode(isMobile) {
            $wrapper.toggleClass("mt-mobile-mode", isMobile);
            $sheet.toggleClass("mt-mobile-mode", isMobile);
            $backdrop.toggleClass("mt-mobile-mode", isMobile);
        }

        function syncToViewport() {
            var isMobile = mobileBreakpoint ? mobileBreakpoint.matches : (window.innerWidth <= mobileBreakpointPx);
            setMobileMode(isMobile);
            if (isMobile) {
                moveIntoSheet();
            } else {
                closeSheet();
                moveBackToToolbar();
            }
        }

        syncToViewport();

        if (mobileBreakpoint && mobileBreakpoint.addEventListener) {
            mobileBreakpoint.addEventListener("change", syncToViewport);
        } else if (mobileBreakpoint && mobileBreakpoint.addListener) {
            mobileBreakpoint.addListener(syncToViewport); // older Safari
        } else {
            $(window).on("resize", syncToViewport);
        }
    })();

    function renderHeader() {
        var $tr = $("<tr>");
        columns.forEach(function (col) {
            var $th = $("<th>").text(col.header || col.field);
            if (col.width) $th.css("width", col.width);
            if (col.headerAlign) $th.css("text-align", col.headerAlign);

            if (!columnVisibility[col.field]) {
                $th.addClass("mt-col-hidden");
            }

            if (col.sortable !== false) {
                $th.addClass("mt-sortable").attr("data-field", col.field);
                $th.append(
                    "<span class='mt-sort-icon'>" +
                    "<svg width='10' height='10' viewBox='0 0 10 10' fill='none'>" +
                    "<path d='M5 1L8.5 6.5H1.5L5 1Z' fill='currentColor'/>" +
                    "</svg></span>"
                );
                $th.on("click", function () {
                    onSortClick(col.field);
                });
            }
            $tr.append($th);
        });
        $thead.empty().append($tr);

        var anyFilterable = columns.some(function (col) { return col.filterable !== false; });

        if (anyFilterable) {
            var $filterTr = $("<tr class='mt-filter-row'>");
            columns.forEach(function (col) {
                var $th = $("<th>");

                if (!columnVisibility[col.field]) {
                    $th.addClass("mt-col-hidden");
                }

                if (col.filterable !== false) {
                    var $input = $("<input type='text' class='mt-filter-input' autocomplete='off' />")
                        .attr("placeholder", "🔍 Filter " + (col.header || col.field) + "...")
                        .attr("data-field", col.field)
                        .on("input", function () {
                            var val = $(this).val().trim().toLowerCase();
                            if (val) {
                                state.columnFilters[col.field] = val;
                            } else {
                                delete state.columnFilters[col.field];
                            }
                            state.currentPage = 1;
                            updateFilterCount();
                            render();
                        });
                    $th.append($input);
                }
                $filterTr.append($th);
            });
            $thead.append($filterTr);
        }
    }

    function onSortClick(field) {
        if (state.sortField === field) {
            state.sortDir = state.sortDir === "asc" ? "desc" : "asc";
        } else {
            state.sortField = field;
            state.sortDir = "asc";
        }
        state.currentPage = 1;
        updateSortIndicators();
        render();
    }

    function updateSortIndicators() {
        $thead.find("th").removeClass("mt-sort-asc mt-sort-desc");
        if (state.sortField) {
            $thead.find("th[data-field='" + state.sortField + "']")
                .addClass(state.sortDir === "asc" ? "mt-sort-asc" : "mt-sort-desc");
        }
    }

    $search.off("input").on("input", function () {
        state.searchTerm = $(this).val().trim().toLowerCase();
        state.currentPage = 1;
        render();
    });

    function renderColumnPanel() {
        $colPanel.empty();
        columns.forEach(function (col) {
            var isVisible = columnVisibility[col.field];
            var checked = isVisible ? "checked" : "";
            var $item = $(
                "<label class='mt-col-item'>" +
                "<input type='checkbox' data-field='" + col.field + "' " + checked + " />" +
                "<span>" + (col.header || col.field) + "</span>" +
                "</label>"
            );
            $item.find("input").on("change", function () {
                var field = $(this).data("field");
                columnVisibility[field] = $(this).is(":checked");
                applyColumnVisibility();
            });
            $colPanel.append($item);
        });
    }

    function applyColumnVisibility() {
        columns.forEach(function (col, idx) {
            var hidden = !columnVisibility[col.field];
            $thead.find("tr").each(function () {
                $(this).find("th").eq(idx).toggleClass("mt-col-hidden", hidden);
            });
            $tbody.find("tr").each(function () {
                $(this).find("td").eq(idx).toggleClass("mt-col-hidden", hidden);
            });
        });
    }

    $colToggleBtn.off("click").on("click", function (e) {
        e.stopPropagation();
        $colToggleWrap.toggleClass("open");
    });

    // ---- Column chooser popup: drag-to-resize (bottom-right grip) ----
    // Lets the user make the popup taller/shorter than ColumnPanelMaxHeight when
    // they want to see more (or fewer) columns at once without scrolling. Purely
    // client-side and in-memory: the size resets to the server-rendered default
    // (ColumnPanelMaxHeight) on next page load, same as the rest of this table's
    // transient UI state (search text, sort, page).
    var $colPanelOuter = $wrapper.find(".mt-col-panel");
    var $colResizeHandle = $wrapper.find(".mt-col-resize-handle");
    var colPanelMinHeight = 90; // keep in sync with .mt-col-panel's CSS min-height

    if ($colResizeHandle.length) {
        var resizeState = null; // { startY, startHeight } while dragging, else null

        function onResizeMove(e) {
            if (!resizeState) return;
            var clientY = e.touches ? e.touches[0].clientY : e.clientY;
            var delta = clientY - resizeState.startY;
            var newHeight = Math.max(colPanelMinHeight, resizeState.startHeight + delta);
            $colPanelOuter[0].style.maxHeight = newHeight + "px";
            $colPanelOuter[0].style.height = newHeight + "px";
        }

        function onResizeEnd() {
            if (!resizeState) return;
            resizeState = null;
            $colPanelOuter.removeClass("mt-col-resizing");
            $(document).off("mousemove touchmove", onResizeMove);
            $(document).off("mouseup touchend", onResizeEnd);
        }

        $colResizeHandle.on("mousedown touchstart", function (e) {
            e.stopPropagation(); // don't let this bubble to the document click-outside-closes handler
            e.preventDefault();
            var clientY = e.touches ? e.touches[0].clientY : e.clientY;
            resizeState = {
                startY: clientY,
                startHeight: $colPanelOuter[0].getBoundingClientRect().height
            };
            $colPanelOuter.addClass("mt-col-resizing");
            $(document).on("mousemove touchmove", onResizeMove);
            $(document).on("mouseup touchend", onResizeEnd);
        });
    }

    function getFilteredSortedRows() {
        var rows = allRows;

        if (state.searchTerm) {
            rows = rows.filter(function (row) {
                return columns.some(function (col) {
                    var val = row[col.field];
                    return val != null && String(val).toLowerCase().indexOf(state.searchTerm) > -1;
                });
            });
        }

        var filterFields = Object.keys(state.columnFilters);
        if (filterFields.length > 0) {
            rows = rows.filter(function (row) {
                return filterFields.every(function (field) {
                    var val = row[field];
                    var term = state.columnFilters[field];
                    return val != null && String(val).toLowerCase().indexOf(term) > -1;
                });
            });
        }

        if (state.sortField) {
            rows = rows.slice().sort(function (a, b) {
                var va = a[state.sortField], vb = b[state.sortField];
                if (va == null) va = "";
                if (vb == null) vb = "";

                var cmp;
                if (!isNaN(va) && !isNaN(vb) && va !== "" && vb !== "") {
                    cmp = Number(va) - Number(vb);
                } else {
                    cmp = String(va).localeCompare(String(vb));
                }
                return state.sortDir === "asc" ? cmp : -cmp;
            });
        }

        return rows;
    }

    function render() {
        var filtered = getFilteredSortedRows();
        var totalRows = filtered.length;

        if (!enablePaging) {
            renderBody(filtered);
            renderInfo(totalRows, 0, filtered.length);
            $pagination.empty();
            return;
        }

        var totalPages = Math.max(1, Math.ceil(totalRows / pageSize));

        if (state.currentPage > totalPages) state.currentPage = totalPages;

        var start = (state.currentPage - 1) * pageSize;
        var pageRows = filtered.slice(start, start + pageSize);

        renderBody(pageRows);
        renderInfo(totalRows, start, pageRows.length);
        renderPagination(totalPages);
    }

    function renderBody(rows) {
        $tbody.empty();

        if (rows.length === 0) {
            $empty.show();

            var visibleColCount = columns.filter(function (col) {
                return columnVisibility[col.field];
            }).length;

            var $emptyTr = $("<tr class='mt-empty-row'>")
                .append($("<td>")
                    .attr("colspan", visibleColCount || 1)
                    .css({ "height": "0", "padding": "0", "border": "none" }));

            $tbody.append($emptyTr);
            applyColumnVisibility();
            return;
        }

        $empty.hide();

        rows.forEach(function (row) {
            $tbody.append(buildRowElement(row));
        });

        applyColumnVisibility();
    }

    // Builds the <tr> for one row (cells, classes, selected state, click-to-select).
    // Used by renderBody for every row on the page, and by updateRow to redraw a single row.
    function buildRowElement(row) {
        var $tr = $("<tr>").attr("data-row-id", row[rowIdField]);

        if (row.rowClass) {
            $tr.addClass(row.rowClass);
        }

        if (typeof rowClassCallback === "function") {
            var extraClass = rowClassCallback(row);
            if (extraClass) {
                $tr.addClass(extraClass);
            }
        }

        columns.forEach(function (col) {
            var val = row[col.field];
            var $td = $("<td>");
            if (col.cellAlign) $td.css("text-align", col.cellAlign);
            if (col.width) $td.css("width", col.width);

            if (!columnVisibility[col.field]) {
                $td.addClass("mt-col-hidden");
            }

            if (col.type === "toggle") {
                var isOn = (val === true || val === "true" || val === 1 || val === "1");
                var $switch = $(
                    "<label class='mt-switch mt-switch-readonly'>" +
                    "<input type='checkbox'" + (isOn ? " checked" : "") + " disabled />" +
                    "<span class='mt-switch-slider'></span>" +
                    "</label>"
                );
                $td.append($switch);
            } else if (col.type === "image") {
                var sizeStyle = col.imageSize ? { width: col.imageSize, height: col.imageSize } : {};

                if (val) {
                    var $img = $("<img>")
                        .addClass("mt-row-image")
                        .attr("src", val)
                        .attr("alt", "")
                        .css(sizeStyle);
                    $img.on("error", function () {
                        $(this).replaceWith(buildNoImage(col.imageSize));   // picture could not be loaded
                    });
                    $td.append($img);
                } else {
                    $td.append(buildNoImage(col.imageSize));             // no picture saved (null / empty)
                }
            } else if (col.type === "badge") {
                if (val === null || val === undefined || val === "") {
                    $td.text("");
                } else {
                    var badgeColor = resolveBadgeColor(col, val);
                    if (badgeColor) {
                        var textColor = getReadableTextColor(badgeColor);
                        var $pill = $("<span class='mt-badge'></span>")
                            .text(val)
                            .css({
                                "--mt-badge-bg": badgeColor,
                                "--mt-badge-color": textColor
                            });
                        $td.append($pill);
                    } else {
                        $td.append($("<span class='mt-badge-plain'></span>").text(val));
                    }
                }
            } else {
                $td.text(col.format ? formatCellValue(val, col.format) : (val != null ? val : ""));
            }

            $tr.append($td);
        });

        if (selectable) {
            if (String(row[rowIdField]) === String(state.selectedId)) {
                $tr.addClass("mt-row-selected");
            }
            $tr.on("click", function () {
                selectRow(row);
            });
        }

        return $tr;
    }

    function selectRow(row) {
        state.selectedId = row[rowIdField];
        $hiddenInput.val(state.selectedId).trigger("change");

        $tbody.find("tr").removeClass("mt-row-selected");
        $tbody.find("tr[data-row-id='" + row[rowIdField] + "']").addClass("mt-row-selected");

        $wrapper.trigger("table:select", [row]);
    }

    function renderInfo(totalRows, start, pageRowCount) {
        if (totalRows === 0) {
            $info.text("No records");
            return;
        }
        var from = start + 1;
        var to = start + pageRowCount;
        $info.text("Showing " + from + "–" + to + " of " + totalRows);
    }

    function renderPagination(totalPages) {
        $pagination.empty();

        var $prev = $("<button type='button' class='mt-page-btn'>‹</button>")
            .prop("disabled", state.currentPage === 1)
            .on("click", function () { goToPage(state.currentPage - 1); });
        $pagination.append($prev);

        var pages = getPageNumbers(state.currentPage, totalPages);
        pages.forEach(function (p) {
            if (p === "...") {
                $pagination.append("<span class='mt-page-btn' style='border:none;cursor:default;'>…</span>");
                return;
            }
            var $btn = $("<button type='button' class='mt-page-btn'>" + p + "</button>");
            if (p === state.currentPage) $btn.addClass("mt-page-active");
            $btn.on("click", function () { goToPage(p); });
            $pagination.append($btn);
        });

        var $next = $("<button type='button' class='mt-page-btn'>›</button>")
            .prop("disabled", state.currentPage === totalPages)
            .on("click", function () { goToPage(state.currentPage + 1); });
        $pagination.append($next);
    }

    function getPageNumbers(current, total) {
        var pages = [];
        var delta = 1;
        for (var i = 1; i <= total; i++) {
            if (i === 1 || i === total || (i >= current - delta && i <= current + delta)) {
                pages.push(i);
            } else if (pages[pages.length - 1] !== "...") {
                pages.push("...");
            }
        }
        return pages;
    }

    function goToPage(p) {
        state.currentPage = p;
        render();
        if (isAnimatedScrolling && $scrollEl.length) {
            animateScrollToTop($scrollEl[0], 700);
        }
    }

    $wrapper.data("mt-api", {
        setData: function (newRows) {
            allRows = newRows;
            state.currentPage = 1;
            render();
        },
        getSelected: function () {
            return allRows.find(function (r) { return String(r[rowIdField]) === String(state.selectedId); });
        },
        // Vaikunth: every row, whatever the search, filters or page.
        getAllRows: function () { return allRows.slice(); },
        // Vaikunth: select a row by its id (e.g. after a page reload), going to the page it is on.
        selectById: function (id) {
            var row = allRows.find(function (r) { return String(r[rowIdField]) === String(id); });
            if (!row) return null;
            var view = (typeof getFilteredSortedRows === "function") ? getFilteredSortedRows() : allRows;
            var idx = view.indexOf(row);
            if (enablePaging && idx >= 0) state.currentPage = Math.floor(idx / pageSize) + 1;
            state.selectedId = row[rowIdField];
            render();
            selectRow(row);
            return row;
        },
        clearSelection: function () {
            state.selectedId = null;
            $hiddenInput.val("");
            $tbody.find("tr").removeClass("mt-row-selected");
        },
        clearData: function () {
            allRows = [];
            state.searchTerm = "";
            state.columnFilters = {};
            state.sortField = null;
            state.sortDir = "asc";
            state.currentPage = 1;
            state.selectedId = null;

            $search.val("");
            $hiddenInput.val("");
            $wrapper.find(".mt-filter-input").val("");
            $thead.find("th").removeClass("mt-sort-asc mt-sort-desc");

            updateFilterCount();
            render();
        },
        // Updates one row in place (matched by row-id-field) and redraws only that row if it is on
        // the current page. Search, sort, filters, page and selection all stay as they are.
        // Pass only the fields that changed or the whole row; other fields keep their values.
        // Returns false if no row with that id exists.
        updateRow: function (row) {
            if (!row || row[rowIdField] == null) return false;
            var id = String(row[rowIdField]);
            var index = -1;
            for (var i = 0; i < allRows.length; i++) {
                if (String(allRows[i][rowIdField]) === id) { index = i; break; }
            }
            if (index < 0) return false;

            allRows[index] = $.extend({}, allRows[index], row);

            var $old = $tbody.children("tr").filter(function () {
                return String($(this).attr("data-row-id")) === id;
            });
            if ($old.length) {
                $old.replaceWith(buildRowElement(allRows[index]));
            }
            return true;
        },
        // Adds one row (it must have a row-id-field value), keeps search/sort/filters,
        // moves to the page that shows it and selects it (fires "table:select").
        // Returns false if the row has no id or that id already exists.
        addRow: function (row) {
            if (!row || row[rowIdField] == null) return false;
            var id = String(row[rowIdField]);
            for (var i = 0; i < allRows.length; i++) {
                if (String(allRows[i][rowIdField]) === id) return false;
            }
            allRows.push(row);

            if (enablePaging) {
                var visible = getFilteredSortedRows();
                for (var j = 0; j < visible.length; j++) {
                    if (String(visible[j][rowIdField]) === id) {
                        state.currentPage = Math.floor(j / pageSize) + 1;
                        break;
                    }
                }
            }
            render();
            selectRow(row);
            return true;
        },
        deleteRow: function (id) {
            allRows = allRows.filter(function (r) { return String(r[rowIdField]) !== String(id); });
            if (String(state.selectedId) === String(id)) {
                state.selectedId = null;
                $hiddenInput.val("");
            }
            render();
        },
        deleteSelected: function () {
            if (state.selectedId == null) return false;
            var idToDelete = state.selectedId;
            allRows = allRows.filter(function (r) { return String(r[rowIdField]) !== String(idToDelete); });
            state.selectedId = null;
            $hiddenInput.val("");
            render();
            return true;
        },
        setColumnVisible: function (field, visible) {
            columnVisibility[field] = visible;
            $colPanel.find("input[data-field='" + field + "']").prop("checked", visible);
            applyColumnVisibility();
        },
        // ---- Replace the table's columns at runtime (e.g. one shared list window) ----
        // newColumns: [{ field, header, visible, sortable, filterable, width, type, headerAlign, cellAlign, format }]
        // (PascalCase keys like Field / Header also work). Rows, search, filters, sort and
        // selection are cleared; call setData() afterwards to show the new rows.
        getColumns: function () {
            return columns.slice();
        },
        // The rows the grid shows: search, column filters and sort applied, ALL pages (Print -> Excel)
        getViewRows: function () {
            return getFilteredSortedRows().slice();
        },
        // The columns shown now (the column chooser's choice included), in order
        getVisibleColumns: function () {
            return columns.filter(function (col) { return columnVisibility[col.field] !== false; });
        },
        setColumns: function (newColumns) {
            function pick(c, camel, pascal) {
                return c[camel] !== undefined ? c[camel] : c[pascal];
            }
            columns = $.map(newColumns || [], function (c) {
                if (!c) return null;
                var field = pick(c, "field", "Field");
                if (!field) return null;
                return [{
                    field: String(field),
                    header: pick(c, "header", "Header") || String(field),
                    visible: pick(c, "visible", "Visible") !== false,
                    sortable: pick(c, "sortable", "Sortable") !== false,
                    filterable: pick(c, "filterable", "Filterable") !== false,
                    width: pick(c, "width", "Width") || null,
                    type: pick(c, "type", "Type") || "text",
                    imageSize: pick(c, "imageSize", "ImageSize") || null,
                    headerAlign: pick(c, "headerAlign", "HeaderAlign") || null,
                    cellAlign: pick(c, "cellAlign", "CellAlign") || null,
                    format: pick(c, "format", "Format") || null,
                    badgeColors: pick(c, "badgeColors", "BadgeColors") || null,
                    badgeDefaultColor: pick(c, "badgeDefaultColor", "BadgeDefaultColor") || null
                }];
            });

            columnVisibility = {};
            columns.forEach(function (col) {
                columnVisibility[col.field] = col.visible !== false;
            });

            // Keep the JSON script in sync: other code (modern-input-with-search.js)
            // reads it to match row keys to column fields.
            $wrapper.find(".mt-columns-data").first().text(JSON.stringify(columns));

            allRows = [];
            state.searchTerm = "";
            state.columnFilters = {};
            state.sortField = null;
            state.sortDir = "asc";
            state.currentPage = 1;
            state.selectedId = null;
            $search.val("");
            $hiddenInput.val("");

            if ($filterToggleBtn.length) {
                $filterToggleBtn.toggle(columns.some(function (col) { return col.filterable !== false; }));
            }
            updateFilterCount();
            renderHeader();
            renderColumnPanel();
            render();
        },
        setRowClassCallback: function (fn) {
            rowClassCallback = fn;
            render();
        },
        setHeight: function (height) {
            var h = height || "auto";
            $wrapper[0].style.setProperty("--mt-wrapper-height", h);
            $wrapper[0].style.setProperty("--mt-body-height", h);
        },
        setWidth: function (width) {
            $wrapper[0].style.setProperty("--mt-wrapper-width", width || "100%");
        },
        // ---- Column filter row (ShowFilterButton) ----
        // toggleFilters(): flips open/closed. toggleFilters(true|false): forces a state.
        // No effect on tables rendered with ShowFilterButton="false" (row is always visible).
        toggleFilters: function (open) {
            if (!$filterToggleBtn.length) return;
            setFiltersOpen(open === undefined ? !$wrapper.hasClass("mt-filters-open") : open, false);
        },
        clearFilters: function () {
            state.columnFilters = {};
            state.currentPage = 1;
            $wrapper.find(".mt-filter-input").val("");
            updateFilterCount();
            render();
        },
        // ---- Toolbar dropdown access (ShowFirstDropdown / ShowSecondDropdown) ----
        // which: "first" | "second"
        setDropdownItems: function (which, items) {
            return setDropdownItemsInternal(which, items);
        },
        getDropdownValue: function (which) {
            var $dd = which === "second" ? $wrapper.find(".mt-toolbar-dd").eq(1) : $wrapper.find(".mt-toolbar-dd").eq(0);
            return $dd.length ? $dd.find("input[type=hidden]").val() : null;
        },
        getDropdownText: function (which) {
            var $dd = which === "second" ? $wrapper.find(".mt-toolbar-dd").eq(1) : $wrapper.find(".mt-toolbar-dd").eq(0);
            return $dd.length ? $dd.find(".dd-selected").text() : null;
        },
        setDropdownValue: function (which, value) {
            var $dd = which === "second" ? $wrapper.find(".mt-toolbar-dd").eq(1) : $wrapper.find(".mt-toolbar-dd").eq(0);
            if (!$dd.length) return false;
            var $option = $dd.find(".dd-option[data-value='" + value + "']");
            if (!$option.length) return false;
            $dd.find(".dd-option").removeClass("dd-option-selected");
            $option.addClass("dd-option-selected");
            $dd.find(".dd-selected").text($option.text().trim());
            $dd.find("input[type=hidden]").val(value).trigger("change");
            return true;
        },
        // ---- Toolbar date-range access (ShowFirstDatePicker / ShowSecondDatePicker) ----
        // which: "first" | "second". Value format should match whatever modern-datepicker
        // expects/displays elsewhere on the page (e.g. "dd-mm-yyyy" per the placeholder).
        // Fires "change" on the input so modern-datepicker's own script (if it listens for
        // that event) and any page-level handlers stay in sync with the new value.
        getDateValue: function (which) {
            var $input = which === "second" ? $secondDateInput : $firstDateInput;
            return $input.length ? $input.val() : null;
        },
        setDateValue: function (which, value) {
            var $input = which === "second" ? $secondDateInput : $firstDateInput;
            if (!$input.length) return false;
            $input.val(value).trigger("change").trigger("input");
            return true;
        }
    });
}

 