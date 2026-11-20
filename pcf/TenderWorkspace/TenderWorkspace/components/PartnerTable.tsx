import * as React from "react";
import {
    Badge, Button, Checkbox, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Dropdown, Field, Input,
    Link, Option, Popover, PopoverSurface, PopoverTrigger, Spinner, Table, TableBody, TableCell, TableHeader, TableHeaderCell,
    TableRow, Text, Textarea, makeStyles, tokens
} from "@fluentui/react-components";
import { Delete20Regular, PersonAdd20Regular } from "./icons";
import { ContactInfo, Contract, Partner, TenderApi } from "../api";
import { EVALUATIONS, OPTION_BASE, STATUS_CODES, evaluationNeedsComment, evaluationOption, formatDanishDate, statusCode } from "../logic";
import { Texts } from "../strings";

interface Props {
    tx: Texts; api: TenderApi; partners: Partner[]; contracts: Contract[]; showContract: boolean;
    checked: Set<string>; onCheckedChange: (s: Set<string>) => void;
    onPatched: (id: string, changes: Partial<Partner>) => void; onRemoved: () => Promise<void>; onError: (e: unknown) => void;
    emptyText: string;
}

const useStyles = makeStyles({
    table: { tableLayout: "auto" },
    chips: { display: "flex", flexWrap: "wrap", gap: "4px", alignItems: "center", cursor: "pointer", minHeight: "24px" },
    muted: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
    dot: { display: "inline-block", width: "10px", height: "10px", borderRadius: "50%", marginRight: "6px" },
    empty: { padding: "24px", color: tokens.colorNeutralForeground3 },
    popover: { display: "flex", flexDirection: "column", gap: "6px", minWidth: "320px", maxWidth: "380px" },
    form: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px" },
    warn: { color: tokens.colorPaletteRedForeground1 }
});

export const PartnerTable: React.FC<Props> = props => {
    const { tx, partners, checked } = props;
    const styles = useStyles();
    const allChecked = partners.length > 0 && partners.every(p => checked.has(p.id));
    const someChecked = partners.some(p => checked.has(p.id));

    const toggleAll = () => {
        const next = new Set(checked);
        if (allChecked) partners.forEach(p => next.delete(p.id)); else partners.forEach(p => next.add(p.id));
        props.onCheckedChange(next);
    };
    const toggle = (id: string) => {
        const next = new Set(checked);
        if (next.has(id)) next.delete(id); else next.add(id);
        props.onCheckedChange(next);
    };

    if (partners.length === 0) return <Text className={styles.empty}>{props.emptyText}</Text>;

    return (
        <Table size="small" className={styles.table} aria-label={tx.t("partners")}>
            <TableHeader>
                <TableRow>
                    <TableHeaderCell style={{ width: 36 }}>
                        <Checkbox checked={allChecked ? true : someChecked ? "mixed" : false} onChange={toggleAll} aria-label={tx.t("selectAll")} />
                    </TableHeaderCell>
                    <TableHeaderCell>{tx.t("colPartner")}</TableHeaderCell>
                    {props.showContract && <TableHeaderCell>{tx.t("colContract")}</TableHeaderCell>}
                    <TableHeaderCell>{tx.t("colContacts")}</TableHeaderCell>
                    <TableHeaderCell style={{ width: 230 }}>{tx.t("colStatus")}</TableHeaderCell>
                    <TableHeaderCell style={{ width: 130 }}>{tx.t("colEvaluation")}</TableHeaderCell>
                    <TableHeaderCell>{tx.t("colComments")}</TableHeaderCell>
                    <TableHeaderCell style={{ width: 100 }}>{tx.t("colLastEmail")}</TableHeaderCell>
                    <TableHeaderCell style={{ width: 40 }} />
                </TableRow>
            </TableHeader>
            <TableBody>
                {partners.map(p => <PartnerRow key={p.id} {...props} partner={p} isChecked={checked.has(p.id)} onToggle={() => toggle(p.id)} />)}
            </TableBody>
        </Table>
    );
};

