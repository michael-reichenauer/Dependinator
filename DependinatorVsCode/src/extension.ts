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
const installInDevContainerCommandId = "dependinator.installInDevContainer";
const extensionId = "michaelreichenauer.dependinator";

// Track the LSP client and the last active webview to route notifications.
let languageClient: LanguageClient | undefined;
let languageClientPromise: Promise<LanguageClient | undefined> | undefined;
let activePanel: vscode.WebviewPanel | undefined;
let lspReady = false;
let resolveLspReady: (() => void) | undefined;
const lspReadyPromise = new Promise<void>(resolve => {
    resolveLspReady = resolve;
});

function isCSharpDocument(document: vscode.TextDocument): boolean {
    if (document.languageId === "csharp")
        return true;

    return document.fileName.toLowerCase().endsWith(".cs");
}

function getDocumentPath(document: vscode.TextDocument): string | undefined {
    if (document.uri.scheme === "file" || document.uri.scheme === "vscode-remote")
        return document.uri.fsPath;

    const uri = document.uri.toString();
    return uri.length > 0 ? uri : undefined;
}

function sendShowNodeForEditor(editor: vscode.TextEditor | undefined): void {
    console.log("DPR: sendShowNodeForEditor");
    if (!editor || !activePanel) {
        console.log("DPR: No editor", editor, activePanel);
        return;
    }
    // if (vscode.window.activeTextEditor !== editor) {
    //     console.log("DPR: Not not same text editor", vscode.window.activeTextEditor, editor);
    //     return;
    // }
    if (!isCSharpDocument(editor.document)) {
        console.log("DPR: Not not C#");
        return;
    }

    const path = getDocumentPath(editor.document);
    if (!path) {
        console.log("DPR: No path");
        return;
    }

    const line = editor.selection.active.line + 1;
    activePanel.webview.postMessage({
        type: "ui/ShowNode",
        message: `${path}@${line}`
    });
    console.log("DPR: Posted ui/ShowNode");
}

function markLspReady(_params: unknown): void {
    if (lspReady)
        return;

    lspReady = true;
    resolveLspReady?.();
    resolveLspReady = undefined;
}

async function waitForLspReady(): Promise<void> {
    if (lspReady)
        return;
    await lspReadyPromise;
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    console.log("DPR: #### Activate extension");
    const installCommand = vscode.commands.registerCommand(installInDevContainerCommandId, async () => {
        console.log("DPR: Running command installInDevContainerCommandId");
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri;
        const isRemoteWorkspace = !!workspaceFolder && workspaceFolder.scheme !== "file";
        const isRemoteExtensionHost = context.extensionUri.scheme !== "file";

        if (isRemoteExtensionHost) {
            vscode.window.showInformationMessage("Dependinator is already installed in the dev container.");
            return;
        }

        if (!isRemoteWorkspace) {
            vscode.window.showInformationMessage(
                "Open a Dev Container workspace to install Dependinator in the container."
            );
            return;
        }

        try {
            await vscode.commands.executeCommand("workbench.view.extensions");
            await vscode.commands.executeCommand("workbench.extensions.search", `@id:${extensionId}`);
        } catch (error) {
            console.warn("Failed to open Extensions view for dev container install.", error);
            await vscode.commands.executeCommand("workbench.view.extensions");
        }

        vscode.window.showInformationMessage(
            "In the Extensions view, select Dependinator and choose Install in Dev Container."
        );
    });
    context.subscriptions.push(installCommand);
    console.log("DPR: vscode.env.uiKind", vscode.env.uiKind);
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
        registerUiMessageForwarding(client, () => activePanel?.webview, markLspReady);
    });

    // Command opens the webview hosting the WASM UI.
    const disposable = vscode.commands.registerCommand(commandId, async () => {
        console.log("DPR: Running command Open Dependinator ...");
        const activeTextEditor = vscode.window.activeTextEditor;
        if (activePanel) {
            activePanel.reveal(activePanel.viewColumn ?? vscode.ViewColumn.One);
            sendShowNodeForEditor(activeTextEditor);
            return;
        }
        const panel = createDependinatorWebviewPanel(context);

        activePanel = panel;
        panel.onDidDispose(() => {
            if (activePanel === panel)
                activePanel = undefined;
        });

        registerWebviewMessageHandler(panel, async (message: WebviewMessage) => {
            if (message.type !== "lsp/message")
                return;
            // console.log("DPR: lsp/message message:", message);

            // Messages from WebView UI to language server
            const client = await languageClientPromise;
            if (!client) {
                console.error("No client to send", message);
                // Forward error message back to WebView UI
                panel.webview.postMessage({
                    type: "ui/error",
                    message: "Language server unavailable"
                });
                return;
            }

            try {
                await waitForLspReady();
                // Forward messages to language server (from WebView UI)
                await client.sendNotification("lsp/message", {
                    message: message.message
                });
            } catch (error) {
                console.error("Failed to send", message);
                // Forward error message back to WebView UI
                panel.webview.postMessage({
                    type: "ui/error",
                    message: String(error)
                });
            }
        });

        console.log("DPR: Await ready after starting view");
        await waitForLspReady();
        console.log("DPR: Awaited ready after starting view");
        sendShowNodeForEditor(activeTextEditor);
    });

    context.subscriptions.push(disposable);
}

export async function deactivate(): Promise<void> {
    console.log("DPR: deactivate Extension");
    if (languageClient) {
        await languageClient.stop();
    }
}
