import * as React from "react";
import {
    Badge, Button, Checkbox, Combobox, Dropdown, DrawerBody, DrawerFooter, DrawerHeader, DrawerHeaderTitle, Field, Input,
    MessageBar, MessageBarBody, Option, OverlayDrawer, Spinner, Text, makeStyles, tokens
} from "@fluentui/react-components";
import { Dismiss20Regular, Search20Regular } from "./icons";
import { Contract, SearchResult, TenderApi, Trade } from "../api";
import { EVALUATIONS, REGIONS, hasBadEvaluation, summarizeEvaluations } from "../logic";
import { Texts } from "../strings";

interface Props {
    tx: Texts; api: TenderApi; trades: Trade[]; contract: Contract; existingAccountIds: string[];
    onClose: () => void; onAdded: (count: number) => Promise<void>; onError: (e: unknown) => void;
}

const MAX_RESULTS = 200;
const ALL_TRADES = "__alltrades__";

const useStyles = makeStyles({
    filters: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px", marginBottom: "8px" },
    full: { gridColumn: "1 / span 2" },
    list: { display: "flex", flexDirection: "column", gap: "2px" },
    item: { display: "flex", alignItems: "center", gap: "8px", padding: "4px 6px", borderRadius: tokens.borderRadiusMedium, ":hover": { backgroundColor: tokens.colorNeutralBackground1Hover } },
    name: { flexGrow: 1, minWidth: 0 },
    muted: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
    evals: { display: "flex", gap: "3px" },
    dot: { display: "inline-block", width: "8px", height: "8px", borderRadius: "50%", marginRight: "3px" }
});

