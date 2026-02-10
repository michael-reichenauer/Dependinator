import * as vscode from "vscode";
import type { WebviewMessage } from "./types";

const viewType = "dependinator.webview";

export type WebviewMessageHandler = (
    message: WebviewMessage,
    panel: vscode.WebviewPanel
) => void | Promise<void>;

export function createDependinatorWebviewPanel(
    context: vscode.ExtensionContext
): vscode.WebviewPanel {
    console.log("DEP: createDependinatorWebviewPanel ...");
    const targetColumn = getTargetViewColumnForWebview();
    const panel = vscode.window.createWebviewPanel(
        viewType,
        "Dependinator",
        targetColumn,
        {
            enableScripts: true,
            retainContextWhenHidden: true,
            localResourceRoots: [vscode.Uri.joinPath(context.extensionUri, "media")]
        }
    );

    panel.webview.html = getWebviewHtml(panel.webview, context.extensionUri);
    return panel;
}

function getTargetViewColumnForWebview(): vscode.ViewColumn {
    const activeColumn = vscode.window.tabGroups.activeTabGroup.viewColumn;
    const otherGroup = vscode.window.tabGroups.all.find(group => {
        const groupColumn = group.viewColumn;
        return groupColumn !== undefined
            && activeColumn !== undefined
            && groupColumn !== activeColumn;
    });

    if (otherGroup?.viewColumn !== undefined)
        return otherGroup.viewColumn;

    // Create a split if there is no other group yet.
    return vscode.ViewColumn.Beside;
}

export function registerWebviewMessageHandler(
    panel: vscode.WebviewPanel,
    handler: WebviewMessageHandler
): void {
    console.log("DEP: registerWebviewMessageHandler");
    panel.webview.onDidReceiveMessage(message => {
        if (!message || typeof message.type !== "string")
            return;
        // console.log("DEP: message from ui:", message);
        return handler(message, panel);
    });
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

// CSP needs a nonce for inline scripts.
function getNonce(): string {
    const possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    let text = "";
    for (let i = 0; i < 32; i++) {
        text += possible.charAt(Math.floor(Math.random() * possible.length));
    }
    return text;
}
