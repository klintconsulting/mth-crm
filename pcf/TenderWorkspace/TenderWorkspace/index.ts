import { IInputs, IOutputs } from "./generated/ManifestTypes";
import * as React from "react";
import { FluentProvider, Text, webLightTheme } from "@fluentui/react-components";
import { App } from "./components/App";
import { TenderApi } from "./api";
import { texts } from "./strings";

/**
 * TenderWorkspace (ESP): building contracts, collaborating partners (subcontractors), selected contacts,
 * evaluation, partner search, copy to another opportunity and tender e-mails - on the opportunity form.
 * Virtual React control using the platform's React 16.14 and Fluent UI 9. Bound to the record's primary name
 * column only to be placeable on the form; the value is never changed.
 */
export class TenderWorkspace implements ComponentFramework.ReactControl<IInputs, IOutputs> {
    private api: TenderApi | null = null;

    public init(context: ComponentFramework.Context<IInputs>): void {
        const xrm = (window as unknown as { Xrm?: { Utility?: { getGlobalContext?: () => { getClientUrl: () => string } } } }).Xrm;
        const clientUrl = xrm?.Utility?.getGlobalContext?.().getClientUrl() ?? window.location.origin;
        this.api = new TenderApi(clientUrl);
    }

    public updateView(context: ComponentFramework.Context<IInputs>): React.ReactElement {
        const tx = texts(context.userSettings.languageId);
        const info = (context.mode as unknown as { contextInfo?: { entityId?: string } }).contextInfo;
        const opportunityId = (info?.entityId ?? "").replace(/[{}]/g, "").toLowerCase();
        const theme = (context as unknown as { fluentDesignLanguage?: { tokenTheme?: typeof webLightTheme } }).fluentDesignLanguage?.tokenTheme ?? webLightTheme;
        const content = opportunityId && this.api
            ? React.createElement(App, { api: this.api, opportunityId, userName: context.userSettings.userName, tx })
            : React.createElement(Text, null, tx.t("saveFirst"));
        return React.createElement(FluentProvider, { theme, style: { background: "transparent", width: "100%" } }, content);
    }

    public getOutputs(): IOutputs {
        return {};
    }

    public destroy(): void {
        this.api = null;
    }
}
