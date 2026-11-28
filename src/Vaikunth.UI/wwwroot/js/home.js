/* Vaikunth Desk · Home (/): the greeting, the apps and Recent files. Pick a file to open, download, rename, star or
   delete it (double-click opens it). Rename, star and delete go through js/ajax-panel.js, so the page never reloads. */
window.VKHome = (function ($) {
    "use strict";
    var VK = window.VK;
    function el(id) { return document.getElementById(id); }
    function set(id, t) { var e = el(id); if (e) e.textContent = t == null ? "" : t; }
    function api() { return $("#recentGrid").data("mt-api"); }
    function current() { var a = api(); return a ? a.getSelected() : null; }
    function need() { var f = current(); if (!f) VK.toast("Pick a file in Recent files first."); return f; }
    function esc(t) { var d = document.createElement("div"); d.textContent = t == null ? "" : String(t); return d.innerHTML; }
    function post(action, fields) { if (window.VKPanel) window.VKPanel.post(action, fields); }

    function clock() {
        var now = new Date(), h = now.getHours();
        set("hubGreeting", h < 5 ? "Working late" : h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening");
        set("hubDate", now.toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long", year: "numeric" }));
    }

    function fill(f) {
        var bar = el("fileBar"); if (!bar) return;
        bar.setAttribute("data-state", f ? "selected" : "empty");
        var ic = el("fbIcon");
        if (!f) {
            set("fbName", "Your files in the cloud");
            set("fbSub", "Pick a file in the list below to open, download, rename, star or delete it. Double-click opens it.");
            ic.innerHTML = '<i class="fa fa-folder-open-o" aria-hidden="true"></i>';
            ic.style.removeProperty("--app");
            return;
        }
        ic.innerHTML = '<img src="' + esc(f.Icon) + '" alt="" />';
        ic.style.setProperty("--app", f.color);
        set("fbName", f.title);
        set("fbSub", f.AppName + " · " + f.Owner + " · changed " + f.Modified.toLowerCase() + " · downloads as " + f.ext);
        document.querySelectorAll('.vk-abtn[data-cmd="fileStar"]').forEach(function (b) {
            b.querySelector("span").textContent = f.starred ? "Unstar" : "Star";
            b.querySelector("i").className = "fa " + (f.starred ? "fa-star" : "fa-star-o");
        });
    }

    VK.on("fileOpen", function () { var f = need(); if (f) window.open(f.url, "_blank"); });
    VK.on("fileDownload", function () {
        var f = need(); if (!f) return;
        VK.toast("Preparing " + f.title + f.ext + "…", "info", { duration: 2500 });
        fetch(f.download, { credentials: "same-origin" }).then(function (r) {
            // Slides builds its file in the browser: the server sends us to the editor, which downloads it at once
            if (r.redirected && /\/editor\?/.test(r.url)) { window.open(r.url, "_blank"); return null; }
            if (!r.ok) return r.text().then(function (t) { throw new Error(t || ("The server said " + r.status)); });
            return r.blob().then(function (blob) {
                var cd = r.headers.get("content-disposition") || "", m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd);
                var name = m ? decodeURIComponent(m[1] || m[2]) : f.title + f.ext;
                VK.download(blob, name);
                VK.toast("Downloaded " + name + ".", "success");
            });
        }).catch(function (e) { VK.toast("Couldn't download it: " + (e && e.message ? e.message : e), "error"); });
    });
    VK.on("fileRename", function () {
        var f = need(); if (!f) return;
        el("renameId").value = f.Id;
        setModernInputValue("#renameTitle", f.title);
        set("renameNote", f.AppName + " file · it keeps its place, its sharing and its history.");
        var d = el("renameDialog"); if (d && !d.open) d.showModal();
        setTimeout(function () { var i = el("renameTitle"); if (i) { i.focus(); i.select(); } }, 60);
    });
    VK.on("fileStar", function () {
        var f = need(); if (!f) return;
        post("/files/star", { documentId: f.Id, starred: f.starred ? "false" : "true", title: f.title });
    });
    VK.on("fileDelete", function () {
        var f = need(); if (!f) return;
        confirmMessageBox({
            title: "Delete " + f.title + "?", type: "error", icon: "warning", danger: true,
            confirmText: "Delete", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'><b>" + esc(f.title) + "</b> (" + esc(f.AppName) + ") goes out of everyone's list.</p>" +
                "<p class='vk-mb-sub'>Download it first if you may need it. Your admin can still bring it back.</p>",
            onConfirm: function () { post("/files/delete", { documentId: f.Id, title: f.title }); }
        });
    });

    var listening = false;
    function init() {
        clock();
        if (!listening) {
            listening = true;
            document.addEventListener("vk:panel", function () { if (document.getElementById("recentGrid")) init(); });
            // double-click a file: open it
            $(document).on("dblclick", "#recentGrid .mt-tbody tr", function () { var f = current(); if (f) window.open(f.url, "_blank"); });
        }
        $("#recentGrid").on("table:select", function (e, row) { fill(row || null); });
        $("#renameDialog").off("submit.vkhome").on("submit.vkhome", "form", function (e) {
            if (!$("#renameTitle").val().trim()) { e.preventDefault(); VK.toast("Type a name for the file.", "warning"); }
        });
        fill(current());
    }
    return { init: init };
})(window.jQuery);
