import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import {
    registerLanguageClientLogging,
    registerUiMessageForwarding,
    startLanguageServer
} from "./languageServer";
import type { WebviewMessage } from "./types";
import { createDependinatorWebviewPanel, registerWebviewMessageHandler } from "./webview";

const commandId = "dependinator.open";

// Track the LSP client and the last active webview to route notifications.
let languageClient: LanguageClient | undefined;
let languageClientPromise: Promise<LanguageClient | undefined> | undefined;
let activePanel: vscode.WebviewPanel | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    // Web extensions can't start a .NET process, so skip the server there.
    const isWeb = vscode.env.uiKind === vscode.UIKind.Web;
    languageClientPromise = isWeb
        ? Promise.resolve(undefined)
        : startLanguageServer(context).catch(error => {
            console.error("Dependinator language server failed to start", error);
            return undefined;
        });

    languageClientPromise.then(client => {
        languageClient = client;
        if (!client)
            return;

        registerLanguageClientLogging(client);
        registerUiMessageForwarding(client, () => activePanel?.webview);
    });

    // Command opens the webview hosting the WASM UI.
    const disposable = vscode.commands.registerCommand(commandId, () => {
        const panel = createDependinatorWebviewPanel(context);

        activePanel = panel;
        panel.onDidDispose(() => {
            if (activePanel === panel)
                activePanel = undefined;
        });

        registerWebviewMessageHandler(panel, async (message: WebviewMessage) => {
            if (message.type !== "lsp/message")
                return;
            // console.log("lsp/message message:", message);

            // Messages from WebView UI to language server
            const client = await languageClientPromise;
            if (!client) {
                console.error("No client to send", message);
                // Forward error message back to WebvView UI
                panel.webview.postMessage({
                    type: "ui/error",
                    message: "Language server unavailable"
                });
                return;
            }

            try {
                // Forward messages to language server (from WebView UI)
                await client.sendNotification("lsp/message", {
                    message: message.message
                });
            } catch (error) {
                console.error("Failed to send", message);
                // Forward error message back to WebvView UI
                panel.webview.postMessage({
                    type: "ui/error",
                    message: String(error)
                });
            }
        });
    });

    context.subscriptions.push(disposable);
}

export async function deactivate(): Promise<void> {
    if (languageClient) {
        await languageClient.stop();
    }
}
