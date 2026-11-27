// ===== Modern Checkbox Control =====
// Auto-initializes every .mc-control on the page.
// Works identically for both visual variants (mc-box / mc-toggle) — the
// variant only changes CSS, not behavior.
//
// - mandatory fields: shows a red icon immediately if left unchecked (from
//   page load), re-checked on change, clears as soon as it's checked
// - readonly (data-readonly="true"): the control renders as `disabled`
//   (see the TagHelper) so it can't be toggled or focused into. Since
//   disabled inputs are excluded from a plain form POST, we mirror its
//   value into a hidden same-name field so it still submits.
//
// window.ModernCheckbox.init(context) can be called again after any
// AJAX/partial load to initialize newly-injected checkboxes. Safe to call
// repeatedly — already-initialized controls are skipped.

window.ModernCheckbox = window.ModernCheckbox || {};

window.ModernCheckbox.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $controls = ($scope.is && $scope.is(".mc-control"))
        ? $scope
        : $scope.find(".mc-control");

    $controls.each(function () {
        var $control = $(this);

        if ($control.data("mc-inited")) return;
        $control.data("mc-inited", true);

        var $input = $control.find("input[type='checkbox']");
        var $errorIcon = $control.find(".mc-icon-error");
        var isMandatory = $control.attr("data-mandatory") === "true";
        var isReadOnly = $input.attr("data-readonly") === "true";

        function markInvalid() {
            $errorIcon.show();
            $control.addClass("mc-invalid");
        }

        function markValid() {
            $errorIcon.hide();
            $control.removeClass("mc-invalid");
        }

        if (isMandatory) {
            // show immediately on load if already unchecked — don't wait for interaction
            if (!$input.prop("checked")) {
                markInvalid();
            }

            $input.on("change", function () {
                if ($input.prop("checked")) {
                    markValid();
                } else {
                    markInvalid();
                }
            });
        }

        if (isReadOnly) {
            var name = $input.attr("name");
            if (name) {
                var $hidden = $("<input>", {
                    type: "hidden",
                    name: name,
                    value: $input.prop("checked") ? "true" : "false"
                });
                $control.append($hidden);
            }
        }
    });
};

$(document).ready(function () {
    window.ModernCheckbox.init();
});

// ----- Public helpers -----

// Usage: validateModernCheckbox("#Agree") -> true/false, also toggles the icon
window.validateModernCheckbox = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return true;

    var $control = $input.closest(".mc-control");
    var isMandatory = $control.attr("data-mandatory") === "true";
    var $errorIcon = $control.find(".mc-icon-error");

    if (isMandatory && !$input.prop("checked")) {
        $errorIcon.show();
        $control.addClass("mc-invalid");
        return false;
    }

    $errorIcon.hide();
    $control.removeClass("mc-invalid");
    return true;
};

// Usage: getModernCheckboxValue("#Agree") -> true/false
window.getModernCheckboxValue = function (inputSelector) {
    return $(inputSelector).prop("checked");
};

// Usage: setModernCheckboxValue("#Agree", true) — also refreshes
// the mandatory icon correctly (unlike a plain .prop() call).
// Skips the write/trigger entirely when the checkbox is already in the
// requested state — otherwise every call (e.g. from
// GridBinder.populateFormFromSelection, even when the server already
// rendered the correct checked state) forces a change event and replays
// the mc-pop "pop" animation / mandatory-icon check for no reason, which
// is what shows up as a flash/blink right after a partial or modal loads.
window.setModernCheckboxValue = function (inputSelector, value) {
    var $input = $(inputSelector);
    var next = !!value;

    if ($input.prop("checked") === next) {
        window.validateModernCheckbox(inputSelector);
        return;
    }

    $input.prop("checked", next).trigger("change");
    window.validateModernCheckbox(inputSelector);
};

// Usage: getModernCheckboxValuesByField() -> { fieldName: true/false, ... }
//        Reads every [data-field] checkbox on the page (or within a scope).
window.getModernCheckboxValuesByField = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var values = {};
    $scope.find(".mc-control input[data-field]").each(function () {
        values[$(this).attr("data-field")] = $(this).prop("checked");
    });
    return values;
};

// Usage: validateAllModernCheckboxes() -> true/false — checks every
//        mandatory checkbox on the page, showing icons on any left
//        unchecked. validateAllModernCheckboxes("#myForm") — scoped.
// Typical use: run this on form submit before allowing it through.
window.validateAllModernCheckboxes = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var allValid = true;

    $scope.find(".mc-control input[type='checkbox']").each(function () {
        var id = $(this).attr("id");
        if (id && !window.validateModernCheckbox("#" + id)) {
            allValid = false;
        }
    });

    return allValid;
};