const PartnerRow: React.FC<Props & { partner: Partner; isChecked: boolean; onToggle: () => void }> = props => {
    const { tx, api, partner: p, onPatched, onError } = props;
    const styles = useStyles();
    const [comments, setComments] = React.useState(p.comments);
    const [pendingEval, setPendingEval] = React.useState<number | null>(null);
    React.useEffect(() => setComments(p.comments), [p.comments]);

    const save = async (fields: Record<string, unknown>, changes: Partial<Partner>) => {
        try { await api.updatePartner(p.id, fields); onPatched(p.id, changes); } catch (e) { onError(e); }
    };
    const contract = props.contracts.find(c => c.id === p.contractId);
    const evalOpt = evaluationOption(p.evaluation);

    const chooseEvaluation = (value: number | null) => {
        if (value !== null && evaluationNeedsComment(value)) { setPendingEval(value); return; }
        void save({ mth_tenderevaluation: value, mth_tenderevaluationcomment: value === null ? null : p.evaluationComment || null },
            { evaluation: value });
    };

    const remove = async () => {
        if (!window.confirm(tx.t("confirmRemovePartner", p.name, contract?.name ?? ""))) return;
        try { await api.removePartner(p.id); await props.onRemoved(); } catch (e) { onError(e); }
    };

    return (
        <TableRow>
            <TableCell><Checkbox checked={props.isChecked} onChange={props.onToggle} aria-label={p.name} /></TableCell>
            <TableCell>
                <Link href={`main.aspx?pagetype=entityrecord&etn=account&id=${p.accountId}`} target="_blank">{p.name}</Link>
            </TableCell>
            {props.showContract && <TableCell><Text size={200}>{contract?.name}</Text></TableCell>}
            <TableCell><ContactPicker tx={tx} api={api} partner={p} onPatched={onPatched} onError={onError} /></TableCell>
            <TableCell>
                <Dropdown size="small" style={{ minWidth: 210 }} value={tx.status(statusCode(p.status))}
                    selectedOptions={p.status !== null ? [String(p.status)] : []}
                    onOptionSelect={(_e, d) => { const v = Number(d.optionValue); void save({ mth_status: v }, { status: v }); }}>
                    {STATUS_CODES.map(c => <Option key={c} value={String(OPTION_BASE + c)}>{tx.status(c)}</Option>)}
                </Dropdown>
            </TableCell>
            <TableCell>
                <Dropdown size="small" style={{ minWidth: 110 }}
                    value={evalOpt ? tx.evaluation(evalOpt.key) : ""} placeholder={tx.t("noEvaluation")}
                    title={p.evaluationComment || undefined}
                    selectedOptions={p.evaluation !== null ? [String(p.evaluation)] : []}
                    onOptionSelect={(_e, d) => chooseEvaluation(d.optionValue ? Number(d.optionValue) : null)}>
                    <Option value="" text={tx.t("noEvaluation")}>{tx.t("noEvaluation")}</Option>
                    {EVALUATIONS.map(e => (
                        <Option key={e.value} value={String(e.value)} text={tx.evaluation(e.key)}>
                            <span className={styles.dot} style={{ backgroundColor: e.color }} />{tx.evaluation(e.key)}
                        </Option>
                    ))}
                </Dropdown>
            </TableCell>
            <TableCell>
                <Input size="small" style={{ width: "100%" }} value={comments} onChange={(_e, d) => setComments(d.value)}
                    onBlur={() => { if (comments !== p.comments) void save({ mth_comments: comments || null }, { comments }); }} />
            </TableCell>
            <TableCell><Text size={200}>{p.lastEmailSent ? formatDanishDate(p.lastEmailSent) : ""}</Text></TableCell>
            <TableCell><Button size="small" appearance="subtle" icon={<Delete20Regular />} title={tx.t("remove")} onClick={() => void remove()} /></TableCell>

            {pendingEval !== null && (
                <EvaluationCommentDialog tx={tx} evaluation={pendingEval} initial={p.evaluationComment}
                    onCancel={() => setPendingEval(null)}
                    onSave={async comment => {
                        await save({ mth_tenderevaluation: pendingEval, mth_tenderevaluationcomment: comment }, { evaluation: pendingEval, evaluationComment: comment });
                        setPendingEval(null);
                    }} />
            )}
        </TableRow>
    );
};

const EvaluationCommentDialog: React.FC<{ tx: Texts; evaluation: number; initial: string; onCancel: () => void; onSave: (c: string) => Promise<void> }> =
    ({ tx, evaluation, initial, onCancel, onSave }) => {
        const [comment, setComment] = React.useState(initial);
        const key = evaluationOption(evaluation)?.key ?? "";
        return (
            <Dialog open onOpenChange={(_e, d) => { if (!d.open) onCancel(); }}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{tx.t("evaluationCommentTitle")}</DialogTitle>
                        <DialogContent>
                            <Field label={tx.t("evaluationComment")} hint={tx.t("evaluationCommentText", tx.evaluation(key))} required>
                                <Textarea value={comment} maxLength={255} onChange={(_e, d) => setComment(d.value)} />
                            </Field>
                        </DialogContent>
                        <DialogActions>
                            <Button onClick={onCancel}>{tx.t("cancel")}</Button>
                            <Button appearance="primary" disabled={!comment.trim()} onClick={() => void onSave(comment.trim())}>{tx.t("save")}</Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        );
    };

