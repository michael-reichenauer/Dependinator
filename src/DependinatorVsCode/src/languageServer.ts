import * as vscode from "vscode";
import type { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import * as logger from "./logger";

/**
 * Locates and starts the Dependinator.Lsp server over stdio.
 * Prefers a published self-contained exe; falls back to a DLL and finally `dotnet run`.
 */
export async function startLanguageServer(
    context: vscode.ExtensionContext
): Promise<LanguageClient | undefined> {
    logger.log("Starting language server");
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri;
    const isRemoteWorkspace = !!workspaceFolder && workspaceFolder.scheme !== "file";
    const isRemoteExtensionHost = context.extensionUri.scheme !== "file";
    // Avoid launching a server that can't access the workspace filesystem.
    if (isRemoteWorkspace && !isRemoteExtensionHost) {
        const action = await vscode.window.showWarningMessage(
            "Dependinator language server needs to run in the dev container. Install the extension in the dev container to enable it.",
            "Install in Dev Container"
        );
        if (action === "Install in Dev Container") {
            await vscode.commands.executeCommand("dependinator.installInDevContainer");
        }
        return undefined;
    }
    const workspaceProject = workspaceFolder
        ? vscode.Uri.joinPath(
            workspaceFolder,
            "src",
            "Dependinator.Lsp",
            "Dependinator.Lsp.csproj"
        )
        : undefined;

    const extensionProject = vscode.Uri.joinPath(
        context.extensionUri,
        "..",
        "Dependinator.Lsp",
        "Dependinator.Lsp.csproj"
    );

    const serverExeName = process.platform === "win32" ? "Dependinator.Lsp.exe" : "Dependinator.Lsp";
    const runtimeIdentifier = getRuntimeIdentifier();
    const serverExeCandidates: vscode.Uri[] = [
        runtimeIdentifier
            ? vscode.Uri.joinPath(
                context.extensionUri,
                "server",
                runtimeIdentifier,
                serverExeName
            )
            : undefined,
        vscode.Uri.joinPath(
            context.extensionUri,
            "server",
            serverExeName
        )
    ].filter((candidate): candidate is vscode.Uri => !!candidate);

    const serverDllCandidates: vscode.Uri[] = [
        vscode.Uri.joinPath(
            context.extensionUri,
            "server",
            "Dependinator.Lsp.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "Dependinator.Lsp",
            "bin",
            "Debug",
            "net10.0",
            "Dependinator.Lsp.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "Dependinator.Lsp",
            "bin",
            "Release",
            "net10.0",
            "Dependinator.Lsp.dll"
        )
    ].filter((candidate): candidate is vscode.Uri => !!candidate);

    let serverCommand = "dotnet";
    let serverArgs: string[] | undefined;
    const serverExe = await firstExisting(serverExeCandidates);
    if (serverExe) {
        serverCommand = serverExe.fsPath;
        serverArgs = [];
        logger.log("Using server exe:", serverExe.fsPath);
    } else {
        const serverDll = await firstExisting(serverDllCandidates);
        if (serverDll) {
            serverArgs = [serverDll.fsPath];
            logger.log("Using server dll:", serverDll.fsPath);
        } else {
            const projectCandidates = [workspaceProject, extensionProject].filter(
                (candidate): candidate is vscode.Uri => !!candidate
            );
            const project = await firstExisting(projectCandidates);
            if (project) {
                serverArgs = [
                    "run",
                    "--project",
                    project.fsPath,
                    "--no-build",
                    "--no-restore",
                    "--nologo"
                ];
                logger.log("Using server project:", project.fsPath);
            } else {
                logger.warn("Dependinator language server project not found.");
                return undefined;
            }
        }
    }

    const { LanguageClient, TransportKind } = await import("vscode-languageclient/node");

    // Keep stdout clean for JSON-RPC by suppressing CLI banners.
    const environment = {
        ...process.env,
        DOTNET_NOLOGO: "1",
        NUGET_XMLDOC_MODE: "skip",
        DOTNET_CLI_TELEMETRY_OPTOUT: "1"
    };
    const executableOptions = workspaceFolder
        ? { cwd: workspaceFolder.fsPath, env: environment }
        : { env: environment };
    const serverOptions: ServerOptions = {
        run: {
            command: serverCommand,
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        },
        debug: {
            command: serverCommand,
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        }
    };

    // Hand the LSP its cloud-sync config and the stored access token at launch
    // (node-only module, loaded dynamically like the language client itself).
    const { getCloudSyncInitializationOptions } = await import("./cloudSyncAuth");
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: "file", language: "csharp" }],
        initializationOptions: await getCloudSyncInitializationOptions(context)
    };

    const client = new LanguageClient(
        "dependinatorLanguageServer",
        "Dependinator Language Server",
        serverOptions,
        clientOptions
    );

    logger.log("Starting lsp client:", serverCommand, serverArgs);
    await client.start();
    logger.log("Started lsp client");
    context.subscriptions.push(client);
    return client;
}

/** Routes "vscode/log" notifications from the LSP to the Dependinator output channel. */
export function registerLanguageClientLogging(client: LanguageClient): void {
    client.onNotification("vscode/log", params => {
        const type = typeof params?.Type === "string" ? params.Type.toLowerCase() : "";
        const message = params?.Message ?? params?.message ?? "";

        switch (type) {
            case "warning":
            case "warn":
                logger.warn(message);
                break;
            case "error":
            case "err":
                logger.error(message);
                break;
            case "log":
            case "info":
            default:
                logger.log(message);
                break;
        }
    });
}

/** Forwards "ui/message" notifications from the LSP to the webview and tracks readiness. */
export function registerUiMessageForwarding(
    client: LanguageClient,
    getWebview: () => vscode.Webview | undefined,
    onLspReady?: (params: unknown) => void
): void {
    client.onNotification("ui/message", params => {
        getWebview()?.postMessage({
            type: "ui/message",
            message: params?.message
        });
    });
    client.onNotification("ui/lspReady", params => {
        logger.log("Language server is ready");
        onLspReady?.(params);
    });
}

async function fileExists(uri: vscode.Uri): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(uri);
        return true;
    } catch {
        return false;
    }
}

async function firstExisting(uris: vscode.Uri[]): Promise<vscode.Uri | undefined> {
    for (const uri of uris) {
        if (await fileExists(uri)) {
            return uri;
        }
    }
    return undefined;
}

// Maps the Node platform/arch to the .NET runtime identifier of the bundled server.
// Must stay in sync with the RIDs published by scripts/prepare-server.sh.
function getRuntimeIdentifier(): string | undefined {
    const platform = process.platform;
    const arch = process.arch;
    const archSuffix = arch === "x64" ? "x64" : arch === "arm64" ? "arm64" : undefined;
    if (!archSuffix)
        return undefined;

    switch (platform) {
        case "win32":
            return `win-${archSuffix}`;
        case "linux":
            return `linux-${archSuffix}`;
        case "darwin":
            return `osx-${archSuffix}`;
        default:
            return undefined;
    }
}
