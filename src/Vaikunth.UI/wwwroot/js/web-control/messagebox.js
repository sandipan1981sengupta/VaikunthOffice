/* ==========================================================================
   Message Box control — JS helpers
   Works with the markup produced by <message-box> TagHelper.
   ========================================================================== */

/**
 * Show a message box by id.
 * @param {string} id
 */
function showMessageBox(id) {
    var el = document.getElementById(id);
    if (!el) return;
    el.classList.add('mb-visible');
    document.body.style.overflow = 'hidden';

    // Once the open animation finishes, clear it from the dialog. A CSS
    // animation with fill-mode "forwards" keeps actively holding the
    // properties it animated (here: transform) even after completion,
    // which silently overrides any transform the drag code sets later.
    // Clearing `animation` releases that hold so JS-driven transforms
    // (dragging) take effect. We must NOT clear it immediately — only
    // after the entrance animation has actually finished playing.
    var dialog = el.querySelector('.mb-dialog');
    if (dialog) {
        // Reset any inline override left from a previous open/close cycle
        // so the stylesheet's entrance animation is free to play again.
        dialog.style.animation = '';

        var releaseAnim = function (e) {
            if (e.target !== dialog) return;
            dialog.style.animation = 'none';
            dialog.removeEventListener('animationend', releaseAnim);
        };
        dialog.addEventListener('animationend', releaseAnim);
        // Safety fallback in case animationend doesn't fire (animations
        // disabled, reduced-motion, etc.) — matches the .32s entrance
        // duration plus a small buffer.
        setTimeout(function () {
            dialog.style.animation = 'none';
            dialog.removeEventListener('animationend', releaseAnim);
        }, 400);
    }

    // Close on backdrop click
    el.addEventListener('mousedown', function backdropHandler(e) {
        if (e.target === el) {
            hideMessageBox(id);
            el.removeEventListener('mousedown', backdropHandler);
        }
    });

    // Close on Escape
    document.addEventListener('keydown', function escHandler(e) {
        if (e.key === 'Escape') {
            hideMessageBox(id);
            document.removeEventListener('keydown', escHandler);
        }
    });

    makeMessageBoxDraggable(id);
}

/**
 * Hide a message box by id.
 * @param {string} id
 */
function hideMessageBox(id) {
    var el = document.getElementById(id);
    if (!el) return;
    if (!el.classList.contains('mb-visible') || el.classList.contains('mb-closing')) return;

    el.classList.add('mb-closing');

    var dialogForReset = el.querySelector('.mb-dialog');
    if (dialogForReset) {
        // Read whatever offset dragging left the dialog at, and hand it
        // to the CSS animation via custom properties, so the close
        // animation's "from" state matches where the dialog actually is
        // instead of snapping back to the centered home position first.
        var m = dialogForReset.style.transform.match(/translate\(([-\d.]+)px,\s*([-\d.]+)px\)/);
        var offsetX = m ? m[1] : 0;
        var offsetY = m ? m[2] : 0;
        dialogForReset.style.setProperty('--mb-drag-x', offsetX + 'px');
        dialogForReset.style.setProperty('--mb-drag-y', offsetY + 'px');

        // Now it's safe to hand control to the CSS animation: clear the
        // inline transform (the keyframe's `from` picks up from the
        // custom properties instead) and clear any inline `animation:
        // none` override left by the entrance animation so the
        // stylesheet's mb-slide-down-out rule is free to run.
        dialogForReset.style.transform = '';
        dialogForReset.style.animation = '';
    }

    function finishClose() {
        el.classList.remove('mb-visible', 'mb-closing');
        document.body.style.overflow = '';

        var dialog = el.querySelector('.mb-dialog');
        if (dialog) {
            dialog.style.transform = '';
            dialog.style.animation = '';
            dialog.style.removeProperty('--mb-drag-x');
            dialog.style.removeProperty('--mb-drag-y');
            dialog.removeAttribute('data-mb-dragged');
        }
    }

    var dialog = el.querySelector('.mb-dialog');
    var target = dialog || el;
    var finished = false;

    function onAnimEnd(e) {
        if (e.target !== target) return;
        if (finished) return;
        finished = true;
        target.removeEventListener('animationend', onAnimEnd);
        finishClose();
    }

    target.addEventListener('animationend', onAnimEnd);

    // Safety fallback in case animationend doesn't fire (e.g. animations
    // disabled or interrupted) — never leave the box stuck open.
    setTimeout(function () {
        if (!finished) {
            finished = true;
            target.removeEventListener('animationend', onAnimEnd);
            finishClose();
        }
    }, 400);
}

