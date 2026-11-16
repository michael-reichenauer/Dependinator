import * as vscode from "vscode";
import * as logger from "./logger";

// The extension's settings that the app's Settings dialog shows too (its VS Code section,
// VsCodeSettingsService.cs). VS Code keeps the one stored value: the dialog reads and writes
// it through these messages, so Settings Sync and workspace settings keep working.
const section = "dependinator";
const followActiveEditorKey = "followActiveEditor";
const autoRefreshEnabledKey = "autoRefresh.enabled";
const autoRefreshDelaySecondsKey = "autoRefresh.delaySeconds";
const minDelaySeconds = 1;

/** The settings as the UI reads them ("ui/vsCodeSettings"). */
function readSettings() {
    const config = vscode.workspace.getConfiguration(section);
    return {
        followActiveEditor: config.get<boolean>(followActiveEditorKey, true),
        autoRefreshEnabled: config.get<boolean>(autoRefreshEnabledKey, true),
        autoRefreshDelaySeconds: Math.max(minDelaySeconds, config.get<number>(autoRefreshDelaySecondsKey, 3))
    };
}

/** Sends the settings to the UI: the answer to "vscode/GetSettings", and after each change. */
export function sendSettings(webview: vscode.Webview): void {
    webview.postMessage({ type: "ui/vsCodeSettings", message: JSON.stringify(readSettings()) });
}

/** Sends the settings again whenever they change, also when changed in VS Code's own Settings. */
export function registerSettingsChangeForwarding(webview: vscode.Webview): vscode.Disposable {
    return vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration(`${section}.${followActiveEditorKey}`)
            || e.affectsConfiguration(`${section}.autoRefresh`))
            sendSettings(webview);
    });
}

/**
 * Saves a setting changed in the UI ("vscode/SetSetting", `{ key, value }` as JSON). It goes
 * where its value comes from now: the workspace's settings when it is set there (a user
 * setting would have no effect under it), else the user's settings.
 */
export async function setSetting(message: unknown): Promise<void> {
    let key: unknown;
    let value: unknown;
    try {
        ({ key, value } = JSON.parse(String(message ?? "")));
    } catch (error) {
        logger.error("Invalid setting from the UI", error);
        return;
    }

    if (!isValid(key, value)) {
        logger.warn(`Ignored setting from the UI: ${String(key)}`);
        return;
    }

    const config = vscode.workspace.getConfiguration(section);
    const target = config.inspect(key)?.workspaceValue !== undefined
        ? vscode.ConfigurationTarget.Workspace
        : vscode.ConfigurationTarget.Global;
    try {
        await config.update(key, value, target);
    } catch (error) {
        logger.error(`Failed to save setting ${key}`, error);
    }
}

function isValid(key: unknown, value: unknown): key is string {
    switch (key) {
        case followActiveEditorKey:
        case autoRefreshEnabledKey:
            return typeof value === "boolean";
        case autoRefreshDelaySecondsKey:
            return typeof value === "number" && Number.isFinite(value) && value >= minDelaySeconds;
        default:
            return false;
    }
}
