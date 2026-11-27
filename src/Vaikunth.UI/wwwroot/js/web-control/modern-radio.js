// ===== Modern Radio Group Control =====
// Auto-initializes every .mr-control on the page.
//
// - mandatory groups: shows a red icon immediately if nothing is
//   selected (from page load), re-checked on change, clears as soon as
//   any option in the group is picked
// - each radio's own data-field is identical across all options in the
//   same group (set once via the TagHelper's FieldName) — reading a
//   group's value means finding whichever option is currently :checked
//
// window.ModernRadio.init(context) can be called again after any
// AJAX/partial load to initialize newly-injected radio groups. Safe to
// call repeatedly — already-initialized groups are skipped.



//Example : how to set or get value
// <modern-radio-group id="rdoLoanDuration" name="LoanDuration" label="Loan duration"
//     mandatory="true" field-name="loanDuration">
//     <modern-radio value="Year" label="Year"></modern-radio>
//     <modern-radio value="Month" label="Month"></modern-radio>
// </modern-radio-group>

// getModernRadioGroupValue("LoanDuration");           // -> "Year" | "Month" | undefined
// setModernRadioGroupValue("LoanDuration", "Month");
// clearModernRadioGroup("LoanDuration");               // unchecks everything in the group

// GET
//var duration = getModernRadioGroupValue("LoanDuration");
// -> "Year" or "Month", or undefined if nothing selected yet

// SET
//setModernRadioGroupValue("LoanDuration", "Month");
// -> checks the "Month" radio, unchecks "Year", fires validation

window.ModernRadio = window.ModernRadio || {};

window.ModernRadio.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $controls = ($scope.is && $scope.is(".mr-control"))
        ? $scope
        : $scope.find(".mr-control");

    $controls.each(function () {
        var $control = $(this);

        if ($control.data("mr-inited")) return;
        $control.data("mr-inited", true);

        var $inputs = $control.find("input[type='radio']");
        var $errorIcon = $control.find(".mr-icon-error");
        var isMandatory = $control.attr("data-mandatory") === "true";

        function markInvalid() {
            $errorIcon.show();
            $control.addClass("mr-invalid");
        }

        function markValid() {
            $errorIcon.hide();
            $control.removeClass("mr-invalid");
        }

        if (isMandatory) {
            // show immediately on load if nothing is selected yet
            if ($inputs.filter(":checked").length === 0) {
                markInvalid();
            }

            $inputs.on("change", function () {
                if ($inputs.filter(":checked").length > 0) {
                    markValid();
                } else {
                    markInvalid();
                }
            });
        }
    });
};

$(document).ready(function () {
    window.ModernRadio.init();
});

// ----- Public helpers -----

// Usage: validateModernRadioGroup("DeliveryMethod") -> true/false, also toggles the icon
// Pass the group's `name` (same value given to the TagHelper's Name property).
window.validateModernRadioGroup = function (groupName) {
    var $control = $(`.mr-control[data-group-name='${groupName}']`);
    if ($control.length === 0) return true;

    var isMandatory = $control.attr("data-mandatory") === "true";
    var $errorIcon = $control.find(".mr-icon-error");
    var hasSelection = $control.find("input[type='radio']:checked").length > 0;

    if (isMandatory && !hasSelection) {
        $errorIcon.show();
        $control.addClass("mr-invalid");
        return false;
    }

    $errorIcon.hide();
    $control.removeClass("mr-invalid");
    return true;
};

// Usage: getModernRadioGroupValue("DeliveryMethod") -> "express" (or undefined if none selected)
window.getModernRadioGroupValue = function (groupName) {
    var $checked = $(`.mr-control[data-group-name='${groupName}'] input[type='radio']:checked`);
    return $checked.length ? $checked.val() : undefined;
};

// Usage: setModernRadioGroupValue("DeliveryMethod", "express") — also
// refreshes the mandatory icon correctly. Skips the write/trigger when
// that value is already selected, so it doesn't replay any CSS
// selection animation for no reason (same fix applied to
// setModernCheckboxValue for the same class of issue).
window.setModernRadioGroupValue = function (groupName, value) {
    var $control = $(`.mr-control[data-group-name='${groupName}']`);
    if ($control.length === 0) return;

    var $target = $control.find(`input[type='radio'][value='${value}']`);
    if ($target.length === 0) return;

    if ($target.prop("checked")) {
        window.validateModernRadioGroup(groupName);
        return;
    }

    $target.prop("checked", true).trigger("change");
    window.validateModernRadioGroup(groupName);
};

// Usage: clearModernRadioGroup("DeliveryMethod") — unchecks every option
// in the group. Native radios have no "unchecked" state you can set via
// .prop("checked", false) alone if you want the mandatory icon to
// re-appear correctly, so this loops all options in the group.
window.clearModernRadioGroup = function (groupName) {
    var $control = $(`.mr-control[data-group-name='${groupName}']`);
    if ($control.length === 0) return;

    $control.find("input[type='radio']").prop("checked", false);
    window.validateModernRadioGroup(groupName);
};

// Usage: getModernRadioGroupValuesByField() -> { fieldName: "selectedValue", ... }
//        Reads every distinct [data-field] radio group on the page (or scope).
window.getModernRadioGroupValuesByField = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var values = {};

    $scope.find(".mr-control").each(function () {
        var $control = $(this);
        var $checked = $control.find("input[data-field]:checked").first();
        if ($checked.length) {
            values[$checked.attr("data-field")] = $checked.val();
        }
    });

    return values;
};

// Usage: getModernRadioValueByField("loanDuration") -> "Year" (or undefined)
// Single-field version of the above — reads just one group by its
// data-field (set via the TagHelper's FieldName), not its `name`.
window.getModernRadioValueByField = function (fieldName, containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var $checked = $scope.find(`.mr-control input[data-field='${fieldName}']:checked`).first();
    return $checked.length ? $checked.val() : undefined;
};

// Usage: setModernRadioValueByField("loanDuration", "Month")
// Single-field version of setModernRadioGroupValue — targets by
// data-field instead of the group's `name`. Skips the write/trigger
// when that value is already selected, same as setModernCheckboxValue.
window.setModernRadioValueByField = function (fieldName, value, containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var $target = $scope.find(`.mr-control input[data-field='${fieldName}'][value='${value}']`).first();
    if ($target.length === 0) return;

    var groupName = $target.closest(".mr-control").attr("data-group-name");

    if ($target.prop("checked")) {
        if (groupName) window.validateModernRadioGroup(groupName);
        return;
    }

    $target.prop("checked", true).trigger("change");
    if (groupName) window.validateModernRadioGroup(groupName);
};

// Usage: validateAllModernRadioGroups() -> true/false — checks every
//        mandatory group on the page, showing icons on any left
//        unselected. validateAllModernRadioGroups("#myForm") — scoped.
// Typical use: run this on form submit before allowing it through.
window.validateAllModernRadioGroups = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var allValid = true;

    $scope.find(".mr-control").each(function () {
        var groupName = $(this).attr("data-group-name");
        if (groupName && !window.validateModernRadioGroup(groupName)) {
            allValid = false;
        }
    });

    return allValid;
};