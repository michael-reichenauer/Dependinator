// Imports curated cloud-provider icons (Azure, AWS, Google) into the embedded icon library
// (src/Dependinator.UI/Diagrams/Icons/Library/<Azure|Aws|Google>/<Name>.svg).
//
// Sources are the official free icon packages (see URLS below); the curated selection lives in
// scripts/cloud-icons/<provider>.manifest ("<source-key> = <Target-Name>" lines). Each svg is
// normalized to the library's invariants: root <svg id> equals the icon name, internal ids are
// icon-prefixed (all icons share one <defs> in the diagram DOM), Google's <style> classes are
// inlined as presentation attributes, and <title>/width/height are stripped.
//
// Usage: ./import-icons [--provider azure|aws|google] [--force-download] [--check]
//   --check verifies the manifests and normalization without writing to the library.

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const cacheDir = path.join(repoRoot, ".cache", "cloud-icons");
const manifestDir = path.join(repoRoot, "scripts", "cloud-icons");
const libraryDir = path.join(repoRoot, "src", "Dependinator.UI", "Diagrams", "Icons", "Library");

// Official icon package urls. The AWS url is hash-versioned and rotates quarterly; on a 404,
// look up the current "Asset package" link at https://aws.amazon.com/architecture/icons/.
const PROVIDERS = {
    azure: {
        group: "Azure",
        // V24, July 2026 — https://learn.microsoft.com/en-us/azure/architecture/icons/
        url: "https://arch-center.azureedge.net/icons/Azure_Public_Service_Icons_V24.zip",
    },
    aws: {
        group: "Aws",
        // Q2-2026 (04302026) — https://aws.amazon.com/architecture/icons/
        url: "https://d1.awsstatic.com/onedam/marketing-channels/website/aws/en_US/architecture/approved/architecture-icons/Icon-package_04302026.4705b90f5aa45b019271a2699e9ce9b97b941ee1.zip",
    },
    google: {
        group: "Google",
        // Legacy per-product set (2021) — https://cloud.google.com/icons
        url: "https://services.google.com/fh/files/misc/google-cloud-legacy-icons.zip",
    },
};

const args = process.argv.slice(2);
const checkOnly = args.includes("--check");
const forceDownload = args.includes("--force-download");
const providerArg = args.includes("--provider") ? args[args.indexOf("--provider") + 1] : null;
if (providerArg && !PROVIDERS[providerArg]) {
    fail(`Unknown provider '${providerArg}' (expected: ${Object.keys(PROVIDERS).join(", ")})`);
}
const providers = providerArg ? [providerArg] : Object.keys(PROVIDERS);

function fail(message) {
    console.error(`import-cloud-icons: ${message}`);
    process.exit(1);
}

async function download(provider) {
    const zipPath = path.join(cacheDir, `${provider}.zip`);
    if (fs.existsSync(zipPath) && !forceDownload) return zipPath;

    const url = PROVIDERS[provider].url;
    console.log(`Downloading ${provider} icons from ${url}`);
    const response = await fetch(url);
    if (!response.ok) {
        fail(
            `Download failed for ${provider} (${response.status} ${response.statusText}). ` +
                `The package url may have rotated — update the url in scripts/import-cloud-icons.mjs.`
        );
    }
    fs.mkdirSync(cacheDir, { recursive: true });
    fs.writeFileSync(zipPath, Buffer.from(await response.arrayBuffer()));
    return zipPath;
}

function extract(provider, zipPath) {
    const extractDir = path.join(cacheDir, provider);
    const stampPath = path.join(extractDir, ".extracted");
    if (fs.existsSync(stampPath) && !forceDownload) return extractDir;

    fs.rmSync(extractDir, { recursive: true, force: true });
    fs.mkdirSync(extractDir, { recursive: true });
    execFileSync("unzip", ["-oq", zipPath, "-d", extractDir]);
    fs.writeFileSync(stampPath, "");
    return extractDir;
}

function* walkSvgs(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const entryPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            if (entry.name === "__MACOSX") continue;
            yield* walkSvgs(entryPath);
        } else if (entry.name.endsWith(".svg")) {
            yield entryPath;
        }
    }
}

// Builds a map of version-stable source key -> [file paths] for one provider. Keys:
// Azure "App-Services" (the name after "-icon-service-", numeric prefixes change per release),
// AWS "Amazon-EC2" (from Architecture-Service-Icons .../48/Arch_<key>_48.svg),
// Google "cloud_run" (the snake_case file name).
function indexSvgs(provider, extractDir) {
    const index = new Map();
    const add = (key, filePath) => {
        if (!index.has(key)) index.set(key, []);
        index.get(key).push(filePath);
    };

    for (const filePath of walkSvgs(extractDir)) {
        const base = path.basename(filePath, ".svg");
        if (provider === "azure") {
            const match = base.match(/^\d+-icon-service-(.+)$/);
            if (match) add(match[1], filePath);
        } else if (provider === "aws") {
            const match = base.match(/^Arch_(.+)_48$/);
            if (match && filePath.includes("Architecture-Service-Icons") && filePath.includes(`${path.sep}48${path.sep}`))
                add(match[1], filePath);
        } else {
            add(base, filePath);
        }
    }
    return index;
}

