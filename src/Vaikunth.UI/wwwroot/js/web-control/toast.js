// =======================================================================================
// toast.js — small dark notifications in the top-right corner
// Used for successful Add / Edit / Delete. Errors and questions stay in the message box.
// Self-contained: the CSS is added to the page once, the first time a toast is shown.
//
//     ShowToast("Department deleted");                                  // green success toast
//     ShowToast("Record updated", "success", { title: "Updated" });
//     ShowToast("Saved as draft", "info", { duration: 5000 });          // info | success | warning | error
//
// A toast closes by itself (default 3.5 s). Hovering pauses it, the × closes it early.
// Several toasts stack; at most 4 are shown, the oldest makes room for a new one.
// =======================================================================================

var ToastSettings = {
    duration: 3500,      // how long a toast stays, in milliseconds
    maxVisible: 4        // older toasts are closed when more than this are on screen
};

function EnsureToastStyles() {
    if (document.getElementById("mt-toast-styles")) return;
    var css =
        // Container: top-right, above the page, the full-page window and the message box
        // (message box 2147482000), below the loading pill (2147482999 / 2147483000)
        ".mt-toast-stack{position:fixed;top:20px;right:20px;z-index:2147482500;display:flex;flex-direction:column;" +
        "gap:10px;width:360px;max-width:calc(100vw - 32px);pointer-events:none}" +

        // Card: dark charcoal glass, deep shadow, faint top highlight, coloured accent strip on the left
        ".mt-toast{--tc:#22c55e;--tc-soft:rgba(34,197,94,.16);position:relative;display:flex;align-items:flex-start;gap:12px;" +
        "padding:14px 40px 16px 18px;border-radius:14px;overflow:hidden;pointer-events:auto;" +
        "background:linear-gradient(180deg,rgba(30,34,44,.94) 0%,rgba(17,20,28,.95) 100%);" +
        "-webkit-backdrop-filter:blur(16px) saturate(150%);backdrop-filter:blur(16px) saturate(150%);" +
        "border:1px solid rgba(255,255,255,.08);" +
        "box-shadow:inset 0 1px 0 rgba(255,255,255,.06),0 20px 44px rgba(0,0,0,.45),0 4px 12px rgba(0,0,0,.25);" +
        "font-family:'Segoe UI',Roboto,Arial,sans-serif;" +
        "opacity:0;transform:translateX(calc(100% + 24px)) scale(.98);" +
        "transition:transform .5s cubic-bezier(.2,.9,.25,1.12),opacity .35s ease}" +
        ".mt-toast.mt-in{opacity:1;transform:translateX(0) scale(1)}" +
        ".mt-toast.mt-out{opacity:0;transform:translateX(30px) scale(.96);transition:transform .3s ease-in,opacity .25s ease-in}" +
        ".mt-toast::before{content:'';position:absolute;left:0;top:0;bottom:0;width:4px;" +
        "background:linear-gradient(180deg,var(--tc),color-mix(in srgb,var(--tc) 55%,#ffffff))}" +

        ".mt-toast-info{--tc:#60a5fa;--tc-soft:rgba(96,165,250,.16)}" +
        ".mt-toast-warning{--tc:#fbbf24;--tc-soft:rgba(251,191,36,.16)}" +
        ".mt-toast-error{--tc:#f87171;--tc-soft:rgba(248,113,113,.16)}" +

        // Icon: tinted circle, the tick draws itself in
        ".mt-toast-icon{flex:none;width:34px;height:34px;border-radius:50%;display:flex;align-items:center;justify-content:center;" +
        "color:var(--tc);background:var(--tc-soft);box-shadow:0 0 0 1px color-mix(in srgb,var(--tc) 35%,transparent),0 0 14px color-mix(in srgb,var(--tc) 30%,transparent)}" +
        ".mt-toast-icon svg{width:18px;height:18px}" +
        ".mt-toast-icon path{stroke-dasharray:30;stroke-dashoffset:30}" +
        ".mt-toast.mt-in .mt-toast-icon path{animation:mt-toast-draw .45s ease-out .25s forwards}" +

        // Text
        ".mt-toast-body{min-width:0;padding-top:1px}" +
        ".mt-toast-title{font-size:14px;font-weight:650;color:#f8fafc;line-height:1.3}" +
        ".mt-toast-text{margin-top:2px;font-size:13px;color:#a8b3c4;line-height:1.45;word-wrap:break-word}" +

        // Close button
        ".mt-toast-close{position:absolute;top:10px;right:10px;width:24px;height:24px;border:none;border-radius:50%;" +
        "background:transparent;color:#7c8799;font-size:16px;line-height:1;cursor:pointer;display:flex;align-items:center;" +
        "justify-content:center;transition:background .12s ease,color .12s ease}" +
        ".mt-toast-close:hover{background:rgba(255,255,255,.1);color:#f8fafc}" +

        // Time bar along the bottom edge; pauses while hovered
        ".mt-toast::after{content:'';position:absolute;left:0;right:0;bottom:0;height:3px;background:rgba(255,255,255,.06)}" +
        ".mt-toast-bar{position:absolute;left:0;right:0;bottom:0;height:3px;z-index:1;transform-origin:left center;" +
        "background:linear-gradient(79deg,rgba(240,0,188,1) 0%,rgba(0,107,247,1) 100%);" +
        "animation:mt-toast-bar linear forwards}" +
        ".mt-toast:hover .mt-toast-bar{animation-play-state:paused}" +

        "@keyframes mt-toast-draw{to{stroke-dashoffset:0}}" +
        "@keyframes mt-toast-bar{from{transform:scaleX(1)}to{transform:scaleX(0)}}" +
        "@media (prefers-reduced-motion:reduce){.mt-toast,.mt-toast.mt-out{transition:opacity .2s ease;transform:none}" +
        ".mt-toast-icon path{stroke-dashoffset:0;animation:none!important}}" +
        "@media (max-width:480px){.mt-toast-stack{top:12px;right:12px;left:12px;width:auto}}";
    var style = document.createElement("style");
    style.id = "mt-toast-styles";
    style.textContent = css;
    document.head.appendChild(style);
}

