// Pure helpers for @mention handling (no PCF/DOM dependencies, unit tested in mention.test.ts).

export interface MentionToken {
    /** Index of the '@' in the text. */
    start: number;
    /** Text typed after '@' (may contain one space, e.g. "Søren K"). */
    query: string;
}

export interface MentionUser {
    id: string;
    fullName: string;
    email: string;
}

// '@' at start of text or after whitespace/'(' followed by up to two words (no '@').
const TOKEN = /(?:^|[\s(])@([\p{L}\p{N}._-]{0,40}(?: [\p{L}\p{N}._-]{0,40})?)$/u;

/** Finds the @mention being typed immediately before the caret, or null. */
export function findActiveToken(text: string, caret: number): MentionToken | null {
    const before = text.substring(0, caret);
    const m = TOKEN.exec(before);
    if (!m) return null;
    const query = m[1];
    return { start: caret - query.length - 1, query };
}

/** Replaces the active token with "@Full Name " and returns the new text and caret position. */
export function insertMention(text: string, token: MentionToken, caret: number, user: MentionUser): { text: string; caret: number } {
    const inserted = "@" + user.fullName + " ";
    const newText = text.substring(0, token.start) + inserted + text.substring(caret);
    return { text: newText, caret: token.start + inserted.length };
}

/** Users (from the picked list) whose "@Full Name" still occurs in the text. */
export function mentionedUsers(text: string, picked: MentionUser[]): MentionUser[] {
    const result: MentionUser[] = [];
    for (const u of picked) {
        if (result.some(r => r.id === u.id)) continue;
        const pattern = new RegExp("(?<![\\p{L}\\p{N}._-])@" + escapeRegex(u.fullName) + "(?![\\p{L}\\p{N}])", "iu");
        if (pattern.test(text)) result.push(u);
    }
    return result;
}

/** Escapes text for use in an OData string literal. */
export function odataString(value: string): string {
    return value.replace(/'/g, "''");
}

/** Converts plain text to safe HTML for a rich-text note (escaping + line breaks). */
export function toNoteHtml(text: string): string {
    const escaped = text
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
    return escaped.split(/\r?\n/).map(l => (l.length ? l : "&nbsp;")).map(l => "<div>" + l + "</div>").join("");
}

/** Builds the OData query for the user picker. */
export function userQuery(query: string, top: number): string {
    const q = odataString(query.trim());
    let filter = "isdisabled eq false and accessmode eq 0";
    if (q.length > 0) {
        filter += " and (contains(fullname,'" + q + "') or startswith(internalemailaddress,'" + q + "'))";
    }
    return "?$select=fullname,internalemailaddress&$filter=" + filter + "&$orderby=fullname&$top=" + top;
}

function escapeRegex(s: string): string {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
