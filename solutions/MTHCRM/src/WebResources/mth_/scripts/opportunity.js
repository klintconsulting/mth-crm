/*
 * Form script for MTH Opportunity (mth_opportunity).
 * Shows the "Project Site" tab (SharePoint Shared Documents of the opportunity's own project site)
 * when the status code is >= MIN_STATUS. Status option value = 273280000 + status code (decision B4).
 * Registered in scripts/app/app.json (form "MTH Opportunity": onLoad + onChange of mth_status).
 * ES5 only (runs in the model-driven app without transpiling).
 */
var Mth = window.Mth || {};
Mth.Opportunity = (function () {
    "use strict";
    var STATUS_BASE = 273280000;
    var MIN_STATUS = 20;          // show from "20 PQ - upcoming"
    var PROJECT_TAB = "projectsite";

    function statusCode(formContext) {
        var attr = formContext.getAttribute("mth_status");
        var value = attr ? attr.getValue() : null;
        return value === null || value === undefined ? null : value - STATUS_BASE;
    }

    function updateProjectSiteTab(formContext) {
        var tab = formContext.ui.tabs.get(PROJECT_TAB);
        if (!tab) { return; }
        var code = statusCode(formContext);
        var isSaved = formContext.ui.getFormType() !== 1; // 1 = create
        tab.setVisible(isSaved && code !== null && code >= MIN_STATUS);
    }

    return {
        onLoad: function (executionContext) {
            updateProjectSiteTab(executionContext.getFormContext());
        },
        onStatusChange: function (executionContext) {
            updateProjectSiteTab(executionContext.getFormContext());
        }
    };
})();
window.Mth = Mth;
