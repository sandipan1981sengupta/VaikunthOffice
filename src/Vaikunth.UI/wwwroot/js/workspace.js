/* Vaikunth Desk · the workspace frame (super admin): theme, maximize, clock, tabs and the data grid.
   Loaded after vaikunth.js by _AdminLayout. */
(function () {
    "use strict";
    var VK = window.VK;
    var root = document.documentElement;

    // ---------- theme: Light / Dark (remembered in this browser) ----------
    function markTheme() {
        var t = root.getAttribute("data-theme") || "light";
        document.querySelectorAll("[data-theme-id]").forEach(function (b) { b.setAttribute("aria-pressed", String(b.getAttribute("data-theme-id") === t)); });
        var bar = document.querySelector('meta[name="theme-color"]');
        if (bar) bar.setAttribute("content", t === "dark" ? "#0E1024" : "#E9EBF8");
    }
    function setTheme(t) {
        root.setAttribute("data-theme", t);
        try { localStorage.setItem("vk-theme", t); } catch (e) { }
        markTheme();
    }
    VK.on("theme", function (t) { setTheme(t === "dark" ? "dark" : "light"); });
    VK.on("themeToggle", function () { setTheme(root.getAttribute("data-theme") === "dark" ? "light" : "dark"); });
    markTheme();

    // ---------- maximize: the whole window, without the browser around it ----------
    function markMax() {
        var on = !!document.fullscreenElement;
        document.querySelectorAll("[data-max-text]").forEach(function (s) { s.textContent = on ? "Restore" : "Maximize"; });
        document.querySelectorAll("[data-max-ic]").forEach(function (i) { i.className = "fa " + (on ? "fa-compress" : "fa-expand"); });
    }
    VK.on("maximize", function () {
        document.querySelectorAll(".vk-menu-pop").forEach(function (m) { m.hidden = true; });
        if (document.fullscreenElement) { if (document.exitFullscreen) document.exitFullscreen(); }
        else if (root.requestFullscreen) root.requestFullscreen().catch(function () { VK.toast("This browser doesn't allow full screen here."); });
    });
    document.addEventListener("fullscreenchange", markMax);

    // ---------- clock ----------
    var clock = document.getElementById("vkClock");
    function tick() { if (clock) clock.textContent = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }); }
    tick(); setInterval(tick, 15000);

    // ---------- tabs: <vk-tabs> buttons show the matching [data-pane]; the open tab stays in the address ----------
    VK.tab = null;
    function openTab(key, keepUrl) {
        var tabs = document.querySelectorAll("[data-vk-tabs] .vk-tab");
        if (!tabs.length) return;
        var found = false;
        tabs.forEach(function (t) { var on = t.getAttribute("data-tab") === key; found = found || on; t.classList.toggle("on", on); t.setAttribute("aria-selected", String(on)); });
        if (!found) { key = tabs[0].getAttribute("data-tab"); tabs[0].classList.add("on"); }
        document.querySelectorAll("[data-pane]").forEach(function (p) { p.hidden = p.getAttribute("data-pane") !== key; });
        var active = document.querySelector('[data-vk-tabs] .vk-tab[data-tab="' + key + '"]');
        var hint = document.querySelector("[data-tab-hint]");
        if (hint && active) hint.textContent = active.getAttribute("data-hint") || "";
        // toolbar buttons that belong to one tab fold away on the others
        document.querySelectorAll(".vk-abtn[data-scope]").forEach(function (b) { b.hidden = (" " + b.getAttribute("data-scope") + " ").indexOf(" " + key + " ") < 0; });
        VK.tab = key;
        if (!keepUrl) {
            try { var u = new URL(location.href); u.searchParams.set("tab", key); history.replaceState(null, "", u); } catch (e) { }
        }
        document.dispatchEvent(new CustomEvent("vk:tab", { detail: key }));
    }
    VK.openTab = openTab;
    document.addEventListener("click", function (e) {
        var t = e.target.closest("[data-vk-tabs] .vk-tab");
        if (t) openTab(t.getAttribute("data-tab"));
    });
    var first = document.querySelector("[data-vk-tabs] .vk-tab.on") || document.querySelector("[data-vk-tabs] .vk-tab");
    if (first) openTab(first.getAttribute("data-tab"), true);

    // ---------- small pop-overs (Filters, Columns) ----------
    document.addEventListener("click", function (e) {
        var opener = e.target.closest("[data-pop]");
        document.querySelectorAll(".vk-pop").forEach(function (p) {
            var mine = opener && p.id === opener.getAttribute("data-pop");
            if (mine) p.hidden = !p.hidden;
            else if (!e.target.closest(".vk-pop")) p.hidden = true;
        });
    });

    // ---------- the data grid ----------
    // <div data-vk-grid data-page-size="10" data-remember="key"> with [data-grid-search], [data-grid-filter="x"] selects,
    // [data-grid-cols] (column list), a table whose rows carry data-row (JSON) and data-f-x (filter values),
    // [data-grid-count] and [data-grid-pager]. Fires "vk:select" on the grid with the row's data (or null).
    VK.grids = {};
    function Grid(el) {
        var table = el.querySelector("table"), body = table.tBodies[0];
        var rows = Array.prototype.slice.call(body.querySelectorAll("tr[data-row]"));
        var empty = body.querySelector("tr[data-empty]");
        var heads = Array.prototype.slice.call(table.tHead.rows[0].cells);
        var size = +el.getAttribute("data-page-size") || 10, page = 1, sortCol = -1, sortDir = 1, selected = null;
        var search = el.querySelector("[data-grid-search]");
        var filters = Array.prototype.slice.call(el.querySelectorAll("[data-grid-filter]"));
        var key = el.getAttribute("data-remember");

        function text(tr, i) { var td = tr.cells[i]; return td ? (td.getAttribute("data-v") || td.textContent).trim() : ""; }
        function matches(tr) {
            var q = search ? search.value.trim().toLowerCase() : "";
            if (q && tr.textContent.toLowerCase().indexOf(q) < 0) return false;
            return filters.every(function (f) {
                var v = f.value; if (!v) return true;
                return (tr.getAttribute("data-f-" + f.getAttribute("data-grid-filter")) || "") === v;
            });
        }
        function shown() { return rows.filter(matches); }
        function render() {
            var list = shown();
            if (sortCol >= 0) {
                var type = heads[sortCol].getAttribute("data-sort");
                list.sort(function (a, b) {
                    var x = text(a, sortCol), y = text(b, sortCol);
                    if (type === "num") { x = parseFloat(x) || 0; y = parseFloat(y) || 0; return (x - y) * sortDir; }
                    return x.localeCompare(y, undefined, { numeric: true, sensitivity: "base" }) * sortDir;
                });
                list.forEach(function (tr) { body.appendChild(tr); });
                if (empty) body.appendChild(empty);
            }
            var pages = Math.max(1, Math.ceil(list.length / size));
            if (page > pages) page = pages;
            var from = (page - 1) * size, to = Math.min(list.length, from + size);
            rows.forEach(function (tr) { tr.hidden = true; });
            list.slice(from, to).forEach(function (tr) { tr.hidden = false; });
            if (empty) empty.hidden = list.length > 0;
            var count = el.querySelector("[data-grid-count]");
            if (count) count.textContent = list.length ? "Showing " + (from + 1) + "–" + to + " of " + list.length : "Nothing to show";
            var pager = el.querySelector("[data-grid-pager]");
            if (pager) {
                var html = "<button type='button' data-page='" + (page - 1) + "'" + (page <= 1 ? " disabled" : "") + " aria-label='Previous page'><i class='fa fa-angle-left'></i></button>";
                for (var p = 1; p <= pages; p++) html += "<button type='button' data-page='" + p + "' class='" + (p === page ? "on" : "") + "'>" + p + "</button>";
                html += "<button type='button' data-page='" + (page + 1) + "'" + (page >= pages ? " disabled" : "") + " aria-label='Next page'><i class='fa fa-angle-right'></i></button>";
                pager.innerHTML = html;
            }
            var activeFilters = filters.filter(function (f) { return f.value; }).length;
            var fb = el.querySelector("[data-filter-count]");
            if (fb) { fb.textContent = activeFilters || ""; fb.hidden = !activeFilters; }
        }
        function select(tr, quiet) {
            rows.forEach(function (r) { r.classList.toggle("on", r === tr); r.setAttribute("aria-selected", String(r === tr)); });
            selected = tr || null;
            var data = null;
            if (tr) { try { data = JSON.parse(tr.getAttribute("data-row")); } catch (e) { } }
            if (!quiet || tr) el.dispatchEvent(new CustomEvent("vk:select", { detail: data }));
            try { var u = new URL(location.href); if (data && data.id) u.searchParams.set("sel", data.id); else u.searchParams.delete("sel"); history.replaceState(null, "", u); } catch (e) { }
        }

        // columns: show / hide, remembered
        var colsBox = el.querySelector("[data-grid-cols]");
        var hiddenCols = [];
        try { hiddenCols = JSON.parse(localStorage.getItem(key) || "[]"); } catch (e) { }
        function applyCols() {
            heads.forEach(function (th, i) {
                var off = hiddenCols.indexOf(th.getAttribute("data-col")) >= 0;
                th.hidden = off;
                rows.forEach(function (tr) { if (tr.cells[i]) tr.cells[i].hidden = off; });
            });
            if (empty && empty.cells[0]) empty.cells[0].colSpan = heads.filter(function (th) { return !th.hidden; }).length;
        }
        if (colsBox) {
            colsBox.innerHTML = heads.filter(function (th) { return th.getAttribute("data-col") && th.getAttribute("data-fixed") === null; }).map(function (th) {
                var c = th.getAttribute("data-col");
                return "<label class='vk-pop-check'><input type='checkbox' value='" + c + "'" + (hiddenCols.indexOf(c) < 0 ? " checked" : "") + " /> " + th.textContent.trim() + "</label>";
            }).join("");
            colsBox.addEventListener("change", function (e) {
                var c = e.target.value;
                hiddenCols = hiddenCols.filter(function (x) { return x !== c; });
                if (!e.target.checked) hiddenCols.push(c);
                try { localStorage.setItem(key, JSON.stringify(hiddenCols)); } catch (x) { }
                applyCols();
            });
        }

        heads.forEach(function (th, i) {
            if (!th.hasAttribute("data-sort")) return;
            th.tabIndex = 0;
            function go() {
                sortDir = sortCol === i ? -sortDir : 1; sortCol = i;
                heads.forEach(function (h) { h.removeAttribute("aria-sort"); });
                th.setAttribute("aria-sort", sortDir > 0 ? "ascending" : "descending");
                render();
            }
            th.addEventListener("click", go);
            th.addEventListener("keydown", function (e) { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); go(); } });
        });
        if (search) search.addEventListener("input", function () { page = 1; render(); });
        filters.forEach(function (f) { f.addEventListener("change", function () { page = 1; render(); }); });
        var clear = el.querySelector("[data-grid-clear]");
        if (clear) clear.addEventListener("click", function () { filters.forEach(function (f) { f.value = ""; }); page = 1; render(); });
        el.addEventListener("click", function (e) {
            var pb = e.target.closest("[data-page]");
            if (pb && !pb.disabled) { page = +pb.getAttribute("data-page"); render(); return; }
            var tr = e.target.closest("tr[data-row]");
            if (tr && !e.target.closest("a, button, input, select")) select(tr === selected ? null : tr);
        });
        body.addEventListener("keydown", function (e) {
            var tr = e.target.closest("tr[data-row]"); if (!tr) return;
            if (e.key === "Enter" || e.key === " ") { e.preventDefault(); select(tr); }
            if (e.key === "ArrowDown" || e.key === "ArrowUp") {
                e.preventDefault();
                var vis = rows.filter(function (r) { return !r.hidden; }), i = vis.indexOf(tr) + (e.key === "ArrowDown" ? 1 : -1);
                if (vis[i]) { vis[i].focus(); select(vis[i]); }
            }
        });
        rows.forEach(function (tr) { tr.tabIndex = 0; });
        applyCols();
        render();

        return {
            el: el,
            selected: function () { if (!selected) return null; try { return JSON.parse(selected.getAttribute("data-row")); } catch (e) { return null; } },
            selectId: function (id) {
                var tr = rows.find(function (r) { try { return JSON.parse(r.getAttribute("data-row")).id === id; } catch (e) { return false; } });
                if (!tr) return;
                var list = shown(), i = list.indexOf(tr);
                if (i >= 0) { page = Math.floor(i / size) + 1; render(); }
                select(tr);
            },
            // the rows that pass search and filters, as the text of the visible columns
            exportCsv: function (fileName) {
                var cols = heads.map(function (th, i) { return { i: i, th: th }; }).filter(function (c) { return !c.th.hidden && c.th.getAttribute("data-col"); });
                var q = function (s) { s = String(s == null ? "" : s).replace(/\s+/g, " ").trim(); return /[",\n;]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
                var lines = [cols.map(function (c) { return q(c.th.textContent); }).join(",")];
                shown().forEach(function (tr) { lines.push(cols.map(function (c) { var td = tr.cells[c.i]; return q(td ? (td.getAttribute("data-export") || td.textContent) : ""); }).join(",")); });
                VK.download(new Blob(["﻿" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" }), fileName);
                return lines.length - 1;
            }
        };
    }
    document.querySelectorAll("[data-vk-grid]").forEach(function (el) { VK.grids[el.id] = Grid(el); });

    // VK.download is defined in vaikunth.js for the editors; make sure it exists here too
    if (!VK.download) VK.download = function (blob, name) {
        var a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = name;
        document.body.appendChild(a); a.click(); a.remove(); setTimeout(function () { URL.revokeObjectURL(a.href); }, 10000);
    };

    // messages from the server come in as toasts too
    document.querySelectorAll("[data-toast]").forEach(function (t) { VK.toast(t.getAttribute("data-toast"), t.getAttribute("data-toast-kind") || "success"); t.remove(); });

    // header card: flush at the top while the page is scrolled, so nothing shows above it
    (function () {
        var head = document.querySelector(".vk-ws-head");
        if (!head) return;
        function stick() { head.classList.toggle("is-stuck", window.scrollY > 12); }
        window.addEventListener("scroll", stick, { passive: true });
        stick();
    })();
})();

// ---------- side rail: a highlight box that slides from the item you were on to the one you open ----------
(function () {
    var rail = document.querySelector(".vk-rail");
    if (!rail) return;
    var items = Array.prototype.slice.call(rail.querySelectorAll(".vk-rail-item"));
    var on = rail.querySelector(".vk-rail-item.on");
    if (!items.length) return;
    var ind = document.createElement("span");
    ind.className = "vk-rail-ind no-anim";
    ind.setAttribute("aria-hidden", "true");
    rail.insertBefore(ind, rail.firstChild);
    rail.classList.add("has-ind");
    function place(item) {
        ind.hidden = !item;
        if (item) { ind.style.top = item.offsetTop + "px"; ind.style.height = item.offsetHeight + "px"; }
    }
    // a full page load (F5, sign-in): start from the item of the last page, if the browser remembers it
    var from = null;
    try { from = sessionStorage.getItem("vk-rail-from"); sessionStorage.removeItem("vk-rail-from"); } catch (e) { }
    place(items.filter(function (i) { return i.getAttribute("href") === from; })[0] || on);
    requestAnimationFrame(function () { requestAnimationFrame(function () { ind.classList.remove("no-anim"); place(on); }); });
    window.addEventListener("resize", function () { place(rail.querySelector(".vk-rail-item.on")); });
    /** Marks the rail item of a section and slides the box to it (the AJAX panel calls this after it opens a page). */
    window.VK = window.VK || {};
    VK.railTo = function (section, href) {
        var target = null;
        items.forEach(function (i) {
            var hit = (section && i.getAttribute("data-section") === section) || (!section && href && i.getAttribute("href") === href);
            i.classList.toggle("on", !!hit);
            if (hit) { i.setAttribute("aria-current", "page"); target = i; } else i.removeAttribute("aria-current");
        });
        place(target);
    };
})();

/* the Inbox item in the side rail shows how many messages aren't read yet (Sudarshana Inbox keeps it fresh on its own page) */
(function () {
    "use strict";
    var badge = document.getElementById("railUnread");
    if (!badge) return;
    function check() {
        if (document.hidden || location.pathname.toLowerCase().indexOf("/inbox") === 0) return;
        fetch("/inbox/editor/counts", { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (res) { if (res && res.ok) { var n = res.counts.inbox; badge.textContent = n > 99 ? "99+" : n; badge.hidden = !n; } })
            .catch(function () { });
    }
    check();
    setInterval(check, 120000);
})();
