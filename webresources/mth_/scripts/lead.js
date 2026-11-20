/*
 * Command scripts for the Lead form (mth_lead). Called by modern commands (appaction) defined in scripts/app/app.json.
 *  - Mth.Lead.convert():    calls Custom API mth_ConvertLead. The first user to convert gets the lead; others get a message.
 *  - Mth.Lead.disqualify(): marks the lead "Disqualified" and closes it (it disappears from all queues).
 * ES5 only (runs in the model-driven app without transpiling). Texts in Danish and English (user language).
 */
var Mth = window.Mth || {};
Mth.Lead = (function () {
    "use strict";
    var OPEN = 273280000, DISQUALIFIED = 273280002;

    function da() {
        try { return Xrm.Utility.getGlobalContext().userSettings.languageId === 1030; } catch (e) { return false; }
    }
    function t(daText, enText) { return da() ? daText : enText; }
    function page() { return Xrm.Page; } // the command passes no parameters; the form is the current page
    function leadId() { return page().data.entity.getId().replace(/[{}]/g, ""); }
    function status() { var a = page().getAttribute("mth_leadstatus"); return a ? a.getValue() : OPEN; }
    function alert(text) { return Xrm.Navigation.openAlertDialog({ text: text }); }

    function convert() {
        if (status() !== OPEN) { alert(t("Leadet er ikke åbent og kan ikke konverteres.", "The lead is not open and cannot be converted.")); return; }
        var name = (page().getAttribute("mth_name") || { getValue: function () { return ""; } }).getValue();
        Xrm.Navigation.openConfirmDialog({
            title: t("Konvertér lead", "Convert lead"),
            text: t("Konvertér \"" + name + "\" til en salgsmulighed? Leadet lukkes for alle andre køer.",
                    "Convert \"" + name + "\" to an opportunity? The lead is closed for all other queues.")
        }).then(function (r) {
            if (!r.confirmed) { return; }
            Xrm.Utility.showProgressIndicator(t("Konverterer…", "Converting…"));
            var url = Xrm.Utility.getGlobalContext().getClientUrl() + "/api/data/v9.2/mth_ConvertLead";
            fetch(url, {
                method: "POST", credentials: "same-origin",
                headers: { "Accept": "application/json", "Content-Type": "application/json; charset=utf-8", "OData-MaxVersion": "4.0", "OData-Version": "4.0" },
                body: JSON.stringify({ LeadId: leadId() })
            }).then(function (res) {
                return res.json().then(function (body) { return { ok: res.ok, body: body }; });
            }).then(function (r2) {
                Xrm.Utility.closeProgressIndicator();
                if (!r2.ok) {
                    alert((r2.body && r2.body.error && r2.body.error.message) || t("Konverteringen fejlede.", "Conversion failed."))
                        .then(function () { page().data.refresh(false); });
                    return;
                }
                Xrm.Navigation.openForm({ entityName: "mth_opportunity", entityId: r2.body.OpportunityId });
            }).catch(function (e) {
                Xrm.Utility.closeProgressIndicator();
                alert(t("Konverteringen fejlede: ", "Conversion failed: ") + e.message);
            });
        });
    }

    function disqualify() {
        if (status() !== OPEN) { alert(t("Leadet er ikke åbent.", "The lead is not open.")); return; }
        Xrm.Navigation.openConfirmDialog({
            title: t("Fravælg lead", "Disqualify lead"),
            text: t("Fravælg leadet? Det lukkes og forsvinder fra alle køer.", "Disqualify the lead? It is closed and removed from all queues.")
        }).then(function (r) {
            if (!r.confirmed) { return; }
            Xrm.WebApi.updateRecord("mth_lead", leadId(), { mth_leadstatus: DISQUALIFIED, statecode: 1, statuscode: 2 }).then(
                function () { page().data.refresh(false); },
                function (e) { alert(e.message); });
        });
    }

    return { convert: convert, disqualify: disqualify };
})();
window.Mth = Mth;
