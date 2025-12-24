import * as vscode from "vscode";
import type { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";

const commandId = "dependinator.open";
const viewType = "dependinator.webview";

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
        client.onNotification("dependinator/serverReady", params => {
            activePanel?.webview.postMessage({
                type: "lsp-ready",
                message: params?.message ?? null
            });
        });
        client.onNotification("ui/message", params => {
            console.log("Received UIMessage", params);
            activePanel?.webview.postMessage({
                type: "us/message",
                message: params?.message,
                data: params?.data
            });
        });
    });

    // Command opens the webview hosting the WASM UI.
    const disposable = vscode.commands.registerCommand(commandId, () => {
        const panel = vscode.window.createWebviewPanel(
            viewType,
            "Dependinator",
            vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "media")]
            }
        );

        activePanel = panel;
        panel.onDidDispose(() => {
            if (activePanel === panel)
                activePanel = undefined;
        });

        // Webview -> extension message bridge.
        panel.webview.onDidReceiveMessage(async message => {
            if (!message || typeof message.type !== "string")
                return;

            if (message.type === "lsp/message") {
                console.log("vscode: received", message);
                // Forward a simple ping to the language server and relay the response.
                const client = await languageClientPromise;
                if (!client) {
                    console.error("No client to send", message);
                    panel.webview.postMessage({
                        type: "ui/error",
                        message: "Language server unavailable"
                    });
                    return;
                }

                try {
                    await client.sendRequest<{ message?: string }>("lsp/message", {
                        message: message.message
                    });

                } catch (error) {
                    console.error("Failed to send", message);
                    panel.webview.postMessage({
                        type: "ui/error",
                        message: String(error)
                    });
                }
                return;
            }
        });

        panel.webview.html = getWebviewHtml(panel.webview, context.extensionUri);
    });

    context.subscriptions.push(disposable);
}

export async function deactivate(): Promise<void> {
    if (languageClient) {
        await languageClient.stop();
    }
}

function getWebviewHtml(webview: vscode.Webview, extensionUri: vscode.Uri): string {
    // Use a webview-safe URI for bundled assets.
    const mediaUri = webview.asWebviewUri(vscode.Uri.joinPath(extensionUri, "media"));
    const baseUri = `${mediaUri.toString()}/`;
    const cspSource = webview.cspSource;
    const nonce = getNonce();

    return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src ${cspSource} data:; style-src ${cspSource} 'unsafe-inline'; font-src ${cspSource}; script-src ${cspSource} 'nonce-${nonce}' 'wasm-unsafe-eval' 'unsafe-eval'; connect-src ${cspSource}; worker-src ${cspSource} blob:;" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>Dependinator</title>
    <base href="./" />
    <link href="${baseUri}_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
    <link href="${baseUri}Dependinator.Wasm.styles.css" rel="stylesheet" />
    <link rel="icon" type="image/png" href="${baseUri}favicon.png" />
    <style>
        body {
            overflow: hidden;
            background-color: #000000;
        }
    </style>
</head>
<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text"></div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="" class="reload">Reload</a>
        <a class="dismiss">x</a>
    </div>

    <script nonce="${nonce}">
        // Bridge base URL and VS Code messaging into the WASM app.
        const dependinatorBaseUri = "${baseUri}";
        const vscode = acquireVsCodeApi();
        window.dependinator = {
            getBaseUri: () => dependinatorBaseUri,
            postMessage: (message) => vscode.postMessage(message)
        };

    </script>
    <script nonce="${nonce}" src="${baseUri}_framework/blazor.webassembly.js" autostart="false"></script>
    <script nonce="${nonce}">
        Blazor.start({
            loadBootResource: function (type, name, defaultUri, integrity) {
                const url = new URL(defaultUri, dependinatorBaseUri);
                return url.toString();
            }
        });
    </script>
    <script nonce="${nonce}" src="${baseUri}_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>`;
}

async function startLanguageServer(
    context: vscode.ExtensionContext
): Promise<LanguageClient | undefined> {
    // Prefer a prebuilt DLL; fallback to `dotnet run` without build/restore noise.
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri;
    const workspaceProject = workspaceFolder
        ? vscode.Uri.joinPath(
            workspaceFolder,
            "DependinatorLanguageServer",
            "DependinatorLanguageServer.csproj"
        )
        : undefined;
    const extensionProject = vscode.Uri.joinPath(
        context.extensionUri,
        "..",
        "DependinatorLanguageServer",
        "DependinatorLanguageServer.csproj"
    );
    const serverDllCandidates: vscode.Uri[] = [
        workspaceFolder
            ? vscode.Uri.joinPath(
                workspaceFolder,
                "DependinatorLanguageServer",
                "bin",
                "Debug",
                "net10.0",
                "DependinatorLanguageServer.dll"
            )
            : undefined,
        workspaceFolder
            ? vscode.Uri.joinPath(
                workspaceFolder,
                "DependinatorLanguageServer",
                "bin",
                "Release",
                "net10.0",
                "DependinatorLanguageServer.dll"
            )
            : undefined,
        vscode.Uri.joinPath(
            context.extensionUri,
            "server",
            "DependinatorLanguageServer.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "DependinatorLanguageServer",
            "bin",
            "Debug",
            "net10.0",
            "DependinatorLanguageServer.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "DependinatorLanguageServer",
            "bin",
            "Release",
            "net10.0",
            "DependinatorLanguageServer.dll"
        )
    ].filter((candidate): candidate is vscode.Uri => !!candidate);

    let serverArgs: string[] | undefined;
    const serverDll = await firstExisting(serverDllCandidates);
    if (serverDll) {
        serverArgs = [serverDll.fsPath];
    } else {
        const projectCandidates = [workspaceProject, extensionProject].filter(
            (candidate): candidate is vscode.Uri => !!candidate
        );
        const project = await firstExisting(projectCandidates);
        if (project) {
            serverArgs = [
                "run",
                "--project",
                project.fsPath,
                "--no-build",
                "--no-restore",
                "--nologo"
            ];
        } else {
            console.warn("Dependinator language server project not found.");
            return undefined;
        }
    }

    const { LanguageClient, TransportKind } = await import("vscode-languageclient/node");

    // Keep stdout clean for JSON-RPC by suppressing CLI banners.
    const environment = {
        ...process.env,
        DOTNET_NOLOGO: "1",
        NUGET_XMLDOC_MODE: "skip",
        DOTNET_CLI_TELEMETRY_OPTOUT: "1"
    };
    const executableOptions = workspaceFolder ? { cwd: workspaceFolder.fsPath, env: environment } : { env: environment };
    const serverOptions: ServerOptions = {
        run: {
            command: "dotnet",
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        },
        debug: {
            command: "dotnet",
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: "file", language: "csharp" }]
    };

    const client = new LanguageClient(
        "dependinatorLanguageServer",
        "Dependinator Language Server",
        serverOptions,
        clientOptions
    );

    await client.start();
    context.subscriptions.push(client);
    return client;
}

async function fileExists(uri: vscode.Uri): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(uri);
        return true;
    } catch {
        return false;
    }
}

async function firstExisting(uris: vscode.Uri[]): Promise<vscode.Uri | undefined> {
    for (const uri of uris) {
        if (await fileExists(uri))
            return uri;
    }
    return undefined;
}

// CSP needs a nonce for inline scripts.
function getNonce(): string {
    const possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    let text = "";
    for (let i = 0; i < 32; i++) {
        text += possible.charAt(Math.floor(Math.random() * possible.length));
    }
    return text;
}
