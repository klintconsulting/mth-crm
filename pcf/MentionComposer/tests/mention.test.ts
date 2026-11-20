// Run: npm test  (node --experimental-strip-types --test tests/)
import { test } from "node:test";
import assert from "node:assert/strict";
import { findActiveToken, insertMention, mentionedUsers, toNoteHtml, userQuery } from "../MentionComposer/mention.ts";
import type { MentionUser } from "../MentionComposer/mention.ts";

const soeren: MentionUser = { id: "1", fullName: "Søren Klint", email: "sk@mth.dk" };
const anne: MentionUser = { id: "2", fullName: "Anne Hansen", email: "ah@mth.dk" };

test("token at start of text", () => {
    assert.deepEqual(findActiveToken("@so", 3), { start: 0, query: "so" });
});

test("bare @ opens picker with empty query", () => {
    assert.deepEqual(findActiveToken("Hej @", 5), { start: 4, query: "" });
});

test("token in the middle of the text, caret right after it", () => {
    const text = "Hej @an og resten";
    assert.deepEqual(findActiveToken(text, 7), { start: 4, query: "an" });
});

test("query may contain one space (first and last name)", () => {
    assert.deepEqual(findActiveToken("Kan @Søren K", 12), { start: 4, query: "Søren K" });
});

test("e-mail address is not a token", () => {
    assert.equal(findActiveToken("skriv til kontakt@sk", 20), null);
});

test("no token when caret is after a finished word and space", () => {
    assert.equal(findActiveToken("@Søren Klint tak ", 17), null);
});

test("insertMention replaces the token and moves the caret", () => {
    const text = "Hej @so, se sagen";
    const token = findActiveToken(text, 7)!;
    const r = insertMention(text, token, 7, soeren);
    assert.equal(r.text, "Hej @Søren Klint , se sagen");
    assert.equal(r.caret, "Hej @Søren Klint ".length);
});

test("several mentions anywhere in the text", () => {
    let text = "@an og ";
    let r = insertMention(text, findActiveToken(text, 3)!, 3, anne);
    text = r.text + "skal @s" + "";
    const caret = text.length;
    r = insertMention(text, findActiveToken(text, caret)!, caret, soeren);
    assert.equal(r.text, "@Anne Hansen  og skal @Søren Klint ");
    assert.deepEqual(mentionedUsers(r.text, [anne, soeren]).map(u => u.id), ["2", "1"]);
});

test("mentionedUsers drops users whose mention was deleted", () => {
    assert.deepEqual(mentionedUsers("Kun @Anne Hansen her", [anne, soeren]).map(u => u.id), ["2"]);
});

test("mentionedUsers does not match a longer name", () => {
    assert.deepEqual(mentionedUsers("@Anne Hansenberg", [anne]), []);
});

test("toNoteHtml escapes and keeps line breaks", () => {
    assert.equal(toNoteHtml("a < b & c\n\n\"d\""), "<div>a &lt; b &amp; c</div><div>&nbsp;</div><div>&quot;d&quot;</div>");
});

test("userQuery escapes quotes and filters active users", () => {
    const q = userQuery("O'Neil", 8);
    assert.match(q, /contains\(fullname,'O''Neil'\)/);
    assert.match(q, /isdisabled eq false and accessmode eq 0/);
    assert.match(q, /\$top=8$/);
});

test("userQuery without text lists users", () => {
    assert.ok(!userQuery("", 8).includes("contains("));
});
