/*
 * Command scripts for the Lead form (mth_lead). Called by modern commands (appaction) defined in scripts/app/app.json.
 *  - Mth.Lead.convert():    calls Custom API mth_ConvertLead. The first user to convert gets the lead; others get a message.
 *  - Mth.Lead.reject():     "Remove from All My Queues": Custom API mth_RejectLead, rejected in all the caller's own queues.
 *  - Mth.Lead.rejectAssignment(): subgrid "Reject" on lead assignments: mth_RejectLeadAssignment, one specific queue.
 *    Either way the lead stays in the other queues.
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
            // Xrm.WebApi handles the session/token itself (a raw fetch got an empty 401 body when the session had expired).
            var request = {
                LeadId: { guid: leadId() },
                getMetadata: function () {
                    return { boundParameter: null, operationType: 0, operationName: "mth_ConvertLead",
                        parameterTypes: { LeadId: { typeName: "Edm.Guid", structuralProperty: 1 } } };
                }
            };
            Xrm.WebApi.online.execute(request).then(function (res) {
                return res.text().then(function (txt) { return txt ? JSON.parse(txt) : {}; });
            }).then(function (body) {
                Xrm.Utility.closeProgressIndicator();
                if (!body.OpportunityId) {
                    alert(t("Konverteringen gav intet svar. Opdatér siden og prøv igen.", "The conversion returned no result. Refresh the page and try again."));
                    return;
                }
                Xrm.Navigation.openForm({ entityName: "mth_opportunity", entityId: body.OpportunityId });
            }, function (e) {
                Xrm.Utility.closeProgressIndicator();
                alert((e && e.message) || t("Konverteringen fejlede.", "Conversion failed."))
                    .then(function () { page().data.refresh(false); });
            });
        });
    }

    // Reject in the caller's own queues only (Custom API mth_RejectLead); the lead stays in the other queues.
    function reject() {
        if (status() !== OPEN) { alert(t("Leadet er ikke åbent.", "The lead is not open.")); return; }
        Xrm.Navigation.openConfirmDialog({
            title: t("Fjern fra alle mine køer", "Remove from all my queues"),
            text: t("Afvis leadet i de køer, du selv er medlem af? Det bliver i de andre køer, så en anden afdeling stadig kan tage det.",
                    "Reject the lead in the queues you are a member of? It stays in the other queues, so another department can still take it.")
        }).then(function (r) {
            if (!r.confirmed) { return; }
            Xrm.Utility.showProgressIndicator(t("Afviser…", "Rejecting…"));
            var request = {
                LeadId: { guid: leadId() },
                getMetadata: function () {
                    return { boundParameter: null, operationType: 0, operationName: "mth_RejectLead",
                        parameterTypes: { LeadId: { typeName: "Edm.Guid", structuralProperty: 1 } } };
                }
            };
            Xrm.WebApi.online.execute(request).then(function (res) {
                return res.text().then(function (txt) { return txt ? JSON.parse(txt) : {}; });
            }).then(function (body) {
                Xrm.Utility.closeProgressIndicator();
                alert(body.Message || t("Leadet er afvist i dine køer.", "The lead is rejected in your queues."))
                    .then(function () { page().data.refresh(false); });
            }, function (e) {
                Xrm.Utility.closeProgressIndicator();
                alert((e && e.message) || t("Afvisningen fejlede.", "The rejection failed."));
            });
        });
    }

    // Subgrid command on mth_leadassignment (lead form "LeadAssignments", queue form "QueueAssignments"):
    // rejects the lead in the selected queue(s) only (Custom API mth_RejectLeadAssignment, one call per row).
    function selectedAssignments() {
        var ids = [];
        // On the lead assignment's own form: that record
        try {
            if (page().data.entity.getEntityName() === "mth_leadassignment") { return [page().data.entity.getId().replace(/[{}]/g, "")]; }
        } catch (e) { /* not a form */ }
        ["LeadAssignments", "QueueAssignments"].forEach(function (name) {
            var c = page() && page().getControl ? page().getControl(name) : null;
            if (!c || !c.getGrid) { return; }
            c.getGrid().getSelectedRows().forEach(function (row) { ids.push(row.getData().getEntity().getId().replace(/[{}]/g, "")); });
        });
        return ids;
    }
    function rejectAssignment() {
        var ids = selectedAssignments();
        if (ids.length === 0) { alert(t("Vælg den kø, du vil afvise leadet i.", "Select the queue to reject the lead in.")); return; }
        Xrm.Navigation.openConfirmDialog({
            title: t("Afvis i den valgte kø", "Reject in the selected queue"),
            text: t("Afvis leadet i den valgte kø? Det bliver i de andre køer.", "Reject the lead in the selected queue? It stays in the other queues.")
        }).then(function (r) {
            if (!r.confirmed) { return; }
            Xrm.Utility.showProgressIndicator(t("Afviser…", "Rejecting…"));
            var messages = [];
            var next = function (i) {
                if (i >= ids.length) {
                    Xrm.Utility.closeProgressIndicator();
                    alert(messages.join("\n")).then(function () { page().data.refresh(false); });
                    return;
                }
                var request = {
                    AssignmentId: { guid: ids[i] },
                    getMetadata: function () {
                        return { boundParameter: null, operationType: 0, operationName: "mth_RejectLeadAssignment",
                            parameterTypes: { AssignmentId: { typeName: "Edm.Guid", structuralProperty: 1 } } };
                    }
                };
                Xrm.WebApi.online.execute(request).then(function (res) {
                    return res.text().then(function (txt) { return txt ? JSON.parse(txt) : {}; });
                }).then(function (body) { messages.push(body.Message || ""); next(i + 1); },
                    function (e) { messages.push((e && e.message) || t("Afvisningen fejlede.", "The rejection failed.")); next(i + 1); });
            };
            next(0);
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

    return { convert: convert, reject: reject, rejectAssignment: rejectAssignment, disqualify: disqualify };
})();
window.Mth = Mth;
