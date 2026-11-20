// Pure logic for the tender workspace (no React/PCF/DOM). Unit tested in tests/logic.test.ts.

export const OPTION_BASE = 273280000;

/** Partner status codes (mth_collaboratingpartner.mth_status, option value = OPTION_BASE + code). */
export const STATUS_CODES = [10, 20, 22, 24, 30, 60, 90, 91, 92, 95, 97, 99];
export const STATUS_SELECTED = OPTION_BASE + 10;

/** Tender evaluation options (mth_tenderevaluation). */
export interface EvaluationOption { value: number; key: "green" | "yellow" | "red" | "black"; color: string }
export const EVALUATIONS: EvaluationOption[] = [
    { value: OPTION_BASE + 0, key: "green", color: "#107c10" },
    { value: OPTION_BASE + 1, key: "yellow", color: "#c19c00" },
    { value: OPTION_BASE + 2, key: "red", color: "#d13438" },
    { value: OPTION_BASE + 3, key: "black", color: "#323130" }
];

export function statusCode(value: number | null | undefined): number | null {
    return value === null || value === undefined ? null : value - OPTION_BASE;
}

/** Yellow, red and black require a comment. */
export function evaluationNeedsComment(value: number | null | undefined): boolean {
    return value !== null && value !== undefined && value !== OPTION_BASE + 0;
}

export function evaluationOption(value: number | null | undefined): EvaluationOption | undefined {
    return EVALUATIONS.find(e => e.value === value);
}

// ---------- regions (Danish postal codes, 4 digits) ----------
export interface Region { key: string; from: string; to: string }
export const REGIONS: Region[] = [
    { key: "capital", from: "1000", to: "3799" },     // Hovedstaden incl. Bornholm
    { key: "zealand", from: "4000", to: "4999" },     // Sjælland
    { key: "funen", from: "5000", to: "5999" },       // Fyn og øerne
    { key: "southJutland", from: "6000", to: "6999" },// Syd- og Sønderjylland
    { key: "midJutland", from: "7000", to: "8999" },  // Midtjylland
    { key: "northJutland", from: "9000", to: "9999" } // Nordjylland
];

export function inRegion(postalCode: string | null | undefined, region: Region | undefined): boolean {
    if (!region) return true;
    const z = (postalCode ?? "").trim();
    if (!/^\d{4}$/.test(z)) return false;
    return z >= region.from && z <= region.to;
}

// ---------- building contracts ----------
export function contractName(alternativeName: string | null | undefined, tradeName: string | null | undefined): string {
    const alt = (alternativeName ?? "").trim();
    return alt.length > 0 ? alt : (tradeName ?? "");
}

export type DeadlineState = "none" | "overdue" | "soon" | "ok";

/** Deadline is a date-only string (YYYY-MM-DD). "soon" = today or within the next 7 days. */
export function deadlineState(deadline: string | null | undefined, today: Date): DeadlineState {
    if (!deadline) return "none";
    const d = parseDateOnly(deadline);
    if (!d) return "none";
    const t = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const days = Math.round((d.getTime() - t.getTime()) / 86400000);
    if (days < 0) return "overdue";
    if (days <= 7) return "soon";
    return "ok";
}

export function parseDateOnly(s: string): Date | null {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
    return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null;
}

export interface StatusCount { code: number; count: number }

/** Count partners per status code, sorted by code; unknown/empty statuses are ignored. */
export function statusSummary(values: (number | null | undefined)[]): StatusCount[] {
    const map = new Map<number, number>();
    for (const v of values) {
        const c = statusCode(v);
        if (c === null) continue;
        map.set(c, (map.get(c) ?? 0) + 1);
    }
    return Array.from(map.entries()).map(([code, count]) => ({ code, count })).sort((a, b) => a.code - b.code);
}

export interface EvaluationSummary { green: number; yellow: number; red: number; black: number }

export function summarizeEvaluations(values: (number | null | undefined)[]): EvaluationSummary {
    const s: EvaluationSummary = { green: 0, yellow: 0, red: 0, black: 0 };
    for (const v of values) {
        const e = evaluationOption(v);
        if (e) s[e.key]++;
    }
    return s;
}

/** True if a partner has ever been evaluated red or black (used by the "hide red/black" search filter). */
export function hasBadEvaluation(s: EvaluationSummary | undefined): boolean {
    return !!s && (s.red > 0 || s.black > 0);
}

// ---------- e-mail template preview (mirrors plugins/Mth.Crm.Plugins/EmailTemplateRenderer.cs) ----------
export interface TemplateValues {
    opportunity?: string | null;
    caseNumber?: string | null;
    buildingContract?: string | null;
    tenderDeadline?: string | null; // YYYY-MM-DD
    partner?: string | null;
    contactFirstNames?: string[];
    sender?: string | null;
}

export const PLACEHOLDERS = ["Opportunity", "CaseNumber", "BuildingContract", "TenderDeadline", "Partner", "ContactFirstName", "Sender"];

const MONTHS_DA = ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];

export function formatDanishDate(iso: string | null | undefined): string {
    const d = iso ? parseDateOnly(iso) : null;
    return d ? `${d.getDate()}. ${MONTHS_DA[d.getMonth()]} ${d.getFullYear()}` : "(ingen frist)";
}

export function joinNames(names: string[] | undefined): string {
    const n: string[] = [];
    for (const raw of names ?? []) {
        const s = (raw ?? "").trim();
        if (s && !n.includes(s)) n.push(s);
    }
    if (n.length === 0) return "alle";
    if (n.length === 1) return n[0];
    return n.slice(0, -1).join(", ") + " og " + n[n.length - 1];
}

export function htmlEncode(s: string): string {
    return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
}

export function renderTemplate(template: string, v: TemplateValues, html: boolean): string {
    if (!template) return template ?? "";
    return template.replace(/\{\{\s*([A-Za-z]+)\s*\}\}/g, (whole: string, name: string) => {
        let value: string;
        switch (name.toLowerCase()) {
            case "opportunity": value = v.opportunity ?? ""; break;
            case "casenumber": value = v.caseNumber ?? ""; break;
            case "buildingcontract": value = v.buildingContract ?? ""; break;
            case "tenderdeadline": value = formatDanishDate(v.tenderDeadline); break;
            case "partner": value = v.partner ?? ""; break;
            case "contactfirstname": value = joinNames(v.contactFirstNames); break;
            case "sender": value = v.sender ?? ""; break;
            default: return whole; // unknown placeholder stays visible
        }
        if (value.trim() === "") value = "–";
        return html ? htmlEncode(value) : value;
    });
}

/** Very small sanitizer for previewing template HTML: drops script/style/iframe and on* / javascript: attributes. */
export function sanitizeHtml(html: string): string {
    return (html ?? "")
        .replace(/<\s*(script|style|iframe|object|embed)[\s\S]*?<\s*\/\s*\1\s*>/gi, "")
        .replace(/<\s*(script|style|iframe|object|embed)[^>]*>/gi, "")
        .replace(/\son\w+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, "")
        .replace(/(href|src)\s*=\s*("|')\s*javascript:[^"']*\2/gi, "$1=\"#\"");
}

/** Escapes a value for an OData string literal. */
export function odataString(value: string): string {
    return value.replace(/'/g, "''");
}

/** Escapes a value for FetchXML attribute values. */
export function xmlAttr(value: string): string {
    return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;");
}

/** Pseudo id for the 'All building contracts' entry in the contract list. */
export const ALL_CONTRACTS = "__all__";
