import * as React from "react";
import {
    Badge, Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Divider, Dropdown, Field, Input,
    Menu, MenuItem, MenuList, MenuPopover, MenuTrigger, MessageBar, MessageBarBody, Option, ProgressBar, Text, makeStyles, tokens
} from "@fluentui/react-components";
import { Attach20Regular, Dismiss16Regular, TextBold20Regular, TextBulletListLtr20Regular, TextItalic20Regular } from "./icons";
import { Contract, EmailAttachmentInput, OpportunityInfo, Partner, SendResult, TenderApi, Template } from "../api";
import { PLACEHOLDERS, TemplateValues, renderTemplate, sanitizeHtml } from "../logic";
import { Texts } from "../strings";

interface Props {
    tx: Texts; api: TenderApi; opportunity: OpportunityInfo; contracts: Contract[]; partners: Partner[]; userName: string;
    onClose: (sentAny: boolean) => void;
}

interface Outcome { partner: Partner; state: "sent" | "skipped" | "test" | "error"; message: string }

const useStyles = makeStyles({
    surface: { maxWidth: "980px", width: "95vw" },
    grid: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" },
    col: { display: "flex", flexDirection: "column", gap: "10px", minWidth: 0 },
    editorBar: { display: "flex", gap: "2px", alignItems: "center", borderBottom: `1px solid ${tokens.colorNeutralStroke2}`, paddingBottom: "4px" },
    editor: {
        minHeight: "220px", maxHeight: "360px", overflowY: "auto", padding: "8px", border: `1px solid ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium, fontFamily: "Arial, sans-serif", fontSize: "13px", outlineStyle: "none",
        ":focus": { border: `1px solid ${tokens.colorBrandStroke1}` }
    },
    preview: {
        minHeight: "220px", maxHeight: "420px", overflowY: "auto", padding: "10px", border: `1px dashed ${tokens.colorNeutralStroke1}`,
        borderRadius: tokens.borderRadiusMedium, backgroundColor: tokens.colorNeutralBackground2, fontFamily: "Arial, sans-serif", fontSize: "13px"
    },
    files: { display: "flex", flexWrap: "wrap", gap: "4px", alignItems: "center" },
    muted: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
    results: { display: "flex", flexDirection: "column", gap: "4px", maxHeight: "300px", overflowY: "auto" },
    resultRow: { display: "flex", gap: "8px", alignItems: "center" }
});

const readFile = (f: File): Promise<EmailAttachmentInput> => new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve({ fileName: f.name, mimeType: f.type === "" ? "application/octet-stream" : f.type, base64: typeof r.result === "string" ? (r.result.split(",")[1] ?? "") : "" });
    r.onerror = () => reject(new Error(r.error?.message ?? "File could not be read"));
    r.readAsDataURL(f);
});

export const EmailDialog: React.FC<Props> = ({ tx, api, opportunity, contracts, partners, userName, onClose }) => {
    const styles = useStyles();
    const [templates, setTemplates] = React.useState<Template[]>([]);
    const [templateId, setTemplateId] = React.useState<string>("");
    const [subject, setSubject] = React.useState("");
    const [bodyHtml, setBodyHtml] = React.useState("");
    const [files, setFiles] = React.useState<EmailAttachmentInput[]>([]);
    const [previewId, setPreviewId] = React.useState<string>(partners[0]?.id ?? "");
    const [progress, setProgress] = React.useState<{ done: number; total: number } | null>(null);
    const [outcomes, setOutcomes] = React.useState<Outcome[]>([]);
    const [error, setError] = React.useState<string | null>(null);
    const editorRef = React.useRef<HTMLDivElement>(null);
    const fileRef = React.useRef<HTMLInputElement>(null);

    const sendable = partners.filter(p => p.contacts.some(c => c.email));
    const skipped = partners.filter(p => !p.contacts.some(c => c.email));
    const contactCount = sendable.reduce((n, p) => n + p.contacts.filter(c => c.email).length, 0);

    const applyTemplate = (t: Template | undefined) => {
        setTemplateId(t?.id ?? "");
        setSubject(t?.subject ?? "");
        const html = t?.body ?? "";
        setBodyHtml(html);
        if (editorRef.current) editorRef.current.innerHTML = html;
    };

    React.useEffect(() => {
        void (async () => {
            try { const ts = await api.templates(); setTemplates(ts); applyTemplate(ts.find(t => t.isDefault) ?? ts[0]); }
            catch (e) { setError((e as Error).message); }
        })();
    }, [api]);

    const exec = (command: string, value?: string) => {
        editorRef.current?.focus();
        document.execCommand(command, false, value); // simple formatting in the contentEditable editor
        setBodyHtml(editorRef.current?.innerHTML ?? "");
    };

    const values = (p: Partner | undefined): TemplateValues => {
        const c = contracts.find(x => x.id === p?.contractId);
        return {
            opportunity: opportunity.name, caseNumber: opportunity.caseNumber, buildingContract: c?.name, tenderDeadline: c?.deadline,
            partner: p?.name, contactFirstNames: (p?.contacts ?? []).filter(x => x.email).map(x => x.firstName), sender: userName
        };
    };
    const previewPartner = partners.find(p => p.id === previewId);

    const addFiles = async (list: FileList | null) => {
        if (!list) return;
        try { const read = await Promise.all(Array.from(list).map(readFile)); setFiles(prev => [...prev, ...read]); }
        catch (e) { setError((e as Error).message); }
    };

    const run = async (targets: Partner[], toSelf: boolean) => {
        setError(null); setOutcomes([]);
        setProgress({ done: 0, total: targets.length });
        const out: Outcome[] = [];
        for (const p of targets) {
            try {
                const r: SendResult = await api.sendPartnerEmail(p.id, subject, bodyHtml, files, toSelf);
                out.push({ partner: p, state: r.result, message: r.message });
            } catch (e) {
                out.push({ partner: p, state: "error", message: (e as Error).message });
            }
            setOutcomes([...out]);
            setProgress({ done: out.length, total: targets.length });
        }
        setProgress(null);
    };

    const sentAny = outcomes.some(o => o.state === "sent");
    const finished = outcomes.length > 0 && !progress && outcomes.every(o => o.state !== "test");
    const canSend = !!subject.trim() && !!bodyHtml.replace(/<[^>]*>/g, "").trim() && !progress;
    const stateBadge = (o: Outcome) => {
        const map = { sent: ["success", tx.t("resultSent")], test: ["informative", tx.t("resultTest")], skipped: ["warning", tx.t("resultSkipped")], error: ["danger", tx.t("resultError")] } as const;
        const [color, label] = map[o.state];
        return <Badge appearance="filled" color={color}>{label}</Badge>;
    };

    return (
        <Dialog open modalType="modal" onOpenChange={(_e, d) => { if (!d.open && !progress) onClose(sentAny); }}>
            <DialogSurface className={styles.surface}>
                <DialogBody>
                    <DialogTitle>{tx.t("emailTitle")}</DialogTitle>
                    <DialogContent>
                        <MessageBar intent="info"><MessageBarBody>
                            {tx.t("recipients", sendable.length, contactCount)} {tx.t("statusNote")}
                        </MessageBarBody></MessageBar>
                        {skipped.length > 0 && <MessageBar intent="warning"><MessageBarBody>{tx.t("skippedNoContacts", skipped.map(p => p.name).join(", "))}</MessageBarBody></MessageBar>}
                        {error && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}

                        <div className={styles.grid} style={{ marginTop: 10 }}>
                            <div className={styles.col}>
                                <Field label={tx.t("template")}>
                                    <Dropdown value={templates.find(t => t.id === templateId)?.name ?? tx.t("noTemplate")} selectedOptions={[templateId]}
                                        onOptionSelect={(_e, d) => applyTemplate(templates.find(t => t.id === d.optionValue))}>
                                        <Option value="" text={tx.t("noTemplate")}>{tx.t("noTemplate")}</Option>
                                        {templates.map(t => <Option key={t.id} value={t.id}>{t.name}</Option>)}
                                    </Dropdown>
                                </Field>
                                <Field label={tx.t("subject")} required><Input value={subject} onChange={(_e, d) => setSubject(d.value)} /></Field>
                                <Field label={tx.t("body")} required>
                                    <div>
                                        <div className={styles.editorBar}>
                                            <Button size="small" appearance="subtle" icon={<TextBold20Regular />} aria-label="Bold" onMouseDown={e => { e.preventDefault(); exec("bold"); }} />
                                            <Button size="small" appearance="subtle" icon={<TextItalic20Regular />} aria-label="Italic" onMouseDown={e => { e.preventDefault(); exec("italic"); }} />
                                            <Button size="small" appearance="subtle" icon={<TextBulletListLtr20Regular />} aria-label="List" onMouseDown={e => { e.preventDefault(); exec("insertUnorderedList"); }} />
                                            <Menu>
                                                <MenuTrigger disableButtonEnhancement><Button size="small" appearance="subtle">{tx.t("insertPlaceholder")}</Button></MenuTrigger>
                                                <MenuPopover><MenuList>
                                                    {PLACEHOLDERS.map(p => <MenuItem key={p} onClick={() => exec("insertText", `{{${p}}}`)}>{`{{${p}}}`}</MenuItem>)}
                                                </MenuList></MenuPopover>
                                            </Menu>
                                        </div>
                                        <div ref={editorRef} className={styles.editor} contentEditable suppressContentEditableWarning
                                            onInput={() => setBodyHtml(editorRef.current?.innerHTML ?? "")} />
                                    </div>
                                </Field>
                                <Field label={tx.t("attachments")}>
                                    <div className={styles.files}>
                                        {files.map((f, i) => (
                                            <Badge key={i} appearance="tint" icon={<Dismiss16Regular onClick={() => setFiles(files.filter((_x, j) => j !== i))} style={{ cursor: "pointer" }} />} iconPosition="after">{f.fileName}</Badge>
                                        ))}
                                        <Button size="small" icon={<Attach20Regular />} onClick={() => fileRef.current?.click()}>{tx.t("addFiles")}</Button>
                                        <input ref={fileRef} type="file" multiple hidden onChange={e => { void addFiles(e.target.files); e.target.value = ""; }} />
                                    </div>
                                </Field>
                            </div>

                            <div className={styles.col}>
                                <Field label={tx.t("preview")}>
                                    <Dropdown value={previewPartner?.name ?? ""} selectedOptions={[previewId]} onOptionSelect={(_e, d) => setPreviewId(d.optionValue ?? "")}>
                                        {partners.map(p => <Option key={p.id} value={p.id}>{p.name}</Option>)}
                                    </Dropdown>
                                </Field>
                                <Text weight="semibold">{renderTemplate(subject, values(previewPartner), false)}</Text>
                                <Text className={styles.muted}>→ {(previewPartner?.contacts ?? []).filter(c => c.email).map(c => `${c.fullName} <${c.email}>`).join("; ")}</Text>
                                <div className={styles.preview} dangerouslySetInnerHTML={{ __html: sanitizeHtml(renderTemplate(bodyHtml, values(previewPartner), true)) }} />
                            </div>
                        </div>

                        {(progress !== null || outcomes.length > 0) && <Divider style={{ margin: "12px 0" }} />}
                        {progress && <Field label={tx.t("sending", progress.done + 1 > progress.total ? progress.total : progress.done + 1, progress.total)}><ProgressBar value={progress.done / Math.max(progress.total, 1)} /></Field>}
                        {outcomes.length > 0 && (
                            <div className={styles.results}>
                                {!progress && finished && <Text weight="semibold">{tx.t("sendDone", outcomes.filter(o => o.state === "sent").length, outcomes.filter(o => o.state === "skipped").length, outcomes.filter(o => o.state === "error").length)}</Text>}
                                {outcomes.map(o => <div key={o.partner.id} className={styles.resultRow}>{stateBadge(o)}<Text>{o.partner.name}</Text><Text className={styles.muted}>{o.message}</Text></div>)}
                            </div>
                        )}
                    </DialogContent>
                    <DialogActions>
                        <Button disabled={!canSend || !previewPartner} onClick={() => previewPartner && void run([previewPartner], true)}>{tx.t("sendTest")}</Button>
                        <Button appearance="primary" disabled={!canSend || sendable.length === 0 || finished} onClick={() => void run(sendable, false)}>{tx.t("sendTo", sendable.length)}</Button>
                        <Button disabled={!!progress} onClick={() => onClose(sentAny)}>{tx.t("close")}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
