/*
 * Resource Overview (web resource mth_/resources/overview.html). Plain ES5-compatible JavaScript, no build step.
 * One row per person with the weekly load (sum of Allocation % of the roles active on each working day, divided by the
 * person's capacity that day: 100 %, or Capacity % from vacation/leave/reduced-capacity rows), and, when expanded, one
 * Gantt bar per case team role and absence. Load colours: < 80 % green, 80-100 % yellow, > 100 % red.
 * Only roles on opportunities with Status Number < 85 count (85 filed, 9x lost/cancelled/deleted).
 * Texts in Danish or English after the user's language. See docs/system/resources.md.
 */
(function () {
    "use strict";
    var Xrm = (window.parent && window.parent.Xrm) || window.Xrm;
    var lang = 1033;
    try { lang = Xrm.Utility.getGlobalContext().userSettings.languageId; } catch (e) { /* standalone */ }
    var DA = lang === 1030;
    var T = {
        title: ["Ressourceoverblik", "Resource Overview"],
        subtitle: ["Belastning pr. person og uge ud fra sagsteamsroller (start, slut, belastning %) og fravær. Klik på et navn for at se sagerne.",
                   "Load per person and week from case team roles (start, end, allocation %) and absence. Click a name to see the cases."],
        from: ["Fra", "From"], weeks: ["Uger", "Weeks"], dept: ["Tilbudsafdeling", "Tender department"], roles: ["Roller", "Roles"],
        person: ["Person", "Person"], expand: ["Vis sager", "Show cases"], onlyBusy: ["Kun personer med roller eller fravær", "Only people with roles or absence"],
        addAbsence: ["+ Fravær", "+ Absence"], reload: ["Opdatér", "Refresh"],
        all: ["Alle", "All"], allRoles: ["Alle roller", "All roles"], tenderRoles: ["Tilbudsroller", "Tender roles"], execRoles: ["Udførelsesroller", "Execution roles"],
        lOk: ["Under 80 %", "Below 80 %"], lWarn: ["80–100 %", "80–100 %"], lOver: ["Over 100 %", "Over 100 %"], lAway: ["Fravær", "Absent"],
        lTender: ["Tilbudsrolle", "Tender role"], lExec: ["Udførelsesrolle", "Execution role"],
        loading: ["Henter …", "Loading …"], week: ["Uge", "Week"], persons: ["{0} personer, {1} roller, {2} fravær", "{0} people, {1} roles, {2} absences"],
        none: ["Ingen personer i perioden med de valgte filtre.", "No people in the period with the selected filters."], failed: ["Fejl: ", "Error: "],
        away: ["Fri", "Off"], noDates: ["{0} roller uden datoer er ikke med.", "{0} roles without dates are not included."],
        addRole: ["+ Ressource", "+ Resource"], "case": ["Sag", "Case"], pick: ["Vælg …", "Pick …"], role: ["Rolle", "Role"],
        start: ["Start", "Start"], end: ["Slut", "End"], alloc: ["Belastning %", "Allocation %"],
        splitAt: ["Del fra dato", "Split from date"], splitAlloc: ["Ny %", "New %"], split: ["Del perioden", "Split period"],
        save: ["Gem", "Save"], remove: ["Fjern fra sagen", "Remove from case"], openCase: ["Åbn sag", "Open case"], cancel: ["Annullér", "Cancel"],
        editTitle: ["Ressource på sag", "Resource on case"], addTitle: ["Tilføj ressource til sag", "Add resource to case"],
        editSub: ["Træk bjælken for at flytte, træk i kanterne for at ændre start og slut.", "Drag the bar to move it, drag its edges to change start and end."],
        addSub: ["Tomme datoer udfyldes ud fra sagens tilbuds- eller ordredatoer.", "Empty dates are filled from the case's tender or order dates."],
        confirmDelete: ["Fjern {0} som {1} fra {2}?", "Remove {0} as {1} from {2}?"],
        splitInvalid: ["Datoen skal ligge efter start og senest på slutdatoen.", "The date must be after the start and no later than the end date."],
        needAll: ["Vælg sag, person og rolle.", "Pick case, person and role."], saving: ["Gemmer …", "Saving …"]
    };
    var ROLES = [[273280000, "Tender Manager"], [273280002, "Tender Responsible"], [273280003, "Tender Coordinator"], [273280004, "Tender Calculator"],
        [273280005, "Pre-Qualification Responsible"], [273280006, "Bid Manager"], [273280001, "Project Manager"], [273280007, "Design Manager"],
        [273280008, "Guarantee Responsible"], [273280009, "Primary Responsible"]];
    // Case mode: embedded on the opportunity form (tab Case Team, web resource with PassParameters and data "mode=case").
    // Shows only the people on this case; their load row is still their total load across all cases, and their bars on
    // other cases are faded. "+ Resource" adds to this case. Edits refresh the form's Case Team subgrid and vice versa.
    var params = {};
    window.location.search.substr(1).split("&").forEach(function (p) {
        var kv = p.split("="); if (kv[0]) { params[decodeURIComponent(kv[0]).toLowerCase()] = decodeURIComponent((kv[1] || "").replace(/\+/g, " ")); }
    });
    var CASE = null;
    if ((params.data || "").indexOf("mode=case") >= 0 || params.typename === "mth_opportunity") { CASE = (params.id || "").replace(/[{}]/g, "").toLowerCase() || "new"; }
    T.saveFirst = ["Gem sagen først for at planlægge sagsteamet.", "Save the case first to plan the case team."];
    T.casePeople = ["{0} personer på sagen, {1} perioder på sagen", "{0} people on the case, {1} periods on the case"];
    function t(k) { var v = T[k]; return v ? v[DA ? 0 : 1] : k; }
    function fmt(s) { var a = arguments; return s.replace(/\{(\d)\}/g, function (m, i) { return a[+i + 1]; }); }
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
    [].forEach.call(document.querySelectorAll("[data-t]"), function (el) { el.textContent = t(el.getAttribute("data-t")); });
    document.documentElement.lang = DA ? "da" : "en";

    // Role groups (mth_opportunityrole.mth_role)
    var TENDER_ROLES = [273280000, 273280002, 273280003, 273280004, 273280005, 273280006];
    var FV = "@OData.Community.Display.V1.FormattedValue";
    var DAY = 86400000;

    // ---- dates (date-only values as local dates, no time zone shifts) ----
    function parseDate(s) { if (!s) { return null; } var p = s.substr(0, 10).split("-"); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function iso(d) { return d.getFullYear() + "-" + ("0" + (d.getMonth() + 1)).slice(-2) + "-" + ("0" + d.getDate()).slice(-2); }
    function addDays(d, n) { var r = new Date(d.getTime()); r.setDate(r.getDate() + n); return r; }
    function monday(d) { var r = new Date(d.getFullYear(), d.getMonth(), d.getDate()); var wd = (r.getDay() + 6) % 7; return addDays(r, -wd); }
    function isoWeek(d) {
        var x = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
        var dayNum = x.getUTCDay() || 7; x.setUTCDate(x.getUTCDate() + 4 - dayNum);
        var y0 = new Date(Date.UTC(x.getUTCFullYear(), 0, 1));
        return Math.ceil(((x - y0) / DAY + 1) / 7);
    }
    function days(a, b) { return Math.round((b - a) / DAY); }
    var MONTHS = DA ? ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"] : ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    function short(d) { return d.getDate() + ". " + MONTHS[d.getMonth()] + " " + d.getFullYear(); }

    // ---- data ----
    function all(entity, query) {
        var out = [];
        function page(q) {
            return Xrm.WebApi.retrieveMultipleRecords(entity, q, 5000).then(function (r) {
                out = out.concat(r.entities);
                if (r.nextLink) { return page(r.nextLink.substr(r.nextLink.indexOf("?"))); }
                return out;
            });
        }
        return page(query);
    }

    var state = { users: [], depts: [], roles: [], absences: [], open: {} };

    function loadStatic() {
        return Promise.all([
            all("systemuser", "?$select=fullname&$filter=isdisabled eq false and accessmode eq 0&$expand=mth_orgunit_systemuser_TenderDept($select=mth_name)&$orderby=fullname"),
            all("mth_organizationalunit", "?$select=mth_name&$filter=mth_istenderdepartment eq true and statecode eq 0&$orderby=mth_name")
        ]).then(function (r) {
            state.users = r[0].map(function (u) {
                return { id: u.systemuserid, name: u.fullname, depts: (u.mth_orgunit_systemuser_TenderDept || []).map(function (d) { return { id: d.mth_organizationalunitid, name: d.mth_name }; }) };
            });
            state.depts = r[1];
            var sel = $("dept");
            sel.innerHTML = '<option value="">' + esc(t("all")) + "</option>" + state.depts.map(function (d) {
                return '<option value="' + d.mth_organizationalunitid + '">' + esc(d.mth_name) + "</option>";
            }).join("");
        });
    }

    function loadPeriod(start, end) {
        var s = iso(start), e = iso(addDays(end, -1));
        return Promise.all([
            all("mth_opportunityrole", "?$select=mth_role,mth_startdate,mth_enddate,mth_allocation,_mth_userid_value,_mth_opportunityid_value" +
                "&$filter=statecode eq 0 and mth_startdate le " + e + " and mth_enddate ge " + s +
                "&$expand=mth_OpportunityId($select=mth_name,mth_statusnumber)"),
            all("mth_availability", "?$select=mth_name,mth_type,mth_startdate,mth_enddate,mth_capacity,_mth_userid_value" +
                "&$filter=statecode eq 0 and mth_startdate le " + e + " and mth_enddate ge " + s),
            all("mth_opportunityrole", "?$select=mth_opportunityroleid&$filter=statecode eq 0 and (mth_startdate eq null or mth_enddate eq null)")
        ]).then(function (r) {
            state.roles = r[0].filter(function (x) {
                var o = x.mth_OpportunityId;
                return o && (x._mth_opportunityid_value === CASE || o.mth_statusnumber == null || o.mth_statusnumber < 85);
            })
                .map(function (x) {
                    return { id: x.mth_opportunityroleid, user: x._mth_userid_value, role: x.mth_role, roleName: x["mth_role" + FV],
                        start: parseDate(x.mth_startdate), end: parseDate(x.mth_enddate), alloc: x.mth_allocation == null ? 100 : x.mth_allocation,
                        oppId: x._mth_opportunityid_value, oppName: x.mth_OpportunityId.mth_name, tender: TENDER_ROLES.indexOf(x.mth_role) >= 0 };
                });
            state.absences = r[1].map(function (x) {
                return { id: x.mth_availabilityid, user: x._mth_userid_value, type: x["mth_type" + FV], name: x.mth_name,
                    start: parseDate(x.mth_startdate), end: parseDate(x.mth_enddate), cap: x.mth_capacity == null ? 0 : x.mth_capacity };
            });
            state.noDates = r[2].length;
        });
    }

    // ---- load calculation ----
    function weekLoad(user, roles, absences, wStart) {
        var loadSum = 0, capSum = 0, partAway = false;
        for (var i = 0; i < 5; i++) { // Monday-Friday
            var d = addDays(wStart, i), cap = 100, load = 0, j;
            for (j = 0; j < absences.length; j++) { if (absences[j].start <= d && d <= absences[j].end) { cap = Math.min(cap, absences[j].cap); } }
            for (j = 0; j < roles.length; j++) { if (roles[j].start <= d && d <= roles[j].end) { load += roles[j].alloc; } }
            if (cap < 100) { partAway = true; }
            loadSum += load; capSum += cap;
        }
        if (capSum === 0) { return { cls: "load-away", text: loadSum > 0 ? "!" : t("away"), over: loadSum > 0, part: false }; }
        if (loadSum === 0) { return { cls: "", text: "", part: partAway }; }
        var pct = Math.round(loadSum / capSum * 100);
        return { cls: pct > 100 ? "load-over" : pct >= 80 ? "load-warn" : "load-ok", text: pct + "%", part: partAway, pct: pct };
    }

    // ---- render ----
    function render() {
        var start = monday(parseDate($("from").value) || new Date()), n = +$("weeks").value, end = addDays(start, n * 7);
        var dept = $("dept").value, roleGroup = $("roles").value, q = $("search").value.trim().toLowerCase();
        var expandAll = $("expand").checked, onlyBusy = $("onlyBusy").checked;
        var wk = 46, today = new Date(), weeks = [];
        for (var i = 0; i < n; i++) { weeks.push(addDays(start, i * 7)); }

        var roles = state.roles.filter(function (r) { return CASE || roleGroup === "" || (roleGroup === "tender") === r.tender; });
        var people = state.users.filter(function (u) {
            if (CASE) { return roles.some(function (r) { return r.user === u.id && r.oppId === CASE; }) && (!q || u.name.toLowerCase().indexOf(q) >= 0); }
            if (dept && !u.depts.some(function (d) { return d.id === dept; })) { return false; }
            if (q && u.name.toLowerCase().indexOf(q) < 0) { return false; }
            if (onlyBusy && !roles.some(function (r) { return r.user === u.id; }) && !state.absences.some(function (a) { return a.user === u.id; })) { return false; }
            return true;
        });

        var html = ['<div class="row head"><div class="name">' + esc(t("person")) + '</div><div class="cells">'];
        weeks.forEach(function (w) {
            var now = today >= w && today < addDays(w, 7);
            html.push('<div class="cell' + (now ? " now" : "") + '" title="' + esc(short(w)) + '">' + esc(t("week")) + " " + isoWeek(w) +
                "<br>" + (w.getDate() <= 7 || w === weeks[0] ? esc(MONTHS[w.getMonth()]) : "&nbsp;") + "</div>");
        });
        html.push("</div></div>");

        var roleCount = 0, absCount = 0;
        people.forEach(function (u) {
            var ur = roles.filter(function (r) { return r.user === u.id; }), ua = state.absences.filter(function (a) { return a.user === u.id; });
            roleCount += ur.length; absCount += ua.length;
            var open = expandAll || (state.open[u.id] === undefined ? !!CASE : state.open[u.id]);
            html.push('<div class="row person" data-user="' + u.id + '"><div class="name" title="' + esc(u.name) + '"><span class="tw">' + (open ? "▾" : "▸") + "</span>" +
                esc(u.name) + u.depts.map(function (d) { return ' <span class="tag">' + esc(d.name) + "</span>"; }).join("") +
                '<button class="add" data-adduser="' + u.id + '" title="' + esc(t("addTitle")) + '">+</button></div><div class="cells">');
            weeks.forEach(function (w) {
                var l = weekLoad(u, ur, ua, w), now = today >= w && today < addDays(w, 7);
                html.push('<div class="cell ' + l.cls + (l.part ? " part" : "") + (now ? " now" : "") + '">' + esc(l.text) + "</div>");
            });
            html.push("</div></div>");
            if (!open) { return; }
            ur.concat(ua.map(function (a) { a.away = true; return a; })).sort(function (a, b) {
                if (CASE && (a.oppId === CASE) !== (b.oppId === CASE)) { return a.oppId === CASE ? -1 : 1; } // this case first
                return a.start - b.start;
            }).forEach(function (x) {
                var s = x.start < start ? start : x.start, e = addDays(x.end, 1) > end ? end : addDays(x.end, 1);
                var left = days(start, s) * wk / 7, width = Math.max(days(s, e) * wk / 7, 6);
                var label = x.away ? x.name : x.oppName + " – " + x.roleName + " " + x.alloc + "%";
                var title = label + " (" + short(x.start) + " – " + short(x.end) + ")";
                html.push('<div class="row detail"><div class="name" title="' + esc(title) + '">' + esc(x.away ? x.type : x.roleName) + '</div><div class="lane" style="width:' + (n * wk) + 'px">' +
                    '<div class="gbar ' + (x.away ? "away" : (x.tender ? "tender" : "exec") + " role" + (CASE && x.oppId !== CASE ? " other" : "")) + '" style="left:' + left + "px;width:" + width + 'px" title="' + esc(title) + '"' +
                    (x.away ? ' data-abs="' + x.id + '">' : ' data-role="' + x.id + '"><i class="grip l"></i><i class="grip r"></i>') + esc(label) + "</div></div></div>");
            });
        });
        $("grid").innerHTML = people.length ? html.join("") : '<div class="empty">' + esc(t("none")) + "</div>";
        $("status").textContent = CASE
            ? fmt(t("casePeople"), people.length, roles.filter(function (r) { return r.oppId === CASE; }).length)
            : fmt(t("persons"), people.length, roleCount, absCount) + (state.noDates ? " · " + fmt(t("noDates"), state.noDates) : "");
    }

    function refresh() {
        var start = monday(parseDate($("from").value) || new Date()), end = addDays(start, +$("weeks").value * 7);
        $("status").textContent = t("loading");
        return loadPeriod(start, end).then(render, fail);
    }
    // After an edit: also refresh the Case Team subgrid on the opportunity form (case mode)
    function caseGrid() { try { return Xrm.Page.getControl("CaseTeam"); } catch (e) { return null; } }
    function changed() { var g = CASE && caseGrid(); if (g) { try { g.refresh(); } catch (e) { /* ignore */ } } return refresh(); }
    function fail(e) { $("status").innerHTML = '<span class="error">' + esc(t("failed") + ((e && e.message) || e)) + "</span>"; }

    // ---- editing: dialog (add, change, split, remove) ----
    var dlg = { role: null, oppId: null, oppName: "" };
    function roleById(id) { for (var i = 0; i < state.roles.length; i++) { if (state.roles[i].id === id) { return state.roles[i]; } } return null; }
    function userName(id) { for (var i = 0; i < state.users.length; i++) { if (state.users[i].id === id) { return state.users[i].name; } } return ""; }
    $("dUser").innerHTML = "";
    $("dRole").innerHTML = ROLES.map(function (r) { return '<option value="' + r[0] + '">' + esc(r[1]) + "</option>"; }).join("");
    function fillUsers() { $("dUser").innerHTML = '<option value=""></option>' + state.users.map(function (u) { return '<option value="' + u.id + '">' + esc(u.name) + "</option>"; }).join(""); }

    function openDialog(role, userId) {
        dlg.role = role; dlg.oppId = role ? role.oppId : CASE; dlg.oppName = role ? role.oppName : (CASE ? state.caseName : "");
        $("dTitle").textContent = t(role ? "editTitle" : "addTitle");
        $("dSub").textContent = t(role ? "editSub" : "addSub");
        $("dOpp").textContent = dlg.oppName; $("dPick").style.display = role || CASE ? "none" : "";
        $("dUser").value = role ? role.user : (userId || ""); $("dRole").value = role ? String(role.role) : String(ROLES[3][0]);
        $("dStart").value = role ? iso(role.start) : ""; $("dEnd").value = role ? iso(role.end) : "";
        $("dAlloc").value = role ? role.alloc : 100;
        $("dSplitBox").style.display = role ? "" : "none"; $("dSplitDate").value = ""; $("dSplitAlloc").value = role ? Math.round(role.alloc / 2) : "";
        $("dDelete").style.display = role ? "" : "none"; $("dOpen").style.display = role ? "" : "none";
        $("dMsg").textContent = ""; $("modal").classList.remove("hidden");
    }
    function closeDialog() { $("modal").classList.add("hidden"); }
    function busy(on) { ["dSave", "dDelete", "dSplit"].forEach(function (id) { $(id).disabled = on; }); $("dMsg").textContent = on ? t("saving") : ""; }
    function err(e) { busy(false); $("dMsg").textContent = (e && e.message) || String(e); }
    function values() {
        var v = { mth_role: +$("dRole").value, mth_allocation: $("dAlloc").value === "" ? null : Math.max(0, Math.min(100, +$("dAlloc").value)) };
        if ($("dStart").value) { v.mth_startdate = $("dStart").value; } else if (dlg.role) { v.mth_startdate = null; }
        if ($("dEnd").value) { v.mth_enddate = $("dEnd").value; } else if (dlg.role) { v.mth_enddate = null; }
        v["mth_UserId@odata.bind"] = "/systemusers(" + $("dUser").value + ")";
        return v;
    }
    $("dPick").addEventListener("click", function () {
        Xrm.Utility.lookupObjects({ entityTypes: ["mth_opportunity"], defaultEntityType: "mth_opportunity", allowMultiSelect: false }).then(function (r) {
            if (r && r.length) { dlg.oppId = r[0].id.replace(/[{}]/g, "").toLowerCase(); dlg.oppName = r[0].name; $("dOpp").textContent = r[0].name; }
        });
    });
    $("dSave").addEventListener("click", function () {
        if (!dlg.oppId || !$("dUser").value || !$("dRole").value) { $("dMsg").textContent = t("needAll"); return; }
        var v = values(); busy(true);
        var p = dlg.role ? Xrm.WebApi.updateRecord("mth_opportunityrole", dlg.role.id, v)
            : Xrm.WebApi.createRecord("mth_opportunityrole", (v["mth_OpportunityId@odata.bind"] = "/mth_opportunities(" + dlg.oppId + ")", v));
        p.then(function () { busy(false); closeDialog(); return changed(); }, err);
    });
    $("dDelete").addEventListener("click", function () {
        var r = dlg.role;
        Xrm.Navigation.openConfirmDialog({ text: fmt(t("confirmDelete"), userName(r.user), r.roleName, r.oppName) }).then(function (c) {
            if (!c.confirmed) { return; }
            busy(true);
            Xrm.WebApi.deleteRecord("mth_opportunityrole", r.id).then(function () { busy(false); closeDialog(); return changed(); }, err);
        });
    });
    // Split: this row ends the day before the split date; a new row for the same person and role starts on it with "New %"
    $("dSplit").addEventListener("click", function () {
        var r = dlg.role, at = parseDate($("dSplitDate").value);
        if (!at || at <= r.start || at > r.end) { $("dMsg").textContent = t("splitInvalid"); return; }
        var newAlloc = $("dSplitAlloc").value === "" ? r.alloc : Math.max(0, Math.min(100, +$("dSplitAlloc").value));
        busy(true);
        Xrm.WebApi.updateRecord("mth_opportunityrole", r.id, { mth_enddate: iso(addDays(at, -1)) }).then(function () {
            return Xrm.WebApi.createRecord("mth_opportunityrole", { mth_role: r.role, mth_startdate: iso(at), mth_enddate: iso(r.end), mth_allocation: newAlloc,
                "mth_UserId@odata.bind": "/systemusers(" + r.user + ")", "mth_OpportunityId@odata.bind": "/mth_opportunities(" + r.oppId + ")" });
        }).then(function () { busy(false); closeDialog(); return changed(); }, err);
    });
    $("dOpen").addEventListener("click", function () { var r = dlg.role; closeDialog(); Xrm.Navigation.openForm({ entityName: "mth_opportunity", entityId: r.oppId }); });
    $("dCancel").addEventListener("click", closeDialog);
    $("modal").addEventListener("click", function (ev) { if (ev.target === $("modal")) { closeDialog(); } });

    // ---- editing: drag a bar to move it, drag an edge to change start or end (snaps to whole days) ----
    var drag = null, justDragged = false;
    $("grid").addEventListener("mousedown", function (ev) {
        var bar = ev.target.closest ? ev.target.closest(".gbar.role") : null;
        if (!bar || ev.button !== 0) { return; }
        var mode = ev.target.classList.contains("l") ? "start" : ev.target.classList.contains("r") ? "end" : "move";
        drag = { bar: bar, role: roleById(bar.getAttribute("data-role")), mode: mode, x: ev.clientX, left: parseFloat(bar.style.left), width: parseFloat(bar.style.width), d: 0 };
        ev.preventDefault();
    });
    document.addEventListener("mousemove", function (ev) {
        if (!drag) { return; }
        var perDay = 46 / 7, d = Math.round((ev.clientX - drag.x) / perDay);
        if (d === drag.d) { return; }
        drag.d = d; drag.bar.classList.add("dragging");
        if (drag.mode === "move") { drag.bar.style.left = (drag.left + d * perDay) + "px"; }
        else if (drag.mode === "start") { drag.bar.style.left = (drag.left + d * perDay) + "px"; drag.bar.style.width = Math.max(6, drag.width - d * perDay) + "px"; }
        else { drag.bar.style.width = Math.max(6, drag.width + d * perDay) + "px"; }
    });
    document.addEventListener("mouseup", function () {
        if (!drag) { return; }
        var g = drag; drag = null;
        if (!g.d || !g.role) { g.bar.classList.remove("dragging"); return; }
        justDragged = true; setTimeout(function () { justDragged = false; }, 0);
        var s = g.role.start, e = g.role.end;
        if (g.mode !== "end") { s = addDays(s, g.d); }
        if (g.mode !== "start") { e = addDays(e, g.d); }
        if (e < s) { if (g.mode === "start") { s = e; } else { e = s; } }
        $("status").textContent = t("saving");
        Xrm.WebApi.updateRecord("mth_opportunityrole", g.role.id, { mth_startdate: iso(s), mth_enddate: iso(e) }).then(changed, function (x) { fail(x); refresh(); });
    });

    // ---- events ----
    $("grid").addEventListener("click", function (ev) {
        var el = ev.target;
        if (justDragged) { return; }
        var add = el.getAttribute("data-adduser");
        if (add) { ev.stopPropagation(); openDialog(null, add); return; }
        var bar = el.closest ? el.closest(".gbar.role") : null;
        if (bar) { openDialog(roleById(bar.getAttribute("data-role"))); return; }
        if (el.getAttribute("data-abs")) { Xrm.Navigation.openForm({ entityName: "mth_availability", entityId: el.getAttribute("data-abs") }).then(refresh); return; }
        var row = el.closest ? el.closest(".person") : null;
        if (row && el.closest(".name")) { var id = row.getAttribute("data-user"); state.open[id] = !state.open[id]; render(); }
    });
    $("addRole").addEventListener("click", function () { openDialog(null); });
    ["weeks", "from"].forEach(function (id) { $(id).addEventListener("change", refresh); });
    ["dept", "roles", "expand", "onlyBusy"].forEach(function (id) { $(id).addEventListener("change", render); });
    $("search").addEventListener("input", render);
    $("reload").addEventListener("click", refresh);
    $("addAbsence").addEventListener("click", function () {
        Xrm.Navigation.openForm({ entityName: "mth_availability" }).then(refresh);
    });

    $("roles").innerHTML = '<option value="">' + esc(t("allRoles")) + '</option><option value="tender" selected>' + esc(t("tenderRoles")) +
        '</option><option value="exec">' + esc(t("execRoles")) + "</option>";
    $("from").value = iso(addDays(monday(new Date()), -14));
    $("status").textContent = t("loading");
    if (CASE) {
        document.body.className = "case";
        ["dept", "roles", "onlyBusy"].forEach(function (id) { $(id).parentNode.style.display = "none"; });
        $("roles").value = "";
    }
    if (CASE === "new") { $("status").textContent = t("saveFirst"); return; }

    // Case mode: period from this case's roles (one week before the first start, at least until the last end)
    function casePeriod() {
        return Promise.all([
            all("mth_opportunityrole", "?$select=mth_startdate,mth_enddate&$filter=statecode eq 0 and _mth_opportunityid_value eq " + CASE),
            Xrm.WebApi.retrieveRecord("mth_opportunity", CASE, "?$select=mth_name")
        ]).then(function (r) {
            state.caseName = r[1].mth_name;
            var s = null, e = null;
            r[0].forEach(function (x) { var a = parseDate(x.mth_startdate), b = parseDate(x.mth_enddate); if (a && (!s || a < s)) { s = a; } if (b && (!e || b > e)) { e = b; } });
            if (!s) { s = new Date(); e = addDays(s, 90); }
            var from = addDays(monday(s), -7), span = Math.ceil(days(from, e || s) / 7) + 2, w = 52;
            [8, 13, 26, 52].some(function (o) { if (o >= span) { w = o; return true; } return false; });
            $("from").value = iso(from); $("weeks").value = String(w);
        });
    }
    loadStatic().then(function () {
        fillUsers();
        if (!CASE) { return refresh(); }
        // Edits in the Case Team subgrid on the form update the chart (the subgrid's onload fires after each change)
        var g = caseGrid(); if (g && g.addOnLoad) { g.addOnLoad(function () { refresh(); }); }
        return casePeriod().then(refresh);
    }).then(null, fail);
})();
