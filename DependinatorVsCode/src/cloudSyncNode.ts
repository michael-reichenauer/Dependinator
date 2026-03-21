import * as http from "node:http";
import * as crypto from "node:crypto";
import * as vscode from "vscode";
import type { CloudSyncEnvelope, WebviewMessage } from "./types";

type CloudAuthState = {
    IsAvailable: boolean;
    IsAuthenticated: boolean;
    User: CloudUserInfo | null;
};

type CloudUserInfo = {
    UserId: string;
    Email?: string | null;
};

type CloudModelMetadata = {
    ModelKey: string;
    NormalizedPath: string;
    UpdatedUtc: string;
    ContentHash: string;
    CompressedSizeBytes: number;
};

type CloudModelList = {
    Models: CloudModelMetadata[];
};

type CloudModelDocument = {
    ModelKey: string;
    NormalizedPath: string;
    UpdatedUtc: string;
    ContentHash: string;
    CompressedSizeBytes: number;
    CompressedContentBase64: string;
};

type CloudSyncConfig = {
    baseUrl: string;
    clerkDomain: string;
};

type JsonResponse<T> = {
    ok: boolean;
    status: number;
    body: T | null;
};

export type CloudSyncBridge = {
    handleWebviewMessage(message: WebviewMessage, panel: vscode.WebviewPanel): Promise<boolean>;
};

const tokenSecretName = "dependinator.cloudSync.accessToken";
const productionClerkDomain = "https://clerk.dependinator.com";
const callbackTimeoutMilliseconds = 5 * 60 * 1000;

export function createCloudSyncBridge(context: vscode.ExtensionContext): CloudSyncBridge {
    return new CloudSyncBridgeImpl(context);
}

