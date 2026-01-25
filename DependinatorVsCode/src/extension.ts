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
        registerUiMessageForwarding(client, () => activePanel?.webview);
    });

    // Command opens the webview hosting the WASM UI.
    const disposable = vscode.commands.registerCommand(commandId, () => {
        console.log("DPR: Running command Open Dependinator ...");
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
    console.log("DPR: deactivate Extension");
    if (languageClient) {
        await languageClient.stop();
    }
}
