/* Sudarshana Inbox · mail.
   Three panes: folders, the list, the open message (shown in a sandboxed frame so a message can't run script or
   change this page). Everything goes through /inbox/editor/* as JSON; new mail is looked for every minute. */
window.VKInbox = (function () {
    "use strict";
    var VK = window.VK;
    var cfg = {}, folder = "inbox", filter = "all", list = [], current = null, query = "", lastUnread = null, pollTimer = null, composeDirty = false, docWired = false;
    var NAMES = { inbox: "Inbox", starred: "Starred", sent: "Sent", drafts: "Drafts", trash: "Trash" };
    var TONES = ["#3F45B5", "#0F8F88", "#C24A1C", "#8E1B3A", "#5B2A86", "#115C45", "#1F56B8", "#B0306E"];

    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
    function initials(n) { var p = String(n || "?").replace(/<.*>/, "").split(/\s+/).filter(Boolean); return ((p[0] || "?").charAt(0) + (p.length > 1 ? p[p.length - 1].charAt(0) : (p[0] || "").charAt(1) || "")).toUpperCase(); }
    function tone(s) { var h = 0; s = String(s || ""); for (var i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0; return TONES[Math.abs(h) % TONES.length]; }
    function utc(s) { return new Date(/Z$|[+-]\d\d:\d\d$/.test(s) ? s : s + "Z"); }
    function when(s) {
        var d = utc(s), now = new Date(), y = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1);
        if (d.toDateString() === now.toDateString()) return d.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
        if (d.toDateString() === y.toDateString()) return "Yesterday";
        if (now - d < 6 * 864e5) return d.toLocaleDateString(undefined, { weekday: "short" });
        return d.toLocaleDateString(undefined, { day: "numeric", month: "short" }) + (d.getFullYear() !== now.getFullYear() ? " " + d.getFullYear() : "");
    }
    function fullWhen(s) { return utc(s).toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", year: "numeric", hour: "numeric", minute: "2-digit" }); }
    /** "Arjun Nair <arjun@x>; meera@x" → "Arjun Nair, meera@x" */
    function names(text) {
        return String(text || "").split(/[;,]/).map(function (p) { p = p.trim(); var m = /^(.*?)\s*<[^>]+>$/.exec(p); return m && m[1] ? m[1] : p; })
            .filter(Boolean).join(", ");
    }
    function addresses(text) { return (String(text || "").match(/[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g) || []).map(function (a) { return a.toLowerCase(); }); }
    function person(email) { email = (email || "").toLowerCase(); for (var i = 0; i < (cfg.people || []).length; i++) if (cfg.people[i].email.toLowerCase() === email) return cfg.people[i]; return null; }
    function label(p) { return p.name + " <" + p.email + ">"; }

    // ---------- server ----------
    function token() { var t = document.querySelector('input[name="__RequestVerificationToken"]'); return t ? t.value : ""; }
    function bar(on) {
        var b = document.getElementById("vkLoadBar");
        if (!b) { b = document.createElement("div"); b.id = "vkLoadBar"; b.className = "vk-loadbar"; document.body.appendChild(b); }
        b.classList.toggle("on", !!on);
    }
    function call(url, body, quiet) {
        if (!quiet) bar(true);
        var opts = { method: body === undefined ? "GET" : "POST", credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json", "RequestVerificationToken": token() } };
        if (body !== undefined) { opts.headers["Content-Type"] = "application/json"; opts.body = JSON.stringify(body); }
        return fetch(url, opts).then(function (r) {
            if (r.redirected && /\/account\/login/i.test(r.url)) { location.href = r.url; throw new Error("Please sign in again."); }
            return r.json().catch(function () { throw new Error("The server didn't answer (" + r.status + ")."); });
        }).then(function (res) { bar(false); return res; }, function (e) { bar(false); throw e; });
    }
    function post(url) { return call(url, {}); }

    // ---------- folders and list ----------
    function counts(c) {
        if (!c) return;
        document.querySelectorAll("[data-count]").forEach(function (b) {
            var n = c[b.getAttribute("data-count")] || 0;
            b.textContent = n > 99 ? "99+" : n; b.hidden = !n;
        });
        var rail = document.getElementById("railUnread");
        if (rail) { rail.textContent = c.inbox > 99 ? "99+" : c.inbox; rail.hidden = !c.inbox; }
        if (lastUnread !== null && c.inbox > lastUnread && folder !== "inbox") VK.toast("New mail in your Inbox.", "info");
        lastUnread = c.inbox;
        document.title = (c.inbox ? "(" + c.inbox + ") " : "") + "Sudarshana Inbox · Vaikunth Desk";
    }

    function load(keep, quiet) {
        if (cfg.blocked) { drawList(); return Promise.resolve(); }
        var before = list.map(function (m) { return m.id; });
        return call("/inbox/editor/list?folder=" + folder, undefined, quiet).then(function (res) {
            if (!res.ok) { VK.toast(res.error || "Couldn't load your mail.", "error"); return; }
            list = res.messages;
            counts(res.counts);
            if (quiet && folder === "inbox") {
                var fresh = list.filter(function (m) { return before.indexOf(m.id) < 0 && !m.read; });
                if (fresh.length) VK.toast(fresh.length === 1 ? "New mail from " + (fresh[0].from || fresh[0].fromEmail) + ": " + (fresh[0].subject || "(no subject)") : fresh.length + " new messages.", "info");
            }
            if (!keep || (current && !list.some(function (m) { return m.id === current.id; }))) { if (!keep) current = null; }
            drawList(true);
            if (!current) showEmpty();
        }).catch(function (e) { if (!quiet) VK.toast(e.message || "Couldn't load your mail.", "error"); });
    }

    function visible() {
        var q = query.toLowerCase();
        return list.filter(function (m) {
            if (filter === "unread" && m.read) return false;
            if (!q) return true;
            return (m.from + " " + m.fromEmail + " " + m.to + " " + m.subject + " " + m.preview).toLowerCase().indexOf(q) >= 0;
        });
    }

    function drawList(animate) {
        var host = document.getElementById("mailList");
        document.getElementById("mailFolderName").textContent = NAMES[folder];
        document.getElementById("mailTrashBar").hidden = folder !== "trash" || !list.length;
        var rows = visible();
        if (!rows.length) {
            host.innerHTML = '<div class="vk-mail-none"><i class="fa fa-' + (query ? "search" : folder === "starred" ? "star-o" : folder === "trash" ? "trash-o" : "inbox") + '"></i><b>' +
                (query ? "Nothing matches “" + esc(query) + "”" : filter === "unread" ? "All read. Well done." : NAMES[folder] + " is empty") + "</b></div>";
            return;
        }
        host.innerHTML = rows.map(function (m, i) {
            var outgoing = folder === "sent" || folder === "drafts" || (m.draft);
            var who = outgoing ? (names(m.to) || "(no one yet)") : (m.from || m.fromEmail);
            return '<div class="vk-mail-row' + (m.read ? "" : " unread") + (current && current.id === m.id ? " sel" : "") + (animate ? " in" : "") + '" role="option" tabindex="0" data-id="' + esc(m.id) +
                '" style="--i:' + Math.min(i, 20) + '"><span class="vk-mail-av" style="--av:' + tone(outgoing ? m.to : m.fromEmail) + '">' + esc(initials(who)) + "</span>" +
                '<span class="vk-mail-rowtext"><span class="vk-mail-rowtop"><b>' + (m.draft ? '<em class="vk-mail-draft">Draft</em> ' : "") + (outgoing && !m.draft ? "To: " : "") + esc(who) +
                '</b><time>' + esc(when(m.when)) + "</time></span><span class=\"vk-mail-subj\">" + esc(m.subject || "(no subject)") + '</span><span class="vk-mail-prev">' + esc(m.preview) + "</span></span>" +
                '<button type="button" class="vk-mail-rowstar' + (m.starred ? " on" : "") + '" data-star="' + esc(m.id) + '" title="' + (m.starred ? "Take the star off" : "Star") + '"><i class="fa fa-star' + (m.starred ? "" : "-o") + '"></i></button></div>';
        }).join("");
    }

    function setFolder(f) {
        folder = f; current = null; query = ""; setModernInputValue("#mailSearch", "");
        document.querySelectorAll(".vk-mail-folder").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-folder") === f); });
        list = []; drawList(); showEmpty(); load();
    }

    // ---------- reading ----------
    function showEmpty() {
        document.getElementById("mailEmpty").hidden = false;
        document.getElementById("mailMsg").hidden = true;
        document.querySelector(".vk-mail").classList.remove("is-reading");
        buttons();
    }
    function buttons() {
        var has = !!current, trash = folder === "trash";
        ["mReply", "mReplyAll", "mForward", "mStar", "mUnread", "mDelete", "mPrint"].forEach(function (c) {
            document.querySelectorAll('#MailHeader [data-cmd="' + c + '"]').forEach(function (b) { b.disabled = !has; b.classList.toggle("is-off", !has); });
        });
        var del = document.querySelector('#MailHeader [data-cmd="mDelete"] span');
        if (del) del.textContent = trash || (current && current.draft) ? "Delete for good" : "Delete";
        var star = document.querySelector('#MailHeader [data-cmd="mStar"] span');
        if (star) star.textContent = current && current.starred ? "Unstar" : "Star";
    }

    function open(id) {
        var row = list.filter(function (m) { return m.id === id; })[0];
        if (row && row.draft) { editDraft(id); return; }
        call("/inbox/editor/open?id=" + encodeURIComponent(id)).then(function (res) {
            if (!res.ok) { VK.toast(res.error, "error"); load(); return; }
            current = res.message;
            if (row && !row.read) { row.read = true; refreshCounts(); }
            drawList();
            render(current);
        }).catch(function (e) { VK.toast(e.message, "error"); });
    }

    function render(m) {
        document.getElementById("mailEmpty").hidden = true;
        var art = document.getElementById("mailMsg");
        art.hidden = false;
        art.classList.remove("in"); void art.offsetWidth; art.classList.add("in");
        document.querySelector(".vk-mail").classList.add("is-reading");
        document.getElementById("msgSubject").textContent = m.subject || "(no subject)";
        var av = document.getElementById("msgAvatar");
        av.textContent = initials(m.from || m.fromEmail); av.style.setProperty("--av", tone(m.fromEmail));
        document.getElementById("msgFrom").innerHTML = esc(m.from || m.fromEmail) + ' <small>&lt;' + esc(m.fromEmail) + "&gt;</small>";
        document.getElementById("msgTo").textContent = "To: " + (names(m.to) || "-");
        var cc = document.getElementById("msgCc"); cc.textContent = m.cc ? "Cc: " + names(m.cc) : ""; cc.hidden = !m.cc;
        document.getElementById("msgWhen").textContent = fullWhen(m.when);
        var sb = document.querySelector(".vk-mail-starbtn");
        sb.classList.toggle("on", !!m.starred); sb.innerHTML = '<i class="fa fa-star' + (m.starred ? "" : "-o") + '"></i>';
        var foot = document.getElementById("msgFoot");
        foot.innerHTML = folder === "trash"
            ? '<button type="button" class="vk-btn vk-btn-light" data-cmd="mRestore"><i class="fa fa-undo"></i> Put back</button><button type="button" class="vk-btn vk-btn-danger" data-cmd="mDelete"><i class="fa fa-trash"></i> Delete for good</button>'
            : '<button type="button" class="vk-btn vk-btn-light" data-cmd="mReply"><i class="fa fa-reply"></i> Reply</button><button type="button" class="vk-btn vk-btn-light" data-cmd="mReplyAll"><i class="fa fa-reply-all"></i> Reply all</button>' +
              '<button type="button" class="vk-btn vk-btn-light" data-cmd="mForward"><i class="fa fa-share"></i> Forward</button>';
        frame(m.body);
        buttons();
    }

    function frame(html) {
        var f = document.getElementById("msgFrame");
        var dark = document.documentElement.getAttribute("data-theme") === "dark";
        f.srcdoc = '<!doctype html><html><head><meta charset="utf-8"><base target="_blank"><style>' +
            ":root{color-scheme:" + (dark ? "dark" : "light") + "}html,body{margin:0;background:transparent}body{padding:4px 2px 24px;font:14.5px/1.65 'Segoe UI',system-ui,-apple-system,Arial,sans-serif;color:" + (dark ? "#E7E9FB" : "#1D1F3A") + ";background:transparent;word-wrap:break-word}" +
            "a{color:" + (dark ? "#8FD9D4" : "#1F56B8") + "}blockquote{margin:10px 0;padding:2px 0 2px 14px;border-left:3px solid " + (dark ? "#3a3f6e" : "#D5D9EE") + ";color:" + (dark ? "#A9AECF" : "#5B5F7E") + "}" +
            "img{max-width:100%;height:auto}p{margin:0 0 10px}h1,h2,h3{line-height:1.3}" +
            "</style></head><body>" + (html || "<p><i>(empty message)</i></p>") + "</body></html>";
        f.onload = function () { try { f.style.height = Math.max(160, f.contentDocument.documentElement.scrollHeight + 8) + "px"; } catch (e) { } };
    }

    function refreshCounts() { call("/inbox/editor/counts", undefined, true).then(function (r) { if (r.ok) counts(r.counts); }).catch(function () { }); }

    // ---------- compose ----------
    function dlg() { return document.getElementById("composeDialog"); }
    function editor() { return document.getElementById("composeBody"); }
    function compose(o) {
        o = o || {};
        document.getElementById("composeTitle").textContent = o.title || "New email";
        document.getElementById("composeDraftId").value = o.draftId || "";
        setModernInputValue("#composeTo", o.to || "");
        setModernInputValue("#composeCc", o.cc || "");
        document.getElementById("composeCcRow").hidden = !o.cc;
        setModernInputValue("#composeSubject", o.subject || "");
        editor().innerHTML = o.body || "";
        composeDirty = false;
        document.getElementById("composeSuggest").hidden = true;
        var d = dlg(); if (!d.open) d.showModal();
        setTimeout(function () {
            var target = o.focusBody ? editor() : document.getElementById("composeTo");
            target.focus();
            if (o.focusBody) { var r = document.createRange(); r.setStart(editor(), 0); r.collapse(true); var s = getSelection(); s.removeAllRanges(); s.addRange(r); }
        }, 60);
    }
    function quote(m, head) {
        return "<p><br></p><p><br></p><blockquote><p><small>" + esc(head) + "</small></p>" + (m.body || "") + "</blockquote>";
    }
    function prefixed(p, s) { s = s || ""; return new RegExp("^" + p + ":", "i").test(s) ? s : p + ": " + s; }
    function reply(all) {
        if (!current) return;
        var m = current, to = [], cc = [], me = (cfg.myEmail || "").toLowerCase();
        if (m.mine) to = addresses(m.to); else to = [m.fromEmail.toLowerCase()];
        if (all) {
            addresses(m.to).concat(addresses(m.cc)).forEach(function (a) { if (a !== me && to.indexOf(a) < 0) cc.push(a); });
        }
        function fmt(a) { var p = person(a); return p ? label(p) : a; }
        compose({
            title: all ? "Reply all" : "Reply", to: to.filter(function (a) { return a !== me || m.mine; }).map(fmt).join("; "), cc: cc.map(fmt).join("; "),
            subject: prefixed("Re", m.subject),
            body: quote(m, "On " + fullWhen(m.when) + ", " + (m.from || m.fromEmail) + " wrote:"), focusBody: true
        });
    }
    function forward() {
        if (!current) return;
        var m = current;
        compose({
            title: "Forward", subject: prefixed("Fwd", m.subject),
            body: "<p><br></p><p>---------- Forwarded message ----------<br>From: " + esc((m.from || "") + " <" + m.fromEmail + ">") + "<br>Date: " + esc(fullWhen(m.when)) +
                "<br>Subject: " + esc(m.subject) + "<br>To: " + esc(names(m.to)) + "</p>" + (m.body || "")
        });
    }
    function editDraft(id) {
        call("/inbox/editor/open?id=" + encodeURIComponent(id)).then(function (res) {
            if (!res.ok) { VK.toast(res.error, "error"); return; }
            var m = res.message;
            compose({ title: "Draft", draftId: m.id, to: m.to, cc: m.cc, subject: m.subject, body: m.body });
        });
    }
    function payload(isDraft) {
        return {
            draftId: document.getElementById("composeDraftId").value || null,
            to: getModernInputValue("#composeTo"), cc: getModernInputValue("#composeCc"), subject: getModernInputValue("#composeSubject"),
            body: editor().innerHTML, isDraft: isDraft
        };
    }
    function hasContent() { var p = payload(true); return !!(p.to.trim() || p.cc.trim() || p.subject.trim() || editor().textContent.trim()); }
    function send(isDraft) {
        var p = payload(isDraft);
        if (!isDraft) {
            var bad = String(p.to + ";" + p.cc).split(/[;,]/).map(function (x) { return x.trim(); }).filter(function (x) { return x && !addresses(x).length; });
            if (bad.length) { VK.toast("“" + bad[0] + "” isn't an email address or a person in your company. Pick a name from the list.", "warning"); return; }
            if (!addresses(p.to).length) { VK.toast("Add at least one person in To.", "warning"); document.getElementById("composeTo").focus(); return; }
        }
        var btn = document.querySelector(".vk-mail-sendbtn");
        if (!isDraft) btn.classList.add("is-sending");
        call("/inbox/editor/send", p).then(function (res) {
            btn.classList.remove("is-sending");
            if (!res.ok) { VK.toast(res.error, "error"); return; }
            composeDirty = false;
            if (isDraft) document.getElementById("composeDraftId").value = res.id;
            VK.toast(res.message, res.warn ? "warning" : "success");
            if (!isDraft) { flyAway(); VK.closeDialog(dlg()); }
            load(true, true);
        }).catch(function (e) { btn.classList.remove("is-sending"); VK.toast(e.message, "error"); });
    }
    /** A paper plane flies off when a message goes. */
    function flyAway() {
        var p = document.createElement("i");
        p.className = "fa fa-paper-plane vk-mail-plane";
        document.body.appendChild(p);
        setTimeout(function () { p.remove(); }, 1300);
    }
    function closeCompose() {
        if (!hasContent() || !composeDirty) { VK.closeDialog(dlg()); return; }
        confirmMessageBox({
            title: "Keep this message as a draft?", type: "info", icon: "arrow", confirmText: "Save draft", cancelText: "Keep writing", asHtml: true,
            text: "<p class='vk-mb-lead'>You can finish it later from <b>Drafts</b>. To throw it away, use <b>Discard</b>.</p>",
            onConfirm: function () { send(true); VK.closeDialog(dlg()); }
        });
    }

    // address suggestions while typing in To / Cc
    var suggestFor = null, suggestIdx = 0;
    function suggest(input) {
        var box = document.getElementById("composeSuggest"), v = input.value, parts = v.split(";"), last = parts[parts.length - 1].trim().toLowerCase();
        if (!last || last.length < 1 || /<.+>/.test(last)) { box.hidden = true; return; }
        var hits = (cfg.people || []).filter(function (p) { return (p.name + " " + p.email + " " + (p.job || "")).toLowerCase().indexOf(last) >= 0 && p.email.toLowerCase() !== (cfg.myEmail || "").toLowerCase(); }).slice(0, 6);
        if (!hits.length) { box.hidden = true; return; }
        suggestFor = input; suggestIdx = 0;
        box.innerHTML = hits.map(function (p, i) {
            return '<button type="button" role="option" data-pick="' + esc(label(p)) + '" class="' + (i === 0 ? "on" : "") + '"><span class="vk-mail-av" style="--av:' + tone(p.email) + '">' + esc(initials(p.name)) +
                "</span><span><b>" + esc(p.name) + "</b><small>" + esc(p.email + (p.job ? " · " + p.job : "")) + "</small></span></button>";
        }).join("");
        var r = input.getBoundingClientRect(), host = dlg().querySelector(".vk-dialog-body").getBoundingClientRect();
        box.style.top = (r.bottom - host.top + dlg().querySelector(".vk-dialog-body").scrollTop + 4) + "px";
        box.style.left = (r.left - host.left) + "px"; box.style.width = r.width + "px";
        box.hidden = false;
    }
    function pick(value) {
        if (!suggestFor) return;
        var parts = suggestFor.value.split(";"); parts[parts.length - 1] = " " + value;
        var v = parts.map(function (x) { return x.trim(); }).filter(Boolean).join("; ") + "; ";
        setModernInputValue("#" + suggestFor.id, v);
        document.getElementById("composeSuggest").hidden = true;
        suggestFor.focus();
        composeDirty = true;
    }

    // ---------- commands ----------
    function act(url, then) { return post(url).then(function (res) { if (!res.ok) { VK.toast(res.error, "error"); return; } VK.toast(res.message, "success"); if (then) then(res); }).catch(function (e) { VK.toast(e.message, "error"); }); }
    function toggleStar(id) {
        var m = list.filter(function (x) { return x.id === id; })[0] || (current && current.id === id ? current : null);
        var on = !(m && m.starred);
        post("/inbox/editor/star?id=" + encodeURIComponent(id) + "&starred=" + on).then(function (res) {
            if (!res.ok) { VK.toast(res.error, "error"); return; }
            list.forEach(function (x) { if (x.id === id) x.starred = on; });
            if (current && current.id === id) { current.starred = on; render(current); }
            if (folder === "starred" && !on) list = list.filter(function (x) { return x.id !== id; });
            drawList(); refreshCounts();
        });
    }
    VK.on("mNew", function () { compose({}); });
    VK.on("mReply", function () { reply(false); });
    VK.on("mReplyAll", function () { reply(true); });
    VK.on("mForward", forward);
    VK.on("mStar", function () { if (current) toggleStar(current.id); });
    VK.on("mUnread", function () {
        if (!current) return;
        var id = current.id;
        act("/inbox/editor/read?id=" + encodeURIComponent(id) + "&read=false", function () { list.forEach(function (x) { if (x.id === id) x.read = false; }); current = null; showEmpty(); drawList(); refreshCounts(); });
    });
    VK.on("mDelete", function () {
        if (!current) return;
        var m = current, forGood = folder === "trash" || m.draft;
        function go() {
            act("/inbox/editor/delete?id=" + encodeURIComponent(m.id), function () {
                var idx = list.findIndex(function (x) { return x.id === m.id; });
                list = list.filter(function (x) { return x.id !== m.id; });
                current = null; drawList(); showEmpty(); refreshCounts();
                var next = visible()[Math.min(idx, visible().length - 1)];
                if (next && idx >= 0) open(next.id);
            });
        }
        if (!forGood) { go(); return; }
        confirmMessageBox({
            title: "Delete this message for good?", type: "error", icon: "error", danger: true, confirmText: "Delete for good", cancelText: "Keep it", asHtml: true,
            text: "<p class='vk-mb-lead'><b>" + esc(m.subject || "(no subject)") + "</b> can't be brought back after this.</p>", onConfirm: go
        });
    });
    VK.on("mRestore", function () { if (current) act("/inbox/editor/restore?id=" + encodeURIComponent(current.id), function () { current = null; showEmpty(); load(); }); });
    VK.on("mEmptyTrash", function () {
        confirmMessageBox({
            title: "Empty the Trash?", type: "error", icon: "error", danger: true, confirmText: "Empty Trash", cancelText: "Cancel", asHtml: true,
            text: "<p class='vk-mb-lead'>All " + list.length + " " + (list.length === 1 ? "message" : "messages") + " in Trash are deleted for good.</p>",
            onConfirm: function () { act("/inbox/editor/emptytrash", function () { current = null; showEmpty(); load(); }); }
        });
    });
    VK.on("mRefresh", function () { load(true).then(function () { VK.toast("Your mail is up to date.", "info"); }); });
    VK.on("mPrint", function () { try { document.getElementById("msgFrame").contentWindow.print(); } catch (e) { window.print(); } });
    VK.on("mSend", function () { send(false); });
    VK.on("mSaveDraft", function () { send(true); });
    VK.on("mDiscard", function () {
        var id = document.getElementById("composeDraftId").value;
        if (id) act("/inbox/editor/delete?id=" + encodeURIComponent(id), function () { VK.closeDialog(dlg()); load(true); });
        else { composeDirty = false; VK.closeDialog(dlg()); VK.toast("Discarded.", "info"); }
    });
    VK.on("mCloseCompose", closeCompose);

    function move(n) {
        var rows = visible(); if (!rows.length) return;
        var i = current ? rows.findIndex(function (m) { return m.id === current.id; }) : -1;
        var next = rows[Math.max(0, Math.min(rows.length - 1, i + n))];
        if (next) { open(next.id); var el = document.querySelector('.vk-mail-row[data-id="' + next.id + '"]'); if (el) el.scrollIntoView({ block: "nearest" }); }
    }

    function wire() {
        var root = document.querySelector(".vk-mail");
        if (!root || root.getAttribute("data-wired")) return;
        root.setAttribute("data-wired", "1");
        root.addEventListener("click", function (e) {
            var st = e.target.closest("[data-star]");
            if (st) { e.stopPropagation(); toggleStar(st.getAttribute("data-star")); return; }
            var row = e.target.closest(".vk-mail-row");
            if (row) { open(row.getAttribute("data-id")); return; }
            var f = e.target.closest("[data-folder]");
            if (f) { setFolder(f.getAttribute("data-folder")); return; }
            var fl = e.target.closest("[data-filter]");
            if (fl) {
                filter = fl.getAttribute("data-filter");
                document.querySelectorAll("[data-filter]").forEach(function (b) { b.setAttribute("aria-pressed", b === fl ? "true" : "false"); });
                drawList(true);
            }
        });
        var search = document.getElementById("mailSearch");
        if (search) search.addEventListener("input", function () { query = search.value.trim(); drawList(); });
        // compose window
        var d = dlg();
        d.addEventListener("cancel", function (e) { e.preventDefault(); closeCompose(); });
        d.addEventListener("input", function (e) {
            composeDirty = true;
            if (e.target.id === "composeTo" || e.target.id === "composeCc") suggest(e.target);
        });
        d.addEventListener("keydown", function (e) {
            var box = document.getElementById("composeSuggest");
            if (!box.hidden && (e.target.id === "composeTo" || e.target.id === "composeCc")) {
                var opts = box.querySelectorAll("button");
                if (e.key === "ArrowDown" || e.key === "ArrowUp") { e.preventDefault(); suggestIdx = (suggestIdx + (e.key === "ArrowDown" ? 1 : -1) + opts.length) % opts.length; opts.forEach(function (o, i) { o.classList.toggle("on", i === suggestIdx); }); }
                else if (e.key === "Enter" || e.key === "Tab") { e.preventDefault(); pick(opts[suggestIdx].getAttribute("data-pick")); }
                else if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); box.hidden = true; }
            }
            if ((e.ctrlKey || e.metaKey) && e.key === "Enter") { e.preventDefault(); send(false); }
        });
        document.getElementById("composeSuggest").addEventListener("mousedown", function (e) { var b = e.target.closest("[data-pick]"); if (b) { e.preventDefault(); pick(b.getAttribute("data-pick")); } });
        d.addEventListener("focusout", function (e) { if (e.target.id === "composeTo" || e.target.id === "composeCc") setTimeout(function () { var sb = document.getElementById("composeSuggest"); if (sb) sb.hidden = true; }, 150); });
        document.getElementById("composeCcBtn").addEventListener("click", function () { var r = document.getElementById("composeCcRow"); r.hidden = false; document.getElementById("composeCc").focus(); });
        d.querySelector(".vk-mail-tools").addEventListener("mousedown", function (e) { if (e.target.closest("button")) e.preventDefault(); });
        d.querySelector(".vk-mail-tools").addEventListener("click", function (e) {
            var b = e.target.closest("[data-fmt]"); if (!b) return;
            var cmd = b.getAttribute("data-fmt"), val = b.getAttribute("data-val");
            editor().focus();
            if (cmd === "createLink") {
                var url = prompt("Link address", "https://");   // a plain prompt: short and keeps the text selection
                if (url && /^(https?:|mailto:)/i.test(url)) document.execCommand("createLink", false, url);
            } else document.execCommand(cmd, false, val || null);
            composeDirty = true;
        });
        if (docWired) return;   // the page can be opened again from the side rail without a reload
        docWired = true;
        // keyboard
        document.addEventListener("keydown", function (e) {
            if (!document.querySelector(".vk-mail") || document.querySelector("dialog[open]") || /input|textarea|select/i.test(e.target.tagName || "") || e.target.isContentEditable || e.ctrlKey || e.metaKey || e.altKey) return;
            var k = e.key.toLowerCase();
            if (k === "c" || k === "n") { e.preventDefault(); compose({}); }
            else if (k === "r" && current) { e.preventDefault(); reply(false); }
            else if (k === "a" && current) { e.preventDefault(); reply(true); }
            else if (k === "f" && current) { e.preventDefault(); forward(); }
            else if (k === "s" && current) toggleStar(current.id);
            else if (k === "u" && current) VK.handlers.mUnread();
            else if ((e.key === "Delete" || k === "#") && current) VK.handlers.mDelete();
            else if (k === "j" || e.key === "ArrowDown") { e.preventDefault(); move(1); }
            else if (k === "k" || e.key === "ArrowUp") { e.preventDefault(); move(-1); }
            else if (e.key === "Enter" && e.target.classList && e.target.classList.contains("vk-mail-row")) open(e.target.getAttribute("data-id"));
        });
        // the theme changes the open message's colours too
        new MutationObserver(function () { if (current && document.getElementById("msgFrame")) frame(current.body); }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
    }

    function init() {
        if (!document.getElementById("inboxCfg")) return;
        try { cfg = JSON.parse(document.getElementById("inboxCfg").textContent); } catch (e) { cfg = {}; }
        folder = "inbox"; filter = "all"; list = []; current = null; query = ""; lastUnread = null; composeDirty = false;
        wire();
        buttons();
        load();
        clearInterval(pollTimer);
        pollTimer = setInterval(function () { if (!document.getElementById("inboxCfg")) { clearInterval(pollTimer); return; } if (!document.hidden) load(true, true); }, 60000);
        if (cfg.openNew) {
            history.replaceState(null, "", location.pathname);
            var p = cfg.newTo ? person(cfg.newTo) : null;
            setTimeout(function () { compose({ to: p ? label(p) + "; " : cfg.newTo || "" }); }, 250);
        }
    }

    return { init: init };
})();
