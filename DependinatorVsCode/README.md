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
- `publisher` in `package.json` should be updated before publishing to the marketplace.
