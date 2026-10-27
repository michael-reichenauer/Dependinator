import { execFile } from "node:child_process";
import { randomBytes } from "node:crypto";
import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import * as logger from "./logger";

// Dependinator's MCP server (Dependinator.Lsp/Mcp) lets AI agents ask about the model open in
// the diagram. The LSP runs it on localhost; the extension keeps its token (secret storage) and
// its port (per workspace), so the URL and token survive restarts, and registers it with VS
// Code's agents (Copilot) and, on request, with Claude Code for the workspace folder.

const tokenSecretName = "dependinator.mcp.token";
const portStateKey = "dependinator.mcp.port";
// The URL last given to Claude Code for this workspace; set means "keep Claude Code connected".
const claudeCodeUrlStateKey = "dependinator.mcp.claudeCodeUrl";
const serverName = "dependinator";

// LSP protocol method shared with Dependinator.Lsp/Mcp.
const startedNotificationMethod = "mcp/serverStarted";

export const mcpProviderId = "dependinator.mcp";
export const connectClaudeCodeCommandId = "dependinator.connectClaudeCode";

let server: { url: string; token: string } | undefined;
const definitionsChanged = new vscode.EventEmitter<void>();

/** The LSP's initializationOptions for the MCP server: { mcp: { token, port } }. */
export async function getMcpInitializationOptions(
    context: vscode.ExtensionContext
): Promise<{ mcp: { token: string; port: number } }> {
    let token = await context.secrets.get(tokenSecretName);
    if (!token) {
        token = randomBytes(32).toString("hex");
        await context.secrets.store(tokenSecretName, token);
    }
    return { mcp: { token, port: context.workspaceState.get<number>(portStateKey) ?? 0 } };
}

/** Follows the LSP's server, registers it with VS Code, and keeps Claude Code's entry current. */
export function registerMcpServer(client: LanguageClient, context: vscode.ExtensionContext): void {
    client.onNotification(startedNotificationMethod, async params => {
        const url: string | undefined = params?.Url ?? params?.url;
        const token = await context.secrets.get(tokenSecretName);
        if (!url || !token)
            return;
        server = { url, token };
        logger.log("MCP server at", url);
        await context.workspaceState.update(portStateKey, Number(new URL(url).port) || 0);
        definitionsChanged.fire();

        // A port taken by another window gives a new URL: update the connection made before.
        const connectedUrl = context.workspaceState.get<string>(claudeCodeUrlStateKey);
        if (connectedUrl && connectedUrl !== url)
            await connectClaudeCode(context, false);
    });

    // VS Code 1.101 and later: Copilot's agent mode lists the server under MCP servers.
    const lm = vscode.lm as Partial<typeof vscode.lm> | undefined;
    if (lm?.registerMcpServerDefinitionProvider) {
        context.subscriptions.push(
            lm.registerMcpServerDefinitionProvider(mcpProviderId, {
                onDidChangeMcpServerDefinitions: definitionsChanged.event,
                provideMcpServerDefinitions: () =>
                    server
                        ? [
                            new vscode.McpHttpServerDefinition(
                                "Dependinator",
                                vscode.Uri.parse(server.url),
                                { Authorization: `Bearer ${server.token}` },
                                server.url
                            )
                        ]
                        : []
            })
        );
    }
}

/** Adds the server to Claude Code for the workspace folder (claude mcp add, local scope). */
export async function connectClaudeCode(context: vscode.ExtensionContext, isUserCommand = true): Promise<void> {
    const folder = vscode.workspace.workspaceFolders?.[0]?.uri;
    if (!server || !folder || folder.scheme !== "file") {
        if (isUserCommand)
            vscode.window.showErrorMessage(
                "Dependinator: Open a folder and wait for Dependinator to start, then try again."
            );
        return;
    }

    const cwd = folder.fsPath;
    // Replaces an older entry; fails harmlessly when there is none.
    await runClaude(["mcp", "remove", "--scope", "local", serverName], cwd);
    const added = await runClaude(
        [
            "mcp",
            "add",
            "--transport",
            "http",
            "--scope",
            "local",
            serverName,
            server.url,
            "--header",
            `Authorization: Bearer ${server.token}`
        ],
        cwd
    );
    if (added.error) {
        logger.error("Could not connect Claude Code:", added.output);
        if (isUserCommand)
            vscode.window.showErrorMessage(
                added.isMissing
                    ? "Dependinator: Claude Code (the claude command) was not found. Install it, then try again."
                    : `Dependinator: Could not connect Claude Code: ${added.output}`
            );
        return;
    }

    await context.workspaceState.update(claudeCodeUrlStateKey, server.url);
    logger.log("Connected Claude Code to the MCP server for", cwd);
    if (isUserCommand)
        vscode.window.showInformationMessage(
            "Dependinator: Claude Code in this folder can now ask about the diagram's model (MCP server " +
                "\"dependinator\"). Start a new Claude Code session, or run /mcp in a running one. Keep the " +
                "Dependinator diagram open while it works."
        );
}

function runClaude(
    args: string[],
    cwd: string
): Promise<{ error: boolean; isMissing: boolean; output: string }> {
    return new Promise(resolve => {
        // On Windows, claude is a .cmd script, which only a shell runs; the shell needs the quotes.
        const isWindows = process.platform === "win32";
        const shellArgs = isWindows ? args.map(arg => (arg.includes(" ") ? `"${arg}"` : arg)) : args;
        execFile("claude", shellArgs, { cwd, shell: isWindows, timeout: 30_000 }, (error, stdout, stderr) =>
            resolve({
                error: !!error,
                isMissing: (error as NodeJS.ErrnoException | null)?.code === "ENOENT",
                output: `${stdout}${stderr}`.trim()
            })
        );
    });
}
