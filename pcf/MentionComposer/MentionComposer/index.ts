import { IInputs, IOutputs } from "./generated/ManifestTypes";
import { findActiveToken, insertMention, mentionedUsers, MentionToken, MentionUser, toNoteHtml, userQuery } from "./mention";

/**
 * MentionComposer: a comment box with an @mention picker, placed above the timeline.
 * Several users can be mentioned anywhere in the text: every '@' opens the picker at that position.
 * On send it creates a note (annotation) on the current record. The note text contains "@Full Name"
 * for each mentioned user; the NoteMentionNotifier plugin sends the in-app notifications.
 * The control is bound to the record's primary name column but never changes it.
 */
export class MentionComposer implements ComponentFramework.StandardControl<IInputs, IOutputs> {
    private context!: ComponentFramework.Context<IInputs>;
    private root!: HTMLDivElement;
    private input!: HTMLTextAreaElement;
    private list!: HTMLUListElement;
    private chips!: HTMLDivElement;
    private sendButton!: HTMLButtonElement;
    private status!: HTMLSpanElement;

    private picked: MentionUser[] = [];
    private suggestions: MentionUser[] = [];
    private activeIndex = 0;
    private token: MentionToken | null = null;
    private searchTimer: number | undefined;
    private searchSeq = 0;
    private sending = false;

    public init(context: ComponentFramework.Context<IInputs>, _notifyOutputChanged: () => void, _state: ComponentFramework.Dictionary, container: HTMLDivElement): void {
        this.context = context;
        this.root = document.createElement("div");
        this.root.className = "mth-mc";

        this.input = document.createElement("textarea");
        this.input.className = "mth-mc-input";
        this.input.rows = 3;
        this.input.placeholder = this.s("Placeholder");
        this.input.addEventListener("input", () => this.onInput());
        this.input.addEventListener("keydown", e => this.onKeyDown(e));
        this.input.addEventListener("click", () => this.onInput());
        this.input.addEventListener("blur", () => window.setTimeout(() => this.closeList(), 150));

        this.list = document.createElement("ul");
        this.list.className = "mth-mc-list";
        this.list.setAttribute("role", "listbox");
        this.list.hidden = true;

        const footer = document.createElement("div");
        footer.className = "mth-mc-footer";
        this.chips = document.createElement("div");
        this.chips.className = "mth-mc-chips";
        this.status = document.createElement("span");
        this.status.className = "mth-mc-status";
        this.sendButton = document.createElement("button");
        this.sendButton.className = "mth-mc-send";
        this.sendButton.type = "button";
        this.sendButton.textContent = this.s("Send");
        this.sendButton.addEventListener("click", () => void this.send());
        footer.append(this.chips, this.status, this.sendButton);

        const editor = document.createElement("div");
        editor.className = "mth-mc-editor";
        editor.append(this.input, this.list);
        this.root.append(editor, footer);
        container.appendChild(this.root);
        this.refreshState();
    }

    public updateView(context: ComponentFramework.Context<IInputs>): void {
        this.context = context;
        this.refreshState();
    }

    public getOutputs(): IOutputs {
        return {}; // the bound column is never changed
    }

    public destroy(): void {
        window.clearTimeout(this.searchTimer);
    }

    // ---------- record context ----------
    private get recordId(): string {
        const info = (this.context.mode as unknown as { contextInfo?: { entityId?: string } }).contextInfo;
        return (info?.entityId ?? "").replace(/[{}]/g, "");
    }

    private get recordTable(): string {
        const info = (this.context.mode as unknown as { contextInfo?: { entityTypeName?: string } }).contextInfo;
        return info?.entityTypeName ?? "";
    }

    private refreshState(): void {
        const saved = this.recordId.length > 0;
        this.input.disabled = !saved || this.sending;
        this.sendButton.disabled = !saved || this.sending || this.input.value.trim().length === 0;
        if (!saved) this.setStatus(this.s("SaveFirst"), false);
        else if (this.status.dataset.kind === "savefirst") this.setStatus("", false);
    }

    // ---------- typing and picker ----------
    private onInput(): void {
        this.token = findActiveToken(this.input.value, this.input.selectionStart ?? this.input.value.length);
        this.renderChips();
        this.refreshState();
        if (!this.token) { this.closeList(); return; }
        window.clearTimeout(this.searchTimer);
        const query = this.token.query;
        this.searchTimer = window.setTimeout(() => void this.search(query), 200);
    }

    private async search(query: string): Promise<void> {
        const seq = ++this.searchSeq;
        try {
            const res = await this.context.webAPI.retrieveMultipleRecords("systemuser", userQuery(query, 8));
            if (seq !== this.searchSeq || !this.token) return; // a newer search is running, or the token was closed
            this.suggestions = res.entities.map(e => ({
                id: e.systemuserid as string,
                fullName: (e.fullname as string) ?? "",
                email: (e.internalemailaddress as string) ?? ""
            }));
            this.activeIndex = 0;
            this.renderList();
        } catch (err) {
            this.setStatus(this.s("Error") + " " + this.message(err), true);
        }
    }

