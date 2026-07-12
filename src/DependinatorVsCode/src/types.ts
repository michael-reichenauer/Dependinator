/** Message envelope exchanged between the extension host and the webview-hosted WASM UI. */
export type WebviewMessage = {
    type: string;
    message?: unknown;
};
