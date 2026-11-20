# Dependinator VS Code Extension — Development

This document is for developers working on the extension itself. For an end-user
overview of what the extension does and how to use it, see [README.md](README.md).

This extension hosts the Dependinator Blazor WASM UI inside a VS Code webview.

## Local development

1. Build and copy the WASM assets into `Dependinator.VsCodeExt/media`:
   - `scripts/prepare-wasm.sh`
2. In `Dependinator.VsCodeExt`, install dependencies and compile:
   - `npm install`
   - `npm run compile`
3. Press F5 in VS Code to launch the extension host.
4. Run the `Dependinator: Open` command.

The debug window runs on your normal VS Code profile, so it shares your user settings and the
stored secrets (the device sync sign-in, the Claude API key) with the installed extension. The
Dependinator settings are its own: `debug.code-workspace`, the workspace it opens, sets them, and
a workspace setting wins over a user setting. Device sync there uses the Dev site
(`dependinator.cloudSync.baseUrl`), the installed extension Production. Changing one of these
settings in the debug window edits `debug.code-workspace`: revert the file rather than commit a
personal choice.

## Notes

- In vscode.dev, the UI can load, but the .NET language server will be disabled.
- A GitHub codespace opened in the browser is not vscode.dev: the extension runs in Node on the codespace and starts the language server as it does in the desktop app. Which of the two it is comes from the bundle (`IS_WEB_EXTENSION_HOST`, set in `esbuild.js`), never from `vscode.env.uiKind`, which is `Web` in both.
- Use `Dependinator: Install in Dev Container` to jump to the Extensions view and install the extension in a dev container.
- WASM assets are published in Release without AOT.
- `publisher` in `package.json` must match your VS Code Marketplace publisher name before publishing.
- Packaging requires the .NET 11 SDK pinned in `global.json` (the language server and WASM target `net11.0`) plus the .NET 10 runtime for CSharpier, and Node 22+ (`@vscode/vsce` pulls in Azure packages that require it).
- Installed extensions use a self-contained language server, so `dotnet` is not required at runtime.

## Build a VSIX locally

1. From `Dependinator.VsCodeExt/`, install dependencies:
   - `npm install`
2. Create a per-platform VSIX (builds WASM assets, publishes the language server, compiles TS):
   - `npm run package:linux-x64`
   - `npm run package:win32-x64`
   - `npm run package:darwin-arm64`
   - `npm run package:darwin-x64`
3. The output is `Dependinator.VsCodeExt/dependinator-<version>.vsix`.

## Publish to the Marketplace

1. Update `Dependinator.VsCodeExt/package.json`:
   - Set `publisher` to your Marketplace publisher ID.
   - Increment `version`.
2. Create a Personal Access Token (VSCE) and publish:
   - `npx vsce publish -p <your_token>`

## GitHub workflow

The `Build VS Code Extension` workflow builds a VSIX per target.
To publish from CI, add the `VSCE_PAT` secret and trigger the workflow with `publish` set to true.
