/* Vishwaroopa Slides · a basic slide editor: thumbnails, layouts, themes, text boxes and shapes you can move,
   resize and type in, speaker notes, and a full-screen slide show. Box positions are in % of the slide. */
(function () {
    "use strict";
    var W = 960, H = 540;
    var data = {};
    try { data = JSON.parse(document.getElementById("docData").textContent || "{}"); } catch (e) { }
    var deck = { theme: data.theme || "sunrise", slides: data.slides || [] };
    if (!deck.slides.length) deck.slides.push(makeSlide("title"));
    var cur = 0, selBox = -1;
    var stage = document.getElementById("stage"), rail = document.getElementById("rail"), notes = document.getElementById("notes");

    function makeSlide(layout) {
        if (layout === "title") return { layout: "title", notes: "", boxes: [
            { kind: "text", x: 8, y: 30, w: 84, h: 20, html: "Click to add a title", fontSize: 54, role: "title" },
            { kind: "text", x: 8, y: 52, w: 84, h: 12, html: "Click to add a subtitle", fontSize: 24, role: "subtitle" }] };
        if (layout === "content") return { layout: "content", notes: "", boxes: [
            { kind: "text", x: 6, y: 6, w: 88, h: 14, html: "Click to add a title", fontSize: 40, role: "title" },
            { kind: "text", x: 6, y: 24, w: 88, h: 66, html: "<ul><li>Click to add text</li></ul>", fontSize: 24, role: "body" }] };
        return { layout: "blank", notes: "", boxes: [] };
    }

    function slideHtml(s, editable) {
        var html = "<div class='sl-slide " + deck.theme + " layout-" + s.layout + "'" + (s.background ? " style='background:" + s.background + "'" : "") + ">";
        s.boxes.forEach(function (b, i) {
            var cls = "sl-box " + (b.kind === "shape" ? "shape" : "") + " " + (b.role || "") + (editable && i === selBox ? " on" : "");
            var style = "left:" + b.x + "%;top:" + b.y + "%;width:" + b.w + "%;height:" + b.h + "%;font-size:" + b.fontSize + "px;"
                + (b.color ? "color:" + b.color + ";" : "") + (b.fill ? "background:" + b.fill + ";" : "");
            html += "<div class='" + cls + "' data-i='" + i + "' style='" + style + "'" + (editable ? " contenteditable='true' spellcheck='false'" : "") + ">"
                + b.html + (editable ? "<span class='sl-handle' contenteditable='false'></span>" : "") + "</div>";
        });
        return html + "</div>";
    }

    function drawRail() {
        rail.innerHTML = deck.slides.map(function (s, i) {
            return "<div class='sl-thumb" + (i === cur ? " on" : "") + "' data-s='" + i + "'><span class='sl-thumb-no'>" + (i + 1) + "</span><div class='sl-thumb-box'>" + slideHtml(s, false) + "</div></div>";
        }).join("");
        rail.querySelectorAll(".sl-thumb-box").forEach(function (b) { b.style.setProperty("--ts", b.clientWidth / W); });
    }
    function drawStage() {
        stage.innerHTML = slideHtml(deck.slides[cur], true);
        notes.value = deck.slides[cur].notes || "";
        document.getElementById("slideNo").textContent = cur + 1;
        document.getElementById("slideCount").textContent = deck.slides.length;
        document.getElementById("themeName").textContent = "Theme: " + deck.theme[0].toUpperCase() + deck.theme.slice(1);
        var l = document.querySelector('select[data-cmd="layout"]'); if (l) l.value = deck.slides[cur].layout;
        document.querySelectorAll(".sl-theme").forEach(function (t) { t.classList.toggle("on", t.getAttribute("data-arg") === deck.theme); });
    }
    function draw() { drawStage(); drawRail(); fit(); }

    // the stage keeps 960×540 and is scaled to fit
    var userZoom = 1;
    function fit() {
        var area = document.getElementById("stageArea");
        var s = Math.min((area.clientWidth - 52) / W, (area.clientHeight - 52) / H) * userZoom;
        stage.style.transform = "scale(" + Math.max(.2, s) + ")";
    }
    window.addEventListener("resize", function () { fit(); drawRail(); });
    VK.onZoom = function (z) { userZoom = z; fit(); };

    // ---- choosing slides ----
    rail.addEventListener("click", function (e) {
        var t = e.target.closest("[data-s]"); if (!t) return;
        syncBoxes(); cur = +t.getAttribute("data-s"); selBox = -1; draw();
    });

    // ---- boxes: select, type, move, resize ----
    function syncBoxes() {
        stage.querySelectorAll(".sl-box").forEach(function (el) {
            var b = deck.slides[cur].boxes[+el.getAttribute("data-i")]; if (!b) return;
            var clone = el.cloneNode(true); var h = clone.querySelector(".sl-handle"); if (h) h.remove();
            b.html = clone.innerHTML;
        });
    }
    function selectBox(i) {
        selBox = i;
        stage.querySelectorAll(".sl-box").forEach(function (el) { el.classList.toggle("on", +el.getAttribute("data-i") === i); });
        var b = deck.slides[cur].boxes[i];
        if (b) { var f = document.querySelector('select[data-cmd="fontSize"]'); if (f) f.value = String(b.fontSize); }
    }
    var drag = null;
    stage.addEventListener("mousedown", function (e) {
        var el = e.target.closest(".sl-box");
        if (!el) { syncBoxes(); selectBox(-1); return; }
        var i = +el.getAttribute("data-i"), b = deck.slides[cur].boxes[i];
        var scale = stage.getBoundingClientRect().width / W;
        if (e.target.classList.contains("sl-handle")) {
            e.preventDefault();
            drag = { mode: "size", b: b, el: el, sx: e.clientX, sy: e.clientY, w: b.w, h: b.h, scale: scale };
        } else if (selBox === i && document.activeElement !== el) {
            // second click on a selected box: drag it (double-click to type)
            e.preventDefault();
            drag = { mode: "move", b: b, el: el, sx: e.clientX, sy: e.clientY, x: b.x, y: b.y, scale: scale };
        } else if (selBox !== i) { syncBoxes(); selectBox(i); }
    });
    document.addEventListener("mousemove", function (e) {
        if (!drag) return;
        var dx = (e.clientX - drag.sx) / drag.scale / W * 100, dy = (e.clientY - drag.sy) / drag.scale / H * 100, b = drag.b;
        if (drag.mode === "move") { b.x = clamp(drag.x + dx, -5, 95); b.y = clamp(drag.y + dy, -5, 95); drag.el.style.left = b.x + "%"; drag.el.style.top = b.y + "%"; }
        else { b.w = clamp(drag.w + dx, 5, 100); b.h = clamp(drag.h + dy, 5, 100); drag.el.style.width = b.w + "%"; drag.el.style.height = b.h + "%"; }
        drag.moved = true;
    });
    document.addEventListener("mouseup", function () { if (drag && drag.moved) { changed(); drawRail(); } drag = null; });
    stage.addEventListener("dblclick", function (e) { var el = e.target.closest(".sl-box"); if (el) el.focus(); });
    stage.addEventListener("input", function () { changed(); railSoon(); });
    notes.addEventListener("input", function () { deck.slides[cur].notes = notes.value; changed(); });
    function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
    var railTimer; function railSoon() { clearTimeout(railTimer); railTimer = setTimeout(function () { syncBoxes(); drawRail(); }, 400); }

    // ---- ribbon ----
    function onBox(fn) { var b = deck.slides[cur].boxes[selBox]; if (!b) { VK.toast("Select a box on the slide first."); return; } syncBoxes(); fn(b); var keep = selBox; drawStage(); selectBox(keep); drawRail(); changed(); }
    ["bold", "italic", "underline", "insertUnorderedList", "insertOrderedList", "justifyLeft", "justifyCenter", "justifyRight", "foreColor"].forEach(function (cmd) {
        VK.on(cmd, function (arg) {
            var el = stage.querySelector(".sl-box.on");
            if (!el) { VK.toast("Select a box on the slide first."); return; }
            if (document.activeElement !== el) { el.focus(); document.execCommand("selectAll"); }
            document.execCommand(cmd, false, arg || null);
            syncBoxes(); drawRail(); changed();
        });
    });
    VK.on("fontSize", function (v) { onBox(function (b) { b.fontSize = +v; }); });
    VK.on("shapeFill", function (v) { onBox(function (b) { b.fill = v; if (b.kind !== "shape") b.color = null; }); });
    VK.on("newSlide", function (layout) { syncBoxes(); deck.slides.splice(cur + 1, 0, makeSlide(layout || "content")); cur++; selBox = -1; draw(); changed(); });
    VK.on("duplicate", function () { syncBoxes(); deck.slides.splice(cur + 1, 0, JSON.parse(JSON.stringify(deck.slides[cur]))); cur++; draw(); changed(); });
    VK.on("deleteSlide", function () {
        if (deck.slides.length === 1) { VK.toast("A deck needs at least one slide."); return; }
        deck.slides.splice(cur, 1); cur = Math.min(cur, deck.slides.length - 1); selBox = -1; draw(); changed();
    });
    VK.on("layout", function (v) {
        var s = deck.slides[cur]; if (s.layout === v) return;
        syncBoxes();
        var fresh = makeSlide(v), old = s.boxes.filter(function (b) { return b.role === "title"; })[0];
        if (old && fresh.boxes[0]) fresh.boxes[0].html = old.html;
        s.layout = v; s.boxes = fresh.boxes.concat(s.boxes.filter(function (b) { return !b.role; }));
        selBox = -1; draw(); changed();
    });
    VK.on("addText", function () { syncBoxes(); deck.slides[cur].boxes.push({ kind: "text", x: 30, y: 40, w: 40, h: 14, html: "New text", fontSize: 28 }); selBox = deck.slides[cur].boxes.length - 1; draw(); changed(); });
    VK.on("addShape", function () { syncBoxes(); deck.slides[cur].boxes.push({ kind: "shape", x: 36, y: 32, w: 28, h: 36, html: "Shape", fontSize: 26, fill: "#E8B04B", color: "#ffffff" }); selBox = deck.slides[cur].boxes.length - 1; draw(); changed(); });
    VK.on("addImage", function () {
        var input = document.createElement("input"); input.type = "file"; input.accept = "image/*";
        input.onchange = function () {
            var f = input.files[0]; if (!f) return;
            var r = new FileReader();
            r.onload = function () { syncBoxes(); deck.slides[cur].boxes.push({ kind: "text", x: 30, y: 25, w: 40, h: 50, html: "<img src='" + r.result + "' alt=''>", fontSize: 20 }); draw(); changed(); };
            r.readAsDataURL(f);
        };
        input.click();
    });
    VK.on("deleteBox", function () { if (selBox < 0) { VK.toast("Select a box on the slide first."); return; } syncBoxes(); deck.slides[cur].boxes.splice(selBox, 1); selBox = -1; draw(); changed(); });
    VK.on("theme", function (t) { syncBoxes(); deck.theme = t; draw(); changed(); });
    document.addEventListener("keydown", function (e) {
        if (!show.hidden) return;
        var inText = document.activeElement && (document.activeElement.isContentEditable || /INPUT|TEXTAREA|SELECT/.test(document.activeElement.tagName));
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") { e.preventDefault(); syncBoxes(); VK.save(content()); }
        else if (!inText && (e.key === "Delete" || e.key === "Backspace") && selBox >= 0) { e.preventDefault(); VK.handlers.deleteBox(); }
        else if (!inText && (e.key === "ArrowDown" || e.key === "PageDown") && cur < deck.slides.length - 1) { cur++; selBox = -1; draw(); }
        else if (!inText && (e.key === "ArrowUp" || e.key === "PageUp") && cur > 0) { cur--; selBox = -1; draw(); }
    });

    // ---- slide show ----
    var show = document.getElementById("show"), showSlide = document.getElementById("showSlide"), at = 0;
    function drawShow() {
        showSlide.innerHTML = slideHtml(deck.slides[at], false);
        var s = Math.min(window.innerWidth / W, window.innerHeight / H);
        showSlide.style.transform = "scale(" + s + ")";
        document.getElementById("showNo").textContent = (at + 1) + " / " + deck.slides.length;
    }
    VK.on("present", function (from) {
        syncBoxes(); at = from === "current" ? cur : 0; show.hidden = false; drawShow();
        if (document.documentElement.requestFullscreen) document.documentElement.requestFullscreen().catch(function () { });
    });
    function endShow() { show.hidden = true; if (document.fullscreenElement) document.exitFullscreen(); }
    document.addEventListener("keydown", function (e) {
        if (show.hidden) return;
        if (e.key === "Escape") endShow();
        else if (["ArrowRight", "ArrowDown", " ", "PageDown", "Enter"].indexOf(e.key) >= 0) { if (at < deck.slides.length - 1) { at++; drawShow(); } else endShow(); }
        else if (["ArrowLeft", "ArrowUp", "PageUp", "Backspace"].indexOf(e.key) >= 0 && at > 0) { at--; drawShow(); }
    });
    show.addEventListener("click", function () { if (at < deck.slides.length - 1) { at++; drawShow(); } else endShow(); });
    document.addEventListener("fullscreenchange", function () { if (!document.fullscreenElement && !show.hidden) show.hidden = true; });
    var presentBtn = document.querySelector('.vk-appbar [data-cmd="present"]'); if (presentBtn) presentBtn.hidden = false;

    // ---- save ----
    function content() { return JSON.stringify(deck); }
    function changed() { VK.changed(function () { syncBoxes(); return content(); }); }
    document.getElementById("docTitle").addEventListener("change", function () { syncBoxes(); VK.save(content()); });

    draw();
    window.VKSlides = { deck: deck, select: function (s, b) { cur = s; selBox = -1; draw(); if (b != null) selectBox(b); } };
})();
