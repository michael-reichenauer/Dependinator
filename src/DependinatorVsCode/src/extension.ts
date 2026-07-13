import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import {
    registerLanguageClientLogging,
    registerUiMessageForwarding,
    startLanguageServer
} from "./languageServer";
import * as logger from "./logger";
import type { WebviewMessage } from "./types";
import {
    createDependinatorWebviewPanel,
    findOtherViewColumn,
    registerWebviewMessageHandler
} from "./webview";

const commandId = "dependinator.open";
const installInDevContainerCommandId = "dependinator.installInDevContainer";
const extensionId = "michaelreichenauer.dependinator";

// Track the LSP client and the last active webview to route notifications.
let languageClient: LanguageClient | undefined;
let languageClientPromise: Promise<LanguageClient | undefined> | undefined;
let activePanel: vscode.WebviewPanel | undefined;

// Latch that gates UI->LSP messages until the server has sent "ui/lspReady".
// Resettable because vscode-languageclient restarts a crashed server, which sends
// a fresh "ui/lspReady" once it has initialized again.
let lspReady = false;
let resolveLspReady: (() => void) | undefined;
let lspReadyPromise = createLspReadyLatch();

function createLspReadyLatch(): Promise<void> {
    return new Promise<void>(resolve => {
        resolveLspReady = resolve;
    });
}

function markLspReady(): void {
    lspReady = true;
    resolveLspReady?.();
    resolveLspReady = undefined;
}

function resetLspReady(): void {
    if (!lspReady)
        return;

    lspReady = false;
    lspReadyPromise = createLspReadyLatch();
}

async function waitForLspReady(): Promise<void> {
    if (lspReady)
        return;
    await lspReadyPromise;
}

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

/** Asks the webview UI to reveal the node for the active C# editor's file and line. */
function sendShowNodeForEditor(editor: vscode.TextEditor | undefined): void {
    if (!editor || !activePanel)
        return;

    if (!isCSharpDocument(editor.document))
        return;

    const path = getDocumentPath(editor.document);
    if (!path)
        return;

    const line = editor.selection.active.line + 1;
    activePanel.webview.postMessage({
        type: "ui/ShowNode",
        message: `${path}@${line}`
    });
}

// Path segments that never affect the parsed model; also guards against
// parse→change→parse loops since design-time builds can write under obj/.
const autoRefreshIgnoredSegments = ["/obj/", "/bin/", "/node_modules/", "/.git/"];

function isAutoRefreshRelevant(uri: vscode.Uri): boolean {
    const path = uri.path.toLowerCase();
    return !autoRefreshIgnoredSegments.some(segment => path.includes(segment));
}

/**
 * Watches workspace source files and asks the webview UI to refresh (same as the
 * Refresh toolbar button) a few seconds after the last change. Returns the
 * disposables owning the watcher and its pending debounce timer.
 */
function registerAutoRefresh(panel: vscode.WebviewPanel): vscode.Disposable {
    const watcher = vscode.workspace.createFileSystemWatcher("**/*.{cs,csproj,sln}");
    let pendingTimer: ReturnType<typeof setTimeout> | undefined;

    const onFileEvent = (uri: vscode.Uri): void => {
        if (!isAutoRefreshRelevant(uri))
            return;

        // Read config at event time so toggling the setting needs no reload.
        const config = vscode.workspace.getConfiguration("dependinator.autoRefresh");
        if (!config.get<boolean>("enabled", true))
            return;

        const delaySeconds = Math.max(1, config.get<number>("delaySeconds", 3));
        if (pendingTimer)
            clearTimeout(pendingTimer);
        pendingTimer = setTimeout(() => {
            pendingTimer = undefined;
            logger.log("Source files changed, refreshing diagram");
            panel.webview.postMessage({ type: "ui/refresh", message: "" });
        }, delaySeconds * 1000);
    };

    watcher.onDidCreate(onFileEvent);
    watcher.onDidChange(onFileEvent);
    watcher.onDidDelete(onFileEvent);

    return new vscode.Disposable(() => {
        if (pendingTimer)
            clearTimeout(pendingTimer);
        watcher.dispose();
    });
}