var TOAST_ICONS = {
    success: '<svg viewBox="0 0 24 24" fill="none"><path d="M5 12.5l4.5 4.5L19 7.5" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    info: '<svg viewBox="0 0 24 24" fill="none"><path d="M12 11v6M12 7.2v.1" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"/></svg>',
    warning: '<svg viewBox="0 0 24 24" fill="none"><path d="M12 7v6M12 17v.1" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"/></svg>',
    error: '<svg viewBox="0 0 24 24" fill="none"><path d="M7 7l10 10M17 7L7 17" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"/></svg>'
};
var TOAST_TITLES = { success: "Success", info: "Information", warning: "Warning", error: "Error" };

/**
 * Shows a toast in the top-right corner.
 * message - the text (shown as plain text)
 * type    - "success" (default), "info", "warning" or "error"
 * options - { title: "Updated", duration: 3500 }
 */
function ShowToast(message, type, options) {
    type = TOAST_ICONS[type] ? type : "success";
    options = options || {};
    var duration = Number(options.duration) > 0 ? Number(options.duration) : ToastSettings.duration;
    EnsureToastStyles();

    var stack = document.querySelector(".mt-toast-stack");
    if (!stack) {
        stack = document.createElement("div");
        stack.className = "mt-toast-stack";
        stack.setAttribute("aria-live", "polite");
        document.body.appendChild(stack);
    }

    // Make room: close the oldest toasts beyond the limit
    var open = stack.querySelectorAll(".mt-toast:not(.mt-out)");
    for (var i = 0; i <= open.length - ToastSettings.maxVisible; i++) CloseToast(open[i]);

    var toast = document.createElement("div");
    toast.className = "mt-toast mt-toast-" + type;
    toast.setAttribute("role", type === "error" ? "alert" : "status");
    toast.innerHTML =
        '<div class="mt-toast-icon">' + TOAST_ICONS[type] + '</div>' +
        '<div class="mt-toast-body"><div class="mt-toast-title"></div><div class="mt-toast-text"></div></div>' +
        '<button type="button" class="mt-toast-close" aria-label="Close">&times;</button>' +
        '<div class="mt-toast-bar"></div>';
    toast.querySelector(".mt-toast-title").textContent = options.title || TOAST_TITLES[type];
    var textEl = toast.querySelector(".mt-toast-text");
    if (message) textEl.textContent = message; else textEl.remove();

    var bar = toast.querySelector(".mt-toast-bar");
    bar.style.animationDuration = duration + "ms";   // the shrinking line only shows the time left

    // The timer is what closes the toast, so it always goes away by itself, even when
    // animations are switched off. Hovering pauses it (and the line); leaving resumes it.
    var remaining = duration, startedAt = Date.now(), timer = setTimeout(function () { CloseToast(toast); }, remaining);
    toast.addEventListener("mouseenter", function () {
        clearTimeout(timer);
        remaining = Math.max(0, remaining - (Date.now() - startedAt));
    });
    toast.addEventListener("mouseleave", function () {
        startedAt = Date.now();
        clearTimeout(timer);
        timer = setTimeout(function () { CloseToast(toast); }, Math.max(remaining, 400));
    });
    toast.querySelector(".mt-toast-close").addEventListener("click", function () {
        clearTimeout(timer);
        CloseToast(toast);
    });

    stack.appendChild(toast);
    requestAnimationFrame(function () {
        requestAnimationFrame(function () { toast.classList.add("mt-in"); });
    });
    return toast;
}

/** Closes one toast (slides out, then the space it used closes up smoothly). */
function CloseToast(toast) {
    if (!toast || toast.classList.contains("mt-out")) return;
    toast.classList.add("mt-out");
    setTimeout(function () {
        // collapse its height so the toasts below glide up instead of jumping
        toast.style.transition = "height .25s ease, margin .25s ease, padding .25s ease";
        toast.style.height = toast.offsetHeight + "px";
        requestAnimationFrame(function () {
            toast.style.height = "0px";
            toast.style.paddingTop = "0px";
            toast.style.paddingBottom = "0px";
            toast.style.marginBottom = "-10px";   // swallow the stack's gap
        });
        setTimeout(function () { if (toast.parentNode) toast.parentNode.removeChild(toast); }, 260);
    }, 260);
}