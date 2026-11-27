/* Shashvata Reader · read a PDF in the browser (pdf.js), with page thumbnails, find, notes and bookmarks.
   The file on the server is JSON: { fileName, pdf (base64), sizeBytes, pages, lastPage, zoom, bookmarks: [page], notes: [...] }.
   Open PDF puts a PDF into an empty Reader file, or starts a new Reader file when this one already has a PDF. */
(function () {
    "use strict";
    var lib = window.pdfjsLib;
    lib.GlobalWorkerOptions.workerSrc = "/_content/Vaikunth.ShashvataReader/lib/pdfjs/pdf.worker.min.js";
    var MAX_MB = 100;
    var CSS_UNITS = 96 / 72;                // pdf.js scale 1 = 72 dpi; 100% here = real size on screen

    var $ = function (id) { return document.getElementById(id); };
    var viewer = $("viewer"), pagesEl = $("pages"), thumbsEl = $("thumbs");
    var state = {};
    try { state = JSON.parse($("docData").textContent || "{}"); } catch (e) { }
    state.bookmarks = state.bookmarks || [];
    state.notes = state.notes || [];

    var pdf = null, sizes = [], cur = 1, rotation = 0, zoomMode = null, pageEls = [], rendered = {}, textCache = {};
    var find = { q: "", hits: [], i: -1 };

    // ---------- helpers ----------
    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
    function bytesText(n) { if (!n) return ""; var u = ["B", "KB", "MB", "GB"], i = 0; while (n >= 1024 && i < 3) { n /= 1024; i++; } return (i ? n.toFixed(n < 10 ? 1 : 0) : n) + " " + u[i]; }
    function toBase64(buf) {
        var b = new Uint8Array(buf), s = "", CH = 0x8000;
        for (var i = 0; i < b.length; i += CH) s += String.fromCharCode.apply(null, b.subarray(i, i + CH));
        return btoa(s);
    }
    function fromBase64(b64) {
        var s = atob(b64), out = new Uint8Array(s.length);
        for (var i = 0; i < s.length; i++) out[i] = s.charCodeAt(i);
        return out;
    }
    function content() { state.lastPage = cur; state.zoom = VK.zoom; return JSON.stringify(state); }
    function changed() { VK.changed(content); }
    function scale() { return (VK.zoom / 100) * CSS_UNITS; }

    // ---------- open a PDF from this computer ----------
    var pick = $("pdfPick");
    VK.on("openPdf", function () { pick.value = ""; pick.click(); });
    pick.addEventListener("change", function () { if (pick.files[0]) openFile(pick.files[0]); });

    function openFile(file) {
        if (!/\.pdf$/i.test(file.name) && file.type !== "application/pdf") { VK.toast("That isn't a PDF. Pick a file that ends in .pdf."); return; }
        if (file.size > MAX_MB * 1048576) { VK.toast("This PDF is " + bytesText(file.size) + ". The most Shashvata Reader takes is " + MAX_MB + " MB."); return; }
        var reader = new FileReader();
        $("loading").hidden = false;
        reader.onload = function () {
            var b64 = toBase64(reader.result);
            var title = file.name.replace(/\.pdf$/i, "");
            var fresh = { fileName: file.name, pdf: b64, sizeBytes: file.size, pages: 0, lastPage: 1, zoom: 0, bookmarks: [], notes: [] };
            if (state.pdf) {
                // this file already has a PDF: the new one becomes a new Reader file, in this tab
                VK.docId = null;
                $("docTitle").value = title;
                VK.save(JSON.stringify(fresh)).then(function (res) {
                    if (res && res.ok) location.href = "/reader/editor?id=" + res.id;
                    else { $("loading").hidden = true; VK.toast((res && res.error) || "Couldn't save the PDF."); }
                });
                return;
            }
            state = fresh;
            var t = $("docTitle");
            if (!t.value || /^untitled$/i.test(t.value.trim())) t.value = title;
            load(true).then(function () { VK.save(content()); });
        };
        reader.onerror = function () { $("loading").hidden = true; VK.toast("Couldn't read that file."); };
        reader.readAsArrayBuffer(file);
    }

    // drag a PDF onto the reader
    var work = $("work"), mask = $("dropMask"), depth = 0;
    work.addEventListener("dragenter", function (e) { if (hasFile(e)) { depth++; mask.hidden = false; } });
    work.addEventListener("dragleave", function () { if (--depth <= 0) { depth = 0; mask.hidden = true; } });
    work.addEventListener("dragover", function (e) { if (hasFile(e)) e.preventDefault(); });
    work.addEventListener("drop", function (e) {
        e.preventDefault(); depth = 0; mask.hidden = true;
        var f = e.dataTransfer.files && e.dataTransfer.files[0];
        if (f) openFile(f);
    });
    function hasFile(e) { return e.dataTransfer && Array.prototype.indexOf.call(e.dataTransfer.types || [], "Files") >= 0; }

    // ---------- load and lay out the pages ----------
    function load(first) {
        if (!state.pdf) { showEmpty(); return Promise.resolve(); }
        $("empty").hidden = true;
        $("loading").hidden = false;
        return lib.getDocument({ data: fromBase64(state.pdf) }).promise.then(function (doc) {
            pdf = doc;
            state.pages = doc.numPages;
            var all = [];
            for (var i = 1; i <= doc.numPages; i++) all.push(doc.getPage(i).then(function (p) { var v = p.getViewport({ scale: 1 }); return { w: v.width, h: v.height }; }));
            return Promise.all(all);
        }).then(function (s) {
            sizes = s;
            $("loading").hidden = true;
            if (!state.zoom || first) fitWidth(); else VK.setZoom(state.zoom);
            build();
            buildThumbs();
            refreshLists();
            goTo(Math.min(state.lastPage || 1, sizes.length), true);
            $("pageCount").textContent = sizes.length;
            $("pageNo").max = sizes.length;
            $("statSize").textContent = (state.fileName ? state.fileName + " · " : "") + bytesText(state.sizeBytes);
        }).catch(function (err) {
            $("loading").hidden = true;
            showEmpty();
            VK.toast("This PDF couldn't be opened" + (err && err.name === "PasswordException" ? ": it has a password." : "."));
        });
    }
    function showEmpty() {
        $("empty").hidden = false;
        $("statPage").innerHTML = '<i class="fa fa-file-pdf-o" style="color:#8E1B3A"></i> No PDF yet';
        pagesEl.innerHTML = ""; thumbsEl.innerHTML = "<p class='rd-side-empty'>Open a PDF to see its pages here.</p>";
        refreshLists();
    }

    function pageSize(i) { var s = sizes[i - 1]; return rotation % 180 ? { w: s.h, h: s.w } : s; }
    function build() {
        rendered = {};
        pagesEl.innerHTML = "";
        pageEls = [];
        var k = scale();
        for (var i = 1; i <= sizes.length; i++) {
            var sz = pageSize(i);
            var el = document.createElement("div");
            el.className = "rd-page";
            el.setAttribute("data-page", i);
            el.style.width = Math.floor(sz.w * k) + "px";
            el.style.height = Math.floor(sz.h * k) + "px";
            el.innerHTML = "<span class='rd-page-no'>" + i + "</span>";
            pagesEl.appendChild(el);
            pageEls.push(el);
        }
        markPages();
        observe();
    }

    // render only the pages near the screen
    var io = null;
    function observe() {
        if (io) io.disconnect();
        io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) { if (en.isIntersecting) renderPage(+en.target.getAttribute("data-page")); });
        }, { root: viewer, rootMargin: "600px 0px" });
        pageEls.forEach(function (el) { io.observe(el); });
    }
    function renderPage(n) {
        if (rendered[n] || !pdf) return;
        rendered[n] = true;
        var el = pageEls[n - 1], k = scale();
        pdf.getPage(n).then(function (page) {
            var vp = page.getViewport({ scale: k, rotation: rotation });
            var dpr = window.devicePixelRatio || 1;
            var canvas = document.createElement("canvas");
            canvas.width = Math.floor(vp.width * dpr); canvas.height = Math.floor(vp.height * dpr);
            canvas.style.width = Math.floor(vp.width) + "px"; canvas.style.height = Math.floor(vp.height) + "px";
            var old = el.querySelector("canvas"); if (old) old.remove();
            el.insertBefore(canvas, el.firstChild);
            return page.render({ canvasContext: canvas.getContext("2d"), viewport: vp, transform: dpr !== 1 ? [dpr, 0, 0, dpr, 0, 0] : null }).promise.then(function () {
                // a text layer on top, so words can be selected and found
                var tl = document.createElement("div");
                tl.className = "textLayer";
                tl.style.setProperty("--scale-factor", vp.scale);
                el.appendChild(tl);
                var task = lib.renderTextLayer({ textContentSource: page.streamTextContent(), container: tl, viewport: vp, textDivs: [] });
                return task.promise.then(function () { highlight(el); });
            });
        }).catch(function () { rendered[n] = false; });
    }

    // ---------- where we are ----------
    viewer.addEventListener("scroll", function () {
        if (!pageEls.length) return;
        var top = viewer.scrollTop + 60, best = 1;
        for (var i = 0; i < pageEls.length; i++) { if (pageEls[i].offsetTop <= top) best = i + 1; else break; }
        if (best !== cur) { cur = best; showPage(); }
    });
    function showPage() {
        $("pageNo").value = cur;
        $("statPage").innerHTML = '<i class="fa fa-file-pdf-o" style="color:#8E1B3A"></i> Page ' + cur + " of " + sizes.length;
        var on = state.bookmarks.indexOf(cur) >= 0;
        var b = $("bookmarkBtn");
        if (b) { b.classList.toggle("on", on); var i = b.querySelector("i"); if (i) i.className = "fa " + (on ? "fa-bookmark" : "fa-bookmark-o"); }
        thumbsEl.querySelectorAll(".rd-thumb").forEach(function (t) { t.classList.toggle("on", +t.getAttribute("data-page") === cur); });
        var active = thumbsEl.querySelector(".rd-thumb.on");
        if (active && !$("side").hidden) {
            var r = active.getBoundingClientRect(), box = thumbsEl.getBoundingClientRect();
            if (r.top < box.top || r.bottom > box.bottom) active.scrollIntoView({ block: "nearest" });
        }
    }
    function goTo(n, instant) {
        if (!pageEls.length) return;
        n = Math.max(1, Math.min(pageEls.length, n | 0));
        viewer.scrollTo({ top: pageEls[n - 1].offsetTop - 14, behavior: instant ? "auto" : "smooth" });
        cur = n; showPage();
    }
    VK.on("goFirst", function () { goTo(1); });
    VK.on("goLast", function () { goTo(sizes.length); });
    VK.on("goPrev", function () { goTo(cur - 1); });
    VK.on("goNext", function () { goTo(cur + 1); });
    $("pageNo").addEventListener("change", function () { goTo(+this.value); });
    viewer.addEventListener("keydown", function (e) {
        if (e.target !== viewer) return;
        if (e.key === "Home") { e.preventDefault(); goTo(1); }
        if (e.key === "End") { e.preventDefault(); goTo(sizes.length); }
        if (e.key === "ArrowRight") { e.preventDefault(); goTo(cur + 1); }
        if (e.key === "ArrowLeft") { e.preventDefault(); goTo(cur - 1); }
    });

    // ---------- zoom and turn ----------
    function rezoom() {
        if (!sizes.length) return;
        var keep = cur;
        build();
        goTo(keep, true);
    }
    VK.onZoom = function () { rezoom(); };
    function fitWidth() {
        if (!sizes.length) return;
        var w = viewer.clientWidth - 64, s = pageSize(1);
        VK.zoom = -1;   // force the redraw even if the number is the same
        VK.setZoom(Math.max(50, Math.min(200, Math.floor(w / (s.w * CSS_UNITS) * 100))));
    }
    function fitPage() {
        if (!sizes.length) return;
        var s = pageSize(1);
        var z = Math.min((viewer.clientWidth - 64) / (s.w * CSS_UNITS), (viewer.clientHeight - 40) / (s.h * CSS_UNITS)) * 100;
        VK.zoom = -1;
        VK.setZoom(Math.max(50, Math.min(200, Math.floor(z))));
    }
    VK.on("zoomTo", function (v) {
        zoomMode = v;
        if (v === "width") fitWidth(); else if (v === "page") fitPage(); else VK.setZoom(+v);
        changed();
    });
    var resizeTimer;
    window.addEventListener("resize", function () { clearTimeout(resizeTimer); resizeTimer = setTimeout(function () { if (zoomMode === "width") fitWidth(); else if (zoomMode === "page") fitPage(); }, 200); });
    VK.on("rotate", function () { rotation = (rotation + 90) % 360; rezoom(); buildThumbs(); });

    // night reading (remembered in this browser)
    function night(on) {
        viewer.classList.toggle("rd-night", on);
        var b = $("nightBtn"); if (b) b.classList.toggle("on", on);
        try { localStorage.setItem("vk-reader-night", on ? "1" : "0"); } catch (e) { }
    }
    VK.on("night", function () { night(!viewer.classList.contains("rd-night")); });
    try { if (localStorage.getItem("vk-reader-night") === "1") night(true); } catch (e) { }

    // ---------- side panel: pages, bookmarks, notes ----------
    function side(which) {
        $("side").hidden = false;
        document.querySelectorAll(".rd-side-tabs button").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-side") === which); });
        document.querySelectorAll("[data-pane-side]").forEach(function (p) { p.hidden = p.getAttribute("data-pane-side") !== which; });
    }
    document.querySelector(".rd-side-tabs").addEventListener("click", function (e) { var b = e.target.closest("button[data-side]"); if (b) side(b.getAttribute("data-side")); });
    VK.on("side", function (w) { side(w); });
    VK.on("toggleSide", function () { $("side").hidden = !$("side").hidden; if (zoomMode === "width") setTimeout(fitWidth, 50); });

    var tio = null;
    function buildThumbs() {
        if (!sizes.length) return;
        thumbsEl.innerHTML = "";
        if (tio) tio.disconnect();
        tio = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                if (!en.isIntersecting) return;
                var t = en.target; tio.unobserve(t);
                var n = +t.getAttribute("data-page");
                pdf.getPage(n).then(function (page) {
                    var vp = page.getViewport({ scale: 1, rotation: rotation });
                    var k = 150 / vp.width, v2 = page.getViewport({ scale: k * 2, rotation: rotation });
                    var c = document.createElement("canvas"); c.width = v2.width; c.height = v2.height;
                    t.querySelector(".rd-thumb-img").appendChild(c);
                    page.render({ canvasContext: c.getContext("2d"), viewport: v2 });
                });
            });
        }, { root: thumbsEl, rootMargin: "300px 0px" });
        for (var i = 1; i <= sizes.length; i++) {
            var s = pageSize(i);
            var t = document.createElement("button");
            t.type = "button"; t.className = "rd-thumb"; t.setAttribute("data-page", i);
            t.innerHTML = "<span class='rd-thumb-img' style='aspect-ratio:" + s.w + "/" + s.h + "'></span><span class='rd-thumb-no'>" + i + "</span>";
            thumbsEl.appendChild(t);
            tio.observe(t);
        }
        markPages();
        showPage();
    }
    thumbsEl.addEventListener("click", function (e) { var t = e.target.closest(".rd-thumb"); if (t) goTo(+t.getAttribute("data-page")); });

    // marks on pages and thumbnails: a ribbon for a bookmark, a count of notes
    function markPages() {
        var count = {};
        state.notes.forEach(function (n) { count[n.page] = (count[n.page] || 0) + 1; });
        pageEls.forEach(function (el, i) {
            var n = i + 1;
            el.classList.toggle("is-bookmarked", state.bookmarks.indexOf(n) >= 0);
            var badge = el.querySelector(".rd-page-notes");
            if (count[n]) {
                if (!badge) { badge = document.createElement("button"); badge.type = "button"; badge.className = "rd-page-notes"; el.appendChild(badge); }
                badge.innerHTML = "<i class='fa fa-sticky-note'></i> " + count[n];
                badge.title = count[n] === 1 ? "1 note on this page" : count[n] + " notes on this page";
                badge.setAttribute("data-page", n);
            } else if (badge) badge.remove();
        });
        thumbsEl.querySelectorAll(".rd-thumb").forEach(function (t) {
            var n = +t.getAttribute("data-page");
            t.classList.toggle("is-bookmarked", state.bookmarks.indexOf(n) >= 0);
            t.classList.toggle("has-notes", !!count[n]);
        });
    }
    pagesEl.addEventListener("click", function (e) {
        var b = e.target.closest(".rd-page-notes");
        if (b) { side("notes"); var first = $("noteList").querySelector('[data-note-page="' + b.getAttribute("data-page") + '"]'); if (first) first.scrollIntoView({ block: "nearest" }); }
    });

    function pageText(n) {
        if (textCache[n] != null) return Promise.resolve(textCache[n]);
        return pdf.getPage(n).then(function (p) { return p.getTextContent(); }).then(function (tc) {
            textCache[n] = tc.items.map(function (i) { return i.str; }).join(" ").replace(/\s+/g, " ").trim();
            return textCache[n];
        });
    }

    function refreshLists() {
        $("bmCount").textContent = state.bookmarks.length;
        $("noteCount").textContent = state.notes.length;
        $("statNotes").textContent = state.notes.length ? (state.notes.length === 1 ? "1 note" : state.notes.length + " notes") + (state.bookmarks.length ? " · " + state.bookmarks.length + " bookmarked" : "") : (state.bookmarks.length ? state.bookmarks.length + " bookmarked" : "");

        var bm = $("bmList");
        if (!state.bookmarks.length) bm.innerHTML = "<p class='rd-side-empty'><i class='fa fa-bookmark-o'></i>No bookmarks yet. Press <b>Bookmark</b> on a page you want to come back to.</p>";
        else {
            bm.innerHTML = state.bookmarks.slice().sort(function (a, b) { return a - b; }).map(function (p) {
                return "<div class='rd-bm' data-go='" + p + "'><i class='fa fa-bookmark'></i><span><b>Page " + p + "</b><small data-bm-text='" + p + "'></small></span>" +
                    "<button type='button' class='rd-x' data-unmark='" + p + "' title='Remove bookmark'><i class='fa fa-times'></i></button></div>";
            }).join("");
            if (pdf) state.bookmarks.forEach(function (p) {
                pageText(p).then(function (t) { var s = bm.querySelector('[data-bm-text="' + p + '"]'); if (s) s.textContent = t.slice(0, 70) + (t.length > 70 ? "…" : ""); });
            });
        }

        var nl = $("noteList");
        if (!state.notes.length) nl.innerHTML = "<p class='rd-side-empty'><i class='fa fa-sticky-note-o'></i>No notes yet. Press <b>Add note</b> to write one on the page you're reading.</p>";
        else nl.innerHTML = state.notes.slice().sort(function (a, b) { return a.page - b.page; }).map(function (n) {
            return "<div class='rd-note c-" + esc(n.color || "gold") + "' data-note-page='" + n.page + "' data-note='" + esc(n.id) + "'>" +
                "<div class='rd-note-head'><button type='button' class='rd-note-go' data-go='" + n.page + "'>Page " + n.page + "</button>" +
                "<span>" + esc(n.by || "") + (n.at ? " · " + new Date(n.at).toLocaleDateString([], { day: "numeric", month: "short" }) : "") + "</span>" +
                "<button type='button' class='rd-x' data-edit-note='" + esc(n.id) + "' title='Edit note'><i class='fa fa-pencil'></i></button>" +
                "<button type='button' class='rd-x' data-del-note='" + esc(n.id) + "' title='Delete note'><i class='fa fa-trash-o'></i></button></div>" +
                "<p>" + esc(n.text).replace(/\n/g, "<br>") + "</p></div>";
        }).join("");
        markPages();
    }
    document.querySelector(".rd-side").addEventListener("click", function (e) {
        var x;
        if ((x = e.target.closest("[data-unmark]"))) { toggleBookmark(+x.getAttribute("data-unmark")); return; }
        if ((x = e.target.closest("[data-del-note]"))) {
            var id = x.getAttribute("data-del-note");
            state.notes = state.notes.filter(function (n) { return n.id !== id; });
            refreshLists(); changed(); VK.toast("Note deleted.");
            return;
        }
        if ((x = e.target.closest("[data-edit-note]"))) { openNote(state.notes.find(function (n) { return n.id === x.getAttribute("data-edit-note"); })); return; }
        if ((x = e.target.closest("[data-go]"))) goTo(+x.getAttribute("data-go"));
    });

    // ---------- bookmarks ----------
    function toggleBookmark(p) {
        var i = state.bookmarks.indexOf(p);
        if (i >= 0) state.bookmarks.splice(i, 1); else state.bookmarks.push(p);
        refreshLists(); showPage(); changed();
        VK.toast(i >= 0 ? "Bookmark on page " + p + " removed." : "Page " + p + " bookmarked.");
    }
    VK.on("bookmark", function () { if (!pdf) { VK.toast("Open a PDF first."); return; } toggleBookmark(cur); });

    // ---------- notes ----------
    var editing = null;
    function openNote(note) {
        if (!pdf) { VK.toast("Open a PDF first."); return; }
        editing = note || null;
        $("notePage").textContent = note ? note.page : cur;
        $("noteText").value = note ? note.text : "";
        var color = note ? note.color : "gold";
        document.querySelectorAll('input[name="noteColor"]').forEach(function (r) { r.checked = r.value === color; });
        $("noteDialog").showModal();
        setTimeout(function () { $("noteText").focus(); }, 40);
    }
    VK.on("addNote", function () { openNote(null); });
    VK.on("saveNote", function () {
        var text = $("noteText").value.trim();
        if (!text) { VK.toast("Write the note first."); return; }
        var color = (document.querySelector('input[name="noteColor"]:checked') || {}).value || "gold";
        if (editing) { editing.text = text; editing.color = color; }
        else state.notes.push({ id: "n" + Date.now().toString(36), page: cur, text: text, color: color, by: VK.me || "", at: new Date().toISOString() });
        VK.closeDialog($("noteDialog"));
        refreshLists(); side("notes"); changed();
        VK.toast(editing ? "Note updated." : "Note added to page " + cur + ".");
    });

    // ---------- find ----------
    var box = $("findBox");
    function runFind(dir) {
        var q = box.value.trim().toLowerCase();
        if (!pdf) return;
        if (!q) { clearFind(); return; }
        if (q !== find.q) {
            find = { q: q, hits: [], i: -1 };
            $("statFind").innerHTML = "<i class='fa fa-search'></i> Searching…";
            var all = [];
            for (var n = 1; n <= sizes.length; n++) all.push(pageText(n));
            return Promise.all(all).then(function (texts) {
                texts.forEach(function (t, i) { if (t.toLowerCase().indexOf(q) >= 0) find.hits.push(i + 1); });
                pageEls.forEach(highlight);
                if (!find.hits.length) { $("statFind").innerHTML = "<i class='fa fa-search'></i> No match for “" + esc(box.value.trim()) + "”"; return; }
                var after = find.hits.findIndex(function (p) { return p >= cur; });
                find.i = after < 0 ? 0 : after;
                showHit();
            });
        }
        if (!find.hits.length) return;
        find.i = (find.i + dir + find.hits.length) % find.hits.length;
        showHit();
    }
    function showHit() {
        goTo(find.hits[find.i]);
        $("statFind").innerHTML = "<i class='fa fa-search'></i> On " + find.hits.length + (find.hits.length === 1 ? " page" : " pages") + " · " + (find.i + 1) + " of " + find.hits.length;
    }
    function clearFind() { find = { q: "", hits: [], i: -1 }; $("statFind").textContent = ""; pageEls.forEach(highlight); }
    function highlight(el) {
        el.querySelectorAll(".textLayer span").forEach(function (s) {
            s.classList.toggle("rd-hit", !!find.q && s.textContent.toLowerCase().indexOf(find.q) >= 0);
        });
    }
    box.addEventListener("keydown", function (e) { if (e.key === "Enter") { e.preventDefault(); runFind(e.shiftKey ? -1 : 1); } if (e.key === "Escape") { box.value = ""; clearFind(); } });
    box.addEventListener("input", function () { if (!box.value.trim()) clearFind(); });
    VK.on("findNext", function () { runFind(1); });
    VK.on("findPrev", function () { runFind(-1); });
    document.addEventListener("keydown", function (e) { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "f" && pdf) { e.preventDefault(); box.focus(); box.select(); } });

    // ---------- save and download ----------
    VK.getContent = function () { return content(); };
    VK.exportLocal = function (fileName) {
        if (!state.pdf) return Promise.reject(new Error("open a PDF first"));
        VK.download(new Blob([fromBase64(state.pdf)], { type: "application/pdf" }), fileName);
    };
    $("docTitle").addEventListener("change", function () { if (state.pdf) VK.save(content()); });

    // start
    if (state.zoom) VK.zoom = state.zoom;
    load(false);
})();
