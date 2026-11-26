/* Hrishikesha Workbook · a basic spreadsheet on the vk-sheet-grid table:
   select, type, formulas (+ - * / and SUM, AVERAGE, MIN, MAX, COUNT over ranges), formats, sort, a column chart, sheets. */
(function () {
    "use strict";
    var grid = document.getElementById("grid");
    var ROWS = +grid.getAttribute("data-rows"), COLS = +grid.getAttribute("data-cols");
    var bar = document.getElementById("formulaBar"), nameBox = document.getElementById("nameBox");

    var data = {};
    try { data = JSON.parse(document.getElementById("docData").textContent || "{}"); } catch (e) { }
    var sheets = (data.sheets || []).map(function (s) { return { name: s.name, rows: s.rows, cols: s.cols, cells: s.cells || {} }; });
    if (!sheets.length) sheets = [{ name: "Sheet1", rows: ROWS, cols: COLS, cells: {} }];
    var si = 0;
    function cells() { return sheets[si].cells; }

    // ---- addresses ----
    function colName(i) { var n = i + 1, s = ""; while (n > 0) { s = String.fromCharCode(65 + (n - 1) % 26) + s; n = Math.floor((n - 1) / 26); } return s; }
    function colIndex(s) { var n = 0; for (var i = 0; i < s.length; i++) n = n * 26 + (s.charCodeAt(i) - 64); return n - 1; }
    function parseAddr(a) { var m = /^([A-Z]+)(\d+)$/.exec(a); return m ? { c: colIndex(m[1]), r: +m[2] } : null; }
    function addr(c, r) { return colName(c) + r; }
    function td(a) { return grid.querySelector('td[data-addr="' + a + '"]'); }

    // ---- formulas: a small recursive-descent parser (no eval) ----
    function evaluate(a, seen) {
        var cell = cells()[a];
        if (!cell || cell.input === "" || cell.input == null) return "";
        var input = String(cell.input);
        if (input[0] !== "=") { var n = Number(input.replace(/,/g, "")); return input.trim() !== "" && !isNaN(n) ? n : input; }
        seen = seen || {};
        if (seen[a]) return "#CYCLE!";
        seen[a] = true;
        try { var v = parse(input.slice(1).toUpperCase(), seen); delete seen[a]; return typeof v === "number" && !isFinite(v) ? "#DIV/0!" : v; }
        catch (e) { delete seen[a]; return e.message && e.message[0] === "#" ? e.message : "#ERROR!"; }
    }
    function parse(src, seen) {
        var i = 0;
        function ws() { while (src[i] === " ") i++; }
        function num(v) { if (typeof v === "string") { if (v === "") return 0; if (v[0] === "#") throw new Error(v); var n = Number(v); if (isNaN(n)) throw new Error("#VALUE!"); return n; } return v; }
        function expr() { var v = term(); ws(); while (src[i] === "+" || src[i] === "-") { var op = src[i++]; var r = term(); v = op === "+" ? num(v) + num(r) : num(v) - num(r); ws(); } return v; }
        function term() { var v = factor(); ws(); while (src[i] === "*" || src[i] === "/") { var op = src[i++]; var r = factor(); v = op === "*" ? num(v) * num(r) : num(v) / num(r); ws(); } return v; }
        function factor() {
            ws();
            if (src[i] === "-") { i++; return -num(factor()); }
            if (src[i] === "(") { i++; var v = expr(); ws(); if (src[i++] !== ")") throw new Error("#ERROR!"); return v; }
            var m = /^\d+(\.\d+)?/.exec(src.slice(i));
            if (m) { i += m[0].length; return Number(m[0]); }
            m = /^([A-Z]+)\(/.exec(src.slice(i));
            if (m) { i += m[0].length; return func(m[1], args()); }
            m = /^([A-Z]+\d+)(:([A-Z]+\d+))?/.exec(src.slice(i));
            if (m) { i += m[0].length; if (m[3]) throw new Error("#VALUE!"); return evaluate(m[1], seen); }
            throw new Error("#ERROR!");
        }
        function args() {
            var list = [];
            ws();
            if (src[i] === ")") { i++; return list; }
            while (true) {
                ws();
                var m = /^([A-Z]+\d+):([A-Z]+\d+)/.exec(src.slice(i));
                if (m) { i += m[0].length; list = list.concat(range(m[1], m[2])); }
                else list.push(expr());
                ws();
                if (src[i] === ",") { i++; continue; }
                if (src[i] === ")") { i++; return list; }
                throw new Error("#ERROR!");
            }
        }
        function range(a, b) {
            var p = parseAddr(a), q = parseAddr(b), out = [];
            for (var r = Math.min(p.r, q.r); r <= Math.max(p.r, q.r); r++)
                for (var c = Math.min(p.c, q.c); c <= Math.max(p.c, q.c); c++) out.push(evaluate(addr(c, r), seen));
            return out;
        }
        function func(name, list) {
            list.forEach(function (v) { if (typeof v === "string" && v[0] === "#") throw new Error(v); });
            var nums = list.filter(function (v) { return typeof v === "number"; });
            switch (name) {
                case "SUM": return nums.reduce(function (s, v) { return s + v; }, 0);
                case "AVERAGE": if (!nums.length) throw new Error("#DIV/0!"); return nums.reduce(function (s, v) { return s + v; }, 0) / nums.length;
                case "MIN": return nums.length ? Math.min.apply(null, nums) : 0;
                case "MAX": return nums.length ? Math.max.apply(null, nums) : 0;
                case "COUNT": return nums.length;
                case "ROUND": return Math.round(num(list[0]) * Math.pow(10, num(list[1] || 0))) / Math.pow(10, num(list[1] || 0));
                default: throw new Error("#NAME?");
            }
        }
        var v = expr(); ws();
        if (i < src.length) throw new Error("#ERROR!");
        return v;
    }

    function show(v, fmt) {
        if (typeof v !== "number") return String(v);
        switch (fmt) {
            case "currency": return "₹" + v.toLocaleString("en-IN", { maximumFractionDigits: 0 });
            case "percent": return (v * 100).toFixed(1) + "%";
            case "number": return v.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            default: return Math.abs(v) < 1e15 ? String(Math.round(v * 1e10) / 1e10) : v.toExponential(4);
        }
    }

    // ---- draw ----
    function render() {
        grid.querySelectorAll("td").forEach(function (el) {
            if (el.classList.contains("editing")) return;
            var a = el.getAttribute("data-addr"), cell = cells()[a] || {}, v = evaluate(a);
            el.textContent = show(v, cell.format);
            el.classList.toggle("num", typeof v === "number");
            el.classList.toggle("err", typeof v === "string" && v[0] === "#");
            el.style.fontWeight = cell.bold ? "700" : "";
            el.style.fontStyle = cell.italic ? "italic" : "";
            el.style.background = cell.fill || "";
            el.style.color = cell.color || "";
            el.style.textAlign = cell.align || "";
        });
        drawTabs();
        if (!document.getElementById("chart").hidden) drawChart();
    }

    // ---- selection ----
    var cur = { c: 0, r: 1 }, anchor = { c: 0, r: 1 }, editing = null;
    function selRange() { return { c1: Math.min(cur.c, anchor.c), c2: Math.max(cur.c, anchor.c), r1: Math.min(cur.r, anchor.r), r2: Math.max(cur.r, anchor.r) }; }
    function eachSel(fn) { var s = selRange(); for (var r = s.r1; r <= s.r2; r++) for (var c = s.c1; c <= s.c2; c++) fn(addr(c, r), c, r); }
    function select(c, r, extend) {
        cur = { c: Math.max(0, Math.min(COLS - 1, c)), r: Math.max(1, Math.min(ROWS, r)) };
        if (!extend) anchor = { c: cur.c, r: cur.r };
        grid.querySelectorAll("td.sel, td.cur").forEach(function (el) { el.classList.remove("sel", "cur"); });
        grid.querySelectorAll("th.hl").forEach(function (el) { el.classList.remove("hl"); });
        var s = selRange();
        eachSel(function (a) { td(a).classList.add("sel"); });
        for (var c2 = s.c1; c2 <= s.c2; c2++) grid.querySelector('thead th[data-col="' + c2 + '"]').classList.add("hl");
        for (var r2 = s.r1; r2 <= s.r2; r2++) grid.querySelector('tbody th[data-row="' + r2 + '"]').classList.add("hl");
        var el = td(addr(cur.c, cur.r));
        el.classList.add("cur");
        el.scrollIntoView({ block: "nearest", inline: "nearest" });
        var a = addr(cur.c, cur.r);
        nameBox.value = s.c1 === s.c2 && s.r1 === s.r2 ? a : addr(s.c1, s.r1) + ":" + addr(s.c2, s.r2);
        bar.value = (cells()[a] || {}).input || "";
        status();
        toggles();
    }
    function status() {
        var nums = [];
        eachSel(function (a) { var v = evaluate(a); if (typeof v === "number") nums.push(v); });
        var el = document.getElementById("statSel");
        if (nums.length > 1) {
            var sum = nums.reduce(function (s, v) { return s + v; }, 0);
            el.innerHTML = "Average: <b>" + show(sum / nums.length) + "</b> &nbsp; Count: <b>" + nums.length + "</b> &nbsp; Sum: <b>" + show(sum) + "</b>";
        } else el.textContent = "";
    }
    function toggles() {
        var cell = cells()[addr(cur.c, cur.r)] || {};
        document.querySelectorAll('.vk-rb-toggle[data-cmd="bold"]').forEach(function (b) { b.classList.toggle("on", !!cell.bold); });
        document.querySelectorAll('.vk-rb-toggle[data-cmd="italic"]').forEach(function (b) { b.classList.toggle("on", !!cell.italic); });
        var f = document.querySelector('select[data-cmd="format"]'); if (f) f.value = cell.format || "general";
    }

    // ---- editing ----
    function setInput(a, value) {
        var cell = cells()[a] || (cells()[a] = {});
        cell.input = value;
        if (value === "" && !cell.bold && !cell.italic && !cell.fill && !cell.color && !cell.align && !cell.format) delete cells()[a];
    }
    function startEdit(initial) {
        var a = addr(cur.c, cur.r), el = td(a);
        editing = a;
        el.classList.add("editing");
        el.innerHTML = "";
        var input = document.createElement("input");
        input.value = initial != null ? initial : ((cells()[a] || {}).input || "");
        el.appendChild(input);
        input.focus();
        input.addEventListener("input", function () { bar.value = input.value; });
    }
    function commit(move) {
        if (!editing) return;
        var el = td(editing), input = el.querySelector("input");
        setInput(editing, input ? input.value : bar.value);
        el.classList.remove("editing");
        editing = null;
        render();
        changed();
        if (move) select(cur.c + move.c, cur.r + move.r); else select(cur.c, cur.r);
    }
    function cancel() { if (!editing) return; td(editing).classList.remove("editing"); editing = null; render(); select(cur.c, cur.r); }

    var dragging = false;
    grid.addEventListener("mousedown", function (e) {
        var el = e.target.closest("td"); if (!el || el.classList.contains("editing")) return;
        if (editing) commit();
        var p = parseAddr(el.getAttribute("data-addr"));
        select(p.c, p.r, e.shiftKey);
        dragging = true;
        e.preventDefault();
    });
    grid.addEventListener("mouseover", function (e) {
        if (!dragging) return;
        var el = e.target.closest("td"); if (!el) return;
        var p = parseAddr(el.getAttribute("data-addr")); select(p.c, p.r, true);
    });
    document.addEventListener("mouseup", function () { dragging = false; });
    grid.addEventListener("dblclick", function (e) { if (e.target.closest("td")) startEdit(); });

    document.addEventListener("keydown", function (e) {
        if (e.target === bar || e.target === nameBox || e.target.id === "docTitle" || e.target.closest("dialog")) return;
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") { e.preventDefault(); VK.save(content()); return; }
        if (editing) {
            if (e.key === "Enter") { e.preventDefault(); commit({ c: 0, r: 1 }); }
            else if (e.key === "Tab") { e.preventDefault(); commit({ c: 1, r: 0 }); }
            else if (e.key === "Escape") cancel();
            return;
        }
        var moves = { ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0], Enter: [0, 1], Tab: [1, 0] };
        if (moves[e.key]) { e.preventDefault(); select(cur.c + moves[e.key][0], cur.r + moves[e.key][1], e.shiftKey && e.key.indexOf("Arrow") === 0); return; }
        if (e.key === "F2") { e.preventDefault(); startEdit(); return; }
        if (e.key === "Delete" || e.key === "Backspace") { e.preventDefault(); run.clear(); return; }
        if (e.ctrlKey || e.metaKey) {
            var k = e.key.toLowerCase();
            if (k === "b") { e.preventDefault(); run.bold(); } else if (k === "i") { e.preventDefault(); run.italic(); }
            else if (k === "c") run.copy(); else if (k === "x") run.cut(); else if (k === "v") { e.preventDefault(); run.paste(); }
            return;
        }
        if (e.key.length === 1) { e.preventDefault(); startEdit(e.key); }
    });
    bar.addEventListener("keydown", function (e) {
        if (e.key === "Enter") { e.preventDefault(); setInput(addr(cur.c, cur.r), bar.value); render(); changed(); select(cur.c, cur.r + 1); }
        if (e.key === "Escape") { bar.value = (cells()[addr(cur.c, cur.r)] || {}).input || ""; }
    });
    nameBox.addEventListener("keydown", function (e) {
        if (e.key !== "Enter") return;
        var p = parseAddr(nameBox.value.trim().toUpperCase()); if (p) select(p.c, p.r);
    });

    // ---- ribbon commands ----
    function fmtSel(fn) { eachSel(function (a) { fn(cells()[a] || (cells()[a] = { input: "" })); }); render(); select(cur.c, cur.r, true); changed(); }
    var clip = null;
    var run = {
        bold: function () { var on = !(cells()[addr(cur.c, cur.r)] || {}).bold; fmtSel(function (c) { c.bold = on; }); },
        italic: function () { var on = !(cells()[addr(cur.c, cur.r)] || {}).italic; fmtSel(function (c) { c.italic = on; }); },
        fill: function (v) { fmtSel(function (c) { c.fill = v; }); },
        color: function (v) { fmtSel(function (c) { c.color = v; }); },
        align: function (v) { fmtSel(function (c) { c.align = v; }); },
        format: function (v) { fmtSel(function (c) { c.format = v === "general" ? null : v; }); },
        clearFormat: function () { fmtSel(function (c) { c.bold = c.italic = false; c.fill = c.color = c.align = c.format = null; }); },
        clear: function () { eachSel(function (a) { setInput(a, ""); }); render(); select(cur.c, cur.r, true); changed(); },
        copy: function () { var s = selRange(), rows = []; for (var r = s.r1; r <= s.r2; r++) { var row = []; for (var c = s.c1; c <= s.c2; c++) row.push(JSON.parse(JSON.stringify(cells()[addr(c, r)] || { input: "" }))); rows.push(row); } clip = rows; VK.toast("Copied"); },
        cut: function () { run.copy(); run.clear(); },
        paste: function () {
            if (!clip) { VK.toast("Nothing copied yet."); return; }
            clip.forEach(function (row, dr) { row.forEach(function (cell, dc) { var a = addr(cur.c + dc, cur.r + dr); if (td(a)) cells()[a] = JSON.parse(JSON.stringify(cell)); }); });
            render(); changed();
        },
        autoSum: function () {
            var s = selRange();
            if (s.r1 !== s.r2) { for (var c = s.c1; c <= s.c2; c++) { setInput(addr(c, s.r2 + 1), "=SUM(" + addr(c, s.r1) + ":" + addr(c, s.r2) + ")"); } render(); changed(); select(s.c1, s.r2 + 1); return; }
            var r = cur.r - 1; while (r >= 1 && typeof evaluate(addr(cur.c, r)) === "number") r--;
            if (r === cur.r - 1) { VK.toast("Select numbers first, or a cell under a column of numbers."); return; }
            setInput(addr(cur.c, cur.r), "=SUM(" + addr(cur.c, r + 1) + ":" + addr(cur.c, cur.r - 1) + ")"); render(); changed(); select(cur.c, cur.r);
        },
        fn: function (name) {
            var s = selRange();
            if (s.r1 === s.r2 && s.c1 === s.c2) { startEdit("=" + name + "("); return; }
            setInput(addr(s.c1, s.r2 + 1), "=" + name + "(" + addr(s.c1, s.r1) + ":" + addr(s.c2, s.r2) + ")"); render(); changed(); select(s.c1, s.r2 + 1);
        },
        sortAsc: function () { sortSel(1); },
        sortDesc: function () { sortSel(-1); },
        insertRow: function () { shiftRows(cur.r, 1); },
        deleteRow: function () { shiftRows(cur.r, -1); },
        chart: function () { document.getElementById("chart").hidden = false; chartRange = selRange(); drawChart(); },
        closeChart: function () { document.getElementById("chart").hidden = true; },
        addSheet: function () { sheets.push({ name: "Sheet" + (sheets.length + 1), rows: ROWS, cols: COLS, cells: {} }); si = sheets.length - 1; render(); select(0, 1); changed(); },
        toggleGrid: function (_, b) { grid.classList.toggle("nogrid"); b.classList.toggle("on", !grid.classList.contains("nogrid")); }
    };
    Object.keys(run).forEach(function (k) { VK.on(k, run[k]); });

    function sortSel(dir) {
        var s = selRange(); if (s.r1 === s.r2) { VK.toast("Select the rows to sort."); return; }
        var rows = [];
        for (var r = s.r1; r <= s.r2; r++) { var row = []; for (var c = s.c1; c <= s.c2; c++) row.push(cells()[addr(c, r)]); rows.push({ key: evaluate(addr(s.c1, r)), row: row }); }
        rows.sort(function (a, b) { return (a.key > b.key ? 1 : a.key < b.key ? -1 : 0) * dir; });
        rows.forEach(function (x, i) { x.row.forEach(function (cell, j) { var a = addr(s.c1 + j, s.r1 + i); if (cell) cells()[a] = cell; else delete cells()[a]; }); });
        render(); changed(); VK.toast("Sorted " + rows.length + " rows. (Formulas keep their own references.)");
    }
    function shiftRows(at, delta) {
        var next = {};
        Object.keys(cells()).forEach(function (a) {
            var p = parseAddr(a); if (!p) return;
            if (p.r < at) next[a] = cells()[a];
            else if (delta < 0 && p.r === at) return;
            else if (p.r + delta >= 1 && p.r + delta <= ROWS) next[addr(p.c, p.r + delta)] = cells()[a];
        });
        sheets[si].cells = next; render(); changed(); select(cur.c, cur.r);
    }

    // ---- column chart of the selection (first column = labels, next column = values) ----
    var chartRange = null;
    function drawChart() {
        var s = chartRange || selRange(), labels = [], values = [];
        for (var r = s.r1; r <= s.r2; r++) {
            var v = evaluate(addr(Math.min(s.c1 + 1, s.c2), r));
            if (typeof v === "number") { labels.push(String(evaluate(addr(s.c1, r)))); values.push(v); }
        }
        document.getElementById("chartTitle").textContent = String(evaluate(addr(Math.min(s.c1 + 1, s.c2), s.r1)) || "Chart");
        var body = document.getElementById("chartBody");
        if (!values.length) { body.innerHTML = "<p style='color:#6B7280;font-size:13px'>Select a label column and a number column, then choose Column chart.</p>"; return; }
        var W = 390, H = 220, pad = 28, max = Math.max.apply(null, values) || 1, bw = (W - pad * 2) / values.length;
        var svg = "<svg viewBox='0 0 " + W + " " + (H + 24) + "' width='100%'>";
        for (var g = 0; g <= 4; g++) { var y = pad + (H - pad * 2) * g / 4; svg += "<line x1='" + pad + "' x2='" + (W - 6) + "' y1='" + y + "' y2='" + y + "' stroke='#EEF2F0'/>"; }
        values.forEach(function (v, i) {
            var h = (H - pad * 2) * v / max, x = pad + i * bw + bw * .18, y = H - pad - h;
            svg += "<rect x='" + x + "' y='" + y + "' width='" + bw * .64 + "' height='" + h + "' rx='4' fill='" + (v === max ? "#E8B04B" : "#115C45") + "'/>";
            svg += "<text x='" + (x + bw * .32) + "' y='" + (H - pad + 15) + "' text-anchor='middle'>" + labels[i].replace(/[<&]/g, "") + "</text>";
            svg += "<text x='" + (x + bw * .32) + "' y='" + (y - 5) + "' text-anchor='middle'>" + (v >= 1000 ? Math.round(v / 1000) + "k" : show(v)) + "</text>";
        });
        body.innerHTML = svg + "</svg>";
    }

    // ---- sheet tabs ----
    function drawTabs() {
        var tabs = document.getElementById("sheetTabs");
        tabs.innerHTML = sheets.map(function (s, i) { return "<button type='button' data-sheet='" + i + "' class='" + (i === si ? "on" : "") + "'>" + s.name.replace(/[<&]/g, "") + "</button>"; }).join("")
            + "<button type='button' class='wb-add' data-cmd='addSheet' title='New sheet'><i class='fa fa-plus-circle'></i></button>";
    }
    document.getElementById("sheetTabs").addEventListener("click", function (e) {
        var b = e.target.closest("[data-sheet]"); if (!b) return;
        si = +b.getAttribute("data-sheet"); render(); select(0, 1);
    });

    // ---- zoom, save ----
    VK.onZoom = function (z) { grid.style.zoom = z; };
    function content() { return JSON.stringify({ sheets: sheets }); }
    function changed() { VK.changed(content); }
    document.getElementById("docTitle").addEventListener("change", function () { VK.save(content()); });

    render();
    select(0, 1);
    window.VKWorkbook = { select: select, run: run };
})();
