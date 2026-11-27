// ===== Modern Input Control =====
// Auto-initializes every .mi-input-wrap on the page.
// - type="int"/"number": strips everything except digits as the user types
// - type="phone": strips everything except digits, +, -, and ( )
// - type="currency": strips everything except digits and . — and allows
//   only ONE decimal point (any typed after the first are ignored)
// - mandatory fields: shows a red icon immediately if empty (from page
//   load), re-checked on blur, clears as soon as the user starts typing
// - autofocus: if the wrapped <input>/<textarea> has the native
//   `autofocus` attribute, focus it. The native attribute only auto-applies
//   on the browser's initial page parse — it does nothing when this markup
//   is injected later via AJAX/partial/modal — so we apply it manually
//   here every time init() runs.
//
// window.ModernInput.init(context) can be called again after any
// AJAX/partial load to initialize newly-injected inputs. Safe to call
// repeatedly — already-initialized inputs are skipped.

window.ModernInput = window.ModernInput || {};

window.ModernInput.init = function (context) {
    var $scope = context ? $(context) : $(document);

    var $wraps = ($scope.is && $scope.is(".mi-input-wrap"))
        ? $scope
        : $scope.find(".mi-input-wrap");

    var $autofocusTarget = null;

    $wraps.each(function () {
        var $wrap = $(this);

        if ($wrap.data("mi-inited")) return;
        $wrap.data("mi-inited", true);

        var $input = $wrap.find(".mi-input");
        var $errorIcon = $wrap.find(".mi-icon-error");
        var $clearBtn = $wrap.find(".mi-clear-btn");
        var inputType = $wrap.attr("data-input-type") || "text";
        var isMandatory = $wrap.attr("data-mandatory") === "true";
        var isPasswordField = $wrap.hasClass("mi-password");

        var filterPattern = null;
        var isTextarea = $input.is("textarea");

        if (!isTextarea) {
            if (inputType === "int" || inputType === "number") {
                filterPattern = /[^0-9]/g;
            } else if (inputType === "phone") {
                filterPattern = /[^0-9+\-()]/g;   // digits, +, -, and parentheses
            }
        }
        // multiline fields (textarea) are always free text — no character filtering

        if (filterPattern) {
            $input.on("input", function () {
                var cleaned = $(this).val().replace(filterPattern, "");
                if ($(this).val() !== cleaned) {
                    $(this).val(cleaned);
                }
            });
        } else if (!isTextarea && inputType === "currency") {
            // custom handler — a simple character filter can't prevent a
            // SECOND decimal point (each "." is individually valid), so we
            // strip non-digit/non-dot chars, then keep only the first dot
            $input.on("input", function () {
                var val = $(this).val();
                var cleaned = val.replace(/[^0-9.]/g, "");

                var firstDot = cleaned.indexOf(".");
                if (firstDot !== -1) {
                    cleaned = cleaned.substring(0, firstDot + 1)
                        + cleaned.substring(firstDot + 1).replace(/\./g, "");
                }

                if (val !== cleaned) {
                    $(this).val(cleaned);
                }
            });
        }

        // ---- Shared right-hand slot: the mandatory-error icon and the
        // clear ("x") button occupy the exact same position (see CSS) and
        // are mutually exclusive — updateRightSlot() is the single place
        // that decides which (if either) is visible, so the two can never
        // both show at once regardless of what triggered the check
        // (typing, blur, programmatic value changes via the public
        // helpers, password clear, etc.). Runs for every field, not just
        // mandatory or password ones — a plain optional text field still
        // gets a clear button once it has a value. ----
        function updateRightSlot() {
            var hasValue = $input.val().length > 0;
            var isEmpty = $input.val().trim() === "";

            if (isMandatory && isEmpty) {
                $errorIcon.show();
                $wrap.addClass("mi-invalid");
                $clearBtn.hide(); // nothing to clear on an empty field anyway
            } else {
                $errorIcon.hide();
                $wrap.removeClass("mi-invalid");
                $clearBtn.toggle(hasValue && !$input.prop("readonly") && !$input.prop("disabled"));
            }
        }

        function markInvalid() {
            $errorIcon.show();
            $wrap.addClass("mi-invalid");
            $clearBtn.hide();
        }

        function markValid() {
            updateRightSlot();
        }

        // Initial state on load (covers a pre-filled Value and the
        // mandatory-empty case) for every field.
        updateRightSlot();
        $input.on("input", updateRightSlot);

        if (isMandatory) {
            $input.on("blur", function () {
                if ($input.val().trim() === "") {
                    markInvalid();
                } else {
                    markValid();
                }
            });
        }

        $clearBtn.on("click", function () {
            if ($input.prop("readonly") || $input.prop("disabled")) return;   // read-only: nothing to clear
            $input.val("").trigger("input").trigger("focus");
            // updateRightSlot() (bound above) already re-runs on "input"
            // and will show the error icon again if the field is mandatory
        });

        // Track the last autofocus-marked input seen in this init pass —
        // mirrors native browser behavior when multiple [autofocus]
        // elements exist (the last one wins), and matches the doc-comment
        // on the ModernInput TagHelper that only one per panel should be set.
        if ($input.is("[autofocus]")) {
            $autofocusTarget = $input;
        }

        // ---- Password mode: show/hide eye toggle ----
        // (clear button is already wired above, shared with every field)
        if (isPasswordField) {
            var $toggleBtn = $wrap.find(".mi-pw-toggle");
            var $eyeOpen = $toggleBtn.find(".mi-pw-eye-open");
            var $eyeClosed = $toggleBtn.find(".mi-pw-eye-closed");

            $toggleBtn.on("click", function () {
                var showing = $input.attr("type") === "text";
                if (showing) {
                    $input.attr("type", "password");
                    $eyeOpen.show();
                    $eyeClosed.hide();
                    $toggleBtn.attr("aria-label", "Show password");
                } else {
                    $input.attr("type", "text");
                    $eyeOpen.hide();
                    $eyeClosed.show();
                    $toggleBtn.attr("aria-label", "Hide password");
                }
                // keep focus + caret in the field rather than jumping to the button
                var el = $input.get(0);
                var pos = el.value.length;
                $input.trigger("focus");
                if (typeof el.setSelectionRange === "function") {
                    try { el.setSelectionRange(pos, pos); } catch (e) { /* no-op */ }
                }
            });
        }
    });

    // Apply focus AFTER the loop (and after the DOM this batch injected has
    // settled) — needed because autofocus doesn't fire on its own once the
    // markup arrives via innerHTML/$.html() instead of a real page load
    // (main-panel AJAX loads, tab-panel loads, full-screen modal loads, etc.)
    if ($autofocusTarget && $autofocusTarget.length) {
        var el = $autofocusTarget.get(0);
        requestAnimationFrame(function () {
            el.focus();
            // put the caret at the end rather than leaving/selecting at position 0
            if (typeof el.setSelectionRange === "function" && el.value) {
                var len = el.value.length;
                try { el.setSelectionRange(len, len); } catch (e) { /* no-op for input types that don't support it */ }
            }
        });
    }
};