/**
 * Update the message text inside a box before showing it —
 * handy for validation messages generated server- or client-side.
 * @param {string} id
 * @param {string} text
 * @param {boolean} [asHtml=false] pass true to set innerHTML instead of textContent
 */
function setMessageBoxText(id, text, asHtml) {
    var el = document.getElementById(id);
    if (!el) return;
    var body = el.querySelector('.mb-body');
    if (!body) return;
    if (asHtml) body.innerHTML = text;
    else body.textContent = text;
}

/**
 * Update the caption/title in the header.
 * @param {string} id
 * @param {string} title
 */
function setMessageBoxTitle(id, title) {
    var el = document.getElementById(id);
    if (!el) return;
    var titleEl = el.querySelector('.mb-title');
    if (titleEl) titleEl.textContent = title;
}

/**
 * Convenience one-liner: set caption + text + show, e.g. for a login
 * validation error.
 * @param {string} id           id of an existing <message-box>
 * @param {string} text         body content
 * @param {string} [title]      optional caption; omit to keep existing title
 * @param {boolean} [asHtml]    pass true to allow HTML in text
 */
function alertMessageBox(id, text, title, asHtml) {
    if (title) setMessageBoxTitle(id, title);
    setMessageBoxText(id, text, asHtml);
    showMessageBox(id);
}

/**
 * Makes a message box's dialog draggable by its header. Safe to call
 * multiple times on the same id — it only wires up once per dialog element.
 * Uses Pointer Events so it works for mouse, touch, and pen alike.
 * @param {string} id
 */
function makeMessageBoxDraggable(id) {
    var overlay = document.getElementById(id);
    if (!overlay) return;
    var dialog = overlay.querySelector('.mb-dialog');
    var header = overlay.querySelector('.mb-header');
    if (!dialog || !header) return;
    if (header.dataset.mbDragWired === '1') return; // already wired
    header.dataset.mbDragWired = '1';

    var dragging = false;
    var startX = 0, startY = 0;
    var startTranslateX = 0, startTranslateY = 0;
    var minX = 0, maxX = 0, minY = 0, maxY = 0;

    function getCurrentTranslate() {
        var m = dialog.style.transform.match(/translate\(([-\d.]+)px,\s*([-\d.]+)px\)/);
        return m ? { x: parseFloat(m[1]), y: parseFloat(m[2]) } : { x: 0, y: 0 };
    }

    function onPointerDown(e) {
        // Ignore drags started on the close button itself
        if (e.target.closest('.mb-close-btn')) return;

        dragging = true;
        overlay.classList.add('mb-dragging');
        var t = getCurrentTranslate();
        startTranslateX = t.x;
        startTranslateY = t.y;
        startX = e.clientX;
        startY = e.clientY;

        // Compute drag bounds ONCE per drag, from the dialog's current
        // rendered box (which already includes startTranslate). Because
        // rect already reflects startTranslate, subtracting it back out
        // gives the untranslated ("home") position, so bounds stay fixed
        // for the whole gesture instead of being recomputed (and drifting)
        // on every pointermove — that drift was what caused the shaking.
        var rect = dialog.getBoundingClientRect();
        var homeLeft = rect.left - startTranslateX;
        var homeTop = rect.top - startTranslateY;
        var minVisible = 60; // px of header that must stay reachable

        minX = -homeLeft - (rect.width - minVisible);
        maxX = window.innerWidth - homeLeft - minVisible;
        minY = -homeTop;
        maxY = window.innerHeight - homeTop - minVisible;

        header.setPointerCapture(e.pointerId);
        header.addEventListener('pointermove', onPointerMove);
        header.addEventListener('pointerup', onPointerUp);
        header.addEventListener('pointercancel', onPointerUp);
    }

    function onPointerMove(e) {
        if (!dragging) return;
        var dx = e.clientX - startX;
        var dy = e.clientY - startY;
        var x = startTranslateX + dx;
        var y = startTranslateY + dy;

        x = Math.max(minX, Math.min(maxX, x));
        y = Math.max(minY, Math.min(maxY, y));

        dialog.style.transform = 'translate(' + x + 'px, ' + y + 'px)';
        dialog.setAttribute('data-mb-dragged', '1');
    }

    function onPointerUp(e) {
        dragging = false;
        overlay.classList.remove('mb-dragging');
        header.removeEventListener('pointermove', onPointerMove);
        header.removeEventListener('pointerup', onPointerUp);
        header.removeEventListener('pointercancel', onPointerUp);
        if (header.hasPointerCapture && header.hasPointerCapture(e.pointerId)) {
            header.releasePointerCapture(e.pointerId);
        }
    }

    header.addEventListener('pointerdown', onPointerDown);
}

