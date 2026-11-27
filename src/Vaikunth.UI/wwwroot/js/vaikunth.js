/* Vaikunth Office · small shared behaviour for every page: ribbon tabs, menus, dialogs, password eye,
   toast, zoom, and a save helper the three editors use. Exposed as window.VK. */
(function () {
    "use strict";
    var VK = window.VK = window.VK || {};

    // ---- password show / hide (vk-input is-password) ----
    document.addEventListener("click", function (e) {
        var eye = e.target.closest("[data-vk-eye]");
        if (!eye) return;
        var input = eye.parentElement.querySelector("input");
        var show = input.type === "password";
        input.type = show ? "text" : "password";
        eye.querySelector("i").className = show ? "fa fa-eye-slash" : "fa fa-eye";
    });

    // ---- ribbon tabs (vk-ribbon) ----
    document.addEventListener("click", function (e) {
        var tab = e.target.closest(".vk-ribbon-tab");
        if (!tab) return;
        var ribbon = tab.closest(".vk-ribbon");
        ribbon.querySelectorAll(".vk-ribbon-tab").forEach(function (t) { t.classList.toggle("active", t === tab); });
        ribbon.querySelectorAll(".vk-ribbon-panel").forEach(function (p) { p.hidden = p.getAttribute("data-panel") !== tab.getAttribute("data-tab"); });
    });

    // ---- menus and dialogs ----
    document.addEventListener("click", function (e) {
        var toggle = e.target.closest("[data-toggle-menu]");
        document.querySelectorAll(".vk-menu-pop").forEach(function (m) {
            if (!toggle || m.parentElement !== toggle.parentElement) m.hidden = true;
        });
        if (toggle) { var pop = toggle.parentElement.querySelector(".vk-menu-pop"); pop.hidden = !pop.hidden; }

        var opener = e.target.closest("[data-open-dialog]");
        if (opener) { var d = document.getElementById(opener.getAttribute("data-open-dialog")); if (d) { closeMenus(); d.showModal(); } }

        // close buttons of a window: slide it down first (they post nothing)
        var closer = e.target.closest(".vk-dialog [data-close], .vk-dialog button[value='cancel']");
        if (closer) { e.preventDefault(); VK.closeDialog(closer.closest("dialog")); }
    });
    function closeMenus() { document.querySelectorAll(".vk-menu-pop").forEach(function (m) { m.hidden = true; }); }

    // ---- glass windows: slide down when they close (× / Close / Esc) ----
    VK.closeDialog = function (d) {
        if (!d || !d.open || d.classList.contains("vk-closing")) return;
        if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) { d.close(); return; }
        d.classList.add("vk-closing");
        setTimeout(function () { d.classList.remove("vk-closing"); d.close(); }, 190);
    };
    document.addEventListener("cancel", function (e) {
        if (e.target.classList && e.target.classList.contains("vk-dialog")) { e.preventDefault(); VK.closeDialog(e.target); }
    }, true);

    // ---- profile photo: crop to a square and shrink to 256 × 256 in the browser, so only a few KB is stored ----
    document.addEventListener("change", function (e) {
        if (e.target.id !== "photoFile" || !e.target.files || !e.target.files[0]) return;
        var input = e.target, file = input.files[0];
        var img = new Image();
        img.onload = function () {
            var side = Math.min(img.naturalWidth, img.naturalHeight), size = 256;
            var c = document.createElement("canvas"); c.width = size; c.height = size;
            c.getContext("2d").drawImage(img, (img.naturalWidth - side) / 2, (img.naturalHeight - side) / 2, side, side, 0, 0, size, size);
            c.toBlob(function (blob) {
                if (!blob) return;
                try {
                    var dt = new DataTransfer();
                    dt.items.add(new File([blob], "photo.jpg", { type: "image/jpeg" }));
                    input.files = dt.files;
                } catch (x) { /* older browsers: the original file is sent and the server checks its size */ }
                var prev = document.getElementById("photoPreview");
                if (prev) prev.innerHTML = '<img alt="" src="' + URL.createObjectURL(blob) + '">';
            }, "image/jpeg", 0.9);
            URL.revokeObjectURL(img.src);
        };
        img.src = URL.createObjectURL(file);
    });
    // a window with a message to show (e.g. "That file isn't a picture") opens by itself after the page loads
    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-open-on-load]").forEach(function (el) { var d = el.closest("dialog"); if (d && !d.open) d.showModal(); });
    });

    // ---- toast ----
    var toastTimer;
    VK.toast = function (text) {
        var t = document.getElementById("toast");
        if (!t) return;
        t.textContent = text;
        t.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.hidden = true; }, Math.max(2600, Math.min(8000, text.length * 55)));
    };

    // ---- commands: every [data-cmd] button / select / colour goes to VK.onCommand(cmd, arg, el) ----
    VK.handlers = {};
    VK.on = function (cmd, fn) { VK.handlers[cmd] = fn; };
    function run(cmd, arg, el) {
        var fn = VK.handlers[cmd];
        if (fn) fn(arg, el);
        else if (VK.fallback) VK.fallback(cmd, arg, el);
    }
    document.addEventListener("mousedown", function (e) {
        // keep the text selection in the editor while clicking ribbon buttons
        if (e.target.closest(".vk-ribbon button, .vk-rb")) e.preventDefault();
    });
    document.addEventListener("click", function (e) {
        var b = e.target.closest("button[data-cmd]");
        if (!b || b.tagName !== "BUTTON") return;
        run(b.getAttribute("data-cmd"), b.getAttribute("data-arg"), b);
    });
    document.addEventListener("change", function (e) {
        var el = e.target;
        if (el.matches("select[data-cmd]")) run(el.getAttribute("data-cmd"), el.value, el);
        if (el.matches("input[type=color][data-color-for]")) {
            var wrap = el.closest(".vk-rcolor"), btn = wrap.querySelector("button[data-cmd]");
            wrap.style.setProperty("--swatch", el.value);
            btn.setAttribute("data-arg", el.value);
            run(btn.getAttribute("data-cmd"), el.value, btn);
        }
    });
    document.addEventListener("input", function (e) {
        if (e.target.matches("input[type=range][data-cmd=zoom]")) VK.setZoom(+e.target.value);
    });

    // ---- zoom (vk-status-bar) ----
    VK.zoom = 100;
    VK.setZoom = function (z) {
        VK.zoom = Math.max(50, Math.min(200, z));
        var r = document.querySelector("input[type=range][data-cmd=zoom]"); if (r) r.value = VK.zoom;
        var v = document.getElementById("zoomVal"); if (v) v.textContent = VK.zoom + "%";
        if (VK.onZoom) VK.onZoom(VK.zoom / 100);
    };
    VK.on("zoomIn", function () { VK.setZoom(VK.zoom + 10); });
    VK.on("zoomOut", function () { VK.setZoom(VK.zoom - 10); });
    VK.on("share", function () { var d = document.getElementById("shareDialog"); if (d) d.showModal(); });
    VK.on("shareSend", function () { VK.closeDialog(document.getElementById("shareDialog")); VK.toast("Shared. They'll get an email with the link."); });
    VK.on("fileMenu", function () { var d = document.getElementById("fileDialog"); if (d) d.showModal(); });
    VK.on("fileSave", function () {
        if (!VK.getContent) return;
        var p = VK.save(VK.getContent());
        if (p) p.then(function (res) {
            if (res && res.ok) { VK.closeDialog(document.getElementById("fileDialog")); VK.toast("Saved on the server at " + res.savedAt + "."); }
            else VK.toast((res && res.error) || "Not saved. Check your connection and try again.");
        });
    });
    VK.on("fileExport", function (arg, btn) { VK.exportFile(btn); });

    // ---- download a copy for Microsoft Office (.docx / .xlsx / .pptx) ----
    // An editor can set VK.exportLocal(fileName) to build the file in the browser (Slides does);
    // otherwise the server turns the current content into the Office file.
    VK.getContent = null;
    VK.exportLocal = null;
    VK.fileName = function () {
        var t = document.getElementById("docTitle"), name = ((t && t.value) || "Untitled").replace(/[\\/:*?"<>|]+/g, " ").trim() || "Untitled";
        return name + (document.body.dataset.ext || "");
    };
    VK.download = function (blob, name) {
        var a = document.createElement("a");
        a.href = URL.createObjectURL(blob); a.download = name;
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 10000);
    };
    VK.loadScript = function (src) {
        return new Promise(function (ok, fail) {
            if (document.querySelector('script[src="' + src + '"]')) return ok();
            var s = document.createElement("script"); s.src = src; s.onload = ok; s.onerror = fail; document.head.appendChild(s);
        });
    };
    var exporting = false;
    VK.exportFile = function (btn) {
        if (!VK.getContent || exporting) return;
        exporting = true;
        var name = VK.fileName();
        VK.toast("Preparing " + name + "…");
        var done = function () { exporting = false; VK.closeDialog(document.getElementById("fileDialog")); };
        var job = VK.exportLocal ? Promise.resolve(VK.exportLocal(name)) : (function () {
            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            return fetch("/" + (document.body.dataset.app || "pages") + "/editor/export", {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token ? token.value : "" },
                body: JSON.stringify({ title: name.replace(/\.[a-z]+$/, ""), content: VK.getContent() })
            }).then(function (r) {
                if (!r.ok) return r.text().then(function (t) { throw new Error(t || ("The server said " + r.status)); });
                return r.blob();
            }).then(function (blob) { VK.download(blob, name); });
        })();
        return job.then(function () { done(); VK.toast("Downloaded " + name + "."); })
            .catch(function (e) { done(); VK.toast("Couldn't make the file: " + (e && e.message ? e.message : e)); });
    };

    // ---- saving (the three editors call VK.save(getContent)) ----
    VK.docId = null;
    VK.save = function (content) {
        var state = document.getElementById("saveState"), title = document.getElementById("docTitle");
        if (state) state.innerHTML = '<i class="fa fa-refresh fa-spin"></i> Saving…';
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        return fetch("/" + (document.body.dataset.app || "pages") + "/editor/save", {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token ? token.value : "" },
            body: JSON.stringify({ documentId: VK.docId, title: title ? title.value : "Untitled", content: content })
        }).then(function (r) { return r.json(); }).then(function (res) {
            if (state) state.innerHTML = res.ok ? '<i class="fa fa-cloud"></i> Saved at ' + res.savedAt : '<i class="fa fa-exclamation-triangle"></i> Not saved';
            if (res.ok) VK.docId = res.id;
            return res;
        }).catch(function () { if (state) state.innerHTML = '<i class="fa fa-exclamation-triangle"></i> Offline'; });
    };
    // Autosave a little after the last change.
    var dirtyTimer;
    VK.changed = function (getContent) {
        var state = document.getElementById("saveState");
        if (state) state.innerHTML = '<i class="fa fa-pencil"></i> Editing';
        clearTimeout(dirtyTimer);
        dirtyTimer = setTimeout(function () { VK.save(getContent()); }, 1500);
    };
})();
