/* Vishwaroopa Slides · turns a deck into a PowerPoint file (.pptx) in the browser with PptxGenJS.
   The editor's slide is 960 × 540 px; PowerPoint's 16:9 slide is 10 × 5.625 inches, so 96 px = 1 inch and 1 px = 0.75 pt. */
(function () {
    "use strict";
    var SW = 10, SH = 5.625;

    // the four themes, as PowerPoint can draw them (solid background, accent bar, text colours)
    var THEMES = {
        sunrise: { bg: "FFF4EA", text: "3A1F12", title: "A33A12", bar: "C24A1C", barH: 0.1, sun: "F4C46C" },
        peacock: { bg: "117A7A", text: "F0FBFA", title: "FFFFFF", bar: null, eye: true },
        night: { bg: "1B1E4B", text: "EEF0FF", title: "FFFFFF", bar: "14A3A0", barH: 0.06 },
        plain: { bg: "FFFFFF", text: "1F2238", title: "1F2238", bar: null }
    };

    // any CSS colour → "RRGGBB" (the browser does the parsing)
    var ctx = document.createElement("canvas").getContext("2d");
    function hex(css, fallback) {
        if (!css) return fallback;
        ctx.fillStyle = "#000"; ctx.fillStyle = css;
        var v = ctx.fillStyle;
        if (v[0] === "#") return v.slice(1).toUpperCase();
        var m = v.match(/\d+(\.\d+)?/g);
        if (!m || m.length < 3) return fallback;
        return m.slice(0, 3).map(function (n) { return ("0" + (+n).toString(16)).slice(-2); }).join("").toUpperCase();
    }

    // a box's HTML → PptxGenJS text runs (paragraphs, bullets, numbers, bold / italic / underline, colours)
    function runs(html, base) {
        var root = document.createElement("div");
        root.innerHTML = html;
        root.querySelectorAll(".sl-handle, img").forEach(function (n) { n.remove(); });
        var out = [];
        function para() { if (out.length) out[out.length - 1].options.breakLine = true; }
        var BLOCK = /^(P|DIV|LI|H[1-6]|UL|OL|BLOCKQUOTE)$/;
        function isBlock(n) { return n && n.nodeType === 1 && BLOCK.test(n.tagName); }
        function walk(node, st) {
            if (node.nodeType === 3) {
                var t = node.nodeValue.replace(/\s+/g, " ");
                if (!t.length) return;
                // spaces between blocks (e.g. between </li> and <li>) are only layout in the HTML
                if (!t.trim() && (/^(UL|OL)$/.test(node.parentNode.tagName) || isBlock(node.previousSibling) || isBlock(node.nextSibling))) return;
                var o = { bold: st.b, italic: st.i, underline: st.u ? { style: "sng" } : undefined, color: st.color || base.color, fontSize: base.size };
                if (st.li && st.li.pending) { o.bullet = st.li.bullet; o.indentLevel = st.level; st.li.pending = false; }
                out.push({ text: t, options: o });
                return;
            }
            if (node.nodeType !== 1) return;
            var tag = node.tagName, s = Object.assign({}, st);
            if (tag === "BR") { out.push({ text: "", options: { breakLine: true, fontSize: base.size } }); return; }
            if (tag === "B" || tag === "STRONG") s.b = true;
            if (tag === "I" || tag === "EM") s.i = true;
            if (tag === "U") s.u = true;
            if (node.style && node.style.color) s.color = hex(node.style.color, s.color);
            if (node.style && /bold|[6-9]00/.test(node.style.fontWeight)) s.b = true;
            if (tag === "FONT" && node.getAttribute("color")) s.color = hex(node.getAttribute("color"), s.color);
            if (tag === "UL" || tag === "OL") { s.list = tag; s.level = (st.level || 0) + (st.list ? 1 : 0); }
            var block = BLOCK.test(tag);
            if (block && tag !== "UL" && tag !== "OL") para();
            // the bullet goes on the first piece of text in the item, however deep it is
            if (tag === "LI") s.li = { pending: true, bullet: s.list === "OL" ? { type: "number" } : true };
            for (var c = node.firstChild; c; c = c.nextSibling) walk(c, s);
            if (block) para();
        }
        walk(root, { b: base.bold, i: false, u: false, level: 0 });
        // trim empty runs at the ends and the last line break
        while (out.length && !out[out.length - 1].text.trim() && !out[out.length - 1].options.bullet) out.pop();
        while (out.length && !out[0].text.trim() && !out[0].options.bullet) out.shift();
        if (out.length) out[out.length - 1].options.breakLine = false;
        return out.length ? out : [{ text: "", options: { fontSize: base.size } }];
    }

    function images(html) {
        var root = document.createElement("div");
        root.innerHTML = html;
        return Array.prototype.map.call(root.querySelectorAll("img"), function (i) { return i.getAttribute("src"); })
            .filter(function (s) { return /^data:image\//.test(s || ""); });
    }

    function build(deck, fileName) {
        var pptx = new PptxGenJS();
        pptx.layout = "LAYOUT_16x9";
        pptx.title = fileName.replace(/\.pptx$/i, "");
        pptx.company = "Vaikunth Office";
        var th = THEMES[deck.theme] || THEMES.plain;

        deck.slides.forEach(function (s) {
            var slide = pptx.addSlide();
            slide.background = { color: hex(s.background, th.bg) };
            if (!s.background) {
                if (th.sun && s.layout === "title")
                    slide.addShape(pptx.ShapeType.ellipse, { x: SW - 3.2, y: SH - 2.4, w: 3.95, h: 3.95, fill: { color: th.sun, transparency: 45 }, line: { type: "none" } });
                if (th.eye)
                    slide.addShape(pptx.ShapeType.ellipse, { x: SW - 0.85, y: 0.23, w: 0.58, h: 0.67, fill: { color: "E8B04B" }, line: { color: "14A3A0", width: 3 } });
                if (th.bar)
                    slide.addShape(pptx.ShapeType.rect, { x: 0, y: SH - th.barH, w: SW, h: th.barH, fill: { color: th.bar }, line: { type: "none" } });
            }
            (s.boxes || []).forEach(function (b) {
                var x = b.x / 100 * SW, y = b.y / 100 * SH, w = b.w / 100 * SW, h = b.h / 100 * SH;
                var isShape = b.kind === "shape", role = b.role || "";
                var color = hex(b.color, isShape ? "FFFFFF" : role === "title" ? th.title : th.text);
                var size = Math.max(6, Math.round((b.fontSize || 24) * 0.75));
                images(b.html || "").forEach(function (src) {
                    slide.addImage({ data: src, x: x, y: y, w: w, h: h, sizing: { type: "contain", w: w, h: h } });
                });
                var opts = {
                    x: x, y: y, w: w, h: h, margin: [4, 8, 4, 8], fontFace: "Segoe UI", color: color, fontSize: size,
                    valign: role === "body" ? "top" : "middle", align: isShape ? "center" : "left", fit: "shrink",
                    bold: role === "title", transparency: role === "subtitle" ? 15 : 0
                };
                if (isShape) {
                    opts.shape = pptx.ShapeType.roundRect;
                    opts.rectRadius = 0.2;
                    opts.fill = { color: hex(b.fill, "C24A1C") };
                } else if (b.fill) {
                    opts.fill = { color: hex(b.fill, "FFFFFF") };
                }
                slide.addText(runs(b.html || "", { color: color, size: size, bold: role === "title" }), opts);
            });
            if (s.notes) slide.addNotes(s.notes);
        });
        return pptx.writeFile({ fileName: fileName, compression: true });
    }

    window.VKPptx = { build: build, runs: runs };
})();
