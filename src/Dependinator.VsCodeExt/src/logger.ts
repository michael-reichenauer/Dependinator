import * as vscode from "vscode";

// Single "Dependinator" output channel shared by all extension modules, mirrored to the
// console so messages also show up in the extension host developer tools.

let channel: vscode.OutputChannel | undefined;

function getChannel(): vscode.OutputChannel {
    if (!channel) {
        channel = vscode.window.createOutputChannel("Dependinator");
    }
    return channel;
}

function format(message: string, args: unknown[]): string {
    if (args.length === 0)
        return message;

    const formattedArgs = args.map(arg => {
        if (typeof arg === "string")
            return arg;
        try {
            return JSON.stringify(arg);
        } catch {
            return String(arg);
        }
    });
    return `${message} ${formattedArgs.join(" ")}`;
}

/** Logs an informational message to the Dependinator output channel. */
export function log(message: string, ...args: unknown[]): void {
    const line = format(message, args);
    getChannel().appendLine(line);
    console.log(`Dependinator: ${line}`);
}

/** Logs a warning to the Dependinator output channel. */
export function warn(message: string, ...args: unknown[]): void {
    const line = format(message, args);
    getChannel().appendLine(`WARN: ${line}`);
    console.warn(`Dependinator: ${line}`);
}

/** Logs an error to the Dependinator output channel. */
export function error(message: string, ...args: unknown[]): void {
    const line = format(message, args);
    getChannel().appendLine(`ERROR: ${line}`);
    console.error(`Dependinator: ${line}`);
}