/** Selected contacts as badges; the popover lists the account's contacts with checkboxes and can create a new contact. */
const ContactPicker: React.FC<{ tx: Texts; api: TenderApi; partner: Partner; onPatched: (id: string, c: Partial<Partner>) => void; onError: (e: unknown) => void }> =
    ({ tx, api, partner, onPatched, onError }) => {
        const styles = useStyles();
        const [open, setOpen] = React.useState(false);
        const [all, setAll] = React.useState<ContactInfo[] | null>(null);
        const [showNew, setShowNew] = React.useState(false);
        const [form, setForm] = React.useState({ firstName: "", lastName: "", jobTitle: "", email: "", mobile: "" });
        const [busy, setBusy] = React.useState(false);

        const load = async () => { try { setAll(await api.accountContacts(partner.accountId)); } catch (e) { onError(e); } };
        const isSelected = (id: string) => partner.contacts.some(c => c.id === id);

        const toggle = async (c: ContactInfo) => {
            try {
                if (isSelected(c.id)) {
                    await api.unselectContact(partner.id, c.id);
                    onPatched(partner.id, { contacts: partner.contacts.filter(x => x.id !== c.id) });
                } else {
                    await api.selectContact(partner.id, c.id);
                    onPatched(partner.id, { contacts: [...partner.contacts, c] });
                }
            } catch (e) { onError(e); }
        };

        const create = async () => {
            setBusy(true);
            try {
                const id = await api.createContact(partner.accountId, form);
                await api.selectContact(partner.id, id);
                const created: ContactInfo = { id, fullName: `${form.firstName} ${form.lastName}`.trim(), firstName: form.firstName, email: form.email, jobTitle: form.jobTitle };
                onPatched(partner.id, { contacts: [...partner.contacts, created] });
                setForm({ firstName: "", lastName: "", jobTitle: "", email: "", mobile: "" });
                setShowNew(false);
                await load();
            } catch (e) { onError(e); } finally { setBusy(false); }
        };
        const validEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim());

        return (
            <Popover open={open} onOpenChange={(_e, d) => { setOpen(d.open); if (d.open) void load(); }} positioning="below-start" trapFocus>
                <PopoverTrigger disableButtonEnhancement>
                    <div className={styles.chips} role="button" tabIndex={0} title={tx.t("chooseContacts")}>
                        {partner.contacts.length === 0
                            ? <Text className={styles.warn} size={200}>{tx.t("noContacts")}</Text>
                            : partner.contacts.map(c => <Badge key={c.id} appearance="tint" color={c.email ? "brand" : "danger"} title={c.email}>{c.fullName}</Badge>)}
                    </div>
                </PopoverTrigger>
                <PopoverSurface>
                    <div className={styles.popover}>
                        <Text weight="semibold">{partner.name}</Text>
                        <Text className={styles.muted}>{tx.t("contactsHint")}</Text>
                        {all === null && <Spinner size="tiny" />}
                        {all !== null && all.length === 0 && <Text className={styles.muted}>{tx.t("noAccountContacts")}</Text>}
                        {all?.map(c => (
                            <Checkbox key={c.id} checked={isSelected(c.id)} onChange={() => void toggle(c)}
                                label={<span>{c.fullName}{c.jobTitle ? `, ${c.jobTitle}` : ""}<br /><span className={styles.muted}>{c.email || "–"}</span></span>} />
                        ))}
                        {!showNew && <Button size="small" icon={<PersonAdd20Regular />} onClick={() => setShowNew(true)}>{tx.t("newContact")}</Button>}
                        {showNew && (
                            <div className={styles.form}>
                                <Field label={tx.t("firstName")} required><Input size="small" value={form.firstName} onChange={(_e, d) => setForm({ ...form, firstName: d.value })} /></Field>
                                <Field label={tx.t("lastName")} required><Input size="small" value={form.lastName} onChange={(_e, d) => setForm({ ...form, lastName: d.value })} /></Field>
                                <Field label={tx.t("email")} required validationState={form.email && !validEmail ? "error" : "none"}>
                                    <Input size="small" type="email" value={form.email} onChange={(_e, d) => setForm({ ...form, email: d.value })} />
                                </Field>
                                <Field label={tx.t("mobile")}><Input size="small" value={form.mobile} onChange={(_e, d) => setForm({ ...form, mobile: d.value })} /></Field>
                                <Field label={tx.t("jobTitle")}><Input size="small" value={form.jobTitle} onChange={(_e, d) => setForm({ ...form, jobTitle: d.value })} /></Field>
                                <div style={{ display: "flex", gap: 6, alignItems: "end" }}>
                                    <Button size="small" appearance="primary" disabled={busy || !form.firstName.trim() || !form.lastName.trim() || !validEmail} onClick={() => void create()}>{tx.t("createContact")}</Button>
                                    <Button size="small" onClick={() => setShowNew(false)}>{tx.t("cancel")}</Button>
                                </div>
                            </div>
                        )}
                    </div>
                </PopoverSurface>
            </Popover>
        );
    };
