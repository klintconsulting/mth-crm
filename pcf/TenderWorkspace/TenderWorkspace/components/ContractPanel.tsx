import * as React from "react";
import {
    Badge, Button, Combobox, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
    Field, Input, Option, Text, makeStyles, mergeClasses, tokens
} from "@fluentui/react-components";
import { Delete20Regular, Edit20Regular } from "./icons";
import { Contract, Partner, TenderApi, Trade } from "../api";
import { deadlineState, formatDanishDate, statusSummary } from "../logic";
import { Texts } from "../strings";
import { ALL_CONTRACTS as ALL } from "../logic";

interface Props {
    tx: Texts; api: TenderApi; opportunityId: string; trades: Trade[]; contracts: Contract[]; partners: Partner[];
    selectedId: string; onSelect: (id: string) => void;
    onChanged: (selectId?: string, message?: string) => Promise<void>; onError: (e: unknown) => void;
}

const useStyles = makeStyles({
    panel: { display: "flex", flexDirection: "column", gap: "6px" },
    heading: { fontWeight: tokens.fontWeightSemibold, marginTop: "4px" },
    card: {
        display: "flex", flexDirection: "column", gap: "4px", padding: "8px 10px", cursor: "pointer",
        borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke2}`, backgroundColor: tokens.colorNeutralBackground1,
        ":hover": { backgroundColor: tokens.colorNeutralBackground1Hover }
    },
    selected: { border: `2px solid ${tokens.colorBrandStroke1}`, padding: "7px 9px", backgroundColor: tokens.colorBrandBackground2 },
    row: { display: "flex", alignItems: "center", gap: "6px" },
    name: { fontWeight: tokens.fontWeightSemibold, flexGrow: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
    sub: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
    badges: { display: "flex", flexWrap: "wrap", gap: "4px" },
    empty: { color: tokens.colorNeutralForeground3, padding: "8px 0" }
});

export const ContractPanel: React.FC<Props> = props => {
    const { tx, api, trades, contracts, partners, selectedId, onSelect } = props;
    const styles = useStyles();
    const [query, setQuery] = React.useState("");
    const [editing, setEditing] = React.useState<Contract | null>(null);
    const today = new Date();

    const filteredTrades = React.useMemo(() => {
        const q = query.trim().toLowerCase();
        return (q ? trades.filter(t => t.name.toLowerCase().includes(q) || t.code.includes(q)) : trades).slice(0, 50);
    }, [query, trades]);

    const addContract = async (tradeId: string | undefined) => {
        const trade = trades.find(t => t.id === tradeId);
        if (!trade) return;
        setQuery("");
        const existing = contracts.find(c => c.tradeId === trade.id);
        if (existing) { onSelect(existing.id); await props.onChanged(existing.id, tx.t("contractExists", existing.name)); return; }
        try {
            const id = await api.createContract(props.opportunityId, trade);
            await props.onChanged(id);
        } catch (e) { props.onError(e); }
    };

    const removeContract = async (c: Contract) => {
        const count = partners.filter(p => p.contractId === c.id).length;
        if (!window.confirm(tx.t("confirmDeleteContract", c.name, count))) return;
        try { await api.deleteContract(c.id); await props.onChanged(ALL); } catch (e) { props.onError(e); }
    };

    const deadlineBadge = (c: Contract) => {
        const state = deadlineState(c.deadline, today);
        if (state === "none") return <Badge appearance="outline" color="informative">{tx.t("noDeadline")}</Badge>;
        const color = state === "overdue" ? "danger" : state === "soon" ? "warning" : "informative";
        return <Badge appearance={state === "ok" ? "outline" : "filled"} color={color}>{formatDanishDate(c.deadline)}</Badge>;
    };

    return (
        <div className={styles.panel}>
            <Text className={styles.heading}>{tx.t("contracts")}</Text>
            <Combobox
                placeholder={tx.t("addContract")} value={query} freeform
                onChange={e => setQuery(e.target.value)}
                onOptionSelect={(_e, d) => void addContract(d.optionValue)}
            >
                {filteredTrades.map(t => <Option key={t.id} value={t.id} text={t.name}>{t.name} <span className={styles.sub}>&nbsp;{t.code}</span></Option>)}
            </Combobox>

            <div className={mergeClasses(styles.card, selectedId === ALL && styles.selected)} onClick={() => onSelect(ALL)}>
                <div className={styles.row}><span className={styles.name}>{tx.t("allContracts")}</span><Badge appearance="tint">{partners.length}</Badge></div>
            </div>

            {contracts.length === 0 && <Text className={styles.empty}>{tx.t("noContracts")}</Text>}
            {contracts.map(c => {
                const ps = partners.filter(p => p.contractId === c.id);
                return (
                    <div key={c.id} className={mergeClasses(styles.card, selectedId === c.id && styles.selected)} onClick={() => onSelect(c.id)}>
                        <div className={styles.row}>
                            <span className={styles.name} title={c.name}>{c.name}</span>
                            <Button size="small" appearance="subtle" icon={<Edit20Regular />} title={tx.t("editContract")} onClick={e => { e.stopPropagation(); setEditing(c); }} />
                            <Button size="small" appearance="subtle" icon={<Delete20Regular />} title={tx.t("deleteContract")} onClick={e => { e.stopPropagation(); void removeContract(c); }} />
                        </div>
                        {c.alternativeName && <span className={styles.sub}>{c.tradeName}</span>}
                        <div className={styles.badges}>
                            {deadlineBadge(c)}
                            {statusSummary(ps.map(p => p.status)).map(sc => (
                                <Badge key={sc.code} appearance="tint" color={sc.code >= 90 ? "subtle" : sc.code >= 30 ? "success" : "brand"} title={tx.status(sc.code)}>
                                    {sc.code}: {sc.count}
                                </Badge>
                            ))}
                        </div>
                    </div>
                );
            })}

            {editing && <EditContractDialog tx={tx} contract={editing} onClose={() => setEditing(null)}
                onSave={async (alt, deadline) => {
                    try { await api.updateContract(editing, alt, deadline); setEditing(null); await props.onChanged(editing.id, tx.t("saved")); }
                    catch (e) { props.onError(e); }
                }} />}
        </div>
    );
};

const EditContractDialog: React.FC<{ tx: Texts; contract: Contract; onClose: () => void; onSave: (alt: string, deadline: string | null) => Promise<void> }> =
    ({ tx, contract, onClose, onSave }) => {
        const [alt, setAlt] = React.useState(contract.alternativeName);
        const [deadline, setDeadline] = React.useState(contract.deadline ?? "");
        const [busy, setBusy] = React.useState(false);
        return (
            <Dialog open onOpenChange={(_e, d) => { if (!d.open) onClose(); }}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{tx.t("editContract")}: {contract.tradeName}</DialogTitle>
                        <DialogContent style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                            <Field label={tx.t("alternativeName")} hint={contract.tradeName}><Input value={alt} onChange={(_e, d) => setAlt(d.value)} /></Field>
                            <Field label={tx.t("deadline")}><Input type="date" value={deadline} onChange={(_e, d) => setDeadline(d.value)} /></Field>
                        </DialogContent>
                        <DialogActions>
                            <Button onClick={onClose}>{tx.t("cancel")}</Button>
                            <Button appearance="primary" disabled={busy} onClick={() => { setBusy(true); void onSave(alt, deadline === "" ? null : deadline).finally(() => setBusy(false)); }}>{tx.t("save")}</Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        );
    };