class CloudSyncBridgeImpl implements CloudSyncBridge {
    readonly context: vscode.ExtensionContext;
    readonly outputChannel: vscode.OutputChannel;
    lastLoggedConfigurationSource: string | undefined;

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.outputChannel = vscode.window.createOutputChannel("Dependinator");
        context.subscriptions.push(this.outputChannel);
    }

    async handleWebviewMessage(message: WebviewMessage, panel: vscode.WebviewPanel): Promise<boolean> {
        if (message.type !== "cloudSync/request")
            return false;

        const request = this.parseEnvelope(message.message);
        if (!request) {
            await panel.webview.postMessage({
                type: "ui/cloudSync/response",
                message: JSON.stringify(
                    this.createEnvelope("", "", null, "Cloud sync request payload was invalid.")
                )
            });
            return true;
        }

        const response = await this.handleEnvelopeAsync(request);
        await panel.webview.postMessage({
            type: "ui/cloudSync/response",
            message: JSON.stringify(response)
        });
        return true;
    }

    async handleEnvelopeAsync(request: CloudSyncEnvelope): Promise<CloudSyncEnvelope> {
        try {
            switch (request.Action) {
                case "login":
                    return this.createSuccessEnvelope(request, await this.loginAsync());
                case "logout":
                    return this.createSuccessEnvelope(request, await this.logoutAsync());
                case "getAuthState":
                    return this.createSuccessEnvelope(request, await this.getAuthStateAsync());
                case "list":
                    return this.createSuccessEnvelope(request, await this.listAsync());
                case "push":
                    return this.createSuccessEnvelope(
                        request,
                        await this.pushAsync(this.parsePayload<CloudModelDocument>(request))
                    );
                case "pull":
                    return this.createSuccessEnvelope(
                        request,
                        await this.pullAsync(this.parsePayload<{ ModelKey: string }>(request))
                    );
                default:
                    return this.createEnvelope(
                        request.RequestId,
                        request.Action,
                        null,
                        `Unsupported cloud sync action '${request.Action}'.`
                    );
            }
        } catch (error) {
            return this.createEnvelope(
                request.RequestId,
                request.Action,
                null,
                this.toErrorMessage(error)
            );
        }
    }

    async loginAsync(): Promise<CloudAuthState> {
        const configuration = this.readConfiguration();
        if (!configuration)
            throw new Error("VS Code cloud sync is not configured. Set dependinator.cloudSync.baseUrl.");

        const token = await this.acquireTokenViaLocalCallbackAsync(configuration);
        await this.context.secrets.store(tokenSecretName, token);
        this.outputChannel.appendLine("Dependinator: Cloud sync access token acquired via Clerk sign-in.");
        return await this.getAuthStateFromTokenAsync(token, configuration);
    }

    async acquireTokenViaLocalCallbackAsync(configuration: CloudSyncConfig): Promise<string> {
        const state = crypto.randomBytes(32).toString("hex");

        return new Promise<string>((resolve, reject) => {
            let settled = false;
            const server = http.createServer((req, res) => {
                if (settled)
                    return;

                const url = new URL(req.url ?? "/", `http://localhost`);
                if (url.pathname !== "/callback") {
                    res.writeHead(404);
                    res.end("Not found");
                    return;
                }

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
                    <html><body>
                    <h2>Dependinator sign-in successful!</h2>
                    <p>You can close this tab and return to VS Code.</p>
                    <script>window.close();</script>
                    </body></html>
                `);

                server.close();
                resolve(token);
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
                const callbackUrl = `http://127.0.0.1:${port}/callback`;
                const signInUrl = `${configuration.clerkDomain}/sign-in?redirect_url=${encodeURIComponent(callbackUrl)}&state=${state}`;

                this.outputChannel.appendLine(`Dependinator: Opening browser for Clerk sign-in (callback on port ${port}).`);
                await vscode.env.openExternal(vscode.Uri.parse(signInUrl));
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

    async logoutAsync(): Promise<CloudAuthState> {
        await this.context.secrets.delete(tokenSecretName);
        const configuration = this.readConfiguration();
        return this.createSignedOutState(configuration !== undefined);
    }

    async getAuthStateAsync(): Promise<CloudAuthState> {
        const configuration = this.readConfiguration();
        if (!configuration)
            return this.createSignedOutState(false);

        const token = await this.readValidTokenAsync();
        if (!token)
            return this.createSignedOutState(true);

        const response = await this.sendApiAsync<CloudAuthState>(
            configuration,
            token,
            "GET",
            "/api/auth/me"
        );
        if (response.ok && response.body?.IsAuthenticated)
            return response.body;

        if (response.ok && response.body && !response.body.IsAuthenticated) {
            this.outputChannel.appendLine(
                "Dependinator: Stored VS Code cloud sync token was not accepted by the API. Check Clerk configuration."
            );
            await this.context.secrets.delete(tokenSecretName);
            return this.createSignedOutState(true);
        }

        if (response.status === 401 || response.status === 403) {
            await this.context.secrets.delete(tokenSecretName);
            return this.createSignedOutState(true);
        }

        throw new Error(this.readProtocolError(response.status, response.body));
    }

    async pushAsync(document: CloudModelDocument): Promise<CloudModelMetadata> {
        const configuration = this.requireConfiguration();
        const token = await this.requireTokenAsync();
        const response = await this.sendApiAsync<CloudModelMetadata>(
            configuration,
            token,
            "PUT",
            `/api/models/${encodeURIComponent(document.ModelKey)}`,
            document
        );
        if (!response.ok || !response.body)
            throw new Error(this.readProtocolError(response.status, response.body));

        return response.body;
    }

    async listAsync(): Promise<CloudModelList> {
        const configuration = this.requireConfiguration();
        const token = await this.requireTokenAsync();
        const response = await this.sendApiAsync<CloudModelList>(
            configuration,
            token,
            "GET",
            "/api/models"
        );
        if (!response.ok || !response.body)
            throw new Error(this.readProtocolError(response.status, response.body));

        return response.body;
    }

    async pullAsync(payload: { ModelKey: string }): Promise<CloudModelDocument> {
        const configuration = this.requireConfiguration();
        const token = await this.requireTokenAsync();
        const response = await this.sendApiAsync<CloudModelDocument>(
            configuration,
            token,
            "GET",
            `/api/models/${encodeURIComponent(payload.ModelKey)}`
        );
        if (!response.ok || !response.body)
            throw new Error(this.readProtocolError(response.status, response.body));

        return response.body;
    }

    readConfiguration(): CloudSyncConfig | undefined {
        const configuration = vscode.workspace.getConfiguration("dependinator");
        const baseUrl = this.normalizeUrl(configuration.get<string>("cloudSync.baseUrl"));
        const clerkDomain = this.normalizeUrl(
            configuration.get<string>("cloudSync.clerkDomain") || productionClerkDomain
        );
        const configurationSource = this.getConfigurationSource(configuration);
        this.logConfigurationSource(configurationSource);

        if (!baseUrl)
            return undefined;

        return {
            baseUrl,
            clerkDomain: clerkDomain.replace(/\/+$/, "")
        };
    }

    requireConfiguration(): CloudSyncConfig {
        const configuration = this.readConfiguration();
        if (!configuration)
            throw new Error("VS Code cloud sync is not configured. Set dependinator.cloudSync.baseUrl.");

        return configuration;
    }

    async requireTokenAsync(): Promise<string> {
        const token = await this.readValidTokenAsync();
        if (!token)
            throw new Error("Cloud sync requires login.");

        return token;
    }

    async readValidTokenAsync(): Promise<string | undefined> {
        const token = await this.context.secrets.get(tokenSecretName);
        if (!token)
            return undefined;

        const claims = this.readTokenClaims(token);
        const expiry = typeof claims.exp === "number" ? claims.exp * 1000 : 0;
        if (!expiry || expiry <= Date.now()) {
            await this.context.secrets.delete(tokenSecretName);
            return undefined;
        }

        return token;
    }

    async getAuthStateFromTokenAsync(token: string, configuration: CloudSyncConfig): Promise<CloudAuthState> {
        const response = await this.sendApiAsync<CloudAuthState>(
            configuration,
            token,
            "GET",
            "/api/auth/me"
        );
        if (response.ok && response.body?.IsAuthenticated)
            return response.body;

        if (response.ok && response.body && !response.body.IsAuthenticated) {
            this.outputChannel.appendLine(
                "Dependinator: Token acquisition succeeded, but the API returned unauthenticated. Check Clerk issuer configuration on the API."
            );
            throw new Error(
                "Cloud sync login completed, but the API did not accept the token. Check Clerk configuration."
            );
        }

        throw new Error(this.readProtocolError(response.status, response.body));
    }

    async sendApiAsync<T>(
        configuration: CloudSyncConfig,
        token: string,
        method: string,
        path: string,
        body?: unknown
    ): Promise<JsonResponse<T>> {
        const url = new URL(path, configuration.baseUrl).toString();
        const headers: Record<string, string> = {
            "X-Dependinator-Authorization": `Bearer ${token}`
        };
        let serializedBody: string | undefined;
        if (body !== undefined) {
            headers["Content-Type"] = "application/json";
            serializedBody = JSON.stringify(body);
        }

        const response = await fetch(url, {
            method,
            headers,
            body: serializedBody
        });

        return await this.readJsonResponseAsync<T>(response);
    }

    async readJsonResponseAsync<T>(response: Response): Promise<JsonResponse<T>> {
        const text = await response.text();
        if (!text)
            return { ok: response.ok, status: response.status, body: null };

        try {
            return { ok: response.ok, status: response.status, body: JSON.parse(text) as T };
        } catch {
            return { ok: response.ok, status: response.status, body: null };
        }
    }

    parseEnvelope(message: unknown): CloudSyncEnvelope | undefined {
        if (typeof message !== "string" || !message)
            return undefined;

        try {
            return JSON.parse(message) as CloudSyncEnvelope;
        } catch {
            return undefined;
        }
    }

    parsePayload<T>(request: CloudSyncEnvelope): T {
        if (!request.Payload)
            throw new Error(`Cloud sync action '${request.Action}' did not include a payload.`);

        return JSON.parse(request.Payload) as T;
    }

    readProtocolError(status: number, body: unknown): string {
        if (body && typeof body === "object") {
            const errorBody = body as { Message?: string; message?: string; error_description?: string; error?: string };
            if (errorBody.Message)
                return errorBody.Message;
            if (errorBody.message)
                return errorBody.message;
            if (errorBody.error_description)
                return errorBody.error_description;
            if (errorBody.error)
                return errorBody.error;
        }

        switch (status) {
            case 0:
                return "Cloud sync request did not return a response.";
            case 401:
                return "Cloud sync requires login.";
            case 404:
                return "Cloud model was not found.";
            default:
                return `Cloud sync request failed with status code ${status}.`;
        }
    }

    readTokenClaims(token: string): Record<string, unknown> {
        const tokenParts = token.split(".");
        if (tokenParts.length < 2)
            throw new Error("Cloud sync token format was invalid.");

        return JSON.parse(this.base64UrlDecode(tokenParts[1])) as Record<string, unknown>;
    }

    base64UrlDecode(value: string): string {
        const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
        const padding = normalized.length % 4 === 0 ? "" : "=".repeat(4 - (normalized.length % 4));
        return decodeURIComponent(
            Array.from(atob(`${normalized}${padding}`))
                .map(char => `%${char.charCodeAt(0).toString(16).padStart(2, "0")}`)
                .join("")
        );
    }

    createSignedOutState(isAvailable: boolean): CloudAuthState {
        return {
            IsAvailable: isAvailable,
            IsAuthenticated: false,
            User: null
        };
    }

    createSuccessEnvelope(request: CloudSyncEnvelope, payload: unknown): CloudSyncEnvelope {
        return this.createEnvelope(request.RequestId, request.Action, JSON.stringify(payload), null);
    }

    createEnvelope(
        requestId: string,
        action: string,
        payload: string | null,
        error: string | null
    ): CloudSyncEnvelope {
        return {
            RequestId: requestId,
            Action: action,
            Payload: payload,
            Error: error
        };
    }

    normalizeUrl(value: string | undefined): string {
        const normalizedValue = value?.trim();
        if (!normalizedValue)
            return "";

        return normalizedValue.endsWith("/") ? normalizedValue : `${normalizedValue}/`;
    }

    getConfigurationSource(configuration: vscode.WorkspaceConfiguration): string {
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

    logConfigurationSource(source: string): void {
        if (this.lastLoggedConfigurationSource === source)
            return;

        this.lastLoggedConfigurationSource = source;
        if (source === "production defaults") {
            this.outputChannel.appendLine("Dependinator: Using production cloud sync config.");
            console.log("DEP: Using production cloud sync config.");
            return;
        }

        this.outputChannel.appendLine(`Dependinator: Using ${source} cloud sync config.`);
        console.log(`DEP: Using ${source} cloud sync config.`);
    }

    toErrorMessage(error: unknown): string {
        return error instanceof Error ? error.message : String(error);
    }
}
