/*
 * NNE company search (web resource mth_/nne/search.html). Plain ES5-compatible JavaScript, no build step.
 * - Search: Custom API mth_NneSearch (server-side call to NNE with the access key from environment variable mth_NneAccessKey).
 * - Import: Custom API mth_NneImportAccount (creates the account from NNE data, or returns the existing one by TDC ID).
 * Behaviour as the Salesforce LWC nneSearch: same search fields, "Show" for companies that already exist, "Create" otherwise,
 * name opens the details. Texts in Danish or English after the user's language.
 */
(function () {
    "use strict";
    var Xrm = (window.parent && window.parent.Xrm) || window.Xrm;
    var lang = 1033;
    try { lang = Xrm.Utility.getGlobalContext().userSettings.languageId; } catch (e) { /* standalone */ }
    var DA = lang === 1030;
    var T = {
        title: ["Find virksomhed i NNE", "Find company in NNE"],
        subtitle: ["Søg i Navne & Numre Erhverv og opret virksomheden som Account.", "Search Navne & Numre Erhverv and create the company as an account."],
        name: ["Firmanavn", "Company name"], nameStartsWith: ["Navn starter med", "Name starts with"],
        cvr: ["CVR", "CVR"], tdcId: ["Unikt ID (TDC)", "Unique ID (TDC)"], street: ["Vej", "Street"], houseNo: ["Husnr.", "House no."],
        streetStartsWith: ["Vej starter med", "Street starts with"], zip: ["Postnr.", "Postal code"], city: ["By", "City"],
        phone: ["Telefon", "Phone"], maxHits: ["Maks. hits", "Max hits"], activeOnly: ["Kun aktive", "Active only"],
        legalOnly: ["Kun juridiske enheder", "Legal entities only"], search: ["Søg", "Search"], clear: ["Ryd", "Clear"],
        colName: ["Navn", "Name"], colAddress: ["Adresse", "Address"], colStatus: ["Status", "Status"], close: ["Luk", "Close"],
        show: ["Vis", "Show"], create: ["Opret", "Create"], createAccount: ["Opret Account", "Create account"], openAccount: ["Åbn Account", "Open account"],
        exists: ["Findes", "Exists"], adProtected: ["Reklamebeskyttet", "No advertising"],
        searching: ["Søger i NNE …", "Searching NNE …"], creating: ["Opretter …", "Creating …"],
        hits: ["{0} af {1} virksomheder", "{0} of {1} companies"], none: ["Ingen virksomheder fundet.", "No companies found."],
        needCriteria: ["Udfyld mindst ét søgefelt.", "Fill in at least one search field."],
        created: ["{0} er oprettet.", "{0} has been created."], failed: ["Fejl: ", "Error: "],
        dCvr: ["CVR", "CVR"], dPno: ["P-nummer", "P-number"], dTdc: ["Unikt ID (TDC)", "Unique ID (TDC)"], dForm: ["Selskabsform", "Company form"],
        dLegal: ["Juridisk enhed", "Legal entity"], dEmail: ["E-mail", "E-mail"], dHome: ["Hjemmeside", "Homepage"],
        dEmp: ["Ansatte", "Employees"], dFounded: ["Stiftet", "Founded"], dTdf: ["Navn i fortegnelse", "Directory name"],
        dStatus: ["Status", "Status"], dAd: ["Reklamebeskyttet", "Advertising protection"], yes: ["Ja", "Yes"], no: ["Nej", "No"]
    };
    function t(k) { var v = T[k]; return v ? v[DA ? 0 : 1] : k; }
    function fmt(s) { var a = arguments; return s.replace(/\{(\d)\}/g, function (m, i) { return a[+i + 1]; }); }
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }

    [].forEach.call(document.querySelectorAll("[data-t]"), function (el) { el.textContent = t(el.getAttribute("data-t")); });
    document.documentElement.lang = DA ? "da" : "en";

    var base = "";
    try { base = Xrm.Utility.getGlobalContext().getClientUrl(); } catch (e) { base = window.location.origin; }
    var rows = [], current = null;

    function api(name, body) {
        return fetch(base + "/api/data/v9.2/" + name, {
            method: "POST", credentials: "same-origin",
            headers: { "Accept": "application/json", "Content-Type": "application/json; charset=utf-8", "OData-MaxVersion": "4.0", "OData-Version": "4.0" },
            body: JSON.stringify(body)
        }).then(function (res) {
            return res.json().then(function (j) {
                if (!res.ok) throw new Error((j && j.error && j.error.message) || res.statusText);
                return j;
            });
        });
    }

    function status(text, isError) { var s = $("status"); s.textContent = text || ""; s.className = "status" + (isError ? " error" : ""); }

    function query() {
        var f = $("f").elements, q = {};
        ["Name", "Cvr", "TdcId", "Street", "HouseNo", "Zip", "City", "Phone"].forEach(function (n) { var v = f[n].value.trim(); if (v) q[n] = v; });
        ["NameStartsWith", "StreetStartsWith", "ActiveOnly", "LegalEntitiesOnly"].forEach(function (n) { q[n] = f[n].checked; });
        var m = parseInt(f.MaxHits.value, 10); q.MaxHits = isNaN(m) ? 50 : Math.max(1, Math.min(500, m));
        return q;
    }

    $("f").addEventListener("submit", function (e) {
        e.preventDefault();
        var q = query();
        if (!["Name", "Cvr", "TdcId", "Street", "Zip", "City", "Phone"].some(function (n) { return q[n]; })) { status(t("needCriteria"), true); return; }
        $("go").disabled = true; status(t("searching"));
        api("mth_NneSearch", q).then(function (r) {
            var res = JSON.parse(r.ResultJson);
            rows = res.companies || [];
            render();
            status(rows.length ? fmt(t("hits"), rows.length, res.total) : t("none"));
        }).catch(function (err) { status(t("failed") + err.message, true); })
          .then(function () { $("go").disabled = false; });
    });
    $("clear").addEventListener("click", function () {
        $("f").reset(); rows = []; render(); status(""); $("f").elements.Name.focus();
    });

    function render() {
        $("results").hidden = rows.length === 0;
        $("rows").innerHTML = rows.map(function (c, i) {
            var inactive = c.statusValue != null && c.statusValue !== 0;
            return "<tr>" +
                "<td><button class='act" + (c.accountId ? "" : " primary") + "' data-i='" + i + "' data-a='" + (c.accountId ? "show" : "create") + "'>" + t(c.accountId ? "show" : "create") + "</button></td>" +
                "<td><button class='link' data-i='" + i + "' data-a='details'>" + esc(c.officialName) + "</button>" +
                    (c.accountId ? "<span class='pill exists'>" + t("exists") + "</span>" : "") +
                    (c.adProtection ? "<span class='pill'>" + t("adProtected") + "</span>" : "") + "</td>" +
                "<td>" + esc(c.street) + "</td><td>" + esc(c.zipCode) + "</td><td>" + esc(c.district) + "</td>" +
                "<td>" + esc(c.phone) + "</td><td>" + esc(c.cvrNo) + "</td>" +
                "<td>" + (inactive ? "<span class='pill inactive'>" + esc(c.statusText) + "</span>" : esc(c.statusText)) + "</td></tr>";
        }).join("");
    }

    $("rows").addEventListener("click", function (e) {
        var b = e.target.closest("button[data-a]"); if (!b) return;
        var c = rows[+b.getAttribute("data-i")], a = b.getAttribute("data-a");
        if (a === "details") showDetails(c);
        else if (a === "show") openAccount(c.accountId);
        else importCompany(c, b);
    });

    function showDetails(c) {
        current = c;
        $("dName").textContent = c.officialName;
        var yn = function (v) { return v == null ? "" : t(v ? "yes" : "no"); };
        var items = [["dCvr", c.cvrNo], ["dPno", c.pNo], ["dTdc", c.tdcId], ["dForm", c.companyForm], ["dLegal", yn(c.legalEntity)],
            ["dEmail", c.email], ["dHome", c.homePage], ["dEmp", c.numberOfEmployees], ["dFounded", c.foundedYear], ["dTdf", c.tdfName],
            ["dStatus", c.statusText], ["dAd", yn(c.adProtection)]];
        $("dList").innerHTML = items.map(function (it) {
            var v = it[1] == null ? "" : String(it[1]);
            if (it[0] === "dHome" && v) v = "<a href='" + esc(/^https?:/i.test(v) ? v : "https://" + v) + "' target='_blank' rel='noopener'>" + esc(v) + "</a>";
            else if (it[0] === "dEmail" && v) v = "<a href='mailto:" + esc(v) + "'>" + esc(v) + "</a>";
            else v = esc(v);
            return "<dt>" + t(it[0]) + "</dt><dd>" + v + "</dd>";
        }).join("");
        $("dImport").textContent = t(c.accountId ? "openAccount" : "createAccount");
        $("details").showModal();
    }
    $("dClose").addEventListener("click", function () { $("details").close(); });
    $("dImport").addEventListener("click", function () {
        $("details").close();
        if (current.accountId) openAccount(current.accountId); else importCompany(current, null);
    });

    function importCompany(c, button) {
        if (button) button.disabled = true;
        status(t("creating"));
        api("mth_NneImportAccount", { TdcId: c.tdcId }).then(function (r) {
            rows.forEach(function (x) { if (x.tdcId === c.tdcId) x.accountId = r.AccountId; });
            render();
            status(r.Created ? fmt(t("created"), c.officialName) : r.Message);
            openAccount(r.AccountId);
        }).catch(function (err) { status(t("failed") + err.message, true); if (button) button.disabled = false; });
    }

    function openAccount(id) {
        // New tab like in Salesforce, so the search stays open
        try { Xrm.Navigation.openForm({ entityName: "account", entityId: id, openInNewWindow: true }); }
        catch (e) { window.open(base + "/main.aspx?pagetype=entityrecord&etn=account&id=" + id, "_blank"); }
    }
})();
