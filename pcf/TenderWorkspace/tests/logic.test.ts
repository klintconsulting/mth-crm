// Run: npm test  (node --experimental-strip-types --test, Node 22+)
import { test } from "node:test";
import assert from "node:assert/strict";
import {
    OPTION_BASE, REGIONS, contractName, deadlineState, evaluationNeedsComment, formatDanishDate, hasBadEvaluation, inRegion,
    joinNames, renderTemplate, sanitizeHtml, statusCode, statusSummary, summarizeEvaluations, xmlAttr, odataString
} from "../TenderWorkspace/logic.ts";

const region = (key: string) => REGIONS.find(r => r.key === key);

test("status code from option value", () => {
    assert.equal(statusCode(OPTION_BASE + 24), 24);
    assert.equal(statusCode(null), null);
});

test("green needs no comment, yellow/red/black do", () => {
    assert.equal(evaluationNeedsComment(OPTION_BASE + 0), false);
    assert.equal(evaluationNeedsComment(OPTION_BASE + 1), true);
    assert.equal(evaluationNeedsComment(OPTION_BASE + 3), true);
    assert.equal(evaluationNeedsComment(null), false);
});

test("regions cover Danish postal codes without overlap", () => {
    assert.equal(inRegion("2300", region("capital")), true);
    assert.equal(inRegion("3700", region("capital")), true);   // Bornholm
    assert.equal(inRegion("5000", region("funen")), true);
    assert.equal(inRegion("6000", region("southJutland")), true);
    assert.equal(inRegion("8000", region("midJutland")), true);
    assert.equal(inRegion("9000", region("northJutland")), true);
    assert.equal(inRegion("6000", region("midJutland")), false);
    for (let z = 1000; z <= 9999; z += 37) {
        const hits = REGIONS.filter(r => inRegion(String(z), r)).length;
        assert.ok(hits <= 1, `postal code ${z} in ${hits} regions`);
    }
});

test("region filter rejects missing or malformed postal codes", () => {
    assert.equal(inRegion("", region("funen")), false);
    assert.equal(inRegion("DK-5000", region("funen")), false);
    assert.equal(inRegion("", undefined), true);
});

test("contract name prefers the alternative name", () => {
    assert.equal(contractName("  El i kælder ", "El-arbejde"), "El i kælder");
    assert.equal(contractName("", "El-arbejde"), "El-arbejde");
    assert.equal(contractName(null, "El-arbejde"), "El-arbejde");
});

test("deadline state", () => {
    const today = new Date(2026, 10, 20);
    assert.equal(deadlineState(null, today), "none");
    assert.equal(deadlineState("2026-11-19", today), "overdue");
    assert.equal(deadlineState("2026-11-20", today), "soon");
    assert.equal(deadlineState("2026-11-27", today), "soon");
    assert.equal(deadlineState("2026-11-28", today), "ok");
});

test("status summary counts per code, sorted", () => {
    assert.deepEqual(statusSummary([OPTION_BASE + 24, OPTION_BASE + 10, OPTION_BASE + 24, null]), [{ code: 10, count: 1 }, { code: 24, count: 2 }]);
});

test("evaluation summary and bad evaluation", () => {
    const s = summarizeEvaluations([OPTION_BASE, OPTION_BASE, OPTION_BASE + 2, null]);
    assert.deepEqual(s, { green: 2, yellow: 0, red: 1, black: 0 });
    assert.equal(hasBadEvaluation(s), true);
    assert.equal(hasBadEvaluation(summarizeEvaluations([OPTION_BASE, OPTION_BASE + 1])), false);
});

test("template rendering mirrors the server (EmailTemplateRenderer.cs)", () => {
    const v = { opportunity: "Havn", caseNumber: "", buildingContract: "El", tenderDeadline: "2026-12-04", partner: "Fyns El & <Co>", contactFirstNames: ["Anne", "Bo"], sender: "Søren" };
    assert.equal(renderTemplate("{{BuildingContract}} – {{Opportunity}} ({{CaseNumber}})", v, false), "El – Havn (–)");
    assert.equal(renderTemplate("<p>Kære {{ contactfirstname }} hos {{Partner}}</p>", v, true), "<p>Kære Anne og Bo hos Fyns El &amp; &lt;Co&gt;</p>");
    assert.equal(renderTemplate("{{TenderDeadline}}", v, false), "4. december 2026");
    assert.equal(renderTemplate("{{Ukendt}}", v, false), "{{Ukendt}}");
    assert.equal(formatDanishDate(null), "(ingen frist)");
});

test("joinNames", () => {
    assert.equal(joinNames([]), "alle");
    assert.equal(joinNames(["Anne", " ", "Anne"]), "Anne");
    assert.equal(joinNames(["Anne", "Bo", "Carl"]), "Anne, Bo og Carl");
});

test("sanitizeHtml removes scripts and handlers for the preview", () => {
    const out = sanitizeHtml('<p onclick="x()">Hej</p><script>alert(1)</script><a href="javascript:evil()">x</a>');
    assert.equal(out.includes("script"), false);
    assert.equal(out.includes("onclick"), false);
    assert.equal(out.includes("javascript:"), false);
    assert.ok(out.includes("<p>Hej</p>"));
});

test("escaping for OData and FetchXML", () => {
    assert.equal(odataString("O'Neil"), "O''Neil");
    assert.equal(xmlAttr(`a"b<c>&'`), "a&quot;b&lt;c&gt;&amp;&apos;");
});
