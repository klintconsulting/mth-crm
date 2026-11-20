import * as React from "react";
import {
    Button, Checkbox, Combobox, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Field,
    MessageBar, MessageBarBody, Option, Spinner, Text, makeStyles, tokens
} from "@fluentui/react-components";
import { Contract, OpportunityInfo, Partner, TenderApi } from "../api";
import { Texts } from "../strings";

interface Props {
    tx: Texts; api: TenderApi; contract: Contract; opportunityId: string; partners: Partner[];
    onClose: (message?: string) => void;
}

const useStyles = makeStyles({
    content: { display: "flex", flexDirection: "column", gap: "10px" },
    list: { display: "flex", flexDirection: "column", maxHeight: "260px", overflowY: "auto" },
    muted: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 }
});

export const CloneDialog: React.FC<Props> = ({ tx, api, contract, opportunityId, partners, onClose }) => {
    const styles = useStyles();
    const [query, setQuery] = React.useState("");
    const [options, setOptions] = React.useState<OpportunityInfo[]>([]);
    const [target, setTarget] = React.useState<OpportunityInfo | null>(null);
    const [picked, setPicked] = React.useState<Set<string>>(new Set(partners.map(p => p.id)));
    const [busy, setBusy] = React.useState(false);
    const [error, setError] = React.useState<string | null>(null);

    React.useEffect(() => {
        const q = query.trim();
        if (q.length < 2 || target?.name === q) return;
        const timer = window.setTimeout(() => {
            api.searchOpportunities(q, opportunityId).then(setOptions).catch(e => setError((e as Error).message));
        }, 250);
        return () => window.clearTimeout(timer);
    }, [query, api, opportunityId, target]);

    const toggle = (id: string) => setPicked(prev => { const n = new Set(prev); if (n.has(id)) n.delete(id); else n.add(id); return n; });

    const clone = async () => {
        if (!target) return;
        setBusy(true); setError(null);
        try {
            const r = await api.cloneContract(contract, partners.filter(p => picked.has(p.id)), target.id);
            onClose(tx.t("cloneDone", r.created, r.existed));
        } catch (e) { setError((e as Error).message); setBusy(false); }
    };

    return (
        <Dialog open onOpenChange={(_e, d) => { if (!d.open && !busy) onClose(); }}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{tx.t("cloneTitle", contract.name)}</DialogTitle>
                    <DialogContent className={styles.content}>
                        {error && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
                        <Field label={tx.t("targetOpportunity")} required>
                            <Combobox freeform placeholder={tx.t("searchOpportunity")} value={query}
                                onChange={e => { setQuery(e.target.value); setTarget(null); }}
                                onOptionSelect={(_e, d) => { const o = options.find(x => x.id === d.optionValue); setTarget(o ?? null); setQuery(o?.name ?? ""); }}>
                                {options.map(o => <Option key={o.id} value={o.id} text={o.name}>{o.name}{o.caseNumber ? ` (${o.caseNumber})` : ""}</Option>)}
                            </Combobox>
                        </Field>
                        <Text weight="semibold">{tx.t("clonePartners")}</Text>
                        <div className={styles.list}>
                            {partners.length === 0 && <Text className={styles.muted}>{tx.t("noPartners")}</Text>}
                            {partners.map(p => (
                                <Checkbox key={p.id} checked={picked.has(p.id)} onChange={() => toggle(p.id)}
                                    label={<span>{p.name} <span className={styles.muted}>{tx.status((p.status ?? 0) - 273280000)}</span></span>} />
                            ))}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button onClick={() => onClose()} disabled={busy}>{tx.t("cancel")}</Button>
                        <Button appearance="primary" disabled={!target || busy} onClick={() => void clone()}>{busy ? <Spinner size="tiny" /> : tx.t("clone")}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
