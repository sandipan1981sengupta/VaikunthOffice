/* Vaikunth Desk · the client admin's People page (/team): the people grid and its details pane, the toolbar and the
   Add / Edit windows. The list and the fields are the ShankhNaad controls (modern-table, modern-input,
   modern-dropdown-with-label, modern-datepicker-with-label). Saves go through js/ajax-panel.js, so the page never reloads. */
window.VKTeam = (function ($) {
    "use strict";
    var VK = window.VK;
    var cfg = { selected: null, company: "" };
    function el(id) { return document.getElementById(id); }
    function set(id, text) { var e = el(id); if (e) e.textContent = text == null ? "" : text; }
    function api() { return $("#peopleGrid").data("mt-api"); }
    function current() { var a = api(); return a ? a.getSelected() : null; }
    function need() { var p = current(); if (!p) VK.toast("Select a person in the list first."); return p; }
    function esc(t) { var d = document.createElement("div"); d.textContent = t == null ? "" : String(t); return d.innerHTML; }
    function open(id) { var d = el(id); if (d && !d.open) d.showModal(); }
    function post(action, fields) { if (window.VKPanel) window.VKPanel.post(action, fields); }
    function label(cmd, text, icon) {
        document.querySelectorAll('[data-cmd="' + cmd + '"]').forEach(function (b) {
            var s = b.querySelector("span"); if (s) s.textContent = text;
            var i = b.querySelector("i"); if (i && icon) i.className = "fa " + icon;
        });
    }

    // ---------- details pane ----------
    function fill(p) {
        var box = el("personInspector");
        if (!box) return;
        box.setAttribute("data-state", p ? "selected" : "empty");
        if (!p) return;
        var face = el("pFace");
        face.className = "vk-avatar vk-team-face tone-" + (p.tone || 0);
        face.textContent = "";
        if (p.photo) { var img = document.createElement("img"); img.src = p.photo; img.alt = ""; face.appendChild(img); }
        else face.textContent = p.initials;
        set("pName", p.name + (p.me ? " (you)" : ""));
        el("pEmail").querySelector("span").textContent = p.Email;
        var st = el("pStatus");
        st.textContent = !p.active ? "Switched off" : p.invitedState ? "Invited" : "Active";
        st.className = "vk-badge " + (!p.active ? "off" : p.invitedState ? "invited" : "ok");
        set("pRole", p.admin ? "Admin" : "Member");
        set("pRoleNote", p.admin ? "Uses the apps, and can add and manage people." : "Uses the apps given to the company.");
        set("pDept", p.Department || "Not set");
        set("pJob", p.JobTitle || "Not set");
        set("pJoined", p.joinedText);
        set("pLast", p.lastText);
        set("pAdded", p.addedText);

        label("sendLink", !p.active ? "Send link" : p.invitedState ? "Resend invite" : "Reset password", p.invitedState ? "fa-paper-plane" : "fa-key");
        label("toggleRole", p.admin ? "Make member" : "Make admin", "fa-id-badge");
        label("toggleActive", p.active ? "Switch off" : "Switch on", p.active ? "fa-ban" : "fa-check-circle");
        el("pLinkBtn").disabled = !p.active;
        el("pRoleBtn").disabled = !!p.me;
        el("pActiveBtn").disabled = !!p.me;
        el("pActiveBtn").classList.toggle("vk-danger-btn", !!p.active);
        el("pSelfNote").hidden = !p.me;
    }

    // ---------- the toolbar ----------
    VK.on("refresh", function () {
        if (window.VKPanel) window.VKPanel.reload().then(function () { VK.toast("Up to date.", "success", { duration: 1800 }); });
        else location.reload();
    });
    VK.on("addPerson", function () {
        open("inviteDialog");
        setTimeout(function () { var n = el("inviteName"); if (n) n.focus(); }, 40);
    });
    VK.on("editPerson", function () {
        var p = need(); if (!p) return;
        el("editPersonId").value = p.Id;
        set("editPersonEmail", "Signs in as " + p.Email);
        setModernInputValue("#editPersonName", p.name);
        setModernInputValue("#editPersonDept", p.Department || "");
        setModernInputValue("#editPersonJob", p.JobTitle || "");
        var d = el("editPersonJoined");
        if (d && d._flatpickr) { if (p.joined) d._flatpickr.setDate(p.joined, true, "d-m-Y"); else d._flatpickr.clear(); }
        else if (d) d.value = p.joined || "";
        open("personDialog");
    });
    VK.on("sendLink", function () {
        var p = need(); if (!p) return;
        if (!p.active) { VK.toast("Switch " + p.name + " on first, then send the link.", "warning"); return; }
        var invite = p.invitedState;
        confirmMessageBox({
            title: invite ? "Send the invite again?" : "Send a password reset link?", type: "info", icon: "arrow",
            confirmText: invite ? "Send invite" : "Send link", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'>" + (invite
                ? "A new invite goes to <b>" + esc(p.Email) + "</b>. The old link stops working."
                : "<b>" + esc(p.name) + "</b> gets an email at <b>" + esc(p.Email) + "</b> with a link to choose a new password.") + "</p>" +
                "<p class='vk-mb-sub'>The link works once and lasts " + (invite ? "7 days" : "2 hours") + ".</p>",
            onConfirm: function () { post("/team/resend", { userId: p.Id }); }
        });
    });
    VK.on("toggleRole", function () {
        var p = need(); if (!p) return;
        if (p.me) { VK.toast("Ask another admin to change your own role.", "warning"); return; }
        var toAdmin = !p.admin;
        confirmMessageBox({
            title: toAdmin ? "Make " + p.name + " an admin?" : "Make " + p.name + " a member?", type: "warning", icon: "arrow",
            confirmText: toAdmin ? "Make admin" : "Make member", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'>" + (toAdmin
                ? "Admins can add people, change roles and switch people off, like you."
                : esc(p.name) + " keeps using the apps but can no longer manage people.") + "</p>",
            onConfirm: function () { post("/team/role", { userId: p.Id, role: toAdmin ? "TenantAdmin" : "Member", name: p.name }); }
        });
    });
    VK.on("toggleActive", function () {
        var p = need(); if (!p) return;
        if (p.me) { VK.toast("You can't switch yourself off.", "warning"); return; }
        if (!p.active) { post("/team/active", { userId: p.Id, isActive: "true", name: p.name }); return; }
        confirmMessageBox({
            title: "Switch " + p.name + " off?", type: "error", icon: "warning", danger: true,
            confirmText: "Switch off", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'>They can't sign in until you switch them on again.</p>" +
                "<ul class='vk-mb-list'><li>Their files stay</li><li>If they are signed in now, they are signed out</li><li>Their license becomes free</li></ul>",
            onConfirm: function () { post("/team/active", { userId: p.Id, isActive: "false", name: p.name }); }
        });
    });

    // Export: the list as the person sees it (search, filters, visible columns)
    VK.on("exportGrid", function () {
        var a = api(); if (!a) return;
        var rows = a.getViewRows ? a.getViewRows() : a.getAllRows();
        var cols = [["name", "Name"], ["Email", "Email"], ["Department", "Department"], ["JobTitle", "Job title"], ["Role", "Role"],
            ["Status", "Status"], ["joinedText", "Joined"], ["lastText", "Last sign-in"]];
        function q(v) { v = v == null ? "" : String(v); return /[",\r\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v; }
        var lines = [cols.map(function (c) { return q(c[1]); }).join(",")].concat(rows.map(function (r) { return cols.map(function (c) { return q(r[c[0]]); }).join(","); }));
        var blob = new Blob(["﻿" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
        var link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = "vaikunth-people.csv";
        document.body.appendChild(link); link.click(); link.remove();
        setTimeout(function () { URL.revokeObjectURL(link.href); }, 2000);
        VK.toast(rows.length + (rows.length === 1 ? " person" : " people") + " exported.");
    });
    VK.on("print", function () { window.print(); });

    // ---------- the windows: check before sending ----------
    function wireForms() {
        $("#inviteDialog").off("submit.vkteam").on("submit.vkteam", "form", check);
        $("#personDialog").off("submit.vkteam").on("submit.vkteam", "form", check);
    }
    function check(e) {
        var f = e.target;
        var name = f.querySelector('[name="displayName"]');
        var mail = f.querySelector('[name="email"]');
        if (name && !name.value.trim()) { e.preventDefault(); VK.toast("Type their name.", "warning"); name.focus(); return; }
        if (mail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(mail.value.trim())) { e.preventDefault(); VK.toast("Check the work email address.", "warning"); mail.focus(); }
    }

    var listening = false;
    function init(options) {
        if (!options) {
            var json = el("teamCfg");
            try { options = json ? JSON.parse(json.textContent) : {}; } catch (e) { options = {}; }
        }
        for (var k in options) cfg[k] = options[k];
        if (!listening) {
            listening = true;
            document.addEventListener("vk:panel", function () { if (document.getElementById("teamCfg")) init(); });
        }
        $("#peopleGrid").on("table:select", function (e, row) { fill(row); });
        var g = api();
        if (g) g.setRowClassCallback(function (r) { return r.active ? (r.invitedState ? "vk-row-late" : "") : "vk-row-paused"; });
        var mailBtn = el("pEmail");
        if (mailBtn) mailBtn.addEventListener("click", function () {
            var p = current(); if (!p || !navigator.clipboard) return;
            navigator.clipboard.writeText(p.Email).then(function () { VK.toast("Copied " + p.Email + "."); }, function () { });
        });
        wireForms();
        if (g && cfg.selected && g.selectById(cfg.selected)) { /* the pane is filled by table:select */ }
        else fill(null);
    }

    return { init: init, fill: fill };
})(window.jQuery);
