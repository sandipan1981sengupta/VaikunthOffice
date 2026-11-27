// ===== Modern Month/Year Picker Control =====
// Same behavior shape as modern-datepicker-with-label.js, but restricted
// to picking a month + year (no day) via flatpickr's official
// monthSelectPlugin. Auto-initializes every .myp-input on the page.
//
// REQUIRES flatpickr's monthSelect plugin to be loaded before this file:
//   <script src=".../flatpickr/plugins/monthSelect/index.js"></script>
//   <link rel="stylesheet" href=".../flatpickr/plugins/monthSelect/style.css">
// (see modern-month-year-picker usage notes for CDN/local paths)
//
// window.ModernMonthYearPicker.init(context) can be called again after
// any AJAX/partial load to initialize newly-injected inputs. Safe to
// call repeatedly — already-initialized inputs are skipped.

window.ModernMonthYearPicker = window.ModernMonthYearPicker || {};
window.ModernMonthYearPicker._instances = window.ModernMonthYearPicker._instances || {};

window.ModernMonthYearPicker.init = function (context) {
    var pickers = window.ModernMonthYearPicker._instances;
    var $scope = context ? $(context) : $(document);

    var $inputs = ($scope.is && $scope.is(".myp-input"))
        ? $scope
        : $scope.find(".myp-input");

    $inputs.each(function () {
        var input = this;
        var $input = $(input);

        if ($input.data("myp-inited")) return;
        $input.data("myp-inited", true);

        var $field = $input.closest(".myp-field");
        var $errorIcon = $field.find(".myp-icon-error");
        var isRequired = $field.attr("data-required") === "true";
        var typingDisabled = $input.attr("data-disable-typing") === "true";
        var dateFormat = $input.attr("data-date-format") || "m-Y";

        function checkRequired() {
            if (isRequired && !input.value.trim()) {
                $errorIcon.show();
            } else {
                $errorIcon.hide();
            }
        }
        checkRequired();   // show immediately on load if empty — don't wait for blur

        if (typeof monthSelectPlugin === "undefined") {
            console.error("ModernMonthYearPicker: flatpickr's monthSelectPlugin is not loaded. " +
                "Include flatpickr/plugins/monthSelect/index.js before modern-month-year-picker.js.");
            return;
        }

        pickers[input.id] = flatpickr(input, {
            disableMobile: true,   // always our own calendar (phones would otherwise get the browser's built-in date picker)
            plugins: [new monthSelectPlugin({
                shorthand: true,       // "Aug" instead of "August"
                dateFormat: dateFormat,
                altFormat: dateFormat
            })],
            allowInput: !typingDisabled,
            clickOpens: !input.disabled,
            onReady: function (selectedDates, dateStr, instance) {
                if (!typingDisabled) {
                    instance.input.addEventListener("input", function () {
                        // month/year only — strip anything but digits and
                        // the format's own separator characters
                        this.value = this.value.replace(/[^0-9\-\/]/g, "").substring(0, 10);
                        checkRequired();
                    });
                }
                instance.input.addEventListener("blur", checkRequired);
                if (input.disabled) {
                    instance.altInput && (instance.altInput.disabled = true);
                }
            },
            onChange: function () {
                checkRequired();
            }
        });

        $field.data("myp-check-required", checkRequired);
    });

    if (!window.ModernMonthYearPicker._iconHandlerBound) {
        window.ModernMonthYearPicker._iconHandlerBound = true;

        $(document).on("click", ".myp-icon", function () {
            var input = $(this)
                .closest(".myp-field")
                .find(".myp-input")[0];

            if (!input || input.disabled) return;

            if (input && pickers[input.id]) {
                pickers[input.id].open();
            }
        });
    }
};

$(document).ready(function () {
    window.ModernMonthYearPicker.init();
});

// ----- Enable / disable dynamically at runtime -----
window.disableModernMonthYearPicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernMonthYearPicker._instances[input.id];

    $input.prop("disabled", true);
    $input.closest(".myp-field").addClass("myp-disabled");

    if (fp) {
        fp.close();
        fp.set("clickOpens", false);
    }
};

window.enableModernMonthYearPicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernMonthYearPicker._instances[input.id];

    $input.prop("disabled", false);
    $input.closest(".myp-field").removeClass("myp-disabled");

    if (fp) {
        fp.set("clickOpens", true);
    }
};

// ----- Validate / get / set / clear, matching modern-datepicker-with-label -----
window.validateModernMonthYearPicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return true;

    var $field = $input.closest(".myp-field");
    var checkFn = $field.data("myp-check-required");
    if (typeof checkFn === "function") checkFn();

    var isRequired = $field.attr("data-required") === "true";
    return !(isRequired && !$input.val().trim());
};

// Usage: getModernMonthYearPickerValue("#Field") -> "08-2026"
window.getModernMonthYearPickerValue = function (inputSelector) {
    return $(inputSelector).val();
};

// Usage: setModernMonthYearPickerValue("#Field", "08-2026")
window.setModernMonthYearPickerValue = function (inputSelector, dateString) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernMonthYearPicker._instances[input.id];

    if (fp) {
        fp.setDate(dateString, true);
    } else {
        $input.val(dateString);
    }

    var $field = $input.closest(".myp-field");
    var checkFn = $field.data("myp-check-required");
    if (typeof checkFn === "function") checkFn();
};

// Usage: clearModernMonthYearPicker("#Field")
window.clearModernMonthYearPicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernMonthYearPicker._instances[input.id];

    if (fp) {
        fp.clear();
    } else {
        $input.val("");
    }

    var $field = $input.closest(".myp-field");
    var checkFn = $field.data("myp-check-required");
    if (typeof checkFn === "function") checkFn();
};

// Usage: clearAllModernMonthYearPickers() or scoped to a container
window.clearAllModernMonthYearPickers = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    $scope.find(".myp-input").each(function () {
        var id = $(this).attr("id");
        if (id) window.clearModernMonthYearPicker("#" + id);
    });
};

// Usage: validateAllModernMonthYearPickers() -> true/false, or scoped
window.validateAllModernMonthYearPickers = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var allValid = true;

    $scope.find(".myp-input").each(function () {
        var id = $(this).attr("id");
        if (id && !window.validateModernMonthYearPicker("#" + id)) {
            allValid = false;
        }
    });

    return allValid;
};
