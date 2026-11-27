// ===== Modern Dropdown Control (with search) =====
// Auto-initializes every .modern-dropdown on the page.
// Fires a "dropdown:change" event with (value, text) whenever selection changes.
//
// window.ModernDropdown.init(context) can be called again after any
// AJAX/partial load to initialize newly-injected dropdowns. Safe to call
// repeatedly — already-initialized dropdowns are skipped.
// (window.initModernDropdowns() is kept as an alias for backward compatibility.)

window.ModernDropdown = window.ModernDropdown || {};

function initDropdown($dropdown) {
    var $toggle = $dropdown.find(".dd-toggle");
    var $menu = $dropdown.find(".dd-menu");
    var $search = $dropdown.find(".dd-search");
    var $options = $dropdown.find(".dd-option");
    var $noResults = $dropdown.find(".dd-no-results");
    var $selectedText = $dropdown.find(".dd-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");

    if ($hiddenInput.val()) {
        $dropdown.addClass("has-value");
    }

    // Hide search box for short lists (optional, comment out to always show)
    $dropdown.find(".dd-search-wrap").toggle($options.length > 3);

    $toggle.on("click", function (e) {
        e.stopPropagation();

        // 🔒 Block interaction if disabled
        if ($dropdown.hasClass("dd-disabled")) return;

        var isOpen = $dropdown.hasClass("open");
        $(".modern-dropdown").not($dropdown).removeClass("open");
        $dropdown.toggleClass("open", !isOpen);

        if (!isOpen) {
            $search.val("");
            $options.show();
            $noResults.hide();
            setTimeout(function () { $search.trigger("focus"); }, 50);
        }
    });

    // Live filter as user types
    $search.on("input", function () {
        if ($dropdown.hasClass("dd-disabled")) return; // 🔒

        var term = $(this).val().trim().toLowerCase();
        var visibleCount = 0;

        $options.each(function () {
            var text = $(this).text().toLowerCase();
            var match = text.indexOf(term) > -1;
            $(this).toggle(match);
            if (match) visibleCount++;
        });

        $noResults.toggle(visibleCount === 0);
    });

    // Don't let clicks/keys inside search box close the dropdown
    $search.on("click", function (e) {
        e.stopPropagation();
    });

    $menu.on("click", ".dd-option", function () {
        if ($dropdown.hasClass("dd-disabled")) return; // 🔒
        selectOption($(this));
    });

    function selectOption($option) {
        var value = $option.data("value");
        var text = $option.text();

        $options.removeClass("selected");
        $option.addClass("selected");

        $selectedText.text(text);
        $hiddenInput.val(value).trigger("change");
        $dropdown.addClass("has-value").removeClass("open");

        $dropdown.trigger("dropdown:change", [value, text]);
    }

    $toggle.attr("tabindex", $dropdown.hasClass("dd-disabled") ? "-1" : "0");
}

window.ModernDropdown.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $dropdowns = ($scope.is && $scope.is(".modern-dropdown"))
        ? $scope
        : $scope.find(".modern-dropdown");

    $dropdowns.each(function () {
        if (!$(this).data("dd-initialized")) {
            $(this).data("dd-initialized", true);
            initDropdown($(this));
        }
    });
};

// Kept for backward compatibility with any existing callers.
window.initModernDropdowns = function (context) {
    window.ModernDropdown.init(context);
};

$(document).ready(function () {
    window.ModernDropdown.init();

    // Close any open dropdown on outside click
    $(document).on("click", function () {
        $(".modern-dropdown.open").removeClass("open");
    });
});

// ----- Fill / replace a dropdown's options from jQuery / AJAX -----
// items: array of { value: "...", text: "..." }
// Usage: setModernDropdownData("#NameDropdown", [{ value: "1", text: "Bangalore" }, ...]);
window.setModernDropdownData = function (dropdownSelector, items, selectedValue) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    var $optionsList = $dropdown.find(".dd-options");
    var $selectedText = $dropdown.find(".dd-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    var placeholder = $selectedText.data("placeholder") || $selectedText.text();

    // Save placeholder the first time so re-filling doesn't lose it
    if (!$selectedText.data("placeholder")) {
        $selectedText.data("placeholder", $selectedText.text());
    }

    $optionsList.empty();

    $.each(items, function (i, item) {
        var isSelected = selectedValue !== undefined && String(item.value) === String(selectedValue);
        var $li = $("<li>")
            .addClass("dd-option" + (isSelected ? " selected" : ""))
            .attr("data-value", item.value)
            .text(item.text);
        $optionsList.append($li);
    });

    if (selectedValue !== undefined && selectedValue !== null && selectedValue !== "") {
        var matchText = "";
        $optionsList.find(".dd-option.selected").each(function () {
            matchText = $(this).text();
        });
        $selectedText.text(matchText || placeholder);
        $hiddenInput.val(selectedValue).trigger("change");
        $dropdown.toggleClass("has-value", !!matchText);
    } else {
        $selectedText.text(placeholder);
        $hiddenInput.val("").trigger("change");
        $dropdown.removeClass("has-value");
    }

    // Re-init so click/search bindings pick up the fresh <li> elements
    $dropdown.removeData("dd-initialized");
    $dropdown.find(".dd-toggle, .dd-search, .dd-menu").off();
    initDropdown($dropdown);
    $dropdown.data("dd-initialized", true);
};

// ----- Select an existing option by value, without re-supplying the
// whole options list (unlike setModernDropdownData) -----
// Usage: setModernDropdownValue("#CityDropdown", "3")
// Returns true if a matching option was found and selected, false otherwise.
window.setModernDropdownValue = function (dropdownSelector, value) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return false;

    var $options = $dropdown.find(".dd-option");
    var $matchingOption = $options.filter(function () {
        return String($(this).data("value")) === String(value);
    });

    if ($matchingOption.length === 0) {
        console.warn("setModernDropdownValue: no option found with value", value);
        return false;
    }

    var $selectedText = $dropdown.find(".dd-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");

    $options.removeClass("selected");
    $matchingOption.addClass("selected");

    $selectedText.text($matchingOption.text());
    $hiddenInput.val(value).trigger("change");
    $dropdown.addClass("has-value");

    $dropdown.trigger("dropdown:change", [value, $matchingOption.text()]);

    return true;
};

// ----- Clear the current selection, keeping the options list intact -----
// Usage: clearModernDropdown("#CityDropdown")
window.clearModernDropdown = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    var $selectedText = $dropdown.find(".dd-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    var placeholder = $selectedText.data("placeholder") || $selectedText.text();

    $dropdown.find(".dd-option.selected").removeClass("selected");
    $selectedText.text(placeholder);
    $hiddenInput.val("").trigger("change");
    $dropdown.removeClass("has-value");
};

// ----- Enable / disable a dropdown dynamically at runtime -----
// Usage: disableModernDropdown("#CityDropdown");  /  enableModernDropdown("#CityDropdown");
window.disableModernDropdown = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    $dropdown.addClass("dd-disabled").removeClass("open");
    $dropdown.find(".dd-toggle").prop("disabled", true).attr("tabindex", "-1");
    $dropdown.find("input[type=hidden]").prop("disabled", true);
};

window.enableModernDropdown = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    $dropdown.removeClass("dd-disabled");
    $dropdown.find(".dd-toggle").prop("disabled", false).attr("tabindex", "0");
    $dropdown.find("input[type=hidden]").prop("disabled", false);
};
