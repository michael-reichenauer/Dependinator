// Records the animated demo gif used by the VS Code extension README
// (src/DependinatorVsCode/resources/demo.gif).
//
// The scripted scenario (adjust the steps in run() below to change the demo):
//   1. Overview of the Dependinator.sln model (fit to screen).
//   2. Ctrl+F search for "ModelService" and select the result.
//   3. The diagram zooms to the node; the node toolbar appears.
//   4. "Show dependencies" opens the Dependencies explorer.
//   5. Tree items are expanded to drill into member-level dependencies.
//   6. The explorer is closed and the view zooms back out (Fit to Screen).
//
// Run via ./scripts/record-demo (repo root), which starts the app if needed and sets
// NODE_PATH so the globally installed playwright (bundled with @playwright/cli
// in the devcontainer) is resolvable. Requires ffmpeg.
//
// The app must be running at http://localhost:5000 (override with DEMO_URL)
// and must already have the Dependinator.sln model loaded/parsed.

import { createRequire } from "module";
import { execFileSync } from "child_process";
import { mkdtempSync, copyFileSync, statSync, rmSync } from "fs";
import { tmpdir } from "os";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const appUrl = process.env.DEMO_URL ?? "http://localhost:5000";
const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const outGif = process.argv[2] ?? join(repoRoot, "src/DependinatorVsCode/resources/demo.gif");

// Recorded at 1280x720, downscaled to the published 800x450 gif.
const viewport = { width: 1280, height: 720 };
const gifSize = "800:450";
const gifFps = 10;

const searchText = "ModelService";
const searchResultFullName = "Dependinator.UI.Modeling.ModelService";

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// A fake cursor (classic pointer arrow) that follows the Playwright mouse, so
// the gif shows what is being clicked (headless recordings have no cursor).
async function installFakeCursor(page) {
    await page.evaluate(() => {
        const cursor = document.createElement("div");
        cursor.id = "demo-cursor";
        cursor.style.cssText =
            "position:fixed;z-index:2147483647;left:-30px;top:-30px;width:18px;height:24px;pointer-events:none;";
        cursor.innerHTML =
            '<svg width="18" height="24" viewBox="0 0 18 24">' +
            '<path d="M1 1 L1 18 L5.5 14.5 L8.5 21 L11.5 19.5 L8.5 13 L14 12.5 Z" ' +
            'fill="white" stroke="black" stroke-width="1.4"/></svg>';
        document.body.appendChild(cursor);
        window.addEventListener(
            "mousemove",
            (e) => {
                cursor.style.left = e.clientX + "px";
                cursor.style.top = e.clientY + "px";
            },
            true
        );
    });
}

// Returns the bounding rect (viewport coordinates) of the element selected by
// pageFunction(arg), or null. pageFunction runs in the browser.
async function rectOf(page, pageFunction, arg) {
    return await page.evaluate(
        ({ fnSource, fnArg }) => {
            const el = eval(`(${fnSource})`)(fnArg);
            if (!el) return null;
            const r = el.getBoundingClientRect();
            return { x: r.x, y: r.y, w: r.width, h: r.height };
        },
        { fnSource: pageFunction.toString(), fnArg: arg }
    );
}

// Moves the fake cursor smoothly to the point and clicks.
async function moveAndClick(page, x, y) {
    await page.mouse.move(x, y, { steps: 30 });
    await wait(350);
    await page.mouse.down();
    await wait(80);
    await page.mouse.up();
}

async function clickRect(page, rect, xFraction = 0.5) {
    if (!rect) throw new Error("Element to click was not found");
    await moveAndClick(page, rect.x + rect.w * xFraction, rect.y + rect.h / 2);
}

const findTreeChevron = (name) => {
    const label = [...document.querySelectorAll(".mud-treeview p")].find(
        (p) => p.textContent.trim() === name
    );
    return label?.closest("li")?.querySelector("button.mud-treeview-item-expand-button");
};

const findTestId = (testId) => document.querySelector(`[data-testid=${testId}]`);

// Waits until the app has been idle (no progress indicators, e.g. from model
// parsing) for a few seconds in a row. A busy server makes the Blazor Server
// UI lag far behind the scripted inputs.
async function waitForQuiet(page, timeoutMs = 300000) {
    const start = Date.now();
    for (let quiet = 0; quiet < 6; ) {
        if (Date.now() - start > timeoutMs) throw new Error("App never became idle");
        await wait(1000);
        const busy = await page.evaluate(
            () => !!document.querySelector(".app-progress-discreet, .app-progress-overlay")
        );
        quiet = busy ? 0 : quiet + 1;
    }
}

async function fitToScreen(page, useCursor) {
    const menuButton = await rectOf(page, (id) => document.querySelector(`${id} button`), "[data-testid=appbar-menu]");
    if (useCursor) await clickRect(page, menuButton);
    else await page.evaluate(() => document.querySelector("[data-testid=appbar-menu] button").click());
    await wait(600);
    const fitItem = await rectOf(page, findTestId, "menu-fit-to-screen");
    if (useCursor) await clickRect(page, fitItem);
    else await page.evaluate(() => document.querySelector("[data-testid=menu-fit-to-screen]").click());
    await page.keyboard.press("Escape"); // close any lingering menu
    // Blur the menu button, so its "Menu" tooltip does not stay visible (the
    // short wait lets MudBlazor restore focus to the button first).
    await wait(500);
    await page.evaluate(() => document.activeElement?.blur());
}

