// Data access for the tender workspace via the Dataverse Web API (same-origin session of the signed-in user).
// fetch is used instead of context.webAPI because N:N associate/disassociate, FetchXML with intersect links
// and the custom API mth_SendPartnerEmail are needed.
import { odataString, OPTION_BASE, STATUS_SELECTED, xmlAttr, Region, contractName } from "./logic";

export interface Trade { id: string; name: string; code: string }
export interface Contract { id: string; name: string; alternativeName: string; tradeId: string; tradeName: string; deadline: string | null }
export interface ContactInfo { id: string; fullName: string; firstName: string; email: string; jobTitle: string }
export interface Partner {
    id: string; name: string; accountId: string; contractId: string;
    status: number | null; evaluation: number | null; evaluationComment: string; comments: string; lastEmailSent: string | null;
    contacts: ContactInfo[];
}
export interface SearchResult { accountId: string; name: string; city: string; postalCode: string; evaluations: (number | null)[] }
export interface Template { id: string; name: string; subject: string; body: string; isDefault: boolean }
export interface OpportunityInfo { id: string; name: string; caseNumber: string }
export interface SendResult { result: "sent" | "skipped" | "test"; emailId: string; message: string }
export interface EmailAttachmentInput { fileName: string; mimeType: string; base64: string }

const FV = "@OData.Community.Display.V1.FormattedValue";
type Row = Record<string, unknown>;
const s = (v: unknown): string => (typeof v === "string" ? v : typeof v === "number" || typeof v === "boolean" ? String(v) : "");
const orNull = (v: string): string | null => (v.trim() === "" ? null : v.trim());
const n = (v: unknown): number | null => (v === null || v === undefined ? null : Number(v));

export class TenderApi {
    private readonly root: string;

    constructor(clientUrl: string) {
        this.root = clientUrl.replace(/\/$/, "") + "/api/data/v9.2/";
    }

