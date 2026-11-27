// ===== Modern Dropdown With Label Control =====
// Separate from modern-dropdown.js — auto-initializes every
// .modern-dropdown-with-label on the page. All global helper functions
// use the "WithLabel" suffix so this coexists cleanly with the original
// modern-dropdown.js on the same page.
// Fires a "dropdown:change" event with (value, text) whenever selection changes.
//
// window.ModernDropdownWithLabel.init(context) can be called again after
// any AJAX/partial load to initialize newly-injected dropdowns. Safe to
// call repeatedly — already-initialized dropdowns are skipped.
// (window.initModernDropdownsWithLabel() kept as an alias for backward compatibility.)

window.ModernDropdownWithLabel = window.ModernDropdownWithLabel || {};

function initDropdownWithLabel($dropdown) {
    var $toggle = $dropdown.find(".ddwl-toggle");
    var $menu = $dropdown.find(".ddwl-menu");
    var $search = $dropdown.find(".ddwl-search");
    var $options = $dropdown.find(".ddwl-option");
    var $noResults = $dropdown.find(".ddwl-no-results");
    var $selectedText = $dropdown.find(".ddwl-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    var $errorIcon = $dropdown.find(".ddwl-icon-error");
    var isRequired = $dropdown.attr("data-required") === "true";

    if ($hiddenInput.val()) {
        $dropdown.addClass("has-value");
    }

    // Show the validation icon immediately if required and nothing is
    // selected yet — don't wait for any interaction.
    function checkRequired() {
        if (isRequired && !$hiddenInput.val()) {
            $errorIcon.show();
        } else {
            $errorIcon.hide();
        }
    }
    checkRequired();

    // Hide search box for short lists (optional, comment out to always show)
    $dropdown.find(".ddwl-search-wrap").toggle($options.length > 3);

    $toggle.on("click", function (e) {
        e.stopPropagation();

        // 🔒 Block interaction if disabled
        if ($dropdown.hasClass("ddwl-disabled")) return;

        var isOpen = $dropdown.hasClass("open");
        $(".modern-dropdown-with-label").not($dropdown).removeClass("open");
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
        if ($dropdown.hasClass("ddwl-disabled")) return; // 🔒

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

    $menu.on("click", ".ddwl-option", function () {
        if ($dropdown.hasClass("ddwl-disabled")) return; // 🔒
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

        checkRequired();   // hides the icon now that something is selected

        $dropdown.trigger("dropdown:change", [value, text]);
    }

    $toggle.attr("tabindex", $dropdown.hasClass("ddwl-disabled") ? "-1" : "0");

    // expose so setModernDropdownWithLabelData / disable / enable can
    // re-check the icon after they change the value programmatically
    $dropdown.data("ddwl-check-required", checkRequired);
}

window.ModernDropdownWithLabel.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $dropdowns = ($scope.is && $scope.is(".modern-dropdown-with-label"))
        ? $scope
        : $scope.find(".modern-dropdown-with-label");

    $dropdowns.each(function () {
        if (!$(this).data("ddwl-initialized")) {
            $(this).data("ddwl-initialized", true);
            initDropdownWithLabel($(this));
        }
    });
};

// Kept for backward compatibility with any existing callers.
window.initModernDropdownsWithLabel = function (context) {
    window.ModernDropdownWithLabel.init(context);
};

$(document).ready(function () {
    window.ModernDropdownWithLabel.init();

    // Close any open dropdown on outside click
    $(document).on("click", function () {
        $(".modern-dropdown-with-label.open").removeClass("open");
    });
});

// ----- Fill / replace a dropdown's options from jQuery / AJAX -----
// items: array of { value: "...", text: "..." }
// Usage: setModernDropdownWithLabelData("#NameDropdown", [{ value: "1", text: "Bangalore" }, ...]);
window.setModernDropdownWithLabelData = function (dropdownSelector, items, selectedValue) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    var $optionsList = $dropdown.find(".ddwl-options");
    var $selectedText = $dropdown.find(".ddwl-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    var placeholder = $selectedText.data("placeholder") || $selectedText.text();

    if (!$selectedText.data("placeholder")) {
        $selectedText.data("placeholder", $selectedText.text());
    }

    $optionsList.empty();

    $.each(items, function (i, item) {
        var isSelected = selectedValue !== undefined && String(item.value) === String(selectedValue);
        var $li = $("<li>")
            .addClass("ddwl-option" + (isSelected ? " selected" : ""))
            .attr("data-value", item.value)
            .text(item.text);
        $optionsList.append($li);
    });

    if (selectedValue !== undefined && selectedValue !== null && selectedValue !== "") {
        var matchText = "";
        $optionsList.find(".ddwl-option.selected").each(function () {
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

    $dropdown.removeData("ddwl-initialized");
    $dropdown.find(".ddwl-toggle, .ddwl-search, .ddwl-menu").off();
    initDropdownWithLabel($dropdown);
    $dropdown.data("ddwl-initialized", true);
};

// ----- Enable / disable a dropdown dynamically at runtime -----
window.disableModernDropdownWithLabel = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    $dropdown.addClass("ddwl-disabled").removeClass("open");
    $dropdown.find(".ddwl-toggle").prop("disabled", true).attr("tabindex", "-1");
    $dropdown.find("input[type=hidden]").prop("disabled", true);
};

window.enableModernDropdownWithLabel = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    $dropdown.removeClass("ddwl-disabled");
    $dropdown.find(".ddwl-toggle").prop("disabled", false).attr("tabindex", "0");
    $dropdown.find("input[type=hidden]").prop("disabled", false);
};

// ----- Validate / clear -----
// Usage: validateModernDropdownWithLabel("#CityDropdown") -> true/false
window.validateModernDropdownWithLabel = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return true;

    var checkFn = $dropdown.data("ddwl-check-required");
    if (typeof checkFn === "function") checkFn();

    var isRequired = $dropdown.attr("data-required") === "true";
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    return !(isRequired && !$hiddenInput.val());
};

// Usage: setModernDropdownWithLabelValue("#CityDropdown", "3") — selects
// whichever existing option has that value, without needing to
// re-supply the whole options list (unlike setModernDropdownWithLabelData).
// Returns true if a matching option was found and selected, false otherwise.
window.setModernDropdownWithLabelValue = function (dropdownSelector, value) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return false;

    var $options = $dropdown.find(".ddwl-option");
    var $matchingOption = $options.filter(function () {
        return String($(this).data("value")) === String(value);
    });

    if ($matchingOption.length === 0) {
        console.warn("setModernDropdownWithLabelValue: no option found with value", value);
        return false;
    }

    var $selectedText = $dropdown.find(".ddwl-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");

    $options.removeClass("selected");
    $matchingOption.addClass("selected");

    $selectedText.text($matchingOption.text());
    $hiddenInput.val(value).trigger("change");
    $dropdown.addClass("has-value");

    var checkFn = $dropdown.data("ddwl-check-required");
    if (typeof checkFn === "function") checkFn();

    $dropdown.trigger("dropdown:change", [value, $matchingOption.text()]);

    return true;
};

// Usage: clearModernDropdownWithLabel("#CityDropdown")
window.clearModernDropdownWithLabel = function (dropdownSelector) {
    var $dropdown = $(dropdownSelector);
    if ($dropdown.length === 0) return;

    var $selectedText = $dropdown.find(".ddwl-selected");
    var $hiddenInput = $dropdown.find("input[type=hidden]");
    var placeholder = $selectedText.data("placeholder") || $selectedText.text();

    $dropdown.find(".ddwl-option.selected").removeClass("selected");
    $selectedText.text(placeholder);
    $hiddenInput.val("").trigger("change");
    $dropdown.removeClass("has-value");

    var checkFn = $dropdown.data("ddwl-check-required");
    if (typeof checkFn === "function") checkFn();
};

// Usage: clearAllModernDropdownsWithLabel() or clearAllModernDropdownsWithLabel("#myForm")
window.clearAllModernDropdownsWithLabel = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    $scope.find(".modern-dropdown-with-label").each(function () {
        var id = $(this).attr("id");
        if (id) window.clearModernDropdownWithLabel("#" + id);
    });
};

// Usage: validateAllModernDropdownsWithLabel() -> true/false, or scoped to a container
window.validateAllModernDropdownsWithLabel = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var allValid = true;

    $scope.find(".modern-dropdown-with-label").each(function () {
        var id = $(this).attr("id");
        if (id && !window.validateModernDropdownWithLabel("#" + id)) {
            allValid = false;
        }
    });

    return allValid;
};