    private renderList(): void {
        this.list.innerHTML = "";
        if (this.suggestions.length === 0) {
            const li = document.createElement("li");
            li.className = "mth-mc-empty";
            li.textContent = this.s("NoUsers");
            this.list.appendChild(li);
        }
        this.suggestions.forEach((u, i) => {
            const li = document.createElement("li");
            li.className = "mth-mc-item" + (i === this.activeIndex ? " mth-mc-active" : "");
            li.setAttribute("role", "option");
            const initials = document.createElement("span");
            initials.className = "mth-mc-avatar";
            initials.textContent = u.fullName.split(" ").filter(p => p).map(p => p[0]).slice(0, 2).join("").toUpperCase();
            const name = document.createElement("span");
            name.className = "mth-mc-name";
            name.textContent = u.fullName;
            const mail = document.createElement("span");
            mail.className = "mth-mc-mail";
            mail.textContent = u.email;
            li.append(initials, name, mail);
            li.addEventListener("mousedown", e => { e.preventDefault(); this.pick(u); });
            this.list.appendChild(li);
        });
        this.list.hidden = false;
    }

    private closeList(): void {
        this.list.hidden = true;
        this.suggestions = [];
    }

    private onKeyDown(e: KeyboardEvent): void {
        if ((e.ctrlKey || e.metaKey) && e.key === "Enter") { e.preventDefault(); void this.send(); return; }
        if (this.list.hidden || this.suggestions.length === 0) return;
        if (e.key === "ArrowDown") { e.preventDefault(); this.activeIndex = (this.activeIndex + 1) % this.suggestions.length; this.renderList(); }
        else if (e.key === "ArrowUp") { e.preventDefault(); this.activeIndex = (this.activeIndex - 1 + this.suggestions.length) % this.suggestions.length; this.renderList(); }
        else if (e.key === "Enter" || e.key === "Tab") { e.preventDefault(); this.pick(this.suggestions[this.activeIndex]); }
        else if (e.key === "Escape") { e.preventDefault(); this.closeList(); }
    }

    private pick(user: MentionUser): void {
        if (!this.token) return;
        const caret = this.input.selectionStart ?? this.input.value.length;
        const r = insertMention(this.input.value, this.token, caret, user);
        this.input.value = r.text;
        this.input.focus();
        this.input.setSelectionRange(r.caret, r.caret);
        if (!this.picked.some(p => p.id === user.id)) this.picked.push(user);
        this.token = null;
        this.closeList();
        this.renderChips();
        this.refreshState();
    }

    /** Shows "Mentions: A, B" for picked users whose @Full Name is still in the text. */
    private renderChips(): void {
        const users = mentionedUsers(this.input.value, this.picked);
        this.chips.innerHTML = "";
        if (users.length === 0) return;
        const label = document.createElement("span");
        label.className = "mth-mc-chips-label";
        label.textContent = this.s("Mentions");
        this.chips.appendChild(label);
        for (const u of users) {
            const chip = document.createElement("span");
            chip.className = "mth-mc-chip";
            chip.textContent = u.fullName;
            this.chips.appendChild(chip);
        }
    }

    // ---------- send ----------
    private async send(): Promise<void> {
        const text = this.input.value.trim();
        if (!text || this.sending || !this.recordId) return;
        this.sending = true;
        this.refreshState();
        this.setStatus(this.s("Sending"), false);
        try {
            const meta = await this.context.utils.getEntityMetadata(this.recordTable, []);
            const entitySet = (meta as unknown as { EntitySetName: string }).EntitySetName;
            const note: ComponentFramework.WebApi.Entity = {
                subject: this.s("CommentSubject"),
                notetext: toNoteHtml(text)
            };
            note["objectid_" + this.recordTable + "@odata.bind"] = "/" + entitySet + "(" + this.recordId + ")";
            await this.context.webAPI.createRecord("annotation", note);
            this.input.value = "";
            this.picked = [];
            this.renderChips();
            this.setStatus(this.s("Sent"), false);
            this.refreshTimeline();
        } catch (err) {
            this.setStatus(this.s("Error") + " " + this.message(err), true);
        } finally {
            this.sending = false;
            this.refreshState();
        }
    }

    /** Refreshes the standard timeline control (id "notescontrol") so the new note appears. */
    private refreshTimeline(): void {
        try {
            const xrm = (window as unknown as { Xrm?: { Page?: { getControl?: (n: string) => { refresh?: () => void } | null } } }).Xrm;
            xrm?.Page?.getControl?.("notescontrol")?.refresh?.();
        } catch {
            // not critical: the note is saved and appears on the next refresh
        }
    }

    // ---------- helpers ----------
    private s(key: string): string {
        return this.context.resources.getString(key);
    }

    private setStatus(text: string, isError: boolean): void {
        this.status.textContent = text;
        this.status.classList.toggle("mth-mc-error", isError);
        this.status.dataset.kind = text !== "" && text === this.s("SaveFirst") ? "savefirst" : "";
    }

    private message(err: unknown): string {
        if (err && typeof err === "object" && "message" in err) return String((err as { message: unknown }).message);
        return String(err);
    }
}