    private async request<T>(method: string, path: string, body?: unknown, extraHeaders?: Record<string, string>): Promise<T> {
        const res = await fetch(this.root + path, {
            method,
            credentials: "same-origin",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json; charset=utf-8",
                "OData-MaxVersion": "4.0",
                "OData-Version": "4.0",
                Prefer: 'odata.include-annotations="OData.Community.Display.V1.FormattedValue"',
                ...(extraHeaders ?? {})
            },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        if (!res.ok) {
            let msg = res.statusText;
            try { const e = await res.json() as { error?: { message?: string } }; msg = e.error?.message ?? msg; } catch { /* keep statusText */ }
            throw new Error(msg);
        }
        if (res.status === 204) {
            const id = /\(([0-9a-f-]{36})\)/i.exec(res.headers.get("OData-EntityId") ?? "");
            return (id ? id[1] : null) as unknown as T;
        }
        return await res.json() as T;
    }

    private async list(path: string): Promise<Row[]> {
        return (await this.request<{ value: Row[] }>("GET", path)).value;
    }

    // ---------- reads ----------
    async opportunity(id: string): Promise<OpportunityInfo> {
        const r = await this.request<Row>("GET", `mth_opportunities(${id})?$select=mth_name,mth_bcprojectnumber`);
        return { id, name: s(r.mth_name), caseNumber: s(r.mth_bcprojectnumber) };
    }

    async trades(): Promise<Trade[]> {
        const rows = await this.list("mth_trades?$select=mth_name,mth_code&$filter=statecode eq 0&$orderby=mth_name");
        return rows.map(r => ({ id: s(r.mth_tradeid), name: s(r.mth_name), code: s(r.mth_code) }));
    }

    async contracts(opportunityId: string): Promise<Contract[]> {
        const rows = await this.list(`mth_buildingcontracts?$select=mth_name,mth_alternativename,mth_tenderdeadline,_mth_tradeid_value&$filter=_mth_opportunityid_value eq ${opportunityId} and statecode eq 0&$orderby=mth_name`);
        return rows.map(r => ({
            id: s(r.mth_buildingcontractid), name: s(r.mth_name), alternativeName: s(r.mth_alternativename),
            tradeId: s(r._mth_tradeid_value), tradeName: s(r[`_mth_tradeid_value${FV}`]), deadline: (r.mth_tenderdeadline as string) ?? null
        }));
    }

    async partners(contractIds: string[]): Promise<Partner[]> {
        if (contractIds.length === 0) return [];
        const ids = contractIds.map(id => `'${id}'`).join(",");
        const rows = await this.list(
            "mth_collaboratingpartners?$select=mth_name,mth_status,mth_tenderevaluation,mth_tenderevaluationcomment,mth_comments,mth_lastemailsent,_mth_accountid_value,_mth_buildingcontractid_value" +
            `&$filter=Microsoft.Dynamics.CRM.In(PropertyName='mth_buildingcontractid',PropertyValues=[${ids}]) and statecode eq 0` +
            "&$orderby=mth_name&$expand=mth_collabpartner_contact($select=fullname,firstname,emailaddress1,jobtitle)");
        return rows.map(r => ({
            id: s(r.mth_collaboratingpartnerid), name: s(r.mth_name), accountId: s(r._mth_accountid_value), contractId: s(r._mth_buildingcontractid_value),
            status: n(r.mth_status), evaluation: n(r.mth_tenderevaluation), evaluationComment: s(r.mth_tenderevaluationcomment),
            comments: s(r.mth_comments), lastEmailSent: (r.mth_lastemailsent as string) ?? null,
            contacts: ((r.mth_collabpartner_contact as Row[]) ?? []).map(toContact)
        }));
    }

    async accountContacts(accountId: string): Promise<ContactInfo[]> {
        const rows = await this.list(`contacts?$select=fullname,firstname,emailaddress1,jobtitle&$filter=_parentcustomerid_value eq ${accountId} and statecode eq 0&$orderby=fullname`);
        return rows.map(toContact);
    }

    async templates(): Promise<Template[]> {
        const rows = await this.list("mth_espemailtemplates?$select=mth_name,mth_subject,mth_body,mth_isdefault&$filter=statecode eq 0&$orderby=mth_name");
        return rows.map(r => ({ id: s(r.mth_espemailtemplateid), name: s(r.mth_name), subject: s(r.mth_subject), body: s(r.mth_body), isDefault: r.mth_isdefault === true }));
    }

    /** Accounts that deliver a trade (or any trade), optionally filtered by name and region. Max `top` rows. */
    async searchPartners(tradeId: string | null, name: string, region: Region | undefined, top: number): Promise<SearchResult[]> {
        const conds: string[] = ['<condition attribute="statecode" operator="eq" value="0" />'];
        if (name.trim()) conds.push(`<condition attribute="name" operator="like" value="%${xmlAttr(name.trim())}%" />`);
        if (region) {
            conds.push(`<condition attribute="address1_postalcode" operator="ge" value="${region.from}" />`);
            conds.push(`<condition attribute="address1_postalcode" operator="le" value="${region.to}" />`);
        }
        const tradeFilter = tradeId ? `<filter><condition attribute="mth_tradeid" operator="eq" value="${xmlAttr(tradeId)}" /></filter>` : "";
        const fetchXml =
            `<fetch top="${top}" distinct="true"><entity name="account">` +
            '<attribute name="accountid" /><attribute name="name" /><attribute name="address1_city" /><attribute name="address1_postalcode" />' +
            `<order attribute="name" /><filter>${conds.join("")}</filter>` +
            `<link-entity name="mth_account_mth_trade" from="accountid" to="accountid" intersect="true">${tradeFilter}</link-entity>` +
            "</entity></fetch>";
        const rows = await this.list("accounts?fetchXml=" + encodeURIComponent(fetchXml));
        const results: SearchResult[] = rows.map(r => ({ accountId: s(r.accountid), name: s(r.name), city: s(r.address1_city), postalCode: s(r.address1_postalcode), evaluations: [] }));
        // previous evaluations of these accounts on any building contract
        if (results.length > 0) {
            const ids = results.map(x => `'${x.accountId}'`).join(",");
            const evals = await this.list(`mth_collaboratingpartners?$select=_mth_accountid_value,mth_tenderevaluation&$filter=Microsoft.Dynamics.CRM.In(PropertyName='mth_accountid',PropertyValues=[${ids}]) and mth_tenderevaluation ne null`);
            for (const e of evals) {
                const hit = results.find(x => x.accountId === s(e._mth_accountid_value));
                if (hit) hit.evaluations.push(n(e.mth_tenderevaluation));
            }
        }
        return results;
    }

    async searchOpportunities(text: string, excludeId: string): Promise<OpportunityInfo[]> {
        const q = odataString(text.trim());
        if (!q) return [];
        const rows = await this.list(`mth_opportunities?$select=mth_name,mth_bcprojectnumber&$filter=(contains(mth_name,'${q}') or contains(mth_bcprojectnumber,'${q}')) and mth_opportunityid ne ${excludeId}&$orderby=mth_name&$top=15`);
        return rows.map(r => ({ id: s(r.mth_opportunityid), name: s(r.mth_name), caseNumber: s(r.mth_bcprojectnumber) }));
    }

    // ---------- writes ----------
    async createContract(opportunityId: string, trade: Trade): Promise<string> {
        return this.request<string>("POST", "mth_buildingcontracts", {
            mth_name: trade.name, "mth_OpportunityId@odata.bind": `/mth_opportunities(${opportunityId})`, "mth_TradeId@odata.bind": `/mth_trades(${trade.id})`
        });
    }

    async updateContract(c: Contract, alternativeName: string, deadline: string | null): Promise<void> {
        await this.request("PATCH", `mth_buildingcontracts(${c.id})`, {
            mth_alternativename: orNull(alternativeName), mth_tenderdeadline: deadline ?? null, mth_name: contractName(alternativeName, c.tradeName)
        });
    }

    async deleteContract(id: string): Promise<void> {
        await this.request("DELETE", `mth_buildingcontracts(${id})`);
    }

    /** Creates a partner with status 10 and, if the account has exactly one contact with e-mail, selects that contact. */
    async addPartner(contractId: string, accountId: string, accountName: string): Promise<string> {
        const id = await this.request<string>("POST", "mth_collaboratingpartners", {
            mth_name: accountName, mth_status: STATUS_SELECTED, mth_partnertype: OPTION_BASE,
            "mth_BuildingContractId@odata.bind": `/mth_buildingcontracts(${contractId})`, "mth_AccountId@odata.bind": `/accounts(${accountId})`
        });
        const withEmail = (await this.accountContacts(accountId)).filter(c => c.email);
        if (withEmail.length === 1) await this.selectContact(id, withEmail[0].id);
        return id;
    }

    async updatePartner(id: string, fields: Record<string, unknown>): Promise<void> {
        await this.request("PATCH", `mth_collaboratingpartners(${id})`, fields);
    }

    async removePartner(id: string): Promise<void> {
        await this.request("DELETE", `mth_collaboratingpartners(${id})`);
    }

    async selectContact(partnerId: string, contactId: string): Promise<void> {
        try {
            await this.request("POST", `mth_collaboratingpartners(${partnerId})/mth_collabpartner_contact/$ref`, { "@odata.id": `${this.root}contacts(${contactId})` });
        } catch (e) {
            if (!/duplicate|already/i.test((e as Error).message)) throw e;
        }
    }

    async unselectContact(partnerId: string, contactId: string): Promise<void> {
        await this.request("DELETE", `mth_collaboratingpartners(${partnerId})/mth_collabpartner_contact(${contactId})/$ref`);
    }

    async createContact(accountId: string, c: { firstName: string; lastName: string; jobTitle: string; email: string; mobile: string }): Promise<string> {
        return this.request<string>("POST", "contacts", {
            firstname: c.firstName.trim(), lastname: c.lastName.trim(), jobtitle: orNull(c.jobTitle),
            emailaddress1: c.email.trim(), mobilephone: orNull(c.mobile), "parentcustomerid_account@odata.bind": `/accounts(${accountId})`
        });
    }

    async sendPartnerEmail(partnerId: string, subject: string, body: string, attachments: EmailAttachmentInput[], sendToSelf: boolean): Promise<SendResult> {
        const r = await this.request<Row>("POST", "mth_SendPartnerEmail", {
            PartnerId: partnerId, Subject: subject, Body: body,
            AttachmentsJson: attachments.length ? JSON.stringify(attachments) : null, SendToSelf: sendToSelf
        });
        return { result: s(r.Result) as SendResult["result"], emailId: s(r.EmailId), message: s(r.Message) };
    }

    /**
     * Copies a building contract with selected partners to another opportunity. Reuses a building contract with the same
     * trade on the target; partners already on it (same account) are skipped. New partners get status 10 and the same selected contacts.
     */
    async cloneContract(source: Contract, partners: Partner[], targetOpportunityId: string): Promise<{ created: number; existed: number }> {
        const targetContracts = await this.contracts(targetOpportunityId);
        let target = targetContracts.find(c => c.tradeId === source.tradeId);
        if (!target) {
            const id = await this.request<string>("POST", "mth_buildingcontracts", {
                mth_name: source.name, mth_alternativename: orNull(source.alternativeName), mth_tenderdeadline: source.deadline,
                "mth_OpportunityId@odata.bind": `/mth_opportunities(${targetOpportunityId})`, "mth_TradeId@odata.bind": `/mth_trades(${source.tradeId})`
            });
            target = { ...source, id };
        }
        const existing = await this.partners([target.id]);
        let created = 0; let existed = 0;
        for (const p of partners) {
            if (existing.some(x => x.accountId === p.accountId)) { existed++; continue; }
            const id = await this.request<string>("POST", "mth_collaboratingpartners", {
                mth_name: p.name, mth_status: STATUS_SELECTED, mth_partnertype: OPTION_BASE,
                "mth_BuildingContractId@odata.bind": `/mth_buildingcontracts(${target.id})`, "mth_AccountId@odata.bind": `/accounts(${p.accountId})`
            });
            for (const c of p.contacts) await this.selectContact(id, c.id);
            created++;
        }
        return { created, existed };
    }
}

function toContact(r: Row): ContactInfo {
    return { id: s(r.contactid), fullName: s(r.fullname), firstName: s(r.firstname), email: s(r.emailaddress1), jobTitle: s(r.jobtitle) };
}
