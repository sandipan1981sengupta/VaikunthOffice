/* Dhruva Planner · one project: a board (drag cards between columns), a list (sortable) and a timeline (drag bars to move dates).
   The file on the server is JSON: { description, start, end, columns: [{key, name}], tasks: [{id, title, status, priority, owner,
   start, due, progress, milestone, notes}] }. Dates are "yyyy-mm-dd". */
(function () {
    "use strict";
    var $ = function (id) { return document.getElementById(id); };
    var plan = {};
    try { plan = JSON.parse($("docData").textContent || "{}"); } catch (e) { }
    plan.columns = plan.columns && plan.columns.length ? plan.columns : [{ key: "todo", name: "To do" }, { key: "doing", name: "In progress" }, { key: "review", name: "Review" }, { key: "done", name: "Done" }];
    plan.tasks = plan.tasks || [];
    plan.description = plan.description || "";

    var view = "board", sortKey = null, sortDir = 1, scaleMode = "day", hideDone = false, editing = null;
    try { view = localStorage.getItem("vk-planner-view") || "board"; } catch (e) { }

    // ---------- dates as whole days ----------
    var DAY = 864e5;
    function dn(s) { return s ? Math.round(Date.parse(s + "T00:00:00Z") / DAY) : null; }
    function ds(n) { return new Date(n * DAY).toISOString().slice(0, 10); }
    function todayN() { var d = new Date(); return Math.round(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) / DAY); }
    var MON = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    function nice(s, year) { if (!s) return ""; var d = new Date(dn(s) * DAY); return d.getUTCDate() + " " + MON[d.getUTCMonth()] + (year ? " " + d.getUTCFullYear() : ""); }
    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
    function initials(n) { var p = String(n || "").trim().split(/\s+/); return ((p[0] || "")[0] || "") + ((p.length > 1 ? p[p.length - 1][0] : "") || ""); }
    function tone(n) { var h = 0; String(n || "").split("").forEach(function (c) { h = (h * 31 + c.charCodeAt(0)) | 0; }); return Math.abs(h) % 6; }
    function isDone(t) { return t.status === "done" || t.progress >= 100; }
    function isLate(t) { return !!t.due && dn(t.due) < todayN() && !isDone(t); }
    function colName(key) { var c = plan.columns.find(function (c) { return c.key === key; }); return c ? c.name : key; }
    var PRI = { high: "High", medium: "Medium", low: "Low" };

    function changed() { draw(); VK.changed(function () { return JSON.stringify(plan); }); }
    VK.getContent = function () { return JSON.stringify(plan); };

    // ---------- which tasks show ----------
    var ownerSel = $("ownerFilter"), search = $("taskSearch");
    function visible() {
        var who = ownerSel.value, q = search.value.trim().toLowerCase();
        return plan.tasks.filter(function (t) {
            if (who && t.owner !== who) return false;
            if (hideDone && isDone(t)) return false;
            if (q && (t.title + " " + t.owner + " " + t.notes).toLowerCase().indexOf(q) < 0) return false;
            return true;
        });
    }
    function owners() {
        var seen = {}, list = [];
        plan.tasks.forEach(function (t) { if (t.owner && !seen[t.owner]) { seen[t.owner] = 1; list.push(t.owner); } });
        if (VK.me && !seen[VK.me]) list.push(VK.me);
        return list.sort();
    }
    function fillOwners() {
        var keep = ownerSel.value;
        ownerSel.innerHTML = "<option value=''>Everyone</option>" + owners().map(function (o) { return "<option>" + esc(o) + "</option>"; }).join("");
        ownerSel.value = keep;
        var dl = $("ownerList");
        if (!dl) { dl = document.createElement("datalist"); dl.id = "ownerList"; document.body.appendChild(dl); $("tOwner").setAttribute("list", "ownerList"); }
        dl.innerHTML = owners().map(function (o) { return "<option value='" + esc(o) + "'>"; }).join("");
    }
    ownerSel.addEventListener("change", draw);
    search.addEventListener("input", draw);
    VK.on("hideDone", function (a, btn) { hideDone = !hideDone; btn.classList.toggle("on", hideDone); draw(); });

    // ---------- summary strip ----------
    function summary() {
        var all = plan.tasks, done = all.filter(isDone).length, t0 = todayN();
        var avg = all.length ? Math.round(all.reduce(function (s, t) { return s + (isDone(t) ? 100 : (t.progress || 0)); }, 0) / all.length) : 0;
        $("projName").textContent = $("docTitle").value || "Project";
        $("projDates").textContent = plan.start || plan.end ? nice(plan.start, true) + " → " + nice(plan.end, true) + (plan.description ? " · " + plan.description : "") : (plan.description || "Set the dates in Project details");
        $("kTasks").textContent = all.length;
        $("kDone").textContent = avg + "%";
        $("kDoneBar").style.width = avg + "%";
        $("kLate").textContent = all.filter(isLate).length;
        $("kLate").parentNode.classList.toggle("on", all.some(isLate));
        $("kSoon").textContent = all.filter(function (t) { var d = dn(t.due); return d !== null && d >= t0 && d <= t0 + 7 && !isDone(t); }).length;
        var next = all.filter(function (t) { return t.milestone && t.due && dn(t.due) >= t0 && !isDone(t); }).sort(function (a, b) { return dn(a.due) - dn(b.due); })[0];
        $("kMile").textContent = next ? next.title + " · " + nice(next.due) : "None";
        $("statTasks").innerHTML = "<i class='fa fa-tasks' style='color:#5B2A86'></i> " + all.length + (all.length === 1 ? " task" : " tasks") + " · " + done + " done";
    }

    // ---------- board ----------
    var board = $("board");
    function card(t) {
        var late = isLate(t);
        return "<article class='pl-card pri-" + esc(t.priority) + (isDone(t) ? " is-done" : "") + (late ? " is-late" : "") + "' draggable='true' data-id='" + esc(t.id) + "' tabindex='0'>" +
            "<div class='pl-card-top'>" + (t.milestone ? "<i class='fa fa-flag pl-mile' title='Milestone'></i>" : "") +
            "<b>" + esc(t.title) + "</b></div>" +
            (t.notes ? "<p class='pl-card-notes'>" + esc(t.notes) + "</p>" : "") +
            (t.progress && !isDone(t) ? "<i class='pl-bar'><i style='width:" + t.progress + "%'></i></i>" : "") +
            "<div class='pl-card-foot'>" +
            "<span class='pl-pri'>" + PRI[t.priority || "medium"] + "</span>" +
            (t.due ? "<span class='pl-due" + (late ? " late" : "") + "'><i class='fa fa-calendar-o'></i> " + nice(t.due) + "</span>" : "") +
            "<span class='vk-fill'></span>" +
            (t.owner ? "<span class='vk-avatar tone-" + tone(t.owner) + " pl-av' title='" + esc(t.owner) + "'>" + esc(initials(t.owner).toUpperCase()) + "</span>" : "") +
            "</div></article>";
    }
    function drawBoard() {
        var list = visible();
        board.innerHTML = plan.columns.map(function (c) {
            var mine = list.filter(function (t) { return t.status === c.key; });
            var empty = !plan.tasks.some(function (t) { return t.status === c.key; });
            return "<div class='pl-col' data-col='" + esc(c.key) + "'>" +
                "<header class='pl-col-head'><span class='pl-col-name' contenteditable='true' spellcheck='false' data-col-name='" + esc(c.key) + "'>" + esc(c.name) + "</span>" +
                "<b class='pl-col-count'>" + mine.length + "</b>" +
                (empty && plan.columns.length > 1 ? "<button type='button' class='pl-col-x' data-del-col='" + esc(c.key) + "' title='Remove this empty column'><i class='fa fa-times'></i></button>" : "") +
                "</header><div class='pl-col-body' data-drop='" + esc(c.key) + "'>" + mine.map(card).join("") +
                (mine.length ? "" : "<p class='pl-col-empty'>Drop a task here</p>") + "</div>" +
                "<button type='button' class='pl-add' data-add='" + esc(c.key) + "'><i class='fa fa-plus'></i> Add a task</button></div>";
        }).join("");
    }
    // drag and drop between columns
    var dragId = null;
    board.addEventListener("dragstart", function (e) {
        var c = e.target.closest(".pl-card"); if (!c) return;
        dragId = c.getAttribute("data-id"); c.classList.add("dragging");
        e.dataTransfer.effectAllowed = "move"; e.dataTransfer.setData("text/plain", dragId);
    });
    board.addEventListener("dragend", function () { dragId = null; board.querySelectorAll(".dragging,.over").forEach(function (x) { x.classList.remove("dragging", "over"); }); });
    board.addEventListener("dragover", function (e) {
        var col = e.target.closest("[data-drop]"); if (!col || !dragId) return;
        e.preventDefault();
        board.querySelectorAll(".over").forEach(function (x) { if (x !== col) x.classList.remove("over"); });
        col.classList.add("over");
    });
    board.addEventListener("drop", function (e) {
        var col = e.target.closest("[data-drop]"); if (!col || !dragId) return;
        e.preventDefault();
        var t = plan.tasks.find(function (x) { return x.id === dragId; }); if (!t) return;
        var key = col.getAttribute("data-drop");
        // place it before the card it was dropped on, or at the end of the column
        var before = e.target.closest(".pl-card");
        plan.tasks.splice(plan.tasks.indexOf(t), 1);
        t.status = key;
        if (key === "done") t.progress = 100; else if (t.progress >= 100) t.progress = 90;
        var at = before && before.getAttribute("data-id") !== t.id ? plan.tasks.findIndex(function (x) { return x.id === before.getAttribute("data-id"); }) : -1;
        if (at >= 0) plan.tasks.splice(at, 0, t); else plan.tasks.push(t);
        changed();
        VK.toast("“" + t.title + "” moved to " + colName(key) + ".");
    });
    board.addEventListener("click", function (e) {
        var x;
        if ((x = e.target.closest("[data-add]"))) { openTask(null, { status: x.getAttribute("data-add") }); return; }
        if ((x = e.target.closest("[data-del-col]"))) {
            var k = x.getAttribute("data-del-col");
            plan.columns = plan.columns.filter(function (c) { return c.key !== k; });
            changed(); return;
        }
        if ((x = e.target.closest(".pl-card"))) openTask(x.getAttribute("data-id"));
    });
    board.addEventListener("keydown", function (e) { var c = e.target.closest(".pl-card"); if (c && e.key === "Enter") openTask(c.getAttribute("data-id")); });
    // rename a column right on the board
    board.addEventListener("keydown", function (e) { if (e.target.matches("[data-col-name]") && e.key === "Enter") { e.preventDefault(); e.target.blur(); } });
    board.addEventListener("focusout", function (e) {
        if (!e.target.matches("[data-col-name]")) return;
        var c = plan.columns.find(function (c) { return c.key === e.target.getAttribute("data-col-name"); });
        var name = e.target.textContent.trim().slice(0, 40);
        if (c && name && name !== c.name) { c.name = name; changed(); } else if (c) e.target.textContent = c.name;
    });
    VK.on("addColumn", function () {
        var key = "c" + Date.now().toString(36);
        plan.columns.splice(Math.max(0, plan.columns.length - 1), 0, { key: key, name: "New column" });
        setView("board"); changed();
        var el = board.querySelector('[data-col-name="' + key + '"]');
        if (el) { el.focus(); document.execCommand && document.execCommand("selectAll", false, null); }
    });
    VK.on("renameColumns", function () {
        setView("board");
        var el = board.querySelector("[data-col-name]");
        if (el) { el.focus(); document.execCommand && document.execCommand("selectAll", false, null); }
        VK.toast("Type a new name on any column, then press Enter.");
    });

    // ---------- list ----------
    var PRI_N = { high: 3, medium: 2, low: 1 };
    function drawList() {
        var list = visible().slice();
        if (sortKey) list.sort(function (a, b) {
            var x, y;
            if (sortKey === "priority") { x = PRI_N[a.priority] || 0; y = PRI_N[b.priority] || 0; }
            else if (sortKey === "progress") { x = a.progress || 0; y = b.progress || 0; }
            else if (sortKey === "status") { x = plan.columns.findIndex(function (c) { return c.key === a.status; }); y = plan.columns.findIndex(function (c) { return c.key === b.status; }); }
            else if (sortKey === "start" || sortKey === "due") { x = dn(a[sortKey]) || 1e9; y = dn(b[sortKey]) || 1e9; }
            else { x = String(a[sortKey] || "").toLowerCase(); y = String(b[sortKey] || "").toLowerCase(); return x.localeCompare(y) * sortDir; }
            return (x - y) * sortDir;
        });
        document.querySelectorAll(".pl-list th").forEach(function (th) { th.setAttribute("aria-sort", th.getAttribute("data-sort") === sortKey ? (sortDir > 0 ? "ascending" : "descending") : "none"); });
        $("listBody").innerHTML = list.length ? list.map(function (t) {
            var late = isLate(t);
            return "<tr data-id='" + esc(t.id) + "' class='" + (isDone(t) ? "is-done" : "") + "'>" +
                "<td><label class='pl-tick' title='Done'><input type='checkbox' data-done='" + esc(t.id) + "'" + (isDone(t) ? " checked" : "") + " /><span></span></label>" +
                (t.milestone ? "<i class='fa fa-flag pl-mile'></i> " : "") + "<b>" + esc(t.title) + "</b></td>" +
                "<td><span class='pl-status s-" + esc(t.status) + "'>" + esc(colName(t.status)) + "</span></td>" +
                "<td><span class='pl-pri pri-" + esc(t.priority) + "'>" + PRI[t.priority || "medium"] + "</span></td>" +
                "<td>" + (t.owner ? "<span class='pl-owner'><span class='vk-avatar tone-" + tone(t.owner) + " pl-av'>" + esc(initials(t.owner).toUpperCase()) + "</span>" + esc(t.owner) + "</span>" : "<span class='vk-muted'>—</span>") + "</td>" +
                "<td>" + nice(t.start) + "</td><td class='" + (late ? "pl-late" : "") + "'>" + nice(t.due) + (late ? " <i class='fa fa-exclamation-circle' title='Overdue'></i>" : "") + "</td>" +
                "<td><span class='pl-prog'><i class='pl-bar'><i style='width:" + (isDone(t) ? 100 : t.progress || 0) + "%'></i></i>" + (isDone(t) ? 100 : t.progress || 0) + "%</span></td></tr>";
        }).join("") : "<tr><td colspan='7' class='vk-empty'>No tasks to show. Use New task to add one.</td></tr>";
    }
    document.querySelector(".pl-list thead").addEventListener("click", function (e) {
        var th = e.target.closest("th[data-sort]"); if (!th) return;
        var k = th.getAttribute("data-sort");
        sortDir = sortKey === k ? -sortDir : 1; sortKey = k; drawList();
    });
    $("listBody").addEventListener("change", function (e) {
        var box = e.target.closest("[data-done]"); if (!box) return;
        var t = plan.tasks.find(function (x) { return x.id === box.getAttribute("data-done"); });
        if (!t) return;
        if (box.checked) { t.status = "done"; t.progress = 100; }
        else { t.status = plan.columns[0].key; t.progress = Math.min(t.progress, 90); }
        changed();
    });
    $("listBody").addEventListener("click", function (e) {
        if (e.target.closest(".pl-tick")) return;
        var tr = e.target.closest("tr[data-id]"); if (tr) openTask(tr.getAttribute("data-id"));
    });

    // ---------- timeline ----------
    var tl = $("timeline");
    function range() {
        var lo = dn(plan.start), hi = dn(plan.end);
        plan.tasks.forEach(function (t) {
            [dn(t.start), dn(t.due)].forEach(function (d) { if (d === null) return; lo = lo === null ? d : Math.min(lo, d); hi = hi === null ? d : Math.max(hi, d); });
        });
        var t0 = todayN();
        if (lo === null) lo = t0; if (hi === null) hi = lo + 27;
        lo = Math.min(lo, t0) - 3; hi = Math.max(hi, t0) + 7;
        if (scaleMode === "week") { lo -= ((new Date(lo * DAY).getUTCDay() + 6) % 7); }
        return { lo: lo, hi: hi };
    }
    function drawTimeline() {
        var r = range(), W = scaleMode === "day" ? 34 : 14, days = r.hi - r.lo + 1, width = days * W, t0 = todayN();
        var months = "", dayRow = "", grid = "", d;
        // month bands
        var start = r.lo;
        while (start <= r.hi) {
            var dt = new Date(start * DAY), end = Math.min(r.hi, Math.round(Date.UTC(dt.getUTCFullYear(), dt.getUTCMonth() + 1, 1) / DAY) - 1);
            months += "<span style='left:" + (start - r.lo) * W + "px;width:" + (end - start + 1) * W + "px'>" + MON[dt.getUTCMonth()] + " " + dt.getUTCFullYear() + "</span>";
            start = end + 1;
        }
        for (d = r.lo; d <= r.hi; d++) {
            var wd = new Date(d * DAY).getUTCDay(), weekend = wd === 0 || wd === 6;
            if (scaleMode === "day") dayRow += "<span class='" + (weekend ? "we" : "") + (d === t0 ? " today" : "") + "' style='left:" + (d - r.lo) * W + "px;width:" + W + "px'>" + new Date(d * DAY).getUTCDate() + "</span>";
            else if (wd === 1) dayRow += "<span style='left:" + (d - r.lo) * W + "px;width:" + 7 * W + "px'>" + nice(ds(d)) + "</span>";
            if (weekend) grid += "<i class='pl-we' style='left:" + (d - r.lo) * W + "px;width:" + W + "px'></i>";
        }
        var list = visible().slice().sort(function (a, b) { return (dn(a.start || a.due) || 1e9) - (dn(b.start || b.due) || 1e9); });
        var rows = list.map(function (t) {
            var s = dn(t.start || t.due), e = dn(t.due || t.start), bar = "";
            if (s !== null) {
                if (e < s) e = s;
                var left = (s - r.lo) * W, w = (e - s + 1) * W, p = isDone(t) ? 100 : (t.progress || 0);
                bar = t.milestone
                    ? "<span class='pl-tl-mile" + (isLate(t) ? " late" : "") + "' data-bar='" + esc(t.id) + "' style='left:" + (left + W / 2 - 9) + "px' title='" + esc(t.title + " · " + nice(t.due, true)) + "'></span><span class='pl-tl-mlabel' style='left:" + (left + W / 2 + 14) + "px'>" + esc(t.title) + "</span>"
                    : "<span class='pl-tl-bar s-" + esc(t.status) + (isLate(t) ? " late" : "") + (isDone(t) ? " done" : "") + "' data-bar='" + esc(t.id) + "' style='left:" + left + "px;width:" + w + "px' title='" + esc(t.title + " · " + nice(t.start) + " → " + nice(t.due) + " · " + p + "%") + "'>" +
                      "<i style='width:" + p + "%'></i><em>" + esc(t.title) + "</em><b class='pl-grip' data-grip='" + esc(t.id) + "'></b></span>";
            }
            return "<div class='pl-tl-row' data-id='" + esc(t.id) + "'><div class='pl-tl-label'>" +
                (t.owner ? "<span class='vk-avatar tone-" + tone(t.owner) + " pl-av'>" + esc(initials(t.owner).toUpperCase()) + "</span>" : "<span class='pl-av-none'></span>") +
                "<span><b>" + esc(t.title) + "</b><small>" + (t.start ? nice(t.start) + " → " : "") + nice(t.due) + "</small></span></div>" +
                "<div class='pl-tl-lane' style='width:" + width + "px'>" + bar + "</div></div>";
        }).join("");
        var proj = plan.start && plan.end ? "<i class='pl-proj-span' style='left:" + (dn(plan.start) - r.lo) * W + "px;width:" + (dn(plan.end) - dn(plan.start) + 1) * W + "px'></i>" : "";
        tl.innerHTML = "<div class='pl-tl-inner' style='--w:" + width + "px'>" +
            "<div class='pl-tl-head'><div class='pl-tl-label pl-tl-corner'>Task</div><div class='pl-tl-scale' style='width:" + width + "px'><div class='pl-tl-months'>" + months + "</div><div class='pl-tl-days'>" + dayRow + "</div></div></div>" +
            "<div class='pl-tl-body'><div class='pl-tl-grid' style='width:" + width + "px'>" + grid + proj +
            (t0 >= r.lo && t0 <= r.hi ? "<i class='pl-today' style='left:" + ((t0 - r.lo) * W + W / 2) + "px'><b>Today</b></i>" : "") + "</div>" +
            (rows || "<p class='vk-empty'>No tasks to show.</p>") + "</div></div>";
        tl.dataset.lo = r.lo; tl.dataset.w = W;
    }
    VK.on("scale", function (s) { scaleMode = s; markButtons(); drawTimeline(); });
    VK.on("today", function () {
        setView("timeline");
        var mark = tl.querySelector(".pl-today");
        if (mark) tl.scrollLeft = Math.max(0, mark.offsetLeft - tl.clientWidth / 2 + 260);
    });
    // drag a bar to move the task; drag its right edge to change the due date
    var drag = null;
    tl.addEventListener("pointerdown", function (e) {
        var bar = e.target.closest("[data-bar]"); if (!bar) return;
        var t = plan.tasks.find(function (x) { return x.id === bar.getAttribute("data-bar"); }); if (!t) return;
        drag = { t: t, bar: bar, x: e.clientX, grip: !!e.target.closest("[data-grip]"), s: dn(t.start || t.due), e: dn(t.due || t.start), moved: 0, left: bar.offsetLeft, width: bar.offsetWidth };
        bar.setPointerCapture(e.pointerId);
        e.preventDefault();
    });
    tl.addEventListener("pointermove", function (e) {
        if (!drag) return;
        var W = +tl.dataset.w, dd = Math.round((e.clientX - drag.x) / W);
        drag.moved = dd;
        if (drag.grip) drag.bar.style.width = Math.max(W, drag.width + dd * W) + "px";
        else drag.bar.style.left = (drag.left + dd * W) + "px";
    });
    tl.addEventListener("pointerup", function () {
        if (!drag) return;
        var d = drag; drag = null;
        if (!d.moved) { openTask(d.t.id); return; }
        if (d.grip) d.t.due = ds(Math.max(d.s, d.e + d.moved));
        else { if (d.t.start) d.t.start = ds(d.s + d.moved); if (d.t.due) d.t.due = ds(d.e + d.moved); }
        changed();
        VK.toast("“" + d.t.title + "” now " + (d.t.start && !d.t.milestone ? nice(d.t.start) + " → " : "") + nice(d.t.due) + ".");
    });
    tl.addEventListener("dblclick", function (e) { var row = e.target.closest(".pl-tl-row"); if (row) openTask(row.getAttribute("data-id")); });

    // ---------- views ----------
    function markButtons() {
        document.querySelectorAll(".pl-view-btn").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-arg") === view); });
        document.querySelectorAll(".pl-scale-btn").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-arg") === scaleMode); });
    }
    function setView(v) {
        view = v;
        try { localStorage.setItem("vk-planner-view", v); } catch (e) { }
        document.querySelectorAll(".pl-pane").forEach(function (p) { p.hidden = p.getAttribute("data-view") !== v; });
        $("statView").textContent = { board: "Board", list: "List", timeline: "Timeline" }[v] + " view";
        markButtons(); draw();
    }
    VK.on("view", setView);
    function draw() {
        fillOwners(); summary();
        if (view === "board") drawBoard(); else if (view === "list") drawList(); else drawTimeline();
    }

    // ---------- task window ----------
    function openTask(id, preset) {
        var t = id ? plan.tasks.find(function (x) { return x.id === id; }) : null;
        editing = t;
        preset = preset || {};
        $("tStatus").innerHTML = plan.columns.map(function (c) { return "<option value='" + esc(c.key) + "'>" + esc(c.name) + "</option>"; }).join("");
        var today = ds(todayN());
        $("tTitle").value = t ? t.title : "";
        $("tStatus").value = t ? t.status : (preset.status || plan.columns[0].key);
        $("tPriority").value = t ? t.priority || "medium" : "medium";
        $("tOwner").value = t ? t.owner || "" : (VK.me || "");
        $("tStart").value = t ? t.start || "" : (preset.milestone ? "" : today);
        $("tDue").value = t ? t.due || "" : ds(todayN() + (preset.milestone ? 14 : 7));
        $("tProgress").value = t ? t.progress || 0 : 0;
        $("tProgressVal").textContent = $("tProgress").value + "%";
        $("tMilestone").checked = t ? !!t.milestone : !!preset.milestone;
        $("tNotes").value = t ? t.notes || "" : "";
        $("tDelete").hidden = !t;
        var dlg = $("taskDialog");
        dlg.querySelector(".vk-dialog-titles h3").textContent = t ? "Edit task" : (preset.milestone ? "New milestone" : "New task");
        dlg.showModal();
        setTimeout(function () { $("tTitle").focus(); }, 40);
    }
    $("tProgress").addEventListener("input", function () { $("tProgressVal").textContent = this.value + "%"; });
    VK.on("newTask", function () { openTask(null); });
    VK.on("newMilestone", function () { openTask(null, { milestone: true }); });
    VK.on("saveTask", function () {
        var title = $("tTitle").value.trim();
        if (!title) { VK.toast("Give the task a name."); $("tTitle").focus(); return; }
        var start = $("tStart").value || null, due = $("tDue").value || null;
        if (start && due && dn(due) < dn(start)) { VK.toast("The due date is before the start. Pick a later due date."); return; }
        var t = editing || { id: "t" + Date.now().toString(36) };
        t.title = title.slice(0, 200);
        t.status = $("tStatus").value;
        t.priority = $("tPriority").value;
        t.owner = $("tOwner").value.trim();
        t.start = start; t.due = due;
        t.progress = +$("tProgress").value;
        t.milestone = $("tMilestone").checked;
        if (t.milestone && t.due) t.start = t.due;
        t.notes = $("tNotes").value.trim();
        if (t.status === "done") t.progress = 100;
        if (!editing) plan.tasks.push(t);
        VK.closeDialog($("taskDialog"));
        changed();
        VK.toast(editing ? "Task saved." : "“" + t.title + "” added.");
    });
    VK.on("deleteTask", function () {
        if (!editing) return;
        var t = editing;
        plan.tasks = plan.tasks.filter(function (x) { return x !== t; });
        VK.closeDialog($("taskDialog"));
        changed();
        VK.toast("“" + t.title + "” deleted.");
    });

    // ---------- project window ----------
    VK.on("projectInfo", function () {
        $("pName").value = $("docTitle").value;
        $("pStart").value = plan.start || ""; $("pEnd").value = plan.end || "";
        $("pDesc").value = plan.description || "";
        $("projectDialog").showModal();
    });
    VK.on("saveProject", function () {
        var name = $("pName").value.trim();
        if (!name) { VK.toast("Give the project a name."); return; }
        var s = $("pStart").value || null, e = $("pEnd").value || null;
        if (s && e && dn(e) < dn(s)) { VK.toast("The project ends before it starts. Pick a later end date."); return; }
        $("docTitle").value = name;
        plan.start = s; plan.end = e; plan.description = $("pDesc").value.trim();
        VK.closeDialog($("projectDialog"));
        changed();
    });
    $("docTitle").addEventListener("change", function () { summary(); VK.save(JSON.stringify(plan)); });

    // keyboard: N = new task (when not typing)
    document.addEventListener("keydown", function (e) {
        if (e.key.toLowerCase() === "n" && !e.ctrlKey && !e.metaKey && !e.altKey && !e.target.closest("input, textarea, select, [contenteditable], dialog")) { e.preventDefault(); openTask(null); }
    });

    setView(view === "list" || view === "timeline" ? view : "board");
})();