// Parses "<source-key> = <Target-Name>" manifest lines ("#" starts a comment). An Azure key may
// be qualified as "<category-dir>/<key>" to disambiguate same-key files with differing content.
function parseManifest(provider) {
    const manifestPath = path.join(manifestDir, `${provider}.manifest`);
    if (!fs.existsSync(manifestPath)) fail(`Missing manifest ${manifestPath}`);

    const entries = [];
    for (const [lineNumber, rawLine] of fs.readFileSync(manifestPath, "utf8").split("\n").entries()) {
        const line = rawLine.replace(/#.*$/, "").trim();
        if (!line) continue;
        const match = line.match(/^(\S+)\s*=\s*(\S+)$/);
        if (!match) fail(`${manifestPath}:${lineNumber + 1}: expected "<source-key> = <Target-Name>", got "${rawLine.trim()}"`);
        entries.push({ key: match[1], name: match[2], where: `${path.basename(manifestPath)}:${lineNumber + 1}` });
    }
    return entries;
}

function resolveSource(index, entry) {
    const [dir, key] = entry.key.includes("/") ? entry.key.split("/", 2) : [null, entry.key];
    let paths = index.get(key) ?? [];
    if (dir) paths = paths.filter((p) => path.basename(path.dirname(p)) === dir);
    if (paths.length === 0) fail(`${entry.where}: no source icon found for key '${entry.key}'`);

    // The Azure package duplicates icons across category folders; identical content is fine,
    // differing content needs a "<category-dir>/<key>" qualified key.
    const contents = paths.map((p) => fs.readFileSync(p, "utf8"));
    if (!contents.every((c) => c === contents[0])) {
        const dirs = paths.map((p) => path.basename(path.dirname(p)));
        fail(`${entry.where}: key '${entry.key}' matches differing files; qualify it as one of: ${dirs.map((d) => `${d}/${key}`).join(", ")}`);
    }
    return contents[0];
}

// --- svg normalization ---------------------------------------------------------------------

function normalizeSvg(svg, name, where) {
    svg = svg.replace(/<\?xml[^>]*\?>/g, "");
    svg = svg.replace(/<!DOCTYPE[^>]*>/g, "");
    svg = svg.replace(/<!--[\s\S]*?-->/g, "");
    svg = svg.replace(/<title>[\s\S]*?<\/title>/g, "");

    svg = inlineStyles(svg, where);
    svg = rewriteIds(svg, name, where);

    // Set the root id and drop the fixed size: the diagram sizes icons via <use width/height>
    // and the picker via css, both against the viewBox.
    svg = svg.replace(/<svg([^>]*)>/, (_, attrs) => {
        attrs = attrs.replace(/\s+(id|width|height)="[^"]*"/g, "");
        return `<svg id="${name}"${attrs}>`;
    });

    // Single line, matching the hand-authored library files.
    svg = svg.replace(/[\r\n\t]+/g, " ").replace(/>\s+</g, "><").trim();
    return svg + "\n";
}

// Converts a <style> block of ".cls-N { decls }" rules (Google legacy icons) into presentation
// attributes on the classed elements, so icons can't leak css rules into the shared diagram DOM.
function inlineStyles(svg, where) {
    const styleMatch = svg.match(/<style[^>]*>([\s\S]*?)<\/style>/);
    if (!styleMatch) return svg;

    const rules = new Map();
    for (const rule of styleMatch[1].split("}")) {
        if (!rule.trim()) continue;
        const [selectors, declarations] = rule.split("{");
        if (declarations === undefined) fail(`${where}: cannot parse style rule "${rule.trim()}"`);
        for (const selector of selectors.split(",")) {
            const className = selector.trim().match(/^\.([\w-]+)$/)?.[1];
            if (!className) fail(`${where}: unsupported style selector "${selector.trim()}" (only single class selectors)`);
            if (!rules.has(className)) rules.set(className, new Map());
            for (const declaration of declarations.split(";")) {
                if (!declaration.trim()) continue;
                const [property, ...value] = declaration.split(":");
                if (value.length === 0) fail(`${where}: cannot parse style declaration "${declaration.trim()}"`);
                rules.get(className).set(property.trim(), value.join(":").trim());
            }
        }
    }

    svg = svg.replace(styleMatch[0], "");
    svg = svg.replace(/<defs>\s*<\/defs>/g, "");

    return svg.replace(/<([a-zA-Z][^>]*?)\s+class="([^"]*)"([^>]*?)(\/?)>/g, (tag, before, classes, after, selfClose) => {
        const attributes = new Map();
        for (const className of classes.trim().split(/\s+/)) {
            const declarations = rules.get(className);
            if (!declarations) fail(`${where}: class "${className}" has no style rule`);
            for (const [property, value] of declarations) attributes.set(property, value);
        }
        let result = `<${before}${after}`;
        for (const [property, value] of attributes) {
            if (!new RegExp(`\\s${property}="`).test(result)) result += ` ${property}="${value}"`;
        }
        return `${result}${selfClose}>`;
    });
}

