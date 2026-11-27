// ---------------------------------------------------------------------
// window.ModernDatePicker.init(context)
//
// Initializes every ".modern-datepicker" input found within `context`
// (defaults to the whole document). Safe to call multiple times:
// already-initialized inputs are skipped via the "dp-inited" data
// guard, so re-running this after a partial view is injected (AJAX,
// tab switch, menu load, etc.) will only initialize the NEW input(s)
// that weren't present when $(document).ready() first ran.
//
// Call this explicitly after injecting any HTML that contains a
// <modern-datepicker>, e.g.:
//
//   $(".main-panel").html(html);
//   window.ModernDatePicker.init(".main-panel");
// ---------------------------------------------------------------------
window.ModernDatePicker = window.ModernDatePicker || {};

// Shared registry of all flatpickr instances, keyed by input id.
// Kept at module scope (not inside init()) so it persists across
// multiple init() calls as different partials get loaded in.
window.ModernDatePicker._instances = window.ModernDatePicker._instances || {};

window.ModernDatePicker.init = function (context) {
    var datePickers = window.ModernDatePicker._instances;
    var $scope = context ? $(context) : $(document);

    var $inputs = ($scope.is && $scope.is(".modern-datepicker"))
        ? $scope
        : $scope.find(".modern-datepicker");

    $inputs.each(function () {
        var input = this;
        var $input = $(input);

        if ($input.data("dp-inited")) return; // already initialized, skip
        $input.data("dp-inited", true);

        if (!input.id) {
            console.warn("modern-datepicker input has no id — flatpickr instance won't be retrievable by id.", input);
        }

        // Read disabled dates from data attribute, fallback to empty array
        var disabled = [];
        try {
            var raw = $input.attr("data-disabled-dates");
            disabled = raw ? JSON.parse(raw) : [];
        } catch (e) {
            console.warn("Invalid disabled-dates JSON for", input.id, e);
        }

        datePickers[input.id] = flatpickr(input, {
            disableMobile: true,   // always our own calendar (phones would otherwise get the browser's built-in date picker)
            dateFormat: "d-m-Y",
            allowInput: true,
            disable: disabled,
            clickOpens: !input.disabled,   // 🔒 don't open calendar on click if disabled
            onReady: function (selectedDates, dateStr, instance) {
                instance.input.addEventListener("input", function () {
                    this.value = this.value.replace(/[^0-9-]/g, "").substring(0, 10);
                });

                // 🔒 keep flatpickr's own disabled state in sync with the input attribute
                if (input.disabled) {
                    instance.altInput && (instance.altInput.disabled = true);
                }
            }
        });
    });

    // The calendar-icon click handler is delegated on document, so it
    // only needs to be bound ONCE ever (not per-init-call) — jQuery's
    // event delegation means it will correctly find icons added later
    // by any future init() call automatically. Guard with a flag on
    // document so multiple init() calls don't stack duplicate handlers.
    if (!window.ModernDatePicker._iconHandlerBound) {
        window.ModernDatePicker._iconHandlerBound = true;

        $(document).on("click", ".datepicker-icon", function () {
            var input = $(this)
                .closest(".datepicker-wrapper")
                .find(".modern-datepicker")[0];

            // 🔒 don't open calendar via icon click if disabled
            if (!input || input.disabled) return;

            if (input && datePickers[input.id]) {
                datePickers[input.id].open();
            }
        });
    }
};

$(document).ready(function () {
    window.ModernDatePicker.init();
});

// ----- Enable / disable a datepicker dynamically at runtime -----
// Usage: disableModernDatepicker("#DOB");  /  enableModernDatepicker("#DOB");
window.disableModernDatepicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernDatePicker._instances[input.id];

    $input.prop("disabled", true);
    $input.closest(".datepicker-wrapper").addClass("dp-disabled");

    if (fp) {
        fp.close();
        fp.set("clickOpens", false);
    }
};

window.enableModernDatepicker = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var fp = window.ModernDatePicker._instances[input.id];

    $input.prop("disabled", false);
    $input.closest(".datepicker-wrapper").removeClass("dp-disabled");

    if (fp) {
        fp.set("clickOpens", true);
    }
};

