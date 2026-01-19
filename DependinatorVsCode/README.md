# Dependinator VS Code Extension

This extension hosts the Dependinator Blazor WASM UI inside a VS Code webview.

## Local development

1. Build and copy the WASM assets into `DependinatorVsCode/media`:
   - `scripts/prepare-wasm.sh`
2. In `DependinatorVsCode`, install dependencies and compile:
   - `npm install`
   - `npm run compile`
3. Press F5 in VS Code to launch the extension host.
4. Run the `Dependinator: Open` command.

## Notes

- In vscode.dev, the UI can load, but the .NET language server will be disabled.
- The default script disables native WASM runtime compilation to avoid needing Python.
- Use `scripts/prepare-wasm.sh --aot` if you want full AOT and compressed artifacts (requires Python).
- `publisher` in `package.json` must match your VS Code Marketplace publisher name before publishing.
- Packaging requires the .NET 10 SDK (see `global.json`) and Node 20+.

## Build a VSIX locally

1. From `DependinatorVsCode/`, install dependencies:
   - `npm install`
2. Create the VSIX (builds WASM assets, publishes the language server, compiles TS):
   - `npm run package`
3. The output is `DependinatorVsCode/dependinator-<version>.vsix`.

## Publish to the Marketplace

1. Update `DependinatorVsCode/package.json`:
   - Set `publisher` to your Marketplace publisher ID.
   - Increment `version`.
2. Create a Personal Access Token (VSCE) and publish:
   - `npx vsce publish -p <your_token>`

## GitHub workflow

The `Build VS Code Extension` workflow builds the VSIX artifact.
To publish from CI, add the `VSCE_PAT` secret and trigger the workflow with `publish` set to true.
