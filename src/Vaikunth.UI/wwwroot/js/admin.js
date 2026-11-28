/* Vaikunth Desk · super admin Clients page: the clients grid and its details pane, the toolbar, money (payments,
   let in again) and the client windows. The lists and fields are the ShankhNaad (Template 1) controls:
   modern-table, modern-input, modern-dropdown-with-label, modern-datepicker-with-label, modern-month-year-picker,
   modern-radio-group and modern-checkbox. Loaded after jQuery, the controls, vaikunth.js and workspace.js. */
window.VKAdmin = (function ($) {
    "use strict";
    var VK = window.VK;
    var cfg = { perPlan: {}, appNames: [], selected: null, thisMonth: "", graceDays: 30, autoBlock: true };
    function el(id) { return document.getElementById(id); }
    function set(id, text) { var e = el(id); if (e) e.textContent = text == null ? "" : text; }
    function api(id) { return $("#" + id).data("mt-api"); }
    function rowsOf(id) { var a = api(id); return a ? a.getAllRows() : []; }
    function clientById(id) { return rowsOf("clientGrid").filter(function (r) { return String(r.Id) === String(id); })[0] || null; }
    function current() { var a = api("clientGrid"); return a ? a.getSelected() : null; }
    /** The client the toolbar works on: the selected row of the open tab's list. */
    function target() {
        if (VK.tab === "payments") {
            var b = api("billGrid"); var r = b && b.getSelected();
            if (r) return clientById(r.Id);
        }
        return current();
    }
    function need() {
        var c = target();
        if (!c) {
            VK.toast(VK.tab === "payments" ? "Select a client in Who has paid first." : "Select a client first.");
            if (VK.tab === "plans" && VK.openTab) VK.openTab("clients");
        }
        return c;
    }
    function files(c) { return c.docs + c.sheets + c.decks + (c.pdfs || 0) + (c.projects || 0); }
    function width(e, pct) { if (e) e.style.width = Math.max(0, Math.min(100, pct)) + "%"; }
    function rupees(n) { return "₹" + Number(n || 0).toLocaleString("en-IN", { maximumFractionDigits: 2 }); }
    function open(id) { var d = el(id); if (d && !d.open) d.showModal(); }

    // ---------- details pane ----------
    function fill(c) {
        var box = el("clientInspector");
        if (!box) return;
        box.setAttribute("data-state", c ? "selected" : "empty");
        if (!c) return;
        set("selIni", c.initials);
        set("selName", c.Name);
        el("selSlug").querySelector("span").textContent = c.Slug;
        var st = el("selStatus");
        st.textContent = c.active ? "Active" : "Paused";
        st.className = "vk-badge " + (c.active ? "ok" : "paused");

        // the bill
        var bb = el("selBillBox");
        bb.setAttribute("data-bill", c.billState);
        set("selMonthly", c.free ? "Free" : c.monthlyText + " a month");
        set("selBillState", c.billText);
        set("selBillMath", c.billMath);
        set("selBillLine", c.billLine);
        set("selOutstanding", c.outstanding > 0 ? c.outstandingText + " (" + c.unpaid.join(", ") + ")" : "Nothing");
        set("selPaidThis", c.paidThisMonth);
        set("selLastPaid", c.lastPaid);
        set("selBillFrom", c.free ? "Free plan" : c.billingFromText);
        el("selBillBtns").hidden = !!c.free;
        el("selLetIn").hidden = !(c.billState === "overdue" || c.billState === "blocked" || c.billState === "onhold");

        set("selUsers", c.users + " / " + c.maxUsers);
        var ub = el("selUsersBar");
        width(ub, c.usersPct);
        ub.className = c.usersPct >= 100 ? "full" : c.usersPct >= 80 ? "warn" : "";
        set("selStoragePct", c.storageText);
        var sb = el("selStorageBar");
        width(sb, c.storagePct > 0 ? Math.max(2, c.storagePct) : 0);
        sb.className = c.storageLevel || "";
        set("selStorage", c.storage);
        set("selStorageFree", c.storageFree);
        set("selQuota", c.quota);

        set("selPlan", c.Plan + (c.free ? " (free)" : " · " + rupees(c.price) + " a license"));
        set("selApps", cfg.appNames.filter(function (a) { return (c.apps & a.flag) !== 0; }).map(function (a) { return a.name; }).join(", ") || "None");
        set("selFiles", files(c) + " (" + c.docs + " documents, " + c.decks + " decks, " + c.sheets + " workbooks, " + c.pdfs + " PDFs, " + c.projects + " projects)");
        var sa = el("selAdmin");
        if (sa) {
            sa.textContent = "";
            if (!c.admin) sa.textContent = "None";
            else {
                var nm = document.createElement("b"); nm.textContent = c.admin + (c.adminPending ? " (invited)" : "");
                var em = document.createElement("small"); em.textContent = c.adminEmail || ""; em.title = c.adminEmail || "";
                sa.appendChild(nm); sa.appendChild(em);
            }
        }
        set("selSince", c.since);

        el("planTenant").value = c.Id;
        if (window.setModernDropdownWithLabelValue) setModernDropdownWithLabelValue("#selPlanPick", String(c.planId));
        set("selMailText", c.adminPending ? "Email welcome link again" : "Email admin a password link");

        var tg = el("selToggle");
        tg.querySelector("i").className = "fa " + (c.active ? "fa-pause-circle" : "fa-play-circle");
        tg.querySelector("span").textContent = c.active ? "Pause client" : "Resume client";
        el("selDelete").disabled = c.active;
        el("selDeleteNote").hidden = !c.active;
        document.querySelectorAll('.vk-abtn[data-cmd="toggleClient"] span').forEach(function (s) { s.textContent = c.active ? "Pause" : "Resume"; });
    }

    // ---------- the toolbar ----------
    /** Toolbar actions post without reloading the page (js/ajax-panel.js); the answer comes as a toast. */
    function post(action, fields) {
        if (window.VKPanel) { window.VKPanel.post(action, fields); return; }
        var f = el("actForm");
        f.action = action;
        el("actTenant").value = fields.tenantId || "";
        el("actName").value = fields.name || "";
        el("actActive").value = fields.isActive == null ? "" : String(fields.isActive);
        f.submit();
    }
    VK.on("refresh", function () {
        if (window.VKPanel) { window.VKPanel.reload().then(function () { VK.toast("Up to date.", "success", { duration: 1800 }); }); return; }
        var u = new URL(location.href);
        u.searchParams.delete("q");
        location.href = u.toString();
    });
    VK.on("openPayments", function () { VK.openTab("payments"); });
    VK.on("newClient", function () { open("newClient"); setTimeout(function () { el("Name").focus(); }, 30); });
    VK.on("editClient", function () {
        var c = current();
        if (!c) { VK.toast("Select a client first."); return; }
        el("EditTenantId").value = c.Id;
        setModernInputValue("#EditName", c.Name);
        setModernDropdownWithLabelValue("#EditPlanId", String(c.planId));
        setModernInputValue("#EditMaxUsers", c.maxUsers);
        var st = el("EditStorage");
        setModernInputValue("#EditStorage", c.storageOverrideMb == null ? "" : c.storageOverrideMb);
        st.placeholder = "Now " + c.quotaMb + " MB from the plan";
        cfg.appNames.forEach(function (a) {
            var box = document.querySelector('#editClient input[name="apps"][value="' + a.flag + '"]');
            if (box) box.checked = (c.apps & a.flag) !== 0;
        });
        if (c.billingFrom) setModernMonthYearPickerValue("#EditBillingMonth", c.billingFrom);
        set("editBillNote", c.free ? "Free plan: never billed. The month counts if the client moves to a paid plan."
            : c.billMath + ". Months before this one are never billed.");
        setModernCheckboxValue("#EditActive", c.active);
        var ag = el("editAdminGroup"), hasAdmin = !!c.adminEmail;
        if (ag) {
            ag.hidden = !hasAdmin;
            ag.querySelectorAll("input").forEach(function (i) { i.disabled = !hasAdmin; });
            if (hasAdmin) {
                setModernInputValue("#EditAdminName", c.admin || "");
                setModernInputValue("#EditAdminEmail", c.adminEmail || "");
                set("editAdminNote", c.adminPending
                    ? "Not signed in yet. If you correct the email, a new welcome email goes to the new address and the old link stops working."
                    : "They sign in with this email. If you change it, tell them: their password stays the same.");
            }
        }
        open("editClient");
    });
    function esc(t) { var d = document.createElement("div"); d.textContent = t == null ? "" : String(t); return d.innerHTML; }
    // Delete: ShankhNaad's message box. A client also needs its short name typed, because it can't be undone.
    VK.on("deleteClient", function () {
        var c = current();
        if (!c) { VK.toast("Select a client first.", "info"); return; }
        if (c.active) { VK.toast("Pause " + c.Name + " first. Only a paused client can be deleted.", "warning"); return; }
        confirmMessageBox({
            title: "Delete " + c.Name + "?", type: "error", icon: "warning", danger: true,
            confirmText: "Delete forever", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'>This can't be undone. These go for good:</p>" +
                "<ul class='vk-mb-list'><li><b>" + esc(c.users) + "</b> " + (c.users === 1 ? "person" : "people") + " and their sign-ins</li>" +
                "<li><b>" + esc(files(c)) + "</b> files (" + esc(c.storage) + ")</li><li>Its payments and its folder on the server</li></ul>" +
                "<label class='vk-mb-label' for='mbDelSlug'>Type <b>" + esc(c.Slug) + "</b> to confirm</label>" +
                "<input id='mbDelSlug' class='vk-mb-input' autocomplete='off' spellcheck='false' />",
            onConfirm: function () {
                var typed = (document.getElementById("mbDelSlug") || {}).value || "";
                if (typed.trim().toLowerCase() !== String(c.Slug).toLowerCase()) {
                    VK.toast("The short name didn't match, so nothing was deleted.", "warning");
                    return;
                }
                post("/admin/delete", { tenantId: c.Id, confirmSlug: typed.trim() });
            }
        });
        setTimeout(function () { var i = document.getElementById("mbDelSlug"); if (i) i.focus(); }, 80);
    });

    // Export: the list of the open tab, as the person sees it (search, filters, visible columns)
    function csv(gridId, fileName) {
        var a = api(gridId);
        if (!a) return 0;
        var cols = a.getVisibleColumns().filter(function (c) { return c.field !== "Id"; });
        var rows = a.getViewRows();
        function q(v) { v = v == null ? "" : String(v); return /[",\n]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v; }
        var lines = [cols.map(function (c) { return q(c.header); }).join(",")];
        rows.forEach(function (r) { lines.push(cols.map(function (c) { return q(r[c.field]); }).join(",")); });
        var blob = new Blob(["﻿" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
        var link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = fileName;
        document.body.appendChild(link); link.click(); link.remove();
        setTimeout(function () { URL.revokeObjectURL(link.href); }, 2000);
        return rows.length;
    }
    VK.on("exportGrid", function () {
        var n = VK.tab === "payments" ? csv("payGrid", "vaikunth-payments.csv") : csv("clientGrid", "vaikunth-clients.csv");
        VK.toast(n + (VK.tab === "payments" ? (n === 1 ? " payment" : " payments") : (n === 1 ? " client" : " clients")) + " exported.");
    });
    VK.on("print", function () { window.print(); });
    VK.on("toggleClient", function () {
        var c = current();
        if (!c) { VK.toast("Select a client first."); return; }
        post("/admin/active", { tenantId: c.Id, name: c.Name, isActive: !c.active });
    });
    VK.on("emailAdmin", function () {
        var c = current();
        if (!c) { VK.toast("Select a client first."); return; }
        if (!c.adminEmail) { VK.toast("This client has no admin to email."); return; }
        if (!c.active) { VK.toast(c.Name + " is paused. Resume it first, so its admin can sign in."); return; }
        post("/admin/resend", { tenantId: c.Id });
    });

    // ---------- money ----------
    /** Fills the Record payment window for one client: the oldest unpaid month and what is left of it. */
    function payFor(c) {
        var hint = el("payHint");
        if (!c) { hint.hidden = true; return; }
        setModernMonthYearPickerValue("#payPeriod", c.unpaidFirst || cfg.thisMonth);
        setModernInputValue("#payAmount", c.monthly ? String(c.monthly) : "");
        hint.hidden = false;
        hint.setAttribute("data-bill", c.billState);
        hint.innerHTML = "";
        var b = document.createElement("b"); b.textContent = c.billMath; hint.appendChild(b);
        var s = document.createElement("span");
        s.textContent = c.outstanding > 0 ? "Unpaid: " + c.unpaid.join(", ") + " · " + c.outstandingText + " in all. " + c.billLine : c.billLine;
        hint.appendChild(s);
    }
    VK.on("recordPayment", function () {
        var c = target();
        if (c && c.free) { VK.toast(c.Name + " is on a free plan. There is nothing to pay."); return; }
        el("payTab").value = VK.tab || "clients";
        if (c) { setModernDropdownWithLabelValue("#payClient", c.Id); payFor(c); }
        else { clearModernDropdownWithLabel("#payClient"); payFor(null); setModernInputValue("#payAmount", ""); }
        setModernInputValue("#payRef", "");
        setModernInputValue("#payNote", "");
        open("paymentDialog");
    });
    VK.on("letIn", function () {
        var c = need(); if (!c) return;
        if (c.free) { VK.toast(c.Name + " is on a free plan. It is never blocked."); return; }
        el("holdTenant").value = c.Id;
        el("holdTab").value = VK.tab || "clients";
        set("holdName", c.Name + " · " + c.billText);
        set("holdLine", c.billLine + (c.outstanding > 0 ? " · " + c.outstandingText + " unpaid" : ""));
        el("holdClear").hidden = !c.holdUntil;
        open("holdDialog");
    });
    VK.on("deletePayment", function () {
        var a = api("payGrid"); var p = a && a.getSelected();
        if (!p) { VK.toast("Select a payment in Money received first.", "info"); return; }
        confirmMessageBox({
            title: "Delete this payment?", type: "error", icon: "warning", danger: true, confirmText: "Delete payment", asHtml: true,
            text: "<p class='vk-mb-lead'><b>" + esc(p.amountText) + "</b> from <b>" + esc(p.Client) + "</b> for " + esc(p.Month) + "</p>" +
                "<p class='vk-mb-sub'>" + esc(p.Method) + (p.Reference ? " · " + esc(p.Reference) : "") + " · paid on " + esc(p.PaidOn) + "</p>" +
                "<p class='vk-mb-sub'>Only if it was noted by mistake. That month counts as unpaid again.</p>",
            onConfirm: function () { post("/admin/payments/delete", { paymentId: p.Id }); }
        });
    });

    // ---------- New client window ----------
    function wireNew() {
        var slugTouched = false;
        function billNote() {
            var plan = cfg.perPlan[$("#PlanId_value").val()];
            var n = parseInt($("#Licenses").val(), 10) || 0;
            if (!plan) return;
            set("newBillNote", plan.price > 0
                ? "Each person uses one license, the admin too. Bill: " + n + " × " + rupees(plan.price) + " = " + rupees(n * plan.price) + " a month, from next month."
                : "Each person uses one license, the admin too. The " + plan.name + " plan is free: never billed.");
        }
        $("#PlanId_value").on("change", function () {
            var plan = cfg.perPlan[this.value];
            if (plan) setModernInputValue("#Licenses", plan.users);
            billNote();
        });
        $("#Licenses").on("input", billNote);
        billNote();
        $("#Slug").on("input", function () { slugTouched = this.value.length > 0; });
        $("#Name").on("input", function () {
            if (slugTouched) return;
            setModernInputValue("#Slug", this.value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 39));
        });
        function sync() {
            var t = el("SendInviteToggle");
            var on = !t || t.checked;
            el("SendInvite").value = on ? "true" : "false";
            el("tempPassword").hidden = on;
            var pw = el("AdminPassword");
            if (pw) pw.required = !on;
        }
        $("#SendInviteToggle").on("change", sync);
        sync();
    }


    function wirePayments() {
        $("#payClient_value").on("change", function () { payFor(clientById(this.value)); });
        // a required dropdown / picker is a hidden field: check it before the window posts
        $("#paymentDialog form").on("submit", function (e) {
            if (e.originalEvent && e.originalEvent.submitter && e.originalEvent.submitter.getAttribute("formmethod") === "dialog") return;
            if (!$("#payClient_value").val()) { e.preventDefault(); VK.toast("Pick the client who paid."); return; }
            if (!$("#payPeriod").val()) { e.preventDefault(); VK.toast("Pick the month this payment is for."); return; }
            if (!(parseFloat(String($("#payAmount").val()).replace(/[^0-9.]/g, "")) > 0)) { e.preventDefault(); VK.toast("Enter the amount received."); return; }
            if (!$("#payPaidOn").val()) { e.preventDefault(); VK.toast("Pick the day the money came in."); }
        });
    }

    // ---------- payment rules: the default ones, or one client's own ----------
    function wireRules() {
        var form = el("rulesForm");
        if (!form) return;
        var defaults = { due: form.getAttribute("data-default-due"), grace: form.getAttribute("data-default-grace"), block: form.getAttribute("data-default-block") === "true" };
        function show(c, own) {
            // c = the client picked (null = the default rules); own = it uses rules of its own
            var forClient = !!c;
            el("ruleOwnWrap").hidden = !forClient;
            el("ruleFromWrap").hidden = !forClient;
            el("ruleTenant").value = forClient ? c.Id : "";
            form.action = forClient ? "/admin/rules/client" : "/admin/billing";
            var editable = !forClient || own;
            el("ruleUseDefault").value = forClient && !own ? "true" : "false";
            var src = forClient && own ? { due: String(c.ruleDueDay), grace: String(c.ruleGrace), block: !!c.ruleAutoBlock } : defaults;
            setModernDropdownWithLabelValue("#ruleDueDay", src.due);
            setModernInputValue("#ruleGrace", src.grace);
            setModernCheckboxValue("#ruleAutoBlock", src.block);
            ["ruleDueDay", "ruleGrace", "ruleAutoBlock"].forEach(function (id) {
                var box = el(id); var wrap = box && (box.closest(".modern-dropdown-with-label, .mi-field, .mi-input-wrap, .mc-control") || box.parentElement);
                if (wrap) wrap.classList.toggle("vk-rule-off", !editable);
            });
            if (forClient && c.billingFrom) setModernMonthYearPickerValue("#ruleFrom", c.billingFrom);
            set("ruleNote", !forClient ? "These rules are for every client that has no rules of its own."
                : own ? c.Name + " has its own rules. Turn them off to follow the default rules again."
                : c.Name + " follows the default rules (" + ordinal(defaults.due) + ", " + (defaults.block ? defaults.grace + " days" : "no block") + "). Turn on Own rules to change them for it only.");
        }
        function ordinal(n) { n = parseInt(n, 10); var s = ["th", "st", "nd", "rd"], v = n % 100; return n + (s[(v - 20) % 10] || s[v] || s[0]); }
        function picked() { var id = $("#ruleClient_value").val(); return id ? clientById(id) : null; }
        $("#ruleClient_value").on("change", function () {
            var c = picked();
            if (c) setModernCheckboxValue("#ruleOwnToggle", !!c.ownRules);
            show(c, c ? !!c.ownRules : false);
        });
        $("#ruleOwnToggle").on("change", function () { var c = picked(); if (c) show(c, this.checked); });
        // picking a row in Who has paid picks that client here too
        $("#billGrid").on("table:select", function (e, row) {
            if (!row) return;
            setModernDropdownWithLabelValue("#ruleClient", row.Id);
            var c = clientById(row.Id);
            if (c) { setModernCheckboxValue("#ruleOwnToggle", !!c.ownRules); show(c, !!c.ownRules); }
        });
        $(form).on("submit", function (e) {
            var g = parseInt($("#ruleGrace").val(), 10);
            if (el("ruleUseDefault").value !== "true" && !(g >= 0 && g <= 365)) { e.preventDefault(); VK.toast("Days before the block must be between 0 and 365.", "warning"); }
        });
        show(null, false);
    }

    var listening = false;
    function init(options) {
        if (!options) {
            var json = el("adminCfg");
            try { options = json ? JSON.parse(json.textContent) : {}; } catch (e) { options = {}; }
        }
        for (var k in options) cfg[k] = options[k];
        if (!listening) {   // after a save or Refresh the content is new: start this page again on it
            listening = true;
            document.addEventListener("vk:panel", function () { if (document.getElementById("adminCfg")) init(); });
        }
        $("#clientGrid").on("table:select", function (e, row) { fill(row); });
        var g = api("clientGrid");
        if (g) g.setRowClassCallback(function (r) {
            return (r.active ? "" : "vk-row-paused ") + (r.billState === "blocked" ? "vk-row-blocked" : r.billState === "overdue" || r.billState === "onhold" ? "vk-row-late" : "");
        });
        var slugBtn = el("selSlug");
        if (slugBtn) slugBtn.addEventListener("click", function () {
            var c = current(); if (!c) return;
            if (navigator.clipboard) navigator.clipboard.writeText(c.Slug).then(function () { VK.toast("Copied " + c.Slug + "."); }, function () { });
        });
        wireNew();
        wirePayments();
        wireRules();
        if (g && cfg.selected && g.selectById(cfg.selected)) { /* details pane filled by table:select */ }
        else fill(null);
    }

    return { init: init, fill: fill };
})(window.jQuery);