/** Opens a source file at a "<file-path>@<line>" location beside the webview. */
async function showEditorForLocation(fileLocation: string, panel: vscode.WebviewPanel): Promise<void> {
    if (fileLocation.length === 0) {
        logger.warn("ShowEditor called with empty file location");
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
            viewColumn: findOtherViewColumn(panel.viewColumn)
        });
        const lineIndex = Math.max(0, line - 1);
        const position = new vscode.Position(lineIndex, 0);
        editor.selection = new vscode.Selection(position, position);
        editor.revealRange(
            new vscode.Range(position, position),
            vscode.TextEditorRevealType.InCenter
        );
    } catch (error) {
        logger.error("Failed to open editor for", fileLocation, error);
        vscode.window.showErrorMessage(`Dependinator: Could not open ${fileLocation}`);
    }
}

/** Extension entry point: registers commands and starts the language server. */
export async function activate(context: vscode.ExtensionContext): Promise<void> {
    logger.log("Activating extension");
    const installCommand = vscode.commands.registerCommand(installInDevContainerCommandId, async () => {
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
            logger.warn("Failed to open Extensions view for dev container install.", error);
            await vscode.commands.executeCommand("workbench.view.extensions");
        }

        vscode.window.showInformationMessage(
            "In the Extensions view, select Dependinator and choose Install in Dev Container."
        );
    });
    context.subscriptions.push(installCommand);

    // Web extensions can't start a .NET process, so skip the server there.
    const isWeb = vscode.env.uiKind === vscode.UIKind.Web;
    languageClientPromise = isWeb
        ? Promise.resolve(undefined)
        : startLanguageServer(context).catch(error => {
            logger.error("Dependinator language server failed to start", error);
            return undefined;
        });

    languageClientPromise.then(async client => {
        languageClient = client;
        if (!client)
            return;

        registerLanguageClientLogging(client);
        registerUiMessageForwarding(client, () => activePanel?.webview, markLspReady);
        client.onDidChangeState(event => {
            // State.Running === 2 (compared numerically to keep this module free of
            // vscode-languageclient value imports, which the web bundle cannot load).
            if (event.newState !== 2)
                resetLspReady();
        });

        // Cloud sync auth handlers are node-only; loaded dynamically like the LSP itself.
        try {
            const cloudSyncAuth = await import("./cloudSyncAuth");
            cloudSyncAuth.registerCloudSyncAuth(client, context);
        } catch (error) {
            logger.error("Dependinator cloud sync auth failed to initialize", error);
        }
    });

    // Command opens the webview hosting the WASM UI.
    const disposable = vscode.commands.registerCommand(commandId, async () => {
        logger.log("Running command Open Dependinator");
        const activeTextEditor = vscode.window.activeTextEditor;
        if (activePanel) {
            activePanel.reveal(activePanel.viewColumn ?? vscode.ViewColumn.One);
            sendShowNodeForEditor(activeTextEditor);
            return;
        }
        const panel = createDependinatorWebviewPanel(context);

        activePanel = panel;
        const autoRefresh = registerAutoRefresh(panel);
        panel.onDidDispose(() => {
            autoRefresh.dispose();
            if (activePanel === panel)
                activePanel = undefined;
        });

        registerWebviewMessageHandler(panel, async (message: WebviewMessage) => {
            if (message.type === "vscode/OpenExternal") {
                const url = String(message.message ?? "");
                if (url)
                    await vscode.env.openExternal(vscode.Uri.parse(url));
                return;
            }

            if (message.type === "vscode/ShowEditor") {
                await showEditorForLocation(String(message.message ?? ""), panel);
                return;
            }

            if (message.type !== "lsp/message")
                return;

            // Messages from WebView UI to language server
            const client = await languageClientPromise;
            if (!client) {
                logger.error("No language client to send message to");
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
                logger.error("Failed to send message to language server", error);
                // Forward error message back to WebView UI
                panel.webview.postMessage({
                    type: "ui/error",
                    message: String(error)
                });
            }
        });
    });

    context.subscriptions.push(disposable);
}

/** Extension shutdown: stops the language server. */
export async function deactivate(): Promise<void> {
    if (languageClient) {
        await languageClient.stop();
    }
}
