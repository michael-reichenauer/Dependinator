import * as http from "node:http";
import * as crypto from "node:crypto";
import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import * as logger from "./logger";

// Cloud sync authentication for the VS Code extension. The extension only acquires the
// Clerk access token (via an external browser and a local loopback callback server) and
// stores it in VS Code secret storage. All cloud-sync API calls are made by the
// Dependinator.Lsp process (see Dependinator.Lsp/CloudSync), which receives the token via
// LSP initializationOptions and the notifications/requests registered here.

type CloudSyncConfig = {
    baseUrl: string;
    clerkAccountsUrl: string;
    clerkPublishableKey: string;
};

const tokenSecretName = "dependinator.cloudSync.accessToken";
const productionClerkAccountsUrl = "https://renewed-hen-98.accounts.dev";
const productionClerkPublishableKey = "pk_test_cmVuZXdlZC1oZW4tOTguY2xlcmsuYWNjb3VudHMuZGV2JA";
const callbackTimeoutMilliseconds = 5 * 60 * 1000;

// LSP protocol methods shared with Dependinator.Lsp/CloudSync.
const signInRequestMethod = "dependinator/cloudSync/signIn";
const clearTokenRequestMethod = "dependinator/cloudSync/clearToken";
const configChangedNotificationMethod = "dependinator/cloudSync/configChanged";
const tokenChangedNotificationMethod = "dependinator/cloudSync/tokenChanged";

let lastLoggedConfigurationSource: string | undefined;

/** Reads the cloud-sync config and stored token for the LSP's initializationOptions. */
export async function getCloudSyncInitializationOptions(
    context: vscode.ExtensionContext
): Promise<{ cloudSync: { baseUrl: string | null; accessToken: string | null } }> {
    const configuration = readConfiguration();
    const accessToken = await context.secrets.get(tokenSecretName);
    return {
        cloudSync: {
            baseUrl: configuration?.baseUrl ?? null,
            accessToken: accessToken ?? null
        }
    };
}

/** Registers the sign-in/token handlers the LSP-hosted cloud sync service depends on. */
export function registerCloudSyncAuth(client: LanguageClient, context: vscode.ExtensionContext): void {
    // The LSP asks the extension to run the interactive Clerk sign-in; errors propagate
    // back to the LSP (and on to the UI) as JSON-RPC errors.
    client.onRequest(signInRequestMethod, async (): Promise<{ token: string }> => {
        const configuration = readConfiguration();
        if (!configuration)
            throw new Error("VS Code cloud sync is not configured. Set dependinator.cloudSync.baseUrl.");

        const token = await acquireTokenViaLocalCallbackAsync(configuration);
        await context.secrets.store(tokenSecretName, token);
        logger.log("Cloud sync access token acquired via Clerk sign-in.");
        return { token };
    });

    client.onRequest(clearTokenRequestMethod, async (): Promise<Record<string, never>> => {
        await context.secrets.delete(tokenSecretName);
        return {};
    });

    // Push setting edits to the LSP so e.g. baseUrl changes apply without a restart.
    context.subscriptions.push(
        vscode.workspace.onDidChangeConfiguration(event => {
            if (!event.affectsConfiguration("dependinator.cloudSync"))
                return;
            const configuration = readConfiguration();
            client
                .sendNotification(configChangedNotificationMethod, { baseUrl: configuration?.baseUrl ?? null })
                .catch(error => logger.warn("Failed to send cloud sync config change to LSP", error));
        })
    );

    // Keep the LSP's in-memory token in sync when another VS Code window signs in or out.
    context.subscriptions.push(
        context.secrets.onDidChange(async event => {
            if (event.key !== tokenSecretName)
                return;
            const accessToken = await context.secrets.get(tokenSecretName);
            client
                .sendNotification(tokenChangedNotificationMethod, { accessToken: accessToken ?? null })
                .catch(error => logger.warn("Failed to send cloud sync token change to LSP", error));
        })
    );
}

// Runs a local loopback HTTP server that serves the Clerk sign-in page, opens it in the
// external browser, and waits for the page to redirect back with the acquired token.
async function acquireTokenViaLocalCallbackAsync(configuration: CloudSyncConfig): Promise<string> {
    const state = crypto.randomBytes(32).toString("hex");

    return new Promise<string>((resolve, reject) => {
        let settled = false;
        const server = http.createServer((req, res) => {
            if (settled)
                return;

            const url = new URL(req.url ?? "/", `http://localhost`);

            // Serve the self-contained Clerk sign-in page
            if (url.pathname === "/") {
                const address = server.address();
                const port = typeof address === "object" && address ? address.port : 0;
                res.writeHead(200, { "Content-Type": "text/html" });
                res.end(buildSignInPageHtml(port, state, configuration));
                return;
            }

            // Handle the token callback
            if (url.pathname === "/callback") {
                const receivedState = url.searchParams.get("state");
                const token = url.searchParams.get("token");

                if (receivedState !== state) {
                    res.writeHead(400);
                    res.end("Invalid state parameter.");
                    return;
                }

                if (!token) {
                    res.writeHead(400);
                    res.end("No token received.");
                    return;
                }

                settled = true;
                res.writeHead(200, { "Content-Type": "text/html" });
                res.end(`
                    <html><body style="background:#1e1e1e;color:#ccc;font-family:sans-serif;text-align:center;margin-top:40vh;">
                    <h2>Dependinator sign-in successful!</h2>
                    <p>You can close this tab and return to VS Code.</p>
                    <script>window.close();</script>
                    </body></html>
                `);

                server.close();
                resolve(token);
                return;
            }

            res.writeHead(404);
            res.end("Not found");
        });

        server.listen(0, "127.0.0.1", async () => {
            const address = server.address();
            if (!address || typeof address === "string") {
                settled = true;
                server.close();
                reject(new Error("Failed to start local callback server."));
                return;
            }

            const port = address.port;
            logger.log(`Opening browser for Clerk sign-in (callback on port ${port}).`);
            await vscode.env.openExternal(vscode.Uri.parse(`http://127.0.0.1:${port}/`));
        });

        setTimeout(() => {
            if (!settled) {
                settled = true;
                server.close();
                reject(new Error("Sign-in timed out. Please try again."));
            }
        }, callbackTimeoutMilliseconds);

        server.on("error", (error) => {
            if (!settled) {
                settled = true;
                reject(new Error(`Local callback server error: ${error.message}`));
            }
        });
    });
}