// All icons share one <defs> in the diagram DOM, so internal ids must be globally unique:
// referenced ids get an icon-name prefix, unreferenced ids (e.g. AWS's id="Rectangle" in every
// file) are dropped. The root <svg> id is rewritten separately in normalizeSvg.
function rewriteIds(svg, name, where) {
    const ids = [...svg.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1]);
    const referenced = new Set(
        [...svg.matchAll(/url\(#([^)]+)\)/g), ...svg.matchAll(/(?:xlink:)?href="#([^"]+)"/g)].map((match) => match[1])
    );

    let counter = 0;
    for (const id of ids) {
        const escaped = id.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        if (referenced.has(id)) {
            counter++;
            svg = svg
                .replaceAll(`id="${id}"`, `id="${name}-i${counter}"`)
                .replace(new RegExp(`url\\(#${escaped}\\)`, "g"), `url(#${name}-i${counter})`)
                .replace(new RegExp(`((?:xlink:)?href=)"#${escaped}"`, "g"), `$1"#${name}-i${counter}"`);
        } else {
            svg = svg.replace(new RegExp(`\\sid="${escaped}"`), "");
        }
    }

    for (const reference of referenced) {
        if (!ids.includes(reference)) fail(`${where}: reference to missing id "#${reference}"`);
    }
    return svg;
}

// --- validation ----------------------------------------------------------------------------

function validate(icons) {
    const errors = [];

    // Names must be unique across the whole library, including the hand-authored groups.
    const names = new Map();
    for (const group of fs.readdirSync(libraryDir, { withFileTypes: true })) {
        if (!group.isDirectory() || Object.values(PROVIDERS).some((p) => p.group === group.name)) continue;
        for (const file of fs.readdirSync(path.join(libraryDir, group.name))) {
            if (file.endsWith(".svg")) names.set(path.basename(file, ".svg"), group.name);
        }
    }
    for (const icon of icons) {
        if (names.has(icon.name)) errors.push(`${icon.where}: name '${icon.name}' already used in group ${names.get(icon.name)}`);
        names.set(icon.name, icon.group);
    }

    for (const icon of icons) {
        const { svg, name, where } = icon;
        if (!svg.startsWith(`<svg id="${name}"`)) errors.push(`${where}: root <svg id> does not equal '${name}'`);
        if (!svg.includes("viewBox=")) errors.push(`${where}: missing viewBox`);
        if (/<style|class="|<title/.test(svg)) errors.push(`${where}: unnormalized <style>/class/<title> content remains`);
        for (const [, reference] of [...svg.matchAll(/url\(#([^)]+)\)/g), ...svg.matchAll(/(?:xlink:)?href="#([^"]+)"/g)]) {
            if (!svg.includes(`id="${reference}"`)) errors.push(`${where}: unresolved reference "#${reference}"`);
        }
    }

    if (errors.length > 0) fail(`validation failed:\n  ${errors.join("\n  ")}`);
}

// --- main ----------------------------------------------------------------------------------

const allIcons = [];
for (const provider of providers) {
    const zipPath = await download(provider);
    const extractDir = extract(provider, zipPath);
    const index = indexSvgs(provider, extractDir);
    for (const entry of parseManifest(provider)) {
        const source = resolveSource(index, entry);
        allIcons.push({
            group: PROVIDERS[provider].group,
            name: entry.name,
            where: entry.where,
            svg: normalizeSvg(source, entry.name, entry.where),
        });
    }
}

validate(allIcons);

if (checkOnly) {
    console.log(`Check ok: ${allIcons.length} icons (${providers.join(", ")})`);
    process.exit(0);
}

for (const provider of providers) {
    const groupDir = path.join(libraryDir, PROVIDERS[provider].group);
    fs.rmSync(groupDir, { recursive: true, force: true });
    fs.mkdirSync(groupDir, { recursive: true });
}
for (const icon of allIcons) {
    fs.writeFileSync(path.join(libraryDir, icon.group, `${icon.name}.svg`), icon.svg);
}

const counts = providers.map((p) => `${PROVIDERS[p].group}: ${allIcons.filter((i) => i.group === PROVIDERS[p].group).length}`);
console.log(`Imported ${allIcons.length} icons (${counts.join(", ")})`);
