import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import * as logger from "./logger";

// The user's Claude API key (from console.anthropic.com) for the AI help in the diagram, such as
// a suggested verdict on a cycle. The extension only keeps the key, in VS Code's secret storage;
// Dependinator.Lsp asks for it whenever the UI asks Claude something, and sends the request (see
// Dependinator.Lsp/Claude).

const apiKeySecretName = "dependinator.claude.apiKey";

// LSP protocol methods shared with Dependinator.Lsp/Claude.
const apiKeyRequestMethod = "dependinator/claude/apiKey";
const askForApiKeyRequestMethod = "dependinator/claude/askForApiKey";

export const setClaudeApiKeyCommandId = "dependinator.setClaudeApiKey";

/** Answers the LSP's requests for the stored key, or for one the user enters now. */
export function registerClaudeApiKey(client: LanguageClient, context: vscode.ExtensionContext): void {
    client.onRequest(apiKeyRequestMethod, async (): Promise<{ apiKey: string | null }> => ({
        apiKey: (await context.secrets.get(apiKeySecretName)) ?? null
    }));
    client.onRequest(askForApiKeyRequestMethod, async (): Promise<{ apiKey: string | null }> => ({
        apiKey: (await askForApiKeyAsync(context)) ?? null
    }));
}

/** Asks the user for a key and stores it; an empty entry removes the stored key. */
export async function askForApiKeyAsync(context: vscode.ExtensionContext): Promise<string | undefined> {
    const value = await vscode.window.showInputBox({
        title: "Dependinator: Claude API Key",
        prompt:
            "A key from console.anthropic.com, billed to your Anthropic account. It is kept in VS Code's " +
            "secret storage and sent only to Anthropic, when you ask Claude. Leave empty to remove it.",
        placeHolder: "sk-ant-...",
        password: true,
        ignoreFocusOut: true
    });
    if (value === undefined)
        return undefined;

    const apiKey = value.trim();
    if (apiKey === "") {
        await context.secrets.delete(apiKeySecretName);
        logger.log("Claude API key removed.");
        return undefined;
    }
    await context.secrets.store(apiKeySecretName, apiKey);
    logger.log("Claude API key stored.");
    return apiKey;
}