// ----- Set / get disabled dates dynamically at runtime -----
// Usage: setModernDatepickerDisabledDates("#DOB", ["2026-08-25", { from: "2026-09-01", to: "2026-09-05" }]);
//
// NOTE: rebuilds the flatpickr instance (fp.set("disable", ...) was
// found to mis-parse ranges). Also converts every date string to an
// explicit local-midnight Date object before handing it to flatpickr —
// plain "YYYY-MM-DD" strings get parsed as UTC midnight by JS, which
// shifts to the PREVIOUS day in timezones ahead of UTC (e.g. IST),
// and flatpickr appeared to be hitting this. Passing real Date
// objects sidesteps that parsing entirely.
window.setModernDatepickerDisabledDates = function (inputSelector, disabledDatesArray) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var datePickers = window.ModernDatePicker._instances;
    var oldFp = datePickers[input.id];
    var currentValue = input.value;   // preserve whatever's already selected

    if (oldFp) {
        oldFp.destroy();
    }

    // Convert strings to explicit local-time Date objects (adding
    // T00:00:00 with no Z forces local-time interpretation, not UTC)
    var normalizedDisable = (disabledDatesArray || []).map(function (item) {
        if (typeof item === "string") {
            return new Date(item + "T00:00:00");
        }
        if (item && item.from && item.to) {
            return {
                from: new Date(item.from + "T00:00:00"),
                to: new Date(item.to + "T00:00:00")
            };
        }
        return item;   // already a Date, function, etc. — pass through
    });

    datePickers[input.id] = flatpickr(input, {
            disableMobile: true,   // always our own calendar (phones would otherwise get the browser's built-in date picker)
        dateFormat: "d-m-Y",
        allowInput: true,
        disable: normalizedDisable,
        clickOpens: !input.disabled,
        onReady: function (selectedDates, dateStr, instance) {
            instance.input.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9-]/g, "").substring(0, 10);
            });
            if (input.disabled) {
                instance.altInput && (instance.altInput.disabled = true);
            }
        }
    });

    if (currentValue) {
        datePickers[input.id].setDate(currentValue, true);
    }
};

// Usage: getModernDatepickerDisabledDates("#DOB")
window.getModernDatepickerDisabledDates = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return [];

    var input = $input[0];
    var fp = window.ModernDatePicker._instances[input.id];

    return fp ? fp.config.disable : [];
};

// ----- Block all dates before/after a cutoff -----
// Usage: setModernDatepickerDateRange("#DOB", "2026-01-01", null);         // block everything before Jan 1, 2026
//        setModernDatepickerDateRange("#DOB", null, "2026-12-31");         // block everything after Dec 31, 2026
//        setModernDatepickerDateRange("#DOB", "2026-01-01", "2026-12-31"); // only allow dates within 2026
//        setModernDatepickerDateRange("#DOB", null, null);                 // remove both limits
//
// Rebuilds the instance (same reasoning as setModernDatepickerDisabledDates)
// and preserves any disabled dates/ranges already set via that function.
window.setModernDatepickerDateRange = function (inputSelector, minDateString, maxDateString) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    var input = $input[0];
    var datePickers = window.ModernDatePicker._instances;
    var oldFp = datePickers[input.id];
    var currentValue = input.value;

    // Defensive: oldFp might exist but be in a broken/partial state
    // (e.g. missing .config) — don't let that crash this function.
    var currentDisable = [];
    try {
        if (oldFp && oldFp.config && oldFp.config.disable) {
            currentDisable = oldFp.config.disable;
        }
    } catch (e) {
        console.warn("Could not read existing disable config, starting fresh:", e);
    }

    try {
        if (oldFp && typeof oldFp.destroy === "function") {
            oldFp.destroy();
        }
    } catch (e) {
        console.warn("Could not destroy previous flatpickr instance cleanly:", e);
    }

    var minDate = minDateString ? new Date(minDateString + "T00:00:00") : undefined;
    var maxDate = maxDateString ? new Date(maxDateString + "T00:00:00") : undefined;

    datePickers[input.id] = flatpickr(input, {
            disableMobile: true,   // always our own calendar (phones would otherwise get the browser's built-in date picker)
        dateFormat: "d-m-Y",
        allowInput: true,
        disable: currentDisable,
        minDate: minDate,
        maxDate: maxDate,
        clickOpens: !input.disabled,
        onReady: function (selectedDates, dateStr, instance) {
            instance.input.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9-]/g, "").substring(0, 10);
            });
            if (input.disabled) {
                instance.altInput && (instance.altInput.disabled = true);
            }
        }
    });

    if (currentValue) {
        datePickers[input.id].setDate(currentValue, true);
    }
};