/**
 * Fully dynamic message box — builds and shows a dialog on the fly,
 * no <message-box> TagHelper markup required in the page at all.
 * Reuses the existing mb-* CSS classes so it looks identical.
 *
 * @param {Object} options
 * @param {string} [options.id="dynamicMessageBox"]  id to (re)use for this box
 * @param {string} [options.title="Confirmation"]     header caption
 * @param {string} options.text                       body content (required)
 * @param {boolean} [options.asHtml=false]             treat text as HTML
 * @param {string} [options.type="info"]               info | success | warning | error
 * @param {string} [options.icon="arrow"]              arrow | check | warning | error | none
 * @param {boolean} [options.showFooter=true]          show OK button
 * @param {string} [options.okText="OK"]                footer button label
 * @param {Function} [options.onOk]                    callback fired when OK is clicked
 * @param {Function} [options.onClose]                  callback fired when the box closes (any way)
 *
 * @example
 * openMessageBox({
 *     title: "Confirmation",
 *     text: "Please enter correct username and password.",
 *     type: "error",
 *     icon: "arrow"
 * });
 */
function openMessageBox(options) {
    options = options || {};
    var id = options.id || 'dynamicMessageBox';
    var title = options.title || 'Confirmation';
    var text = options.text || '';
    var type = options.type || 'info';
    var icon = options.icon || 'arrow';
    var showFooter = options.showFooter !== false;
    var okText = options.okText || 'OK';

    var icons = {
        arrow: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M5 12h14M13 6l6 6-6 6" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        check: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M20 6L9 17l-5-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        warning: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        error: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2"/><path d="M15 9l-6 6M9 9l6 6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
        none: ''
    };
    var iconSvg = icons[icon] !== undefined ? icons[icon] : icons.arrow;

    // Reuse existing element with this id if present, otherwise create it.
    var el = document.getElementById(id);
    if (!el) {
        el = document.createElement('div');
        el.id = id;
        document.body.appendChild(el);
    }
    el.setAttribute('role', 'dialog');
    el.setAttribute('aria-modal', 'true');
    el.setAttribute('aria-labelledby', id + '_title');
    el.className = 'mb-overlay mb-type-' + type;

    el.innerHTML =
        '<div class="mb-dialog">' +
        '<div class="mb-header">' +
        '<div class="mb-header-left">' +
        (icon !== 'none' ? '<span class="mb-icon">' + iconSvg + '</span>' : '') +
        '<span id="' + id + '_title" class="mb-title"></span>' +
        '</div>' +
        '<button type="button" class="mb-close-btn" aria-label="Close">' +
        '<span>&times;</span>' +
        '</button>' +
        '</div>' +
        '<div class="mb-body"></div>' +
        (showFooter ? '<div class="mb-footer"><button type="button" class="mb-ok-btn">' + okText + '</button></div>' : '') +
        '</div>';

    // Set title/text via DOM APIs so callers don't need to worry about escaping.
    el.querySelector('.mb-title').textContent = title;
    var bodyEl = el.querySelector('.mb-body');
    if (options.asHtml) bodyEl.innerHTML = text;
    else bodyEl.textContent = text;

    function close() {
        hideMessageBox(id);
        if (typeof options.onClose === 'function') options.onClose();
    }

    el.querySelector('.mb-close-btn').onclick = close;
    var okBtn = el.querySelector('.mb-ok-btn');
    if (okBtn) {
        okBtn.onclick = function () {
            if (typeof options.onOk === 'function') options.onOk();
            close();
        };
    }

    showMessageBox(id);
    return id;
}

