/*
 * "Follow" command on the Opportunity, Lead and Account forms (modern command, appaction, defined in scripts/app/app.json).
 *  - Mth.Follow.toggle(): follows the record if the user does not follow it yet, otherwise stops following it.
 *    A follow is a row in mth_follow owned by the user (key mth_key = "<record id>|<user id>").
 *    Followers get an in-app notification when a tracked column changes or a note is added (plugin FollowNotifier).
 *  - Mth.Follow.onLoad(executionContext): optional form OnLoad handler that shows "You follow this record" as a form notification.
 * ES5 only (runs in the model-driven app without transpiling). Texts in Danish and English (user language).
 */
var Mth = window.Mth || {};
Mth.Follow = (function () {
    "use strict";
    // Navigation property (lookup) on mth_follow per followed table
    var LOOKUP = { mth_opportunity: "mth_OpportunityId", mth_lead: "mth_LeadId", account: "mth_AccountId" };
    var SET = { mth_opportunity: "mth_opportunities", mth_lead: "mth_leads", account: "accounts" };
    var NOTE_ID = "mth_follow_state";

    function da() {
        try { return Xrm.Utility.getGlobalContext().userSettings.languageId === 1030; } catch (e) { return false; }
    }
    function t(daText, enText) { return da() ? daText : enText; }
    function clean(id) { return (id || "").replace(/[{}]/g, "").toLowerCase(); }
    function userId() { return clean(Xrm.Utility.getGlobalContext().userSettings.userId); }
    function alert(text) { return Xrm.Navigation.openAlertDialog({ text: text }); }

    function current(formContext) {
        var page = formContext || Xrm.Page;
        var table = page.data.entity.getEntityName();
        var id = clean(page.data.entity.getId());
        var name = page.data.entity.getPrimaryAttributeValue() || "";
        return { page: page, table: table, id: id, name: name, key: id + "|" + userId() };
    }

    function find(rec) {
        return Xrm.WebApi.retrieveMultipleRecords("mth_follow",
            "?$select=mth_followid&$filter=mth_key eq '" + rec.key + "'").then(function (r) { return r.entities[0] || null; });
    }

    function showState(rec, following) {
        try {
            if (following) {
                rec.page.ui.setFormNotification(t("Du følger denne post og får besked under klokken, når den ændres.",
                    "You follow this record and are notified (bell icon) when it changes."), "INFO", NOTE_ID);
            } else { rec.page.ui.clearFormNotification(NOTE_ID); }
        } catch (e) { /* not a form */ }
    }

    function toggle() {
        var rec = current();
        if (!rec.id) { alert(t("Gem posten, før du følger den.", "Save the record before following it.")); return; }
        if (!LOOKUP[rec.table]) { alert(t("Denne tabel kan ikke følges.", "This table cannot be followed.")); return; }
        Xrm.Utility.showProgressIndicator("");
        find(rec).then(function (existing) {
            if (existing) {
                return Xrm.WebApi.deleteRecord("mth_follow", existing.mth_followid).then(function () {
                    Xrm.Utility.closeProgressIndicator();
                    showState(rec, false);
                    alert(t("Du følger ikke længere \"" + rec.name + "\".", "You no longer follow \"" + rec.name + "\"."));
                });
            }
            var data = { mth_name: rec.name.substring(0, 400), mth_recordtable: rec.table, mth_recordid: rec.id, mth_key: rec.key };
            data[LOOKUP[rec.table] + "@odata.bind"] = "/" + SET[rec.table] + "(" + rec.id + ")";
            return Xrm.WebApi.createRecord("mth_follow", data).then(function () {
                Xrm.Utility.closeProgressIndicator();
                showState(rec, true);
                alert(t("Du følger nu \"" + rec.name + "\". Du får besked under klokken, når vigtige felter ændres, eller der skrives en note.",
                        "You now follow \"" + rec.name + "\". You are notified (bell icon) when key fields change or a note is added."));
            });
        }).then(null, function (e) {
            Xrm.Utility.closeProgressIndicator();
            alert((e && e.message) || t("Det lykkedes ikke.", "It did not work."));
        });
    }

    function onLoad(executionContext) {
        var rec = current(executionContext.getFormContext());
        if (!rec.id || !LOOKUP[rec.table]) { return; }
        find(rec).then(function (existing) { showState(rec, !!existing); }, function () { /* ignore */ });
    }

    return { toggle: toggle, onLoad: onLoad };
})();
window.Mth = Mth;
