// Bundles the extension with esbuild so vscode-languageclient and the other
// production dependencies are inlined into the output instead of shipped as
// loose node_modules files. This clears the "you should bundle your extension"
// warning from vsce and reduces the file count in the .vsix.
//
// Two targets are produced:
//   dist/node/extension.js  - desktop host (Node), used via package.json "main"
//   dist/web/extension.js   - web host (browser), used via package.json "browser"
//
// languageServer.ts and cloudSyncAuth.ts use Node-only APIs (node:http,
// node:crypto, vscode-languageclient/node). They are only reached behind the
// `isWeb` guard via dynamic import(), so for the web bundle they are marked
// external (never executed there); for the node bundle they bundle normally.

const esbuild = require("esbuild");
const fs = require("node:fs");

const watch = process.argv.includes("--watch");
const production = process.argv.includes("--production");

// Remove previous output so stale per-file builds are never packaged.
fs.rmSync("dist", { recursive: true, force: true });

/** @type {import('esbuild').BuildOptions} */
const common = {
    entryPoints: ["src/extension.ts"],
    bundle: true,
    sourcemap: !production,
    minify: production,
    logLevel: "info",
};

const nodeConfig = {
    ...common,
    platform: "node",
    format: "cjs",
    target: "node18",
    outfile: "dist/node/extension.js",
    external: ["vscode"],
};

const webConfig = {
    ...common,
    platform: "browser",
    format: "cjs",
    target: "es2020",
    outfile: "dist/web/extension.js",
    // vscode is provided by the host; the Node-only modules below are only
    // dynamically imported on the desktop host and never executed on web.
    external: ["vscode", "node:http", "node:crypto", "vscode-languageclient/node"],
};

async function main() {
    if (watch) {
        const [nodeCtx, webCtx] = await Promise.all([
            esbuild.context(nodeConfig),
            esbuild.context(webConfig),
        ]);
        await Promise.all([nodeCtx.watch(), webCtx.watch()]);
        console.log("esbuild watching...");
        return;
    }

    await Promise.all([esbuild.build(nodeConfig), esbuild.build(webConfig)]);
}

main().catch(error => {
    console.error(error);
    process.exit(1);
});