/**
 * Confirmation box with two buttons (e.g. Cancel / Delete), built on the fly
 * like openMessageBox and using the same mb-* look.
 * The confirm callback runs only when the user presses the confirm button;
 * Cancel, the X, Escape and a click outside the box all just close it.
 *
 * @param {Object} options
 * @param {string} [options.id="confirmMessageBox"]  id for this box (kept separate from
 *                                                   openMessageBox's id so a result message
 *                                                   can open while this one is still closing)
 * @param {string} [options.title="Confirmation"]    header caption
 * @param {string} options.text                      body content
 * @param {boolean} [options.asHtml=false]           treat text as HTML
 * @param {string} [options.type="warning"]          info | success | warning | error
 * @param {string} [options.icon="warning"]          arrow | check | warning | error | none
 * @param {string} [options.confirmText="Yes"]       confirm button label, e.g. "Delete"
 * @param {string} [options.cancelText="Cancel"]     cancel button label
 * @param {boolean} [options.danger=false]           red confirm button (for delete)
 * @param {Function} options.onConfirm              runs when the confirm button is pressed
 * @param {Function} [options.onCancel]              runs when the box is closed any other way
 *
 * @example
 * confirmMessageBox({
 *     title: "Delete",
 *     text: "Are you sure you want to delete this record?",
 *     confirmText: "Delete",
 *     danger: true,
 *     onConfirm: function () { ... }
 * });
 */
function confirmMessageBox(options) {
    options = options || {};
    var id = options.id || 'confirmMessageBox';
    var title = options.title || 'Confirmation';
    var type = options.type || 'warning';
    var icon = options.icon || 'warning';
    var confirmText = options.confirmText || 'Yes';
    var cancelText = options.cancelText || 'Cancel';

    var icons = {
        arrow: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M5 12h14M13 6l6 6-6 6" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        check: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M20 6L9 17l-5-5" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        warning: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        error: '<svg viewBox="0 0 24 24" class="mb-icon-svg"><circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2"/><path d="M15 9l-6 6M9 9l6 6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
        none: ''
    };
    var iconSvg = icons[icon] !== undefined ? icons[icon] : icons.warning;

    var el = document.getElementById(id);
    if (!el) {
        el = document.createElement('div');
        el.id = id;
        document.body.appendChild(el);
    }
    el.setAttribute('role', 'alertdialog');
    el.setAttribute('aria-modal', 'true');
    el.setAttribute('aria-labelledby', id + '_title');
    el.className = 'mb-overlay mb-type-' + type;

    el.innerHTML =
        '<div class="mb-dialog">' +
        '<div class="mb-header">' +
        '<div class="mb-header-left">' +
        (icon !== 'none' ? '<span class="mb-icon">' + iconSvg + '</span>' : '') +
        '<span id="' + id + '_title" class="mb-title"></span>' +
        '</div>' +
        '<button type="button" class="mb-close-btn" aria-label="Close"><span>&times;</span></button>' +
        '</div>' +
        '<div class="mb-body"></div>' +
        '<div class="mb-footer">' +
        '<button type="button" class="mb-cancel-btn"></button>' +
        '<button type="button" class="mb-ok-btn' + (options.danger ? ' mb-danger-btn' : '') + '"></button>' +
        '</div>' +
        '</div>';

    el.querySelector('.mb-title').textContent = title;
    var bodyEl = el.querySelector('.mb-body');
    if (options.asHtml) bodyEl.innerHTML = options.text || '';
    else bodyEl.textContent = options.text || '';
    var cancelBtn = el.querySelector('.mb-cancel-btn');
    var okBtn = el.querySelector('.mb-ok-btn');
    cancelBtn.textContent = cancelText;
    okBtn.textContent = confirmText;

    var decided = false; // makes sure only one of confirm / cancel ever runs

    function cancel() {
        if (decided) return;
        decided = true;
        hideMessageBox(id);
        if (typeof options.onCancel === 'function') options.onCancel();
    }

    okBtn.onclick = function () {
        if (decided) return;
        decided = true;
        hideMessageBox(id);
        if (typeof options.onConfirm === 'function') options.onConfirm();
    };
    cancelBtn.onclick = cancel;
    el.querySelector('.mb-close-btn').onclick = cancel;

    showMessageBox(id);

    // Escape and a click outside also count as Cancel (showMessageBox closes the box for those)
    var observer = new MutationObserver(function () {
        if (el.classList.contains('mb-closing') && !decided) {
            decided = true;
            if (typeof options.onCancel === 'function') options.onCancel();
        }
        if (!el.classList.contains('mb-visible')) observer.disconnect();
    });
    observer.observe(el, { attributes: true, attributeFilter: ['class'] });

    // Start with Cancel focused, so pressing Enter by accident does not delete
    setTimeout(function () { cancelBtn.focus(); }, 50);
    return id;
}

/* Auto-wire elements marked to open on page load (data-auto-show="true"
   is set by the TagHelper's AutoShow property via mb-visible class already,
   but this guards against any timing issues with dynamically inserted markup). */
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.mb-overlay.mb-visible').forEach(function (el) {
        document.body.style.overflow = 'hidden';
    });
});