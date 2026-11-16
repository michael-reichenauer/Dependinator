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

/** The settings, read at use time so a change needs no reload; also what the UI is sent. */
export function readSettings() {
    const config = vscode.workspace.getConfiguration(section);
    return {
        followActiveEditor: config.get<boolean>(followActiveEditorKey, true),
        autoRefreshEnabled: config.get<boolean>(autoRefreshEnabledKey, true),
        autoRefreshDelaySeconds: Math.max(minDelaySeconds, config.get<number>(autoRefreshDelaySecondsKey, 3))
    };
}

/** True when a configuration change is a change of auto refresh on or off. */
export function affectsAutoRefreshEnabled(e: vscode.ConfigurationChangeEvent): boolean {
    return e.affectsConfiguration(`${section}.${autoRefreshEnabledKey}`);
}

/** Sends the settings to the UI ("ui/vsCodeSettings"): the answer to "vscode/GetSettings", and after each change. */
export function sendSettings(webview: vscode.Webview): void {
    webview.postMessage({ type: "ui/vsCodeSettings", message: JSON.stringify(readSettings()) });
}

/**
 * Sends the settings again whenever they change, also when changed in VS Code's own Settings.
 * Debounced: a few quick changes from the dialog (a spin button) are sent once, as they ended.
 */
export function registerSettingsChangeForwarding(webview: vscode.Webview): vscode.Disposable {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const listener = vscode.workspace.onDidChangeConfiguration(e => {
        if (!e.affectsConfiguration(`${section}.${followActiveEditorKey}`)
            && !e.affectsConfiguration(`${section}.autoRefresh`))
            return;
        if (timer)
            clearTimeout(timer);
        timer = setTimeout(() => {
            timer = undefined;
            sendSettings(webview);
        }, 100);
    });
    return new vscode.Disposable(() => {
        if (timer)
            clearTimeout(timer);
        listener.dispose();
    });
}

/**
 * Saves a setting changed in the UI ("vscode/SetSetting", `{ key, value }` as JSON). It goes
 * where its value comes from now: the workspace's settings when it is set there (a user
 * setting would have no effect under it), else the user's settings, where the default value
 * removes the entry, as VS Code's own Settings do. If the write fails or another value still
 * wins, the UI is sent the settings as they are.
 */
export async function setSetting(message: unknown, webview: vscode.Webview): Promise<void> {
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
        sendSettings(webview);
        return;
    }

    const config = vscode.workspace.getConfiguration(section);
    const inspected = config.inspect(key);
    const isInWorkspace = inspected?.workspaceValue !== undefined;
    const target = isInWorkspace ? vscode.ConfigurationTarget.Workspace : vscode.ConfigurationTarget.Global;
    const storedValue = !isInWorkspace && value === inspected?.defaultValue ? undefined : value;
    try {
        await config.update(key, storedValue, target);
    } catch (error) {
        logger.error(`Failed to save setting ${key}`, error);
        sendSettings(webview);
        return;
    }

    if (vscode.workspace.getConfiguration(section).get(key) !== value)
        sendSettings(webview); // Overridden elsewhere, such as in the remote settings
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
