/* Vaikunth Desk · no page reloads on the workspace pages.
   A page opts in with data-ajax-panel on its <main> content (the super admin page does). Inside it, every form that
   posts (the windows, the rules, the toolbar actions) is sent with fetch: the server answers with a message
   (shown as a toast) and the address of the page; then only the page content is fetched again (X-Partial: 1) and put
   in place, the controls are started on it and the open tab and selected row stay. F5 still loads the whole page.
   VKPanel.post(url, fields) sends a post the same way, VKPanel.reload() just fetches the content again. */
window.VKPanel = (function () {
    "use strict";
    var VK = window.VK || {};
    var busy = false;

    function main() { return document.querySelector("[data-ajax-panel]"); }
    function token() { var t = document.querySelector('input[name="__RequestVerificationToken"]'); return t ? t.value : ""; }

    // a thin bar along the top while the server works
    function loading(on) {
        var bar = document.getElementById("vkLoadBar");
        if (!bar) {
            bar = document.createElement("div");
            bar.id = "vkLoadBar";
            bar.className = "vk-loadbar";
            document.body.appendChild(bar);
        }
        bar.classList.toggle("on", !!on);
        document.documentElement.classList.toggle("vk-busy", !!on);
    }

    /** Starts the ShankhNaad controls and the page's own script on new content. */
    function start(root, quiet) {
        var W = window;
        try {
            if (W.ModernInput) W.ModernInput.init(root);
            if (W.ModernDropdownWithLabel) W.ModernDropdownWithLabel.init(root);
            if (W.ModernDropdown) W.ModernDropdown.init(root);
            if (W.ModernDatePickerWithLabel) W.ModernDatePickerWithLabel.init(root);
            if (W.ModernDatePicker) W.ModernDatePicker.init(root);
            if (W.ModernMonthYearPicker) W.ModernMonthYearPicker.init(root);
            if (W.ModernRadio) W.ModernRadio.init(root);
            if (W.ModernCheckbox) W.ModernCheckbox.init(root);
            if (W.ModernTable) W.ModernTable.init(root);
            if (W.TabControl) W.TabControl.init(root);
            if (W.ModernColumnChart) W.ModernColumnChart.init(root);
        } catch (e) { console.error("Starting the controls failed", e); }
        root.querySelectorAll("[data-toast]").forEach(function (t) { VK.toast(t.getAttribute("data-toast"), t.getAttribute("data-toast-kind") || "success"); t.remove(); });
        if (VK.openTab) VK.openTab(VK.tab || "clients", true);
        if (!quiet) document.dispatchEvent(new CustomEvent("vk:panel", { detail: root }));
    }

    /** Fetches the page content again (url: the page's address, by default the one in the address bar). */
    function reload(url) {
        var m = main();
        if (!m) { location.reload(); return Promise.resolve(); }
        var u = new URL(url || location.href, location.origin);
        if (VK.tab) u.searchParams.set("tab", VK.tab);
        loading(true);
        return fetch(u.toString(), { headers: { "X-Partial": "1", "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" })
            .then(function (r) {
                if (r.redirected && /\/account\/login/i.test(r.url)) { location.href = r.url; throw new Error("signed out"); }
                if (!r.ok) throw new Error("HTTP " + r.status);
                return r.text();
            })
            .then(function (html) {
                // calendars of the old content are drawn on <body>: remove them
                document.querySelectorAll(".flatpickr-calendar").forEach(function (c) { c.remove(); });
                var y = window.scrollY;
                var inner = m.querySelector(".vk-hub-scroll, .vk-ws-scroll"), iy = inner ? inner.scrollTop : 0;   // these pages scroll inside
                m.innerHTML = html;
                m.classList.add("vk-panel-swapped");   // no rise-in animation on a content reload
                try { history.replaceState(null, "", u.pathname + u.search); } catch (e) { }
                start(m);
                window.scrollTo(0, y);
                var inner2 = m.querySelector(".vk-hub-scroll, .vk-ws-scroll");
                if (inner2) { inner2.style.scrollBehavior = "auto"; inner2.scrollTop = iy; inner2.style.scrollBehavior = ""; }
            })
            .catch(function (e) { if (e.message !== "signed out") VK.toast("The page couldn't be loaded. Check your connection and try again.", "error"); })
            .finally(function () { loading(false); });
    }

    /** Sends a post; shows the answer as a toast; closes the window on success; then loads the content again. */
    function send(action, body, form) {
        if (busy) return Promise.resolve();
        busy = true;
        cache = {};   // something changed on the server: pages fetched before may be out of date
        loading(true);
        var buttons = form ? Array.prototype.slice.call(form.querySelectorAll("button[type=submit], button:not([type])")) : [];
        buttons.forEach(function (b) { b.disabled = true; });
        return fetch(action, { method: "POST", body: body, headers: { "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" })
            .then(function (r) {
                if (r.redirected && /\/account\/login/i.test(r.url)) { location.href = r.url; throw new Error("signed out"); }
                if (!r.ok) throw new Error("HTTP " + r.status);
                if ((r.headers.get("content-type") || "").indexOf("application/json") < 0) { location.reload(); throw new Error("signed out"); }
                return r.json();
            })
            .then(function (res) {
                if (res.message) VK.toast(res.message, res.ok ? "success" : "error");
                if (!res.ok) return;   // not saved: the window stays open with what was typed, so it can be corrected
                var dlg = form && form.closest("dialog");
                if (dlg && dlg.open) { if (VK.closeDialog) VK.closeDialog(dlg); else dlg.close(); }
                busy = false;
                return reload(res.url);
            })
            .catch(function (e) { if (e.message !== "signed out") VK.toast("That didn't go through. Check your connection and try again.", "error"); })
            .finally(function () {
                busy = false;
                loading(false);
                buttons.forEach(function (b) { b.disabled = false; });
            });
    }

    function post(action, fields) {
        var fd = new FormData();
        fd.append("__RequestVerificationToken", token());
        Object.keys(fields || {}).forEach(function (k) { if (fields[k] != null) fd.append(k, fields[k]); });
        return send(action, fd, null);
    }

    // every posting form inside the panel goes through fetch (after the page's own checks, which may stop it)
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (e.defaultPrevented || !(form instanceof HTMLFormElement)) return;
        if (!form.closest("[data-ajax-panel]") || form.hasAttribute("data-full-post")) return;
        var submitter = e.submitter;
        if (submitter && submitter.getAttribute("formmethod") === "dialog") return;
        if ((form.getAttribute("method") || "get").toLowerCase() !== "post") return;
        e.preventDefault();
        var fd;
        try { fd = new FormData(form, submitter); } catch (err) { fd = new FormData(form); if (submitter && submitter.name) fd.append(submitter.name, submitter.value); }
        send((submitter && submitter.getAttribute("formaction")) || form.action, fd, form);
    });

    /* ---- moving between the workspace pages (Apps, Calendar, Inbox, People, Clients) without loading the whole page ----
       Only the content inside <main> is fetched (X-Partial: 1, the page's _PanelLayout); the side rail, header, theme,
       fonts, jQuery and the controls stay. Styles a page needs are added to <head> once, its scripts run once; a link
       hovered for a moment is fetched ahead, so the click shows it at once. Back and Forward work the same way. */
    var PAGES = /^\/(team|admin|days|inbox)?\/?$/i;
    var cache = {}, TTL = 20000, seq = 0;

    function wsMain() { return document.querySelector(".vk-ws-main"); }
    function pageUrl(a) {
        if (!a || !a.href || a.hasAttribute("download") || a.hasAttribute("data-full-load")) return null;
        var t = a.getAttribute("target");
        if (t && t !== "_self") return null;
        var u = new URL(a.href, location.href);
        if (u.origin !== location.origin || !PAGES.test(u.pathname)) return null;
        return u;
    }
    function get(url) {
        var hit = cache[url];
        if (hit && Date.now() - hit.t < TTL) return hit.p;
        var p = fetch(url, { headers: { "X-Partial": "1", "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" })
            .then(function (r) {
                if (!r.ok) throw new Error("HTTP " + r.status);
                return r.text().then(function (html) { return { html: html, url: r.url, redirected: r.redirected }; });
            });
        cache[url] = { t: Date.now(), p: p };
        p.catch(function () { delete cache[url]; });
        return p;
    }
    function styleIn(href) {
        return Array.prototype.some.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) { return l.href === href; });
    }
    function scriptIn(src) {
        return Array.prototype.some.call(document.scripts, function (x) { return x.src === src; });
    }
    function runScripts(list) {
        return list.reduce(function (chain, old) {
            return chain.then(function () {
                return new Promise(function (done) {
                    var src = old.getAttribute("src");
                    if (src && scriptIn(new URL(src, location.href).href)) { done(); return; }
                    var s = document.createElement("script");
                    if (src) { s.src = src; s.async = false; s.onload = s.onerror = function () { done(); }; document.body.appendChild(s); }
                    else { s.text = old.textContent; document.body.appendChild(s); s.remove(); done(); }
                });
            });
        }, Promise.resolve());
    }

    function nav(href, push) {
        var m = wsMain();
        if (!m) { location.href = href; return Promise.resolve(); }
        var my = ++seq;
        loading(true);
        return get(href).then(function (res) {
            if (my !== seq) return;
            var landed = new URL(res.url || href, location.href);
            var doc = new DOMParser().parseFromString("<body>" + res.html + "</body>", "text/html");
            var meta = doc.getElementById("vkPanelMeta");
            if (!meta || !PAGES.test(landed.pathname) || /\/account\//i.test(landed.pathname)) { location.href = landed.href; return; }
            // styles first, so the new content never shows unstyled
            var waits = [];
            doc.querySelectorAll('link[rel="stylesheet"]').forEach(function (l) {
                var full = new URL(l.getAttribute("href"), location.href).href;
                l.remove();
                if (styleIn(full)) return;
                var n = document.createElement("link");
                n.rel = "stylesheet"; n.href = full;
                waits.push(new Promise(function (ok) { n.onload = n.onerror = function () { ok(); }; setTimeout(ok, 3000); }));
                document.head.appendChild(n);
            });
            var scripts = [];
            doc.querySelectorAll("script").forEach(function (x) {
                var type = (x.getAttribute("type") || "").toLowerCase();
                if (type && type !== "text/javascript" && type !== "module") return;   // JSON settings stay in the page
                scripts.push(x); x.remove();
            });
            meta.remove();
            return Promise.all(waits).then(function () {
                if (my !== seq) return;
                document.dispatchEvent(new CustomEvent("vk:leave", { detail: m }));
                document.querySelectorAll(".flatpickr-calendar").forEach(function (c) { c.remove(); });
                document.querySelectorAll(".vk-menu-pop:not([hidden])").forEach(function (p) { p.hidden = true; });
                VK.tab = null;
                m.innerHTML = doc.body.innerHTML;
                m.classList.remove("vk-panel-swapped");
                void m.offsetWidth;   // replay the rise-in animation for a new page
                if (meta.getAttribute("data-ajax") === "1") m.setAttribute("data-ajax-panel", "on"); else m.removeAttribute("data-ajax-panel");
                document.body.classList.toggle("vk-fixed-shell", meta.getAttribute("data-fixed") === "1");
                document.title = meta.getAttribute("data-title") || document.title;
                var sub = document.getElementById("vkHeadSub");
                if (sub) sub.textContent = meta.getAttribute("data-headsub") || "";
                if (VK.railTo) VK.railTo(meta.getAttribute("data-section"), landed.pathname);
                if (push !== false) { try { history.pushState({ vkPanel: 1 }, "", landed.pathname + landed.search); } catch (e) { } }
                window.scrollTo(0, 0);
                start(m, true);
                return runScripts(scripts);
            });
        })
        .catch(function () { if (my === seq) location.href = href; })
        .finally(function () { if (my === seq) loading(false); });
    }

    document.addEventListener("click", function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var a = e.target.closest && e.target.closest("a[href]");
        var u = pageUrl(a);
        if (!u || !wsMain()) return;
        e.preventDefault();
        if (u.pathname + u.search === location.pathname + location.search && !u.hash) { delete cache[u.href]; }
        nav(u.href, true);
    });
    // fetch ahead when the pointer rests on a page link
    var hoverTimer = null;
    document.addEventListener("mouseover", function (e) {
        var a = e.target.closest && e.target.closest("a[href]");
        var u = pageUrl(a);
        clearTimeout(hoverTimer);
        if (!u || !wsMain() || u.pathname === location.pathname) return;
        hoverTimer = setTimeout(function () { get(u.href).catch(function () { }); }, 90);
    });
    try { history.replaceState({ vkPanel: 1 }, "", location.href); } catch (e) { }
    window.addEventListener("popstate", function (e) {
        if (!wsMain() || !PAGES.test(location.pathname)) return;
        delete cache[location.href];
        nav(location.href, false);
    });

    return { reload: reload, post: post, send: send, start: start, nav: nav };
})();
