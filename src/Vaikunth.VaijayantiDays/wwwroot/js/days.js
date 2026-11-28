/* Vaijayanti Days · the calendar.
   Month, week, day and agenda views are drawn here from /days/editor/events (JSON). Times are kept in UTC on the
   server and shown in this browser's own time; an all-day event covers whole days (midnight UTC to midnight UTC).
   Buttons use data-cmd (VK.on), windows are vk-dialogs with the ShankhNaad controls, questions use the message box. */
window.VKDays = (function () {
    "use strict";
    var VK = window.VK;
    var cfg = {}, view = "month", cursor = today(), events = [], upcoming = [], loadedRange = null, busy = false, docWired = false;
    var HOUR = 48;   // pixels per hour in week and day view
    var MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    var DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    // ---------- dates ----------
    function today() { var d = new Date(); return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
    function addDays(d, n) { return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n); }
    function sameDay(a, b) { return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate(); }
    function weekStart(d) { return addDays(d, -((d.getDay() + 6) % 7)); }
    function pad(n) { return (n < 10 ? "0" : "") + n; }
    function dmy(d) { return pad(d.getDate()) + "-" + pad(d.getMonth() + 1) + "-" + d.getFullYear(); }
    function parseDmy(s) { var m = /^(\d{1,2})-(\d{1,2})-(\d{4})$/.exec((s || "").trim()); return m ? new Date(+m[3], +m[2] - 1, +m[1]) : null; }
    function hm(d) { return pad(d.getHours()) + ":" + pad(d.getMinutes()); }
    function time12(d) {
        var h = d.getHours(), m = d.getMinutes();
        return ((h % 12) || 12) + (m ? ":" + pad(m) : "") + (h < 12 ? " am" : " pm");
    }
    function longDay(d) { return d.toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long" }); }
    function shortDay(d) { return d.toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" }); }
    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }

    /** Server event → event with local s / e (all-day: local midnights of the UTC days). */
    function norm(ev) {
        var s = new Date(ev.start.endsWith("Z") ? ev.start : ev.start + "Z"), e = new Date(ev.end.endsWith("Z") ? ev.end : ev.end + "Z");
        if (ev.allDay) {
            s = new Date(s.getUTCFullYear(), s.getUTCMonth(), s.getUTCDate());
            e = new Date(e.getUTCFullYear(), e.getUTCMonth(), e.getUTCDate());
            if (e <= s) e = addDays(s, 1);
        }
        ev.s = s; ev.e = e;
        return ev;
    }
    function onDay(ev, day) { var next = addDays(day, 1); return ev.s < next && (ev.e > day || (+ev.e === +ev.s && sameDay(ev.s, day))); }
    function whenText(ev) {
        if (ev.allDay) {
            var last = addDays(ev.e, -1);
            return sameDay(ev.s, last) ? longDay(ev.s) + " · all day" : shortDay(ev.s) + " – " + shortDay(last) + " · all day";
        }
        return sameDay(ev.s, ev.e)
            ? longDay(ev.s) + " · " + time12(ev.s) + " – " + time12(ev.e)
            : shortDay(ev.s) + " " + time12(ev.s) + " – " + (sameDay(ev.s, ev.e) ? "" : shortDay(ev.e) + " ") + time12(ev.e);
    }

    // ---------- server ----------
    function token() { var t = document.querySelector('input[name="__RequestVerificationToken"]'); return t ? t.value : ""; }
    function bar(on) {
        var b = document.getElementById("vkLoadBar");
        if (!b) { b = document.createElement("div"); b.id = "vkLoadBar"; b.className = "vk-loadbar"; document.body.appendChild(b); }
        b.classList.toggle("on", !!on);
    }
    function call(url, body) {
        bar(true);
        var opts = { method: body === undefined ? "GET" : "POST", credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json", "RequestVerificationToken": token() } };
        if (body !== undefined) { opts.headers["Content-Type"] = "application/json"; opts.body = JSON.stringify(body); }
        return fetch(url, opts).then(function (r) {
            if (r.redirected && /\/account\/login/i.test(r.url)) { location.href = r.url; throw new Error("Please sign in again."); }
            return r.json().catch(function () { throw new Error("The server didn't answer (" + r.status + ")."); });
        }).then(function (res) { bar(false); return res; }, function (e) { bar(false); throw e; });
    }

    function range() {
        if (view === "month") { var first = new Date(cursor.getFullYear(), cursor.getMonth(), 1), s = weekStart(first); return { from: s, to: addDays(s, 42) }; }
        if (view === "week") { var w = weekStart(cursor); return { from: w, to: addDays(w, 7) }; }
        if (view === "day") return { from: cursor, to: addDays(cursor, 1) };
        return { from: cursor, to: addDays(cursor, 60) };
    }

    function load() {
        if (cfg.blocked) { draw(); return; }
        var r = range();
        // the mini month needs its own whole month too
        var mini = new Date(cursor.getFullYear(), cursor.getMonth(), 1), from = r.from < weekStart(mini) ? r.from : weekStart(mini), to = r.to > addDays(weekStart(mini), 42) ? r.to : addDays(weekStart(mini), 42);
        loadedRange = { from: from, to: to };
        busy = true;
        call("/days/editor/events?from=" + encodeURIComponent(from.toISOString()) + "&to=" + encodeURIComponent(to.toISOString()))
            .then(function (res) {
                busy = false;
                if (!res.ok) { VK.toast(res.error || "Couldn't load the calendar.", "error"); events = []; }
                else events = res.events.map(norm);
                draw();
            })
            .catch(function (e) { busy = false; VK.toast(e.message || "Couldn't load the calendar.", "error"); draw(); });
        loadUpcoming();
    }
    function loadUpcoming() {
        var now = new Date();
        call("/days/editor/events?from=" + encodeURIComponent(now.toISOString()) + "&to=" + encodeURIComponent(addDays(today(), 45).toISOString()))
            .then(function (res) { upcoming = res.ok ? res.events.map(norm) : []; drawUpcoming(); })
            .catch(function () { });
    }
    function byId(id) {
        for (var i = 0; i < events.length; i++) if (events[i].id === id) return events[i];
        for (var j = 0; j < upcoming.length; j++) if (upcoming[j].id === id) return upcoming[j];
        return null;
    }

    // ---------- drawing ----------
    function draw() {
        var title = document.getElementById("daysTitle"), r = range();
        if (view === "month") title.textContent = MONTHS[cursor.getMonth()] + " " + cursor.getFullYear();
        else if (view === "week") {
            var last = addDays(r.from, 6);
            title.textContent = r.from.getMonth() === last.getMonth()
                ? r.from.getDate() + " – " + last.getDate() + " " + MONTHS[last.getMonth()] + " " + last.getFullYear()
                : shortDay(r.from) + " – " + shortDay(last) + " " + last.getFullYear();
        }
        else if (view === "day") title.textContent = longDay(cursor) + " " + cursor.getFullYear();
        else title.textContent = "From " + longDay(cursor);
        document.querySelectorAll(".vk-days-seg button").forEach(function (b) { b.setAttribute("aria-pressed", b.getAttribute("data-view") === view ? "true" : "false"); });
        document.querySelectorAll('#DaysHeader [data-cmd="dMonth"],#DaysHeader [data-cmd="dWeek"],#DaysHeader [data-cmd="dDay"],#DaysHeader [data-cmd="dAgenda"]').forEach(function (b) {
            b.classList.toggle("is-on", b.getAttribute("data-cmd") === "d" + view.charAt(0).toUpperCase() + view.slice(1));
        });
        var host = document.getElementById("daysView");
        host.className = "vk-days-view is-" + view;
        host.innerHTML = view === "month" ? month() : view === "agenda" ? agenda() : timeGrid(view === "week" ? 7 : 1);
        host.classList.remove("vk-days-in"); void host.offsetWidth; host.classList.add("vk-days-in");
        if (view === "week" || view === "day") {
            var sc = host.querySelector(".vk-tg-scroll");
            var nh = new Date().getHours(); if (sc) sc.scrollTop = (sameDayInRange() && nh >= 10 ? nh - 2 : 7.5) * HOUR;
            layoutTimed(host);
        }
        miniCal();
        try { localStorage.setItem("vk-days-view", view); } catch (e) { }
    }
    function sameDayInRange() { var r = range(), n = new Date(); return n >= r.from && n < r.to; }

    function chip(ev, day) {
        var starts = sameDay(ev.s, day), cont = !starts;
        return '<button type="button" class="vk-ev ev-' + esc(ev.color) + (ev.allDay ? " is-allday" : "") + (cont ? " is-cont" : "") + (ev.mine ? "" : " is-invite") +
            (ev.my === "no" ? " is-declined" : "") + '" data-ev="' + esc(ev.id) + '" title="' + esc(ev.title + " · " + whenText(ev)) + '">' +
            (ev.allDay || cont ? "" : '<i class="vk-ev-time">' + esc(time12(ev.s)) + "</i>") + "<b>" + esc(ev.title) + "</b></button>";
    }

    function month() {
        var first = new Date(cursor.getFullYear(), cursor.getMonth(), 1), s = weekStart(first), t = today(), html = '<div class="vk-mo-head">';
        DAYS.forEach(function (d, i) { html += "<span" + (i > 4 ? ' class="we"' : "") + ">" + d + "</span>"; });
        html += '</div><div class="vk-mo-grid">';
        for (var i = 0; i < 42; i++) {
            var d = addDays(s, i), list = events.filter(function (ev) { return onDay(ev, d); })
                .sort(function (a, b) { return (b.allDay - a.allDay) || (a.s - b.s); });
            var cls = "vk-mo-cell" + (d.getMonth() !== cursor.getMonth() ? " other" : "") + (sameDay(d, t) ? " today" : "") + (i % 7 > 4 ? " we" : "");
            html += '<div class="' + cls + '" data-day="' + dmy(d) + '" style="--i:' + i + '"><span class="vk-mo-num">' +
                (d.getDate() === 1 ? MONTHS[d.getMonth()].slice(0, 3) + " " : "") + d.getDate() + "</span>";
            list.slice(0, 3).forEach(function (ev) { html += chip(ev, d); });
            if (list.length > 3) html += '<button type="button" class="vk-mo-more" data-goday="' + dmy(d) + '">+' + (list.length - 3) + " more</button>";
            html += "</div>";
        }
        return html + "</div>";
    }

    function timeGrid(n) {
        var s = n === 7 ? weekStart(cursor) : cursor, t = today(), html = '<div class="vk-tg" style="--days:' + n + '"><div class="vk-tg-head"><span class="vk-tg-gut"></span>';
        for (var i = 0; i < n; i++) {
            var d = addDays(s, i);
            html += '<button type="button" class="vk-tg-day' + (sameDay(d, t) ? " today" : "") + '" data-goday="' + dmy(d) + '"><small>' + DAYS[(d.getDay() + 6) % 7] + "</small><b>" + d.getDate() + "</b></button>";
        }
        html += '</div><div class="vk-tg-all"><span class="vk-tg-gut">all day</span>';
        for (var j = 0; j < n; j++) {
            var day = addDays(s, j);
            html += '<div class="vk-tg-allcell" data-day="' + dmy(day) + '">';
            events.filter(function (ev) { return (ev.allDay || ev.e - ev.s >= 864e5) && onDay(ev, day); }).forEach(function (ev) { html += chip(ev, day); });
            html += "</div>";
        }
        html += '</div><div class="vk-tg-scroll"><div class="vk-tg-body" style="height:' + (24 * HOUR) + 'px"><div class="vk-tg-hours">';
        for (var h = 0; h < 24; h++) html += '<span style="top:' + (h * HOUR) + 'px">' + (h === 0 ? "" : ((h % 12) || 12) + (h < 12 ? " am" : " pm")) + "</span>";
        html += "</div>";
        for (var k = 0; k < n; k++) {
            var dd = addDays(s, k);
            html += '<div class="vk-tg-col' + (sameDay(dd, t) ? " today" : "") + '" data-day="' + dmy(dd) + '">';
            events.filter(function (ev) { return !ev.allDay && ev.e - ev.s < 864e5 && onDay(ev, dd); }).forEach(function (ev) {
                var from = Math.max(0, (ev.s - dd) / 36e5), to = Math.min(24, (ev.e - dd) / 36e5);
                html += '<button type="button" class="vk-tg-ev ev-' + esc(ev.color) + (to - from < .76 ? " is-short" : "") + (ev.mine ? "" : " is-invite") + (ev.my === "no" ? " is-declined" : "") +
                    '" data-ev="' + esc(ev.id) + '" data-from="' + from + '" data-to="' + to + '" style="top:' + (from * HOUR) + "px;height:" + Math.max(20, (to - from) * HOUR - 2) + 'px">' +
                    "<b>" + esc(ev.title) + "</b><small>" + esc(time12(ev.s) + " – " + time12(ev.e)) + (ev.location ? " · " + esc(ev.location) : "") + "</small></button>";
            });
            if (sameDay(dd, t)) { var now = new Date(); html += '<span class="vk-tg-now" style="top:' + ((now.getHours() + now.getMinutes() / 60) * HOUR) + 'px"></span>'; }
            html += "</div>";
        }
        return html + "</div></div></div>";
    }

    /** Side by side when events overlap in the same column. */
    function layoutTimed(host) {
        host.querySelectorAll(".vk-tg-col").forEach(function (col) {
            var items = Array.prototype.slice.call(col.querySelectorAll(".vk-tg-ev")).map(function (el) {
                return { el: el, from: +el.getAttribute("data-from"), to: +el.getAttribute("data-to") };
            }).sort(function (a, b) { return a.from - b.from || b.to - a.to; });
            var groups = [], cur = null, end = -1;
            items.forEach(function (it) {
                if (!cur || it.from >= end) { cur = []; groups.push(cur); end = it.to; } else end = Math.max(end, it.to);
                cur.push(it);
            });
            groups.forEach(function (g) {
                var lanes = [];
                g.forEach(function (it) {
                    var lane = 0;
                    while (lanes[lane] !== undefined && lanes[lane] > it.from) lane++;
                    lanes[lane] = it.to; it.lane = lane;
                });
                g.forEach(function (it) {
                    var w = 100 / lanes.length;
                    it.el.style.left = "calc(" + (it.lane * w) + "% + 2px)";
                    it.el.style.width = "calc(" + w + "% - 4px)";
                });
            });
        });
    }

    function agenda() {
        var html = "", last = null, list = events.filter(function (ev) { return ev.e > cursor; }).sort(function (a, b) { return a.s - b.s; });
        if (!list.length) return '<div class="vk-days-empty"><img src="/_content/Vaikunth.UI/images/app-days.svg" alt="" width="84" height="84" /><b>Nothing in the next 60 days</b><span>Add an event and invite your team.</span>' +
            '<button type="button" class="vk-btn vk-btn-primary" data-cmd="dNew"><i class="fa fa-plus"></i> New event</button></div>';
        list.forEach(function (ev, i) {
            var d = ev.s < cursor ? cursor : new Date(ev.s.getFullYear(), ev.s.getMonth(), ev.s.getDate());
            if (!last || !sameDay(last, d)) {
                html += (last ? "</div>" : "") + '<div class="vk-ag-day' + (sameDay(d, today()) ? " today" : "") + '" style="--i:' + i + '"><div class="vk-ag-date"><b>' + d.getDate() + "</b><small>" +
                    esc(d.toLocaleDateString(undefined, { weekday: "short", month: "short" })) + "</small></div><div class=\"vk-ag-list\">";
                last = d;
            }
            html += '<button type="button" class="vk-ag-ev ev-' + esc(ev.color) + '" data-ev="' + esc(ev.id) + '"><i class="vk-ev-dot ev-' + esc(ev.color) + '"></i><span class="vk-ag-time">' +
                (ev.allDay ? "All day" : esc(time12(ev.s) + " – " + time12(ev.e))) + '</span><b>' + esc(ev.title) + "</b>" +
                (ev.location ? '<small><i class="fa fa-map-marker"></i> ' + esc(ev.location) + "</small>" : "") +
                (ev.mine ? "" : '<em class="vk-ag-from">from ' + esc(ev.owner) + "</em>") + rsvp(ev) + "</button>";
        });
        return html + "</div></div>";
    }
    function rsvp(ev) {
        if (ev.mine || !ev.my) return "";
        var t = { yes: "Going", no: "Not going", maybe: "Maybe", pending: "Not answered" }[ev.my] || "";
        return '<span class="vk-rsvp r-' + esc(ev.my) + '">' + t + "</span>";
    }

    function miniCal() {
        var host = document.getElementById("miniCal"); if (!host) return;
        var first = new Date(cursor.getFullYear(), cursor.getMonth(), 1), s = weekStart(first), t = today();
        var html = '<div class="vk-mini-top"><b>' + MONTHS[cursor.getMonth()] + " " + cursor.getFullYear() + '</b><span><button type="button" data-cmd="dPrevMonth" aria-label="Last month"><i class="fa fa-angle-left"></i></button>' +
            '<button type="button" data-cmd="dNextMonth" aria-label="Next month"><i class="fa fa-angle-right"></i></button></span></div><div class="vk-mini-grid">';
        DAYS.forEach(function (d) { html += "<span class=\"h\">" + d.charAt(0) + "</span>"; });
        for (var i = 0; i < 42; i++) {
            var d = addDays(s, i), has = events.some(function (ev) { return onDay(ev, d); });
            html += '<button type="button" data-goday="' + dmy(d) + '" class="' + (d.getMonth() !== cursor.getMonth() ? "other " : "") + (sameDay(d, t) ? "today " : "") +
                (sameDay(d, cursor) ? "sel " : "") + (has ? "has" : "") + '">' + d.getDate() + "</button>";
        }
        host.innerHTML = html + "</div>";
    }

    function drawUpcoming() {
        var host = document.getElementById("daysUpcoming"); if (!host) return;
        var list = upcoming.slice().sort(function (a, b) { return a.s - b.s; }).slice(0, 5);
        if (!list.length) { host.innerHTML = '<p class="vk-days-none">Nothing in the next few weeks.</p>'; return; }
        host.innerHTML = list.map(function (ev) {
            var d = ev.s;
            return '<button type="button" class="vk-up ev-' + esc(ev.color) + '" data-ev="' + esc(ev.id) + '"><span class="vk-up-date"><b>' + d.getDate() + "</b><small>" +
                MONTHS[d.getMonth()].slice(0, 3) + '</small></span><span class="vk-up-text"><b>' + esc(ev.title) + "</b><small>" +
                esc(ev.allDay ? "All day" : d.toLocaleDateString(undefined, { weekday: "short" }) + " · " + time12(d)) + "</small></span></button>";
        }).join("");
    }

    // ---------- the event window ----------
    function pickColor(c) { var r = document.querySelector('input[name="evColor"][value="' + c + '"]') || document.querySelector('input[name="evColor"]'); if (r) r.checked = true; }
    function snap(d) { var m = d.getMinutes(); d = new Date(d); d.setMinutes(m < 15 ? 0 : m < 45 ? 30 : 60, 0, 0); return d; }

    function openEditor(ev, start) {
        var dlg = document.getElementById("eventDialog");
        dlg.querySelector(".vk-dialog-titles h3").textContent = ev ? "Edit event" : "New event";
        document.getElementById("evId").value = ev ? ev.id : "";
        var s, e;
        if (ev) { s = ev.s; e = ev.allDay ? addDays(ev.e, -1) : ev.e; }
        else {
            s = start || snap(new Date(Math.max(Date.now(), +cursor + 10 * 36e5)));
            if (!start && !sameDay(s, cursor) && cursor > today()) s = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate(), 10);
            e = new Date(+s + 36e5);
        }
        setModernInputValue("#evTitle", ev ? ev.title : "");
        setModernDatePickerWithLabelValue("#evStartDate", dmy(s));
        setModernDatePickerWithLabelValue("#evEndDate", dmy(e));
        setModernDropdownWithLabelValue("#evStartTime", ev && ev.allDay ? "10:00" : hm(s));
        setModernDropdownWithLabelValue("#evEndTime", ev && ev.allDay ? "10:30" : hm(e));
        setModernCheckboxValue("#evAllDay", !!(ev && ev.allDay));
        setModernInputValue("#evLocation", ev && ev.location || "");
        setModernInputValue("#evNotes", ev && ev.notes || "");
        pickColor(ev ? ev.color : "indigo");
        var invited = {};
        (ev ? ev.people : []).forEach(function (p) { invited[p.id] = true; });
        (cfg.people || []).forEach(function (p) {
            var box = document.getElementById("evp_" + p.id.replace(/-/g, ""));
            if (box) setModernCheckboxValue(box, !!invited[p.id]);
        });
        var find = document.getElementById("evPeopleFind");
        if (find) { setModernInputValue("#evPeopleFind", ""); filterPeople(""); }
        allDayLook();
        VK.openDialog ? VK.openDialog(dlg) : dlg.showModal();
        setTimeout(function () { var t = document.getElementById("evTitle"); if (t) t.focus(); }, 60);
    }
    function allDayLook() {
        var on = getModernCheckboxValue("#evAllDay");
        ["#evStartTime", "#evEndTime"].forEach(function (sel) {
            var el = document.querySelector(sel); if (!el) return;
            var wrap = el.closest(".mdl-wrapper, .modern-dropdown-with-label, [class*='dropdown']") || el.parentElement;
            wrap.classList.toggle("vk-off", !!on);
        });
    }
    function filterPeople(q) {
        q = (q || "").toLowerCase().trim();
        document.querySelectorAll("#evPeople .vk-ev-person").forEach(function (row) { row.hidden = q && row.getAttribute("data-name").indexOf(q) < 0; });
    }
    function dropdownValue(sel) { var h = document.getElementById(sel.slice(1) + "_value"); return h ? h.value : ""; }

    function save() {
        var title = (getModernInputValue("#evTitle") || "").trim();
        if (!title) { VK.toast("Give the event a name.", "warning"); document.getElementById("evTitle").focus(); return; }
        var sd = parseDmy(getModernDatePickerWithLabelValue("#evStartDate")), ed = parseDmy(getModernDatePickerWithLabelValue("#evEndDate"));
        if (!sd) { VK.toast("Pick the day it starts.", "warning"); return; }
        if (!ed) ed = sd;
        var allDay = !!getModernCheckboxValue("#evAllDay"), s, e;
        if (allDay) {
            if (ed < sd) { VK.toast("The event ends before it starts.", "warning"); return; }
            s = new Date(Date.UTC(sd.getFullYear(), sd.getMonth(), sd.getDate()));
            e = new Date(Date.UTC(ed.getFullYear(), ed.getMonth(), ed.getDate() + 1));
        } else {
            var st = (dropdownValue("#evStartTime") || "10:00").split(":"), et = (dropdownValue("#evEndTime") || "10:30").split(":");
            s = new Date(sd.getFullYear(), sd.getMonth(), sd.getDate(), +st[0], +st[1]);
            e = new Date(ed.getFullYear(), ed.getMonth(), ed.getDate(), +et[0], +et[1]);
            if (e <= s) { VK.toast("The event ends before it starts. Change the end time.", "warning"); return; }
        }
        var people = [];
        document.querySelectorAll('#evPeople input[type=checkbox][name="people"]').forEach(function (c) { if (c.checked) people.push(c.value); });
        var colorEl = document.querySelector('input[name="evColor"]:checked');
        var id = document.getElementById("evId").value || null;
        var body = {
            event: { eventId: id, title: title, startUtc: s.toISOString(), endUtc: e.toISOString(), allDay: allDay,
                location: getModernInputValue("#evLocation"), notes: getModernInputValue("#evNotes"), color: colorEl ? colorEl.value : "indigo", people: people },
            whenText: whenText(norm({ start: s.toISOString(), end: e.toISOString(), allDay: allDay }))
        };
        call("/days/editor/save", body).then(function (res) {
            if (!res.ok) { VK.toast(res.error || "Not saved.", "error"); return; }
            VK.closeDialog(document.getElementById("eventDialog"));
            VK.toast(res.message || "Saved.", "success");
            var ev = res.event && norm(res.event);
            if (ev && (view !== "agenda") && (ev.s < range().from || ev.s >= range().to)) cursor = new Date(ev.s.getFullYear(), ev.s.getMonth(), ev.s.getDate());
            load();
        }).catch(function (e2) { VK.toast(e2.message || "Not saved.", "error"); });
    }

    // ---------- one event, opened ----------
    function show(ev) {
        var dlg = document.getElementById("eventView");
        document.getElementById("evvBand").className = "vk-ev-view-band ev-" + ev.color;
        document.getElementById("evvTitle").textContent = ev.title;
        document.getElementById("evvWhen").textContent = whenText(ev);
        var people = ev.people || [], html = '<div class="vk-evv-rows">';
        if (ev.location) html += '<p><i class="fa fa-map-marker"></i><span>' + linkify(ev.location) + "</span></p>";
        html += '<p><i class="fa fa-user-circle-o"></i><span>Organised by <b>' + esc(ev.mine ? "you" : ev.owner) + "</b></span></p>";
        if (ev.notes) html += '<p><i class="fa fa-align-left"></i><span class="vk-evv-notes">' + esc(ev.notes) + "</span></p>";
        html += "</div>";
        if (people.length) {
            var yes = people.filter(function (p) { return p.response === "yes"; }).length;
            html += '<h4 class="vk-evv-h">' + people.length + " invited · " + yes + " going</h4><div class=\"vk-evv-people\">";
            people.forEach(function (p) {
                html += '<span class="vk-evv-person"><span class="vk-evv-init">' + esc(initials(p.name)) + '</span><span><b>' + esc(p.name) + "</b><small>" + esc(p.email) +
                    '</small></span><em class="vk-rsvp r-' + esc(p.response) + '">' + ({ yes: "Going", no: "Not going", maybe: "Maybe", pending: "Not answered" }[p.response] || "") + "</em></span>";
            });
            html += "</div>";
        }
        document.getElementById("evvBody").innerHTML = html;
        var foot = "";
        if (ev.mine) foot = '<button type="button" class="vk-btn vk-btn-danger" data-cmd="dDelete" data-arg="' + esc(ev.id) + '"><i class="fa fa-trash"></i> Delete</button><span class="vk-fill"></span>' +
            '<button class="vk-btn vk-btn-light" value="cancel">Close</button><button type="button" class="vk-btn vk-btn-primary" data-cmd="dEdit" data-arg="' + esc(ev.id) + '"><i class="fa fa-pencil"></i> Edit</button>';
        else foot = '<span class="vk-evv-ask">Going?</span><span class="vk-days-seg vk-evv-rsvp">' +
            ["yes", "maybe", "no"].map(function (r) {
                return '<button type="button" data-cmd="dRespond" data-arg="' + esc(ev.id) + "|" + r + '" aria-pressed="' + (ev.my === r) + '">' + { yes: "Yes", maybe: "Maybe", no: "No" }[r] + "</button>";
            }).join("") + '</span><span class="vk-fill"></span><button class="vk-btn vk-btn-light" value="cancel">Close</button>';
        document.getElementById("evvFoot").innerHTML = foot;
        if (!dlg.open) dlg.showModal();
    }
    function initials(n) { var p = String(n || "?").split(/\s+/).filter(Boolean); return ((p[0] || "?").charAt(0) + (p.length > 1 ? p[p.length - 1].charAt(0) : (p[0] || "").charAt(1) || "")).toUpperCase(); }
    function linkify(s) { return /^https?:\/\//i.test(s) ? '<a href="' + esc(s) + '" target="_blank" rel="noopener">' + esc(s) + "</a>" : esc(s); }

    // ---------- commands ----------
    function go(v) { view = v; load(); }
    function step(n) {
        if (view === "month") cursor = new Date(cursor.getFullYear(), cursor.getMonth() + n, 1);
        else cursor = addDays(cursor, n * (view === "week" ? 7 : view === "day" ? 1 : 30));
        load();
    }
    VK.on("dNew", function () { openEditor(null); });
    VK.on("dToday", function () { cursor = today(); load(); });
    VK.on("dPrev", function () { step(-1); });
    VK.on("dNext", function () { step(1); });
    VK.on("dPrevMonth", function () { cursor = new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1); load(); });
    VK.on("dNextMonth", function () { cursor = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1); load(); });
    VK.on("dMonth", function () { go("month"); });
    VK.on("dWeek", function () { go("week"); });
    VK.on("dDay", function () { go("day"); });
    VK.on("dAgenda", function () { go("agenda"); });
    VK.on("dRefresh", function () { load(); VK.toast("Calendar is up to date.", "info"); });
    VK.on("dPrint", function () { window.print(); });
    VK.on("dSave", save);
    VK.on("dEdit", function (id) { var ev = byId(id); document.getElementById("eventView").close(); if (ev) openEditor(ev); });
    VK.on("dDelete", function (id) {
        var ev = byId(id); if (!ev) return;
        confirmMessageBox({
            title: "Delete this event?", type: "error", icon: "error", danger: true, confirmText: "Delete", cancelText: "Keep it", asHtml: true,
            text: "<p class='vk-mb-lead'><b>" + esc(ev.title) + "</b> on " + esc(whenText(ev)) + " is removed from your calendar" +
                (ev.people && ev.people.length ? " and from the calendars of the " + ev.people.length + " invited " + (ev.people.length === 1 ? "person" : "people") : "") + ".</p>",
            onConfirm: function () {
                call("/days/editor/delete?id=" + encodeURIComponent(id), {}).then(function (res) {
                    if (!res.ok) { VK.toast(res.error, "error"); return; }
                    document.getElementById("eventView").close();
                    VK.toast(res.message, "success"); load();
                });
            }
        });
    });
    VK.on("dRespond", function (arg) {
        var p = String(arg).split("|");
        call("/days/editor/respond?id=" + encodeURIComponent(p[0]) + "&response=" + p[1], {}).then(function (res) {
            if (!res.ok) { VK.toast(res.error, "error"); return; }
            VK.toast(res.message, "success");
            var ev = byId(p[0]);
            if (ev) { ev.my = p[1]; (ev.people || []).forEach(function (x) { if (x.id === cfg.me) x.response = p[1]; }); show(ev); }
            load();
        });
    });

    function wire() {
        var host = document.querySelector(".vk-days");
        if (!host || host.getAttribute("data-wired")) return;
        host.setAttribute("data-wired", "1");
        host.addEventListener("click", function (e) {
            var evb = e.target.closest("[data-ev]");
            if (evb) { var ev = byId(evb.getAttribute("data-ev")); if (ev) show(ev); return; }
            var g = e.target.closest("[data-goday]");
            if (g) { cursor = parseDmy(g.getAttribute("data-goday")); if (view === "month" && g.closest(".vk-mini")) { draw(); miniCal(); load(); return; } view = view === "month" && !g.closest(".vk-mini") ? "day" : view; load(); return; }
            var col = e.target.closest(".vk-tg-col");
            if (col) {
                var day = parseDmy(col.getAttribute("data-day")), r = col.getBoundingClientRect(), h = (e.clientY - r.top) / HOUR;
                openEditor(null, new Date(day.getFullYear(), day.getMonth(), day.getDate(), Math.floor(h), h % 1 >= .5 ? 30 : 0));
                return;
            }
            var cell = e.target.closest(".vk-mo-cell, .vk-tg-allcell");
            if (cell) { var d = parseDmy(cell.getAttribute("data-day")); openEditor(null, new Date(d.getFullYear(), d.getMonth(), d.getDate(), 10)); }
        });
        var allDay = document.getElementById("evAllDay");
        if (allDay) allDay.addEventListener("change", allDayLook);
        var find = document.getElementById("evPeopleFind");
        if (find) find.addEventListener("input", function () { filterPeople(find.value); });
        // when the start moves, the end keeps the same length
        var sd = document.getElementById("evStartDate");
        if (sd) sd.addEventListener("change", function () {
            var s = parseDmy(sd.value), e = parseDmy(getModernDatePickerWithLabelValue("#evEndDate"));
            if (s && (!e || e < s)) setModernDatePickerWithLabelValue("#evEndDate", dmy(s));
        });
        if (docWired) return;   // the page can be opened again from the side rail without a reload
        docWired = true;
        document.addEventListener("keydown", function (e) {
            if (!document.querySelector(".vk-days") || document.querySelector("dialog[open]") || /input|textarea|select/i.test((e.target.tagName || "")) || e.ctrlKey || e.metaKey || e.altKey) return;
            var k = e.key.toLowerCase();
            if (k === "t") VK.handlers.dToday(); else if (k === "m") go("month"); else if (k === "w") go("week"); else if (k === "d") go("day");
            else if (k === "a") go("agenda"); else if (k === "n" || k === "c") { e.preventDefault(); openEditor(null); }
            else if (e.key === "ArrowLeft") step(-1); else if (e.key === "ArrowRight") step(1);
        });
        // the red "now" line moves on its own
        setInterval(function () { if (document.querySelector(".vk-days") && (view === "week" || view === "day") && !document.querySelector("dialog[open]")) { var sc = document.querySelector(".vk-tg-scroll"), top = sc ? sc.scrollTop : 0; draw(); if (sc) document.querySelector(".vk-tg-scroll").scrollTop = top; } }, 5 * 60 * 1000);
    }

    function init() {
        if (!document.getElementById("daysCfg")) return;
        try { cfg = JSON.parse(document.getElementById("daysCfg").textContent); } catch (e) { cfg = {}; }
        events = []; upcoming = []; loadedRange = null; busy = false;
        try { var v = localStorage.getItem("vk-days-view"); if (/^(month|week|day|agenda)$/.test(v || "")) view = v; } catch (e) { }
        wire();
        load();
        if (cfg.openNew) {
            history.replaceState(null, "", location.pathname);
            setTimeout(function () { openEditor(null); }, 250);
        }
    }

    return { init: init };
})();
