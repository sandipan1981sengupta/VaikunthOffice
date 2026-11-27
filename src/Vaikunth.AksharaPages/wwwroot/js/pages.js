/* Akshara Pages · basic word processing on a contenteditable page, with the ribbon from Vaikunth.UI. */
(function () {
    "use strict";
    var page = document.getElementById("page");
    var data = {};
    try { data = JSON.parse(document.getElementById("docData").textContent || "{}"); } catch (e) { }
    page.innerHTML = data.html || data.Html || "<p></p>";
    var pageSize = data.pageSize || "A4";
    page.classList.toggle("letter", pageSize === "Letter");
    document.execCommand("styleWithCSS", false, true);

    function content() { return JSON.stringify({ html: page.innerHTML, pageSize: pageSize }); }
    function changed() { counts(); VK.changed(content); }
    VK.getContent = function () { return content(); };

    // ---- plain formatting commands go straight to the browser ----
    var native = ["bold", "italic", "underline", "strikeThrough", "subscript", "superscript", "insertUnorderedList", "insertOrderedList",
        "outdent", "indent", "justifyLeft", "justifyCenter", "justifyRight", "justifyFull", "undo", "redo", "removeFormat", "cut", "copy",
        "fontName", "fontSize", "foreColor", "hiliteColor"];
    VK.fallback = function (cmd, arg) {
        if (native.indexOf(cmd) < 0) return;
        page.focus();
        document.execCommand(cmd, false, arg || null);
        refreshToggles();
        changed();
    };
    VK.on("formatBlock", function (tag) { page.focus(); document.execCommand("formatBlock", false, "<" + tag + ">"); changed(); });
    VK.on("paste", function () {
        if (navigator.clipboard && navigator.clipboard.readText)
            navigator.clipboard.readText().then(function (t) { page.focus(); document.execCommand("insertText", false, t); changed(); })
                .catch(function () { VK.toast("Use Ctrl+V to paste."); });
        else VK.toast("Use Ctrl+V to paste.");
    });

    // ---- insert ----
    var saved = null;
    function keepSelection() { var s = window.getSelection(); saved = s.rangeCount ? s.getRangeAt(0).cloneRange() : null; }
    function restoreSelection() { page.focus(); if (saved) { var s = window.getSelection(); s.removeAllRanges(); s.addRange(saved); } }
    VK.on("insertTable", function () { keepSelection(); document.getElementById("tableDialog").showModal(); });
    VK.on("insertTableOk", function () {
        var rows = Math.max(1, Math.min(30, +document.getElementById("tblRows").value || 3));
        var cols = Math.max(1, Math.min(10, +document.getElementById("tblCols").value || 3));
        var html = "<table><tr>";
        for (var c = 0; c < cols; c++) html += "<th>Heading " + (c + 1) + "</th>";
        html += "</tr>";
        for (var r = 1; r < rows; r++) { html += "<tr>"; for (c = 0; c < cols; c++) html += "<td>&nbsp;</td>"; html += "</tr>"; }
        html += "</table><p></p>";
        document.getElementById("tableDialog").close();
        restoreSelection();
        document.execCommand("insertHTML", false, html);
        changed();
    });
    VK.on("insertLink", function () { keepSelection(); document.getElementById("linkDialog").showModal(); });
    VK.on("insertLinkOk", function () {
        var url = document.getElementById("linkUrl").value.trim();
        document.getElementById("linkDialog").close();
        if (!/^https?:\/\//i.test(url)) { VK.toast("Links must start with http:// or https://"); return; }
        restoreSelection();
        document.execCommand("createLink", false, url);
        changed();
    });
    VK.on("insertImage", function () {
        var input = document.createElement("input");
        input.type = "file"; input.accept = "image/*";
        keepSelection();
        input.onchange = function () {
            var f = input.files[0]; if (!f) return;
            var reader = new FileReader();
            reader.onload = function () { restoreSelection(); document.execCommand("insertImage", false, reader.result); changed(); };
            reader.readAsDataURL(f);
        };
        input.click();
    });
    VK.on("insertHr", function () { page.focus(); document.execCommand("insertHorizontalRule"); changed(); });
    VK.on("insertDate", function () {
        page.focus();
        document.execCommand("insertText", false, new Date().toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" }));
        changed();
    });
    VK.on("insertVerse", function () {
        page.focus();
        document.execCommand("insertHTML", false, "<blockquote>Yogah karmasu kaushalam: Excellence in every action. (Bhagavad Gita 2.50)</blockquote><p></p>");
        changed();
    });

    // ---- layout ----
    VK.on("pageSize", function (v) { pageSize = v; page.classList.toggle("letter", v === "Letter"); changed(); });
    VK.on("margins", function (v) { page.classList.remove("narrow", "wide"); if (v !== "normal") page.classList.add(v); });

    // ---- find (highlights every match for a moment) ----
    VK.on("find", function () {
        var word = prompt("Find in document:");
        if (!word) return;
        var n = 0, walker = document.createTreeWalker(page, NodeFilter.SHOW_TEXT), nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        nodes.forEach(function (node) {
            var i = node.nodeValue.toLowerCase().indexOf(word.toLowerCase());
            if (i < 0) return;
            var range = document.createRange(); range.setStart(node, i); range.setEnd(node, i + word.length);
            var mark = document.createElement("mark"); mark.className = "pg-find"; range.surroundContents(mark); n++;
        });
        VK.toast(n ? n + " found" : "Not found");
        setTimeout(function () { page.querySelectorAll("mark.pg-find").forEach(function (m) { m.replaceWith.apply(m, m.childNodes); }); page.normalize(); }, 2500);
    });
    VK.on("spell", function () { VK.toast("Spelling is checked as you type (red underline)."); });
    VK.on("wordCount", function () { counts(); VK.toast(document.getElementById("wordCount").textContent + ", " + document.getElementById("charCount").textContent); });

    // ---- zoom ----
    VK.onZoom = function (z) { page.style.zoom = z; document.querySelector(".pg-ruler").style.setProperty("--z", z); };
    VK.on("zoom100", function () { VK.setZoom(100); });

    // ---- ribbon buttons show what is on at the cursor ----
    function refreshToggles() {
        document.querySelectorAll(".vk-rb-toggle").forEach(function (b) {
            var on = false;
            try { on = document.queryCommandState(b.getAttribute("data-cmd")); } catch (e) { }
            b.classList.toggle("on", !!on);
        });
    }
    document.addEventListener("selectionchange", function () { if (page.contains(document.getSelection().anchorNode)) refreshToggles(); });

    // ---- status bar ----
    function counts() {
        var text = page.innerText || "";
        var words = (text.match(/\S+/g) || []).length;
        document.getElementById("wordCount").textContent = words + (words === 1 ? " word" : " words");
        document.getElementById("charCount").textContent = text.replace(/\s/g, "").length + " characters";
        var pages = Math.max(1, Math.ceil(page.scrollHeight / 1123));
        document.getElementById("pageCount").textContent = pages;
    }

    page.addEventListener("input", changed);
    document.getElementById("docTitle").addEventListener("change", function () { VK.save(content()); });
    document.addEventListener("keydown", function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") { e.preventDefault(); VK.save(content()); }
    });
    counts();
})();
