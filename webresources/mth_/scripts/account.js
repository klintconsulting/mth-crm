/*
 * Command scripts for Account (modern commands defined in scripts/app/app.json).
 *  - Mth.Account.nneSearch(): opens the NNE company search (mth_/nne/search.html) as a large dialog and refreshes the list afterwards.
 */
var Mth = window.Mth || {};
Mth.Account = (function () {
    "use strict";
    function nneSearch() {
        var da = false;
        try { da = Xrm.Utility.getGlobalContext().userSettings.languageId === 1030; } catch (e) { /* ignore */ }
        Xrm.Navigation.navigateTo(
            { pageType: "webresource", webresourceName: "mth_/nne/search.html" },
            { target: 2, position: 1, width: { value: 90, unit: "%" }, height: { value: 90, unit: "%" }, title: da ? "Find virksomhed i NNE" : "Find company in NNE" }
        ).then(function () {
            try { Xrm.Page.ui.refresh(); } catch (e) { /* not on a grid page */ }
        });
    }
    return { nneSearch: nneSearch };
})();
window.Mth = Mth;