export const SearchDrawer: React.FC<Props> = ({ tx, api, trades, contract, existingAccountIds, onClose, onAdded, onError }) => {
    const styles = useStyles();
    const [tradeId, setTradeId] = React.useState<string>(contract.tradeId);
    const [tradeText, setTradeText] = React.useState<string>(contract.tradeName);
    const [regionKey, setRegionKey] = React.useState<string>("");
    const [name, setName] = React.useState("");
    const [hideBad, setHideBad] = React.useState(true);
    const [results, setResults] = React.useState<SearchResult[] | null>(null);
    const [searching, setSearching] = React.useState(false);
    const [picked, setPicked] = React.useState<Set<string>>(new Set());
    const [adding, setAdding] = React.useState(false);

    const search = React.useCallback(async () => {
        setSearching(true);
        try {
            const region = REGIONS.find(r => r.key === regionKey);
            setResults(await api.searchPartners(tradeId === ALL_TRADES ? null : tradeId, name, region, MAX_RESULTS));
        } catch (e) { onError(e); } finally { setSearching(false); }
    }, [api, tradeId, regionKey, name, onError]);

    // search automatically when trade or region changes (name: on Enter / button)
    React.useEffect(() => { void search(); }, [tradeId, regionKey]);

    const visible = (results ?? []).filter(r => !hideBad || !hasBadEvaluation(summarizeEvaluations(r.evaluations)));
    const hiddenCount = (results?.length ?? 0) - visible.length;
    const selectable = visible.filter(r => !existingAccountIds.includes(r.accountId));

    const toggle = (id: string) => setPicked(prev => { const n = new Set(prev); if (n.has(id)) n.delete(id); else n.add(id); return n; });

    const add = async () => {
        setAdding(true);
        let count = 0;
        try {
            for (const r of visible.filter(x => picked.has(x.accountId))) {
                await api.addPartner(contract.id, r.accountId, r.name);
                count++;
            }
            await onAdded(count);
        } catch (e) { onError(e); setAdding(false); }
    };

    const filteredTrades = React.useMemo(() => {
        const q = tradeText.trim().toLowerCase();
        const list = q && q !== contract.tradeName.toLowerCase() ? trades.filter(t => t.name.toLowerCase().includes(q) || t.code.includes(q)) : trades;
        return list.slice(0, 60);
    }, [tradeText, trades, contract.tradeName]);

    return (
        <OverlayDrawer open position="end" size="medium" onOpenChange={(_e, d) => { if (!d.open) onClose(); }}>
            <DrawerHeader>
                <DrawerHeaderTitle action={<Button appearance="subtle" icon={<Dismiss20Regular />} aria-label={tx.t("close")} onClick={onClose} />}>
                    {tx.t("searchTitle", contract.name)}
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody>
                <div className={styles.filters}>
                    <Field label={tx.t("trade")}>
                        <Combobox value={tradeText} freeform selectedOptions={[tradeId]}
                            onChange={e => setTradeText(e.target.value)}
                            onOptionSelect={(_e, d) => { if (d.optionValue) { setTradeId(d.optionValue); setTradeText(d.optionText ?? ""); } }}>
                            <Option value={ALL_TRADES} text={tx.t("allTrades")}>{tx.t("allTrades")}</Option>
                            {filteredTrades.map(t => <Option key={t.id} value={t.id} text={t.name}>{t.name} ({t.code})</Option>)}
                        </Combobox>
                    </Field>
                    <Field label={tx.t("region")}>
                        <Dropdown value={regionKey ? tx.region(regionKey) : tx.t("allRegions")} selectedOptions={[regionKey]}
                            onOptionSelect={(_e, d) => setRegionKey(d.optionValue ?? "")}>
                            <Option value="" text={tx.t("allRegions")}>{tx.t("allRegions")}</Option>
                            {REGIONS.map(r => <Option key={r.key} value={r.key} text={tx.region(r.key)}>{tx.region(r.key)} ({r.from}–{r.to})</Option>)}
                        </Dropdown>
                    </Field>
                    <Field label={tx.t("name")} className={styles.full}>
                        <Input value={name} onChange={(_e, d) => setName(d.value)} onKeyDown={e => { if (e.key === "Enter") void search(); }}
                            contentAfter={<Button size="small" appearance="transparent" icon={<Search20Regular />} aria-label={tx.t("search")} onClick={() => void search()} />} />
                    </Field>
                    <Checkbox className={styles.full} checked={hideBad} onChange={(_e, d) => setHideBad(!!d.checked)} label={tx.t("hideBad")} />
                </div>

                {searching && <Spinner size="small" />}
                {!searching && results && (
                    <>
                        <Text className={styles.muted}>
                            {tx.t("results", visible.length)}{hiddenCount > 0 ? ` ${tx.t("hiddenBad", hiddenCount)}` : ""}
                        </Text>
                        {results.length >= MAX_RESULTS && <MessageBar intent="warning"><MessageBarBody>{tx.t("tooMany", MAX_RESULTS)}</MessageBarBody></MessageBar>}
                        <div className={styles.list}>
                            {visible.map(r => {
                                const added = existingAccountIds.includes(r.accountId);
                                const ev = summarizeEvaluations(r.evaluations);
                                return (
                                    <div key={r.accountId} className={styles.item}>
                                        <Checkbox disabled={added} checked={added || picked.has(r.accountId)} onChange={() => toggle(r.accountId)} aria-label={r.name} />
                                        <div className={styles.name}>
                                            <Text weight="semibold">{r.name}</Text><br />
                                            <span className={styles.muted}>{[r.postalCode, r.city].filter(Boolean).join(" ")}</span>
                                        </div>
                                        <div className={styles.evals} title={tx.t("previousEvaluations")}>
                                            {EVALUATIONS.filter(e => ev[e.key] > 0).map(e => (
                                                <Badge key={e.key} appearance="outline" size="small"><span className={styles.dot} style={{ backgroundColor: e.color }} />{ev[e.key]}</Badge>
                                            ))}
                                        </div>
                                        {added && <Badge appearance="tint" color="success">{tx.t("alreadyAdded")}</Badge>}
                                    </div>
                                );
                            })}
                        </div>
                    </>
                )}
            </DrawerBody>
            <DrawerFooter>
                <Button appearance="primary" disabled={adding || picked.size === 0} onClick={() => void add()}>
                    {adding ? <Spinner size="tiny" /> : tx.t("addSelected", Array.from(picked).filter(id => selectable.some(s => s.accountId === id)).length)}
                </Button>
                <Button onClick={onClose}>{tx.t("cancel")}</Button>
            </DrawerFooter>
        </OverlayDrawer>
    );
};
