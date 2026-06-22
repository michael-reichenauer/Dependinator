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
type CloudSyncBridge = {
    handleWebviewMessage(message: WebviewMessage, panel: vscode.WebviewPanel): Promise<boolean>;
};

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
    console.log("DEP: sendShowNodeForEditor");
    if (!editor || !activePanel) {
        console.log("DEP: No editor", editor, activePanel);
        return;
    }

    if (!isCSharpDocument(editor.document)) {
        console.log("DEP: Not not C#");
        return;
    }

    const path = getDocumentPath(editor.document);
    if (!path) {
        console.log("DEP: No path");
        return;
    }

    const line = editor.selection.active.line + 1;
    activePanel.webview.postMessage({
        type: "ui/ShowNode",
        message: `${path}@${line}`
    });
    console.log("DEP: Posted ui/ShowNode");
}

function getTargetViewColumnForShowEditor(panel: vscode.WebviewPanel): vscode.ViewColumn {
    const panelColumn = panel.viewColumn;
    const otherGroup = vscode.window.tabGroups.all.find(group => {
        const groupColumn = group.viewColumn;
        return groupColumn !== undefined
            && panelColumn !== undefined
            && groupColumn !== panelColumn;
    });

    if (otherGroup?.viewColumn !== undefined)
        return otherGroup.viewColumn;

    // Fall back to a split so the Dependinator webview stays visible.
    return vscode.ViewColumn.Beside;
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
    console.log("DEP: #### Activate extension");
    const installCommand = vscode.commands.registerCommand(installInDevContainerCommandId, async () => {
        console.log("DEP: Running command installInDevContainerCommandId");
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
    console.log("DEP: vscode.env.uiKind", vscode.env.uiKind);
    // Web extensions can't start a .NET process, so skip the server there.
    const isWeb = vscode.env.uiKind === vscode.UIKind.Web;
    const cloudSyncBridgePromise: Promise<CloudSyncBridge | undefined> = isWeb
        ? Promise.resolve(undefined)
        : import("./cloudSyncNode")
            .then(module => module.createCloudSyncBridge(context))
            .catch(error => {
                console.error("Dependinator cloud sync bridge failed to initialize", error);
                return undefined;
            });
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
        console.log("DEP: Running command Open Dependinator ...");
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
            const cloudSyncBridge = await cloudSyncBridgePromise;
            if (cloudSyncBridge && await cloudSyncBridge.handleWebviewMessage(message, panel))
                return;

            if (message.type === "vscode/ShowEditor") {
                const fileLocation = String(message.message ?? "");
                console.log("Show editor for", fileLocation);
                // Show the editor for fileLocation, which has the form of "<file-path>@<file-line>";
                if (fileLocation.length === 0) {
                    console.warn("ShowEditor called with empty file location");
                    return;
                }

                const atIndex = fileLocation.lastIndexOf("@");
                const filePath = atIndex > 0 ? fileLocation.slice(0, atIndex) : fileLocation;
                const lineText = atIndex > 0 ? fileLocation.slice(atIndex + 1) : "";
                const parsedLine = Number.parseInt(lineText, 10);
                const line = Number.isFinite(parsedLine) && parsedLine > 0 ? parsedLine : 1;

                const uri = filePath.startsWith("file:")
                    || filePath.startsWith("vscode-remote:")
                    || filePath.startsWith("vscode-userdata:")
                    ? vscode.Uri.parse(filePath)
                    : vscode.Uri.file(filePath);

                try {
                    const document = await vscode.workspace.openTextDocument(uri);
                    const editor = await vscode.window.showTextDocument(document, {
                        preview: false,
                        viewColumn: getTargetViewColumnForShowEditor(panel)
                    });
                    const lineIndex = Math.max(0, line - 1);
                    const position = new vscode.Position(lineIndex, 0);
                    editor.selection = new vscode.Selection(position, position);
                    editor.revealRange(
                        new vscode.Range(position, position),
                        vscode.TextEditorRevealType.InCenter
                    );
                } catch (error) {
                    console.error("Failed to open editor for", fileLocation, error);
                    vscode.window.showErrorMessage(`Dependinator: Could not open ${fileLocation}`);
                }
                return;
            }

            if (message.type !== "lsp/message")
                return;
            // console.log("DEP: lsp/message message:", message);

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

        // await waitForLspReady();
        // sendShowNodeForEditor(activeTextEditor);
    });

    context.subscriptions.push(disposable);
}

export async function deactivate(): Promise<void> {
    console.log("DEP: deactivate Extension");
    if (languageClient) {
        await languageClient.stop();
    }
}
