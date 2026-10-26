import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import * as logger from "./logger";

// The experimental local route for the AI help in the diagram: Dependinator.Lsp runs Claude Code
// (`claude -p`) on this computer, with the user's own Claude login instead of an API key. It is
// unclear whether Anthropic allows a third-party tool to use a Claude subscription this way, so
// the route is hidden: the setting below is not listed in the Settings UI (it is set by hand in
// settings.json), and the first use asks the user to confirm a warning, remembered per machine.

const enabledSetting = "experimental.localClaudeCode";
const confirmedStateKey = "dependinator.claudeCode.confirmed";

// LSP protocol methods shared with Dependinator.Lsp/Claude.
const enabledRequestMethod = "dependinator/claude/claudeCodeEnabled";
const confirmRequestMethod = "dependinator/claude/confirmClaudeCode";

/** Answers the LSP's questions: whether the route is on, and whether the user confirmed it. */
export function registerClaudeCode(client: LanguageClient, context: vscode.ExtensionContext): void {
    client.onRequest(enabledRequestMethod, (): { isEnabled: boolean } => ({ isEnabled: isEnabled() }));
    client.onRequest(confirmRequestMethod, async (): Promise<{ isEnabled: boolean }> => ({
        isEnabled: isEnabled() && (await confirmAsync(context))
    }));
}

function isEnabled(): boolean {
    return vscode.workspace.getConfiguration("dependinator").get<boolean>(enabledSetting) === true;
}

async function confirmAsync(context: vscode.ExtensionContext): Promise<boolean> {
    if (context.globalState.get<boolean>(confirmedStateKey) === true)
        return true;
    const enable = "Enable Claude Code";
    const choice = await vscode.window.showWarningMessage(
        "Dependinator: Ask Claude Code on this computer? (experimental)",
        {
            modal: true,
            detail:
                "Dependinator will run the claude command (Claude Code) with your own Claude login to answer " +
                "review questions, instead of using an Anthropic API key. The prompt includes excerpts of your " +
                "source code. It is unclear whether Anthropic's terms allow a third-party tool to use a Claude " +
                "subscription this way, so use it for your own experiments only, at your own risk. To turn it " +
                'off, remove "dependinator.experimental.localClaudeCode" from your settings.'
        },
        enable
    );
    if (choice !== enable)
        return false;
    await context.globalState.update(confirmedStateKey, true);
    logger.log("Claude Code route confirmed.");
    return true;
}