$(document).ready(function () {
    window.ModernInput.init();
});

// ----- Public helpers -----
// Usage: validateModernInput("#Name") -> true/false, also toggles the icon
window.validateModernInput = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return true;

    var $wrap = $input.closest(".mi-input-wrap");
    var isMandatory = $wrap.attr("data-mandatory") === "true";
    var $errorIcon = $wrap.find(".mi-icon-error");
    var $clearBtn = $wrap.find(".mi-clear-btn");

    if (isMandatory && $input.val().trim() === "") {
        $errorIcon.show();
        $wrap.addClass("mi-invalid");
        $clearBtn.hide();
        return false;
    }

    $errorIcon.hide();
    $wrap.removeClass("mi-invalid");
    $clearBtn.toggle($input.val().length > 0 && !$input.prop("readonly") && !$input.prop("disabled"));
    return true;
};

// Usage: getModernInputValue("#Name") -> current value
window.getModernInputValue = function (inputSelector) {
    return $(inputSelector).val();
};

// Usage: setModernInputValue("#Name", "Ravi Kumar") — also refreshes
// the mandatory icon correctly (unlike a plain .val() call)
window.setModernInputValue = function (inputSelector, value) {
    $(inputSelector).val(value);
    window.validateModernInput(inputSelector);
};

// Usage: clearModernInput("#Name") — shorthand for setting to "".
// If the field is mandatory, the red icon correctly reappears; the clear
// button hides either way, since there's nothing left to clear.
window.clearModernInput = function (inputSelector) {
    var $input = $(inputSelector);
    if ($input.length === 0) return;

    $input.val("");

    var $wrap = $input.closest(".mi-input-wrap");
    if ($wrap.length === 0) return;

    var isMandatory = $wrap.attr("data-mandatory") === "true";
    var $errorIcon = $wrap.find(".mi-icon-error");
    var $clearBtn = $wrap.find(".mi-clear-btn");

    $clearBtn.hide();

    if (isMandatory) {
        $errorIcon.show();
        $wrap.addClass("mi-invalid");
    } else {
        $errorIcon.hide();
        $wrap.removeClass("mi-invalid");
    }
};

// Usage: clearAllModernInputs() — clears every modern-input on the page.
//        clearAllModernInputs("#myForm") — clears only fields inside that container.
window.clearAllModernInputs = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    $scope.find(".mi-input-wrap .mi-input").each(function () {
        var id = $(this).attr("id");
        if (id) window.clearModernInput("#" + id);
    });
};

// Usage: validateAllModernInputs() -> true/false — checks every mandatory
//        field on the page, showing icons on any that are empty.
//        validateAllModernInputs("#myForm") — scoped to one container.
// Typical use: run this on form submit before allowing it through.
window.validateAllModernInputs = function (containerSelector) {
    var $scope = containerSelector ? $(containerSelector) : $(document);
    var allValid = true;

    $scope.find(".mi-input-wrap .mi-input").each(function () {
        var id = $(this).attr("id");
        if (id && !window.validateModernInput("#" + id)) {
            allValid = false;
        }
    });

    return allValid;
};