async function run() {
    const workDir = mkdtempSync(join(tmpdir(), "dep-demo-"));
    // channel "chromium" uses the full chromium build (installed in the
    // devcontainer) instead of the headless-shell build, which may be missing.
    const browser = await chromium.launch({ channel: "chromium" });
    const context = await browser.newContext({
        viewport,
        recordVideo: { dir: workDir, size: viewport },
    });
    const page = await context.newPage();
    const videoStart = Date.now();

    console.log(`Loading ${appUrl} ...`);
    await page.goto(appUrl);
    await page.waitForSelector("#svgcanvas", { timeout: 30000 });
    await wait(4000); // let the model load and render

    // Hide snackbars (e.g. "Connection refused" when the cloud-sync API is not
    // running) — they are not part of the demo.
    await page.addStyleTag({ content: ".mud-snackbar { display: none !important; }" });
    await installFakeCursor(page);
    await page.mouse.move(640, 500);

    // Pre-roll (before the gif's first frame): navigate once into the model and
    // back. Fit to Screen only gives the nice opened-containers overview when
    // invoked while zoomed in (CanvasService.PanZoomToFit clamps maxZoom to the
    // current zoom), so navigate deep to a node first and wait until that
    // zoom-in animation (plus first-time rendering) has fully finished.
    // On a freshly started app the model may still be loading and the search
    // then finds nothing (it does not re-run when loading finishes) — retry
    // until results appear.
    for (let attempt = 0; ; attempt++) {
        await page.keyboard.press("Control+f");
        await page.waitForSelector(".search-dialog input");
        await wait(700); // let the dialog's autofocus land before typing
        await page.evaluate(() => document.querySelector(".search-dialog input")?.focus());
        await page.keyboard.type(searchText);
        const found = await page
            .waitForSelector(".search-dialog__item", { timeout: 5000 })
            .catch(() => null);
        if (found) break;
        if (attempt >= 30) throw new Error("Model never became searchable");
        await page.keyboard.press("Escape");
        await wait(3000);
    }
    await wait(400);
    await page.keyboard.press("Enter");
    // The node toolbar appears once the node is selected.
    await page.waitForSelector("[data-testid=node-dependencies]", { timeout: 30000 });
    await wait(5000); // let the zoom-in animation and rendering finish
    await fitToScreen(page, false);
    await wait(2000); // zoom-out animation
    await page.mouse.click(300, 660); // empty area: clear the node selection
    await wait(400);

    // Background model parsing may still be running (fresh app start); wait for
    // it to finish so the scene inputs are processed without lag.
    console.log("Waiting for the app to be idle ...");
    await waitForQuiet(page);

    // ---- Scene starts here -------------------------------------------------
    const sceneStart = Date.now();
    await wait(1600); // hold the overview

    // Search for a node with Ctrl+F.
    await page.keyboard.press("Control+f");
    await page.waitForSelector(".search-dialog input");
    await wait(700);
    await page.keyboard.type(searchText, { delay: 90 });
    await wait(1400);

    // Click the search result: the diagram zooms to the node and selects it.
    const result = await rectOf(
        page,
        (fullName) =>
            [...document.querySelectorAll(".search-dialog__item")].find(
                (item) => item.querySelector(".search-dialog__full")?.textContent === fullName
            ),
        searchResultFullName
    );
    await clickRect(page, result, 0.3);
    await wait(5000); // zoom animation
    await wait(1300); // hold: selected node with its toolbar

    // Open the Dependencies explorer from the node toolbar.
    await clickRect(page, await rectOf(page, findTestId, "node-dependencies"));
    await wait(2200);

    // Drill into the dependency tree, down to member level.
    await clickRect(page, await rectOf(page, findTreeChevron, "Models"));
    await wait(1800);
    await clickRect(page, await rectOf(page, findTreeChevron, "IModelMgr"));
    await wait(2600);

    // Close the explorer and zoom back out to the overview.
    await clickRect(page, await rectOf(page, findTestId, "explorer-close"));
    await wait(900);
    await fitToScreen(page, true);
    await page.mouse.move(640, 620, { steps: 20 }); // park the cursor
    await wait(2800); // zoom-out animation + final hold

    const sceneEnd = Date.now();
    // ---- Scene ends here ---------------------------------------------------

    await context.close();
    const videoPath = await page.video().path();
    await browser.close();

    const trimStart = Math.max(0, (sceneStart - videoStart) / 1000 - 0.2);
    const duration = (sceneEnd - sceneStart) / 1000;
    console.log(`Converting video to gif (start ${trimStart.toFixed(1)}s, ${duration.toFixed(1)}s) ...`);

    const gifTmp = join(workDir, "demo.gif");
    execFileSync("ffmpeg", [
        "-y", "-v", "error",
        "-i", videoPath,
        "-ss", trimStart.toString(),
        "-t", duration.toString(),
        "-vf",
        `fps=${gifFps},scale=${gifSize}:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=5`,
        "-loop", "0",
        gifTmp,
    ]);

    copyFileSync(gifTmp, outGif);
    rmSync(workDir, { recursive: true, force: true });
    const sizeMb = (statSync(outGif).size / 1024 / 1024).toFixed(1);
    console.log(`Wrote ${outGif} (${sizeMb} MB)`);
}

await run();
