// Runs the Ask panel's eval questions (scripts/ask-eval.json) in the running dev host and scores
// the answers: see scripts/ask-eval for how to run it.
import { createRequire } from "module";
import { mkdirSync, readFileSync, writeFileSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const baseUrl = process.env.ASK_EVAL_URL ?? "http://localhost:5000";
const answerTimeout = 5 * 60_000;

const args = process.argv.slice(2);
const route = args.includes("-c") ? "code" : "claude";
const only = args.filter((a) => !a.startsWith("-"));
const { cases: allCases } = JSON.parse(readFileSync(join(repoRoot, "scripts/ask-eval.json"), "utf8"));
const cases = only.length > 0 ? allCases.filter((c) => only.includes(c.name)) : allCases;
if (cases.length === 0) {
  console.error(`No case named ${only.join(", ")}`);
  process.exit(2);
}

// The answer names a node when its name (dots for the parser's separators, no parameters) has
// the expected parts in a row: "Core.Parsing.ParserService" names ParserService and its members.
const namesNode = (name, expected) =>
  `.${name.replaceAll("*", ".").split("(")[0]}.`.includes(`.${expected}.`);

function score(question, answer) {
  const missing = [];
  for (const expected of question.nodes ?? []) {
    const alternatives = expected.split("|");
    if (!answer.names.some((n) => alternatives.some((a) => namesNode(n, a)))) missing.push(expected);
  }
  if (question.text && !new RegExp(question.text, "i").test(answer.text)) missing.push(`text /${question.text}/`);
  return missing;
}

// The model parses in steps after the page opens; the counts stop changing when it is done.
async function waitForModel(page) {
  let last = "";
  for (let i = 0; i < 120; i++) {
    const response = await page.request.post(`${baseUrl}/mcp`, {
      headers: { "content-type": "application/json", accept: "application/json, text/event-stream" },
      data: { jsonrpc: "2.0", id: 1, method: "tools/call", params: { name: "get_model", arguments: {} } },
    });
    const body = await response.text();
    const json = JSON.parse(body.match(/data: (.*)/)?.[1] ?? body);
    const text = json.result?.content?.[0]?.text ?? "";
    const counts = text.startsWith("{") ? `${JSON.parse(text).nodeCount}/${JSON.parse(text).linkCount}` : "";
    if (counts !== "" && counts === last) return counts;
    last = counts;
    await page.waitForTimeout(5000);
  }
  throw new Error("The model did not finish loading");
}

async function setUseClaudeCode(page, on) {
  await page.getByTestId("appbar-menu").click();
  await page.getByTestId("menu-settings").hover();
  const item = page.getByTestId("menu-use-claude-code");
  await item.waitFor();
  const isOn = (await item.getAttribute("data-checked")) === "true";
  if (isOn === on) {
    await page.keyboard.press("Escape");
    return isOn;
  }
  await item.click();
  if (on) await page.getByTestId("use-claude-code-confirm").click();
  await page.waitForTimeout(1000); // The setting is saved after the click returns.
  return isOn;
}

async function ask(page, question) {
  const done = page.locator('[data-testid="ask-answer"], [data-testid="ask-error"]');
  const before = await done.count();
  const started = Date.now();
  await page.getByTestId("ask-question").fill(question);
  await page.getByTestId("ask-send").click();
  await done.nth(before).waitFor({ timeout: answerTimeout });
  const seconds = Math.round((Date.now() - started) / 1000);
  const entry = page.getByTestId("ask-entry").last();
  if ((await entry.getByTestId("ask-error").count()) > 0)
    return { error: await entry.getByTestId("ask-error").innerText(), seconds, names: [], text: "" };
  const text = await entry.getByTestId("ask-answer-text").innerText();
  const rows = await entry.getByTestId("ask-answer-node-name").allInnerTexts();
  const titles = await entry.getByTestId("ask-node-link").evaluateAll((links) => links.map((l) => l.title));
  const meta = (await entry.getByTestId("ask-answer-meta").innerText()).replace(/\s+/g, " ");
  return { seconds, text, meta, names: [...rows, ...titles.map((t) => t.replace(/^Go to /, ""))] };
}

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
await page.goto(baseUrl);
const counts = await waitForModel(page);
const wasClaudeCodeOn = route === "code" ? await setUseClaudeCode(page, true) : null;
console.log(`Asking ${cases.length} cases on the ${route === "code" ? "Claude Code" : "Claude API"} route (model ${counts} nodes/links)\n`);

const results = [];
try {
  for (const testCase of cases) {
    // A new page per case: a new conversation.
    await page.goto(baseUrl);
    await page.getByTestId("toolbar-ask").click();
    if (route === "code") await page.getByTestId("ask-route-code").click();
    for (const question of testCase.questions) {
      const answer = await ask(page, question.question);
      const missing = answer.error ? [`error: ${answer.error}`] : score(question, answer);
      const passed = missing.length === 0;
      results.push({ case: testCase.name, question: question.question, passed, missing, ...answer });
      console.log(`${passed ? "✓" : "✗"} ${testCase.name}: ${question.question}`);
      console.log(`    ${answer.seconds} s${answer.meta ? ` · ${answer.meta}` : ""}${passed ? "" : ` · missing ${missing.join(", ")}`}`);
    }
  }
} finally {
  if (wasClaudeCodeOn === false) await setUseClaudeCode(page, false);
  await browser.close();
}

const passed = results.filter((r) => r.passed).length;
const seconds = results.reduce((sum, r) => sum + r.seconds, 0);
const cents = results.reduce((sum, r) => sum + Number(r.meta?.match(/([\d.]+)¢/)?.[1] ?? 0), 0);
console.log(`\n${passed}/${results.length} answers passed · ${seconds} s${cents > 0 ? ` · ${cents.toFixed(1)}¢` : ""}`);
const folder = join(repoRoot, "tests", "ask-eval-results");
mkdirSync(folder, { recursive: true });
const file = join(folder, `${route}-${new Date().toISOString().slice(0, 16).replace(":", "")}.json`);
writeFileSync(file, JSON.stringify({ route, counts, results }, null, 2));
console.log(`Answers in ${file}`);
process.exit(passed === results.length ? 0 : 1);