// Self-contained sign-in page: loads Clerk JS, mounts the sign-in component, and redirects
// to the loopback callback with the acquired JWT ('dependinator' template).
function buildSignInPageHtml(port: number, state: string, configuration: CloudSyncConfig): string {
    const clerkCdnBase = configuration.clerkAccountsUrl.replace(".accounts.", ".clerk.accounts.");
    const clerkJsUrl = `${clerkCdnBase}/npm/@clerk/clerk-js@latest/dist/clerk.browser.js`;

    return `<!DOCTYPE html>
<html>
<head>
    <title>Dependinator — Sign In</title>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1"/>
    <script
        data-clerk-publishable-key="${configuration.clerkPublishableKey}"
        crossorigin="anonymous"
        src="${clerkJsUrl}"
        async></script>
</head>
<body style="background:#1e1e1e;color:#ccc;font-family:sans-serif;margin:0;">
    <div id="app" style="display:flex;justify-content:center;align-items:center;min-height:100vh;">
        <p>Loading sign-in...</p>
    </div>
    <script>
        var PORT = ${port};
        var STATE = ${JSON.stringify(state)};

        var redirected = false;
        function redirectToCallback(token) {
            if (redirected) return;
            redirected = true;
            window.location.href = "http://127.0.0.1:" + PORT + "/callback"
                + "?token=" + encodeURIComponent(token)
                + "&state=" + encodeURIComponent(STATE);
        }

        (async function () {
            // Wait for Clerk to load
            var start = Date.now();
            while (true) {
                var c = window.Clerk;
                if (c && c.loaded) break;
                if (c && !c.loaded && typeof c.load === "function") {
                    try { await c.load(); break; } catch (e) { }
                }
                if (Date.now() - start > 20000) {
                    document.getElementById("app").innerHTML =
                        "<p>Failed to load sign-in service. Please try again.</p>";
                    return;
                }
                await new Promise(function (r) { setTimeout(r, 200); });
            }

            var clerk = window.Clerk;

            // If already signed in, get token and redirect immediately
            if (clerk.session) {
                try {
                    var token = await clerk.session.getToken({ template: 'dependinator' });
                    if (token) { redirectToCallback(token); return; }
                } catch (e) { }
            }

            // Mount the Clerk sign-in component
            var appEl = document.getElementById("app");
            appEl.innerHTML = "<div id='clerk-signin'></div>";
            clerk.mountSignIn(document.getElementById("clerk-signin"));

            // Detect sign-in completion via listener
            clerk.addListener(async function (resources) {
                if (resources.user && clerk.session) {
                    try {
                        var token = await clerk.session.getToken({ template: 'dependinator' });
                        if (token) redirectToCallback(token);
                    } catch (e) { }
                }
            });

            // Poll as fallback for cross-tab magic link completion
            setInterval(async function () {
                if (clerk.user && clerk.session) {
                    try {
                        var token = await clerk.session.getToken({ template: 'dependinator' });
                        if (token) redirectToCallback(token);
                    } catch (e) { }
                }
            }, 1000);
        })();
    </script>
</body>
</html>`;
}

// Reads dependinator.cloudSync settings; Clerk values fall back to production defaults.
// Returns undefined (cloud sync disabled) when no baseUrl is configured.
function readConfiguration(): CloudSyncConfig | undefined {
    const configuration = vscode.workspace.getConfiguration("dependinator");
    const baseUrl = normalizeUrl(configuration.get<string>("cloudSync.baseUrl"));
    const clerkAccountsUrl = (
        configuration.get<string>("cloudSync.clerkAccountsUrl") || productionClerkAccountsUrl
    ).replace(/\/+$/, "");
    const clerkPublishableKey =
        configuration.get<string>("cloudSync.clerkPublishableKey") || productionClerkPublishableKey;
    logConfigurationSource(getConfigurationSource(configuration));

    if (!baseUrl)
        return undefined;

    return {
        baseUrl,
        clerkAccountsUrl,
        clerkPublishableKey
    };
}

function normalizeUrl(value: string | undefined): string {
    const normalizedValue = value?.trim();
    if (!normalizedValue)
        return "";

    return normalizedValue.endsWith("/") ? normalizedValue : `${normalizedValue}/`;
}

function getConfigurationSource(configuration: vscode.WorkspaceConfiguration): string {
    const settingKeys = ["cloudSync.baseUrl"];
    for (const settingKey of settingKeys) {
        const inspected = configuration.inspect<string>(settingKey);
        if (inspected?.workspaceFolderValue !== undefined || inspected?.workspaceValue !== undefined)
            return "workspace override";
        if (inspected?.globalValue !== undefined)
            return "user override";
    }

    return "production defaults";
}

function logConfigurationSource(source: string): void {
    if (lastLoggedConfigurationSource === source)
        return;

    lastLoggedConfigurationSource = source;
    logger.log(`Using ${source === "production defaults" ? "production" : source} cloud sync config.`);
}
