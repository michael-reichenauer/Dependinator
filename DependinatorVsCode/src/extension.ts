import * as vscode from "vscode";

const commandId = "dependinator.open";
const viewType = "dependinator.webview";

export function activate(context: vscode.ExtensionContext): void {
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

        panel.webview.onDidReceiveMessage(message => {
            if (!message || typeof message.type !== "string")
                return;

            if (message.type === "ready") {
                console.log("Dependinator webview ready");
                panel.webview.postMessage({ type: "ready-ack" });
                return;
            }

            if (message.type === "ping") {
                console.log("Dependinator ping", message.id ?? null);
                panel.webview.postMessage({ type: "pong", id: message.id ?? null });
                return;
            }
        });

        panel.webview.html = getWebviewHtml(panel.webview, context.extensionUri);
    });

    context.subscriptions.push(disposable);
}

export function deactivate(): void {
}

function getWebviewHtml(webview: vscode.Webview, extensionUri: vscode.Uri): string {
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
        const dependinatorBaseUri = "${baseUri}";
        const vscode = acquireVsCodeApi();
        window.dependinator = {
            getBaseUri: () => dependinatorBaseUri,
            postMessage: (message) => vscode.postMessage(message)
        };

        window.addEventListener("message", (event) =>
        {
            if (!event?.data?.type)
                return;
            if (event.data.type === "ready-ack")
            {
                console.log("Dependinator: bridge ready");
            }
            if (event.data.type === "pong")
            {
                console.log("Dependinator: pong", event.data.id ?? null);
            }
        });

        vscode.postMessage({ type: "ready" });
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

function getNonce(): string {
    const possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    let text = "";
    for (let i = 0; i < 32; i++) {
        text += possible.charAt(Math.floor(Math.random() * possible.length));
    }
    return text;
}
