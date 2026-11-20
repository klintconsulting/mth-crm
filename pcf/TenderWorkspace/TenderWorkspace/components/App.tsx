import * as React from "react";
import {
    Button, MessageBar, MessageBarBody, Spinner, Text, Tooltip, makeStyles, tokens
} from "@fluentui/react-components";
import { ArrowClockwise20Regular, Copy20Regular, Mail20Regular, PersonAdd20Regular } from "./icons";
import { Contract, OpportunityInfo, Partner, TenderApi, Trade } from "../api";
import { Texts } from "../strings";
import { ALL_CONTRACTS as ALL } from "../logic";
import { ContractPanel } from "./ContractPanel";
import { PartnerTable } from "./PartnerTable";
import { SearchDrawer } from "./SearchDrawer";
import { EmailDialog } from "./EmailDialog";
import { CloneDialog } from "./CloneDialog";

export interface AppProps {
    api: TenderApi;
    opportunityId: string;
    userName: string;
    tx: Texts;
}



const useStyles = makeStyles({
    root: { display: "flex", flexDirection: "column", gap: "8px", minHeight: "560px", width: "100%", fontFamily: tokens.fontFamilyBase },
    toolbar: { display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" },
    title: { fontSize: tokens.fontSizeBase500, fontWeight: tokens.fontWeightSemibold, marginRight: "auto" },
    body: { display: "flex", gap: "12px", alignItems: "stretch", minHeight: "500px" },
    left: { width: "300px", flexShrink: 0 },
    main: { flexGrow: 1, minWidth: 0 }
});

export const App: React.FC<AppProps> = ({ api, opportunityId, userName, tx }) => {
    const styles = useStyles();
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);
    const [info, setInfo] = React.useState<string | null>(null);
    const [opportunity, setOpportunity] = React.useState<OpportunityInfo | null>(null);
    const [trades, setTrades] = React.useState<Trade[]>([]);
    const [contracts, setContracts] = React.useState<Contract[]>([]);
    const [partners, setPartners] = React.useState<Partner[]>([]);
    const [selectedContractId, setSelectedContractId] = React.useState<string>(ALL);
    const [checked, setChecked] = React.useState<Set<string>>(new Set());
    const [searchOpen, setSearchOpen] = React.useState(false);
    const [emailOpen, setEmailOpen] = React.useState(false);
    const [cloneOpen, setCloneOpen] = React.useState(false);

    const fail = (e: unknown) => setError((e as Error).message ?? String(e));

    const reload = React.useCallback(async (keepSelection = true) => {
        try {
            const cs = await api.contracts(opportunityId);
            const ps = await api.partners(cs.map(c => c.id));
            setContracts(cs);
            setPartners(ps);
            if (!keepSelection) setChecked(new Set());
            else setChecked(prev => new Set(Array.from(prev).filter(id => ps.some(p => p.id === id))));
            setSelectedContractId(prev => (prev === ALL || cs.some(c => c.id === prev) ? prev : ALL));
        } catch (e) { fail(e); }
    }, [api, opportunityId]);

    React.useEffect(() => {
        void (async () => {
            try {
                const [opp, ts] = await Promise.all([api.opportunity(opportunityId), api.trades()]);
                setOpportunity(opp);
                setTrades(ts);
                await reload();
            } catch (e) { fail(e); } finally { setLoading(false); }
        })();
    }, [api, opportunityId, reload]);

    const selectedContract = contracts.find(c => c.id === selectedContractId);
    const visiblePartners = selectedContractId === ALL ? partners : partners.filter(p => p.contractId === selectedContractId);
    const checkedPartners = partners.filter(p => checked.has(p.id));

    /** Optimistic local update of one partner after a successful save. */
    const patchPartner = (id: string, changes: Partial<Partner>) =>
        setPartners(prev => prev.map(p => (p.id === id ? { ...p, ...changes } : p)));

    if (loading) return <Spinner label={tx.t("loading")} />;

    return (
        <div className={styles.root}>
            <div className={styles.toolbar}>
                <Text className={styles.title}>{tx.t("title")}{opportunity ? ` – ${opportunity.name}` : ""}</Text>
                <Tooltip content={selectedContract ? "" : tx.t("noContractSelected")} relationship="description">
                    <Button icon={<PersonAdd20Regular />} disabled={!selectedContract} onClick={() => setSearchOpen(true)}>{tx.t("findPartners")}</Button>
                </Tooltip>
                <Button appearance="primary" icon={<Mail20Regular />} disabled={checkedPartners.length === 0} onClick={() => setEmailOpen(true)}>
                    {tx.t("sendEmail", checkedPartners.length)}
                </Button>
                <Button icon={<Copy20Regular />} disabled={!selectedContract} onClick={() => setCloneOpen(true)}>{tx.t("copyContract")}</Button>
                <Tooltip content={tx.t("refresh")} relationship="label">
                    <Button icon={<ArrowClockwise20Regular />} onClick={() => { setError(null); void reload(); }} />
                </Tooltip>
            </div>

            {error && <MessageBar intent="error"><MessageBarBody>{tx.t("error")}: {error}</MessageBarBody></MessageBar>}
            {info && <MessageBar intent="success"><MessageBarBody>{info}</MessageBarBody></MessageBar>}

            <div className={styles.body}>
                <div className={styles.left}>
                    <ContractPanel
                        tx={tx} api={api} opportunityId={opportunityId} trades={trades} contracts={contracts} partners={partners}
                        selectedId={selectedContractId} onSelect={setSelectedContractId}
                        onChanged={async (selectId, message) => { await reload(); if (selectId) setSelectedContractId(selectId); setInfo(message ?? null); }}
                        onError={fail}
                    />
                </div>
                <div className={styles.main}>
                    <PartnerTable
                        tx={tx} api={api} partners={visiblePartners} contracts={contracts} showContract={selectedContractId === ALL}
                        checked={checked} onCheckedChange={setChecked}
                        onPatched={patchPartner} onRemoved={async () => { await reload(); }} onError={fail}
                        emptyText={contracts.length === 0 ? tx.t("noContracts") : tx.t("noPartners")}
                    />
                </div>
            </div>

            {searchOpen && selectedContract && (
                <SearchDrawer
                    tx={tx} api={api} trades={trades} contract={selectedContract}
                    existingAccountIds={partners.filter(p => p.contractId === selectedContract.id).map(p => p.accountId)}
                    onClose={() => setSearchOpen(false)}
                    onAdded={async count => { setSearchOpen(false); await reload(); setInfo(tx.t("added", count)); }}
                    onError={fail}
                />
            )}
            {emailOpen && opportunity && (
                <EmailDialog
                    tx={tx} api={api} opportunity={opportunity} contracts={contracts} partners={checkedPartners} userName={userName}
                    onClose={sentAny => { setEmailOpen(false); if (sentAny) void reload(false); }}
                />
            )}
            {cloneOpen && selectedContract && (
                <CloneDialog
                    tx={tx} api={api} contract={selectedContract} opportunityId={opportunityId}
                    partners={partners.filter(p => p.contractId === selectedContract.id)}
                    onClose={message => { setCloneOpen(false); if (message) setInfo(message); }}
                />
            )}
        </div>
    );